using CConner100.RichEditBoxLite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EditorControl = CConner100.RichEditBoxLite.RichEditBoxLite;

namespace RichEditBoxLite.TestApp;

// Opt-in mounted-control regression for #27. The ordinary test app is unchanged.
internal sealed class HtmlRoundTripChecks : Page
{
    private readonly StackPanel _panel = new() { Spacing = 12, Margin = new Thickness(24) };
    private readonly string _output;

    public HtmlRoundTripChecks(string output)
    {
        _output = output;
        Content = _panel;
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            results.Add($"{(condition ? "PASS" : "FAIL")} {name}");
        }
        try
        {
            var original = new EditorControl { Height = 240 };
            _panel.Children.Add(new TextBlock { Text = "Original" });
            _panel.Children.Add(original);
            await Task.Delay(150);
            const string text = "This is title\n  - body\n  - new topic\nnew line space Yes i do";
            var source = original.Document;
            source.SetText(TextSetOptions.None, text);
            source.GetRange(0, 13).CharacterFormat.Size = 24;
            source.GetRange(0, 13).CharacterFormat.Bold = FormatEffect.On;
            source.GetRange(14, 22).ParagraphFormat.SetIndents(0, 24, 0);
            source.GetRange(23, 36).ParagraphFormat.SetIndents(0, 24, 0);
            source.GetRange(52, source.Length).CharacterFormat.Italic = FormatEffect.On;
            source.ClearUndoRedoHistory();
            await Task.Delay(150); // Drain delayed TextBox notifications.
            Check(source.Text == text, "mounted input preserves LF paragraphs and spaces");
            Check(!source.CanUndo(), "programmatic bridge echo does not create an edit");
            Check(source.GetRange(14, 15).CharacterFormat.Size == 14, "title size does not spread to body");
            Check(source.GetRange(14, 15).ParagraphFormat.LeftIndent == 24, "body indentation survives mounting");

            var html = source.GetHtml();
            var reopened = new EditorControl { Height = 240 };
            _panel.Children.Add(new TextBlock { Text = "Reopened HTML" });
            _panel.Children.Add(reopened);
            await Task.Delay(150);
            reopened.Document.SetHtml(html);
            await Task.Delay(150);
            Check(reopened.Document.Text == text, "fresh mounted editor preserves text");
            Check(reopened.Document.GetHtml() == html, "mounted HTML round trip is stable");
            Check(Enumerable.Range(0, text.Length).All(i =>
                source.GetRange(i, i + 1).CharacterFormat.Size == reopened.Document.GetRange(i, i + 1).CharacterFormat.Size &&
                source.GetRange(i, i + 1).CharacterFormat.Bold == reopened.Document.GetRange(i, i + 1).CharacterFormat.Bold &&
                source.GetRange(i, i + 1).CharacterFormat.Italic == reopened.Document.GetRange(i, i + 1).CharacterFormat.Italic &&
                source.GetRange(i, i + 1).ParagraphFormat.LeftIndent == reopened.Document.GetRange(i, i + 1).ParagraphFormat.LeftIndent),
                "all character positions retain size, bold, italic and indent");

            // Exercise a real TextBox-originated change with CR separators.
            var bridge = FindBridge(original) ?? throw new InvalidOperationException("Input bridge not mounted");
            bridge.Text += "\rtyped";
            await Task.Delay(150);
            Check(source.Text == text + "\ntyped", "input bridge edits retain paragraph structure");
            source.Undo();
            await Task.Delay(150);
            Check(source.GetHtml() == html, "undo restores formatting after bridge edit");
            Directory.CreateDirectory(_output);
            await File.WriteAllTextAsync(Path.Combine(_output, "exported.html"),
                "<!doctype html><meta charset='utf-8'><title>Issue 27 exported HTML</title>" + html);
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex);
        }
        Directory.CreateDirectory(_output);
        await File.WriteAllLinesAsync(Path.Combine(_output, "results.txt"), results);
        _panel.Children.Add(new TextBlock { Text = string.Join("\n", results) });
    }

    private static TextBox? FindBridge(DependencyObject parent)
    {
        if (parent is TextBox textBox) return textBox;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (FindBridge(VisualTreeHelper.GetChild(parent, i)) is { } result) return result;
        }
        return null;
    }
}
