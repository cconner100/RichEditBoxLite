using System.Diagnostics;
using CConner100.RichEditBoxLite;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Streams;

namespace RichEditBoxLite.TestApp;

public sealed partial class MainPage : Page
{
    private readonly List<string> _events = [];
    private readonly Dictionary<string, StackPanel> _sections;

    public MainPage()
    {
        InitializeComponent();
        _sections = new()
        {
            ["Playground"] = PlaygroundSection,
            ["Formatting"] = FormattingSection,
            ["Rtf"] = RtfSection,
            ["Input"] = InputSection,
            ["Properties"] = PropertiesSection,
            ["Events"] = EventsSection,
            ["Stress"] = StressSection
        };
        UnsupportedProperties.ItemsSource = CompatibilityCoverage.UnsupportedProperties;
        Editor.Document.SetText(TextSetOptions.None,
            "Welcome to RichEditBoxLite.\nSelect text and explore the Test UI.\nBienvenido: á é í ó ú ü ñ ¿ ¡");
        UpdateStatus();
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_sections is null || SectionList.SelectedItem is not ListViewItem item || item.Tag is not string tag) return;
        foreach (var section in _sections.Values) section.Visibility = Visibility.Collapsed;
        _sections[tag].Visibility = Visibility.Visible;
    }

    private void Editor_TextChanging(object? sender, RichEditBoxLiteTextChangingEventArgs e) => Log("TextChanging", $"content={e.IsContentChanging}");
    private void Editor_TextChanged(object sender, RoutedEventArgs e) { UpdateStatus(); Log("TextChanged", $"length={Editor.Document.Length}"); }
    private void Editor_SelectionChanging(object? sender, RichEditBoxLiteSelectionChangingEventArgs e)
    {
        e.Cancel = CancelSelection.IsOn;
        Log("SelectionChanging", $"start={e.SelectionStart}, length={e.SelectionLength}, cancel={e.Cancel}");
    }
    private void Editor_SelectionChanged(object sender, RoutedEventArgs e) { UpdateStatus(); Log("SelectionChanged", SelectionDetails()); }
    private void Editor_CopyingToClipboard(object? sender, RichEditBoxLiteClipboardEventArgs e) { e.Handled = HandleClipboard.IsOn; Log("CopyingToClipboard", $"handled={e.Handled}"); }
    private void Editor_CuttingToClipboard(object? sender, RichEditBoxLiteClipboardEventArgs e) { e.Handled = HandleClipboard.IsOn; Log("CuttingToClipboard", $"handled={e.Handled}"); }
    private void Editor_Paste(object? sender, RichEditBoxLitePasteEventArgs e) { e.Handled = HandleClipboard.IsOn; Log("Paste", $"handled={e.Handled}"); }
    private void Editor_ContextMenuOpening(object? sender, RichEditBoxLiteContextMenuOpeningEventArgs e) => Log("ContextMenuOpening", $"x={e.Position.X:0}, y={e.Position.Y:0}, handled={e.Handled}");
    private void Editor_CandidateWindowBoundsChanged(object? sender, RichEditBoxLiteCandidateWindowBoundsChangedEventArgs e) => Log("CandidateWindowBoundsChanged", e.Bounds.ToString());
    private void Editor_TextCompositionStarted(object? sender, RichEditBoxLiteCompositionEventArgs e) => Log("TextCompositionStarted", CompositionDetails(e));
    private void Editor_TextCompositionChanged(object? sender, RichEditBoxLiteCompositionEventArgs e) => Log("TextCompositionChanged", CompositionDetails(e));
    private void Editor_TextCompositionEnded(object? sender, RichEditBoxLiteCompositionEventArgs e) => Log("TextCompositionEnded", CompositionDetails(e));

    private void Bold_Click(object sender, RoutedEventArgs e) => Toggle(effect => effect.Bold, (effect, value) => effect.Bold = value);
    private void Italic_Click(object sender, RoutedEventArgs e) => Toggle(effect => effect.Italic, (effect, value) => effect.Italic = value);
    private void Underline_Click(object sender, RoutedEventArgs e) => ToggleUnderline();
    private void Strike_Click(object sender, RoutedEventArgs e) => Toggle(effect => effect.Strikethrough, (effect, value) => effect.Strikethrough = value);
    private void Subscript_Click(object sender, RoutedEventArgs e) => Toggle(effect => effect.Subscript, (effect, value) => effect.Subscript = value);
    private void Superscript_Click(object sender, RoutedEventArgs e) => Toggle(effect => effect.Superscript, (effect, value) => effect.Superscript = value);
    private void Heading1_Click(object sender, RoutedEventArgs e) => SetHeading(RichTextHeadingLevel.Heading1);
    private void Heading2_Click(object sender, RoutedEventArgs e) => SetHeading(RichTextHeadingLevel.Heading2);
    private void NormalParagraph_Click(object sender, RoutedEventArgs e) => SetHeading(RichTextHeadingLevel.None);
    private void ClearFormatting_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.Selection.ClearFormatting();
        DocumentApiResult.Text = "Character and paragraph formatting cleared";
        UpdateStatus();
    }
    private void Undo_Click(object sender, RoutedEventArgs e) { Editor.Document.Undo(); DocumentApiResult.Text = "Undo"; }
    private void Redo_Click(object sender, RoutedEventArgs e) { Editor.Document.Redo(); DocumentApiResult.Text = "Redo"; }
    private void Uppercase_Click(object sender, RoutedEventArgs e) { Editor.Document.Selection.ChangeCase(LetterCase.Upper); DocumentApiResult.Text = "Selection changed to upper case"; }
    private void Lowercase_Click(object sender, RoutedEventArgs e) { Editor.Document.Selection.ChangeCase(LetterCase.Lower); DocumentApiResult.Text = "Selection changed to lower case"; }
    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var range = Editor.Document.GetRange(0, Editor.Document.Length);
        var found = range.FindText("Uno", Editor.Document.Length, FindOptions.Case);
        if (found > 0) Editor.Document.Selection.SetRange(range.StartPosition, range.EndPosition);
        DocumentApiResult.Text = found > 0 ? $"Found at UTF-16 position {range.StartPosition}" : "Not found";
    }
    private void AlignLeft_Click(object sender, RoutedEventArgs e) => SetAlignment(ParagraphAlignment.Left);
    private void AlignCenter_Click(object sender, RoutedEventArgs e) => SetAlignment(ParagraphAlignment.Center);
    private void AlignRight_Click(object sender, RoutedEventArgs e) => SetAlignment(ParagraphAlignment.Right);
    private void BulletList_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.Selection.ParagraphFormat.ListType = MarkerType.Bullet;
        DocumentApiResult.Text = "Bullet paragraph";
        UpdateStatus();
    }
    private void NumberList_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.Selection.ParagraphFormat.ListType = MarkerType.Arabic;
        DocumentApiResult.Text = "Numbered paragraph";
        UpdateStatus();
    }

    private void SaveRtf_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.GetText(TextGetOptions.FormatRtf, out var rtf);
        RtfText.Text = rtf;
        PlainTextView.Text = Editor.Document.Text;
    }
    private void LoadRtf_Click(object sender, RoutedEventArgs e)
    {
        try { Editor.Document.SetText(TextSetOptions.FormatRtf, RtfText.Text); RuntimeStatus.Text = "● RTF imported"; }
        catch (Exception exception) { RuntimeStatus.Text = $"RTF rejected: {exception.Message}"; }
        PlainTextView.Text = Editor.Document.Text;
    }
    private void LoadRichFixture_Click(object sender, RoutedEventArgs e)
    {
        RtfText.Text = @"{\rtf1\ansi{\colortbl;\red0\green102\blue204;}\b RichEditBoxLite\b0\par \i Español:\i0  \cf1 á é í ó ú ü ñ ¿ ¡\cf0\par \ul https://platform.uno\ulnone\par • First\par • Second}";
        LoadRtf_Click(sender, e);
    }
    private void MalformedRtf_Click(object sender, RoutedEventArgs e) { RtfText.Text = @"{\rtf1\ansi \b malformed"; LoadRtf_Click(sender, e); }
    private void InsertImage_Click(object sender, RoutedEventArgs e)
    {
        using var stream = new InMemoryRandomAccessStream();
        Editor.Document.Selection.InsertImage(32, 32, 0, VerticalCharacterAlignment.Baseline, "test image", stream);
        RuntimeStatus.Text = "● Inline object inserted";
    }

    private void ProofingLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Editor is null || ProofingLanguageCombo.SelectedItem is not ComboBoxItem item) return;
        Editor.ProofingLanguage = item.Content?.ToString() ?? "en-US";
    }
    private void CheckSpelling_Click(object sender, RoutedEventArgs e) =>
        SpellingResults.ItemsSource = Editor.SpellCheck.Check(Editor.Document.Text, Editor.ProofingLanguage)
            .Select(error => $"{error.Word} ({error.Start}:{error.Length}) → {string.Join(", ", error.Suggestions)}").ToArray();
    private void IgnoreWord_Click(object sender, RoutedEventArgs e) { Editor.SpellCheck.Ignore(Editor.Document.Selection.Text); CheckSpelling_Click(sender, e); }
    private void AddCustomWord_Click(object sender, RoutedEventArgs e) { Editor.SpellCheck.AddWord(Editor.ProofingLanguage, Editor.Document.Selection.Text); CheckSpelling_Click(sender, e); }

    private void PropertyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (Editor is null) return;
        Editor.AcceptsReturn = AcceptsReturnToggle.IsOn;
        Editor.IsReadOnly = ReadOnlyToggle.IsOn;
        Editor.IsSpellCheckEnabled = SpellCheckToggle.IsOn;
        Editor.IsTextPredictionEnabled = PredictionToggle.IsOn;
        Editor.IsColorFontEnabled = ColorFontToggle.IsOn;
        Editor.PreventKeyboardDisplayOnProgrammaticFocus = PreventKeyboardToggle.IsOn;
        Editor.IsEnabled = EnabledToggle.IsOn;
    }
    private void PropertyText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Editor is null) return;
        Editor.Header = HeaderValue.Text;
        Editor.Description = DescriptionValue.Text;
        Editor.PlaceholderText = PlaceholderValue.Text;
    }
    private void MaxLengthValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (Editor is not null && !double.IsNaN(args.NewValue)) Editor.MaxLength = Math.Max(0, (int)args.NewValue);
    }
    private void PropertyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Editor is null) return;
        Editor.TextWrapping = (TextWrapping)Math.Max(0, WrappingCombo.SelectedIndex);
        Editor.CharacterCasing = (CharacterCasing)Math.Max(0, CasingCombo.SelectedIndex);
        Editor.TextAlignment = (TextAlignment)Math.Max(0, AlignmentCombo.SelectedIndex);
    }
    private void ResetProperties_Click(object sender, RoutedEventArgs e)
    {
        Editor.ClearValue(CConner100.RichEditBoxLite.RichEditBoxLite.AcceptsReturnProperty);
        Editor.ClearValue(CConner100.RichEditBoxLite.RichEditBoxLite.IsReadOnlyProperty);
        Editor.ClearValue(CConner100.RichEditBoxLite.RichEditBoxLite.IsSpellCheckEnabledProperty);
        Editor.ClearValue(CConner100.RichEditBoxLite.RichEditBoxLite.MaxLengthProperty);
        AcceptsReturnToggle.IsOn = true; ReadOnlyToggle.IsOn = false; SpellCheckToggle.IsOn = true; MaxLengthValue.Value = 0;
    }

    private void InsertEnglishSample_Click(object sender, RoutedEventArgs e) => Editor.Document.Selection.Text = "The quick brown fox edits a rich Uno note. ";
    private void InsertSpanishSample_Click(object sender, RoutedEventArgs e) => Editor.Document.Selection.Text = "¿Cómo está? El pingüino añadió á é í ó ú ü ñ. ¡Excelente! ";
    private void ClearDocument_Click(object sender, RoutedEventArgs e) => Editor.Document.SetText(TextSetOptions.None, string.Empty);
    private void ClearEventLog_Click(object sender, RoutedEventArgs e) { _events.Clear(); RefreshEventLog(); }
    private void EventFilter_TextChanged(object sender, TextChangedEventArgs e) => RefreshEventLog();

    private void StressLongNote_Click(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        Editor.Document.SetText(TextSetOptions.None, string.Concat(Enumerable.Repeat("Rich note línea española áéíóúüñ. ", 3125)));
        stopwatch.Stop();
        StressProgress.Value = 100;
        StressResult.Text = $"{Editor.Document.Length:N0} UTF-16 code units loaded in {stopwatch.ElapsedMilliseconds} ms";
    }
    private void StressFormatRuns_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.SetText(TextSetOptions.None, string.Concat(Enumerable.Repeat("format ", 1000)));
        var stopwatch = Stopwatch.StartNew();
        for (var position = 0; position < Editor.Document.Length; position += 14)
            Editor.Document.GetRange(position, Math.Min(Editor.Document.Length, position + 7)).CharacterFormat.Bold = FormatEffect.On;
        stopwatch.Stop();
        StressResult.Text = $"Created many format runs in {stopwatch.ElapsedMilliseconds} ms";
    }
    private void StressTable_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.SetText(TextSetOptions.FormatRtf, @"{\rtf1\ansi Name\tab Value\par Uno\tab Skia\par Español\tab áéíóúñ\par}");
        StressResult.Text = "Editable table projection fixture loaded";
    }

    private void Toggle(Func<RichTextCharacterFormat, FormatEffect> get, Action<RichTextCharacterFormat, FormatEffect> set)
    {
        var format = Editor.Document.Selection.CharacterFormat;
        set(format, get(format) == FormatEffect.On ? FormatEffect.Off : FormatEffect.On);
        UpdateStatus();
    }
    private void ToggleUnderline()
    {
        var format = Editor.Document.Selection.CharacterFormat;
        format.Underline = format.Underline == UnderlineType.None ? UnderlineType.Single : UnderlineType.None;
        UpdateStatus();
    }
    private void SetAlignment(ParagraphAlignment alignment) { Editor.Document.Selection.ParagraphFormat.Alignment = alignment; DocumentApiResult.Text = $"Alignment: {alignment}"; }
    private void SetHeading(RichTextHeadingLevel heading)
    {
        Editor.Document.Selection.ParagraphFormat.HeadingLevel = heading;
        DocumentApiResult.Text = $"Paragraph style: {heading}";
        UpdateStatus();
    }
    private string SelectionDetails() => $"start={Editor.Document.Selection.StartPosition}, end={Editor.Document.Selection.EndPosition}";
    private static string CompositionDetails(RichEditBoxLiteCompositionEventArgs e) => $"text={e.Text}, start={e.Start}, length={e.Length}";
    private void UpdateStatus()
    {
        CharacterCount.Text = $"{Editor.Document.Length:N0} characters";
        SelectionStatus.Text = $"Selection {Editor.Document.Selection.StartPosition}:{Editor.Document.Selection.Length}";
        var format = Editor.Document.Selection.CharacterFormat;
        var paragraph = Editor.Document.Selection.ParagraphFormat;
        FormatStatus.Text = $"B:{format.Bold} I:{format.Italic} U:{format.Underline} H:{paragraph.HeadingLevel} L:{paragraph.ListType}";
        PlainTextView.Text = Editor.Document.Text;
    }
    private void Log(string name, string details)
    {
        if (PauseEvents.IsChecked == true) return;
        _events.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss.fff}  {name}  sender=RichEditBoxLite  {details}");
        if (_events.Count > 500) _events.RemoveAt(_events.Count - 1);
        RefreshEventLog();
    }
    private void RefreshEventLog()
    {
        var filter = EventFilter?.Text;
        EventLog.ItemsSource = string.IsNullOrWhiteSpace(filter)
            ? _events.ToArray()
            : _events.Where(value => value.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
