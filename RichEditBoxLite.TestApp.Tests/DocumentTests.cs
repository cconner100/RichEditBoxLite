using CConner100.RichEditBoxLite;
using Microsoft.UI.Text;
using RichDocument = CConner100.RichEditBoxLite.RichEditTextDocument;

namespace RichEditBoxLite.TestApp.Tests;

public class DocumentTests
{
    [Test]
    public void PlainText_UsesUtf16PositionsAndSelectionReplacement()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "A😀B");

        document.Selection.SetRange(1, 3);
        document.Selection.Text = "ñ";

        document.Text.Should().Be("AñB");
        document.Selection.StartPosition.Should().Be(2);
    }

    [Test]
    public void FormattingAndRtf_RoundTripCanonicalProfile()
    {
        var source = new RichDocument();
        source.SetText(TextSetOptions.None, "Uno áéíóúüñ");
        var range = source.GetRange(0, 3);
        range.CharacterFormat.Bold = FormatEffect.On;
        range.CharacterFormat.Underline = UnderlineType.Single;
        source.GetText(TextGetOptions.FormatRtf, out var rtf);

        var target = new RichDocument();
        target.SetText(TextSetOptions.FormatRtf, rtf);

        target.Text.Should().Be(source.Text);
        target.GetRange(0, 3).CharacterFormat.Bold.Should().Be(FormatEffect.On);
        target.GetRange(0, 3).CharacterFormat.Underline.Should().Be(UnderlineType.Single);
    }

    [Test]
    public void UndoRedo_AndGroupedEditsRestoreSnapshots()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "one");
        document.ClearUndoRedoHistory();
        document.BeginUndoGroup();
        document.GetRange(3, 3).Text = " two";
        document.GetRange(7, 7).Text = " three";
        document.EndUndoGroup();

        document.Text.Should().Be("one two three");
        document.Undo();
        document.Text.Should().Be("one");
        document.Redo();
        document.Text.Should().Be("one two three");
    }

    [Test]
    public void RangeFindMoveCaseAndParagraphFormatting()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "first Uno\nsecond");
        var range = document.GetRange(0, document.Length);

        range.FindText("uno", document.Length, FindOptions.None).Should().Be(3);
        range.ChangeCase(LetterCase.Upper);
        document.Text.Should().Contain("UNO");
        range.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        range.ParagraphFormat.Alignment.Should().Be(ParagraphAlignment.Center);
    }

    [Test]
    public void RtfParserRejectsUnbalancedAndExcessiveNesting()
    {
        var document = new RichDocument();
        var unbalanced = () => document.SetText(TextSetOptions.FormatRtf, @"{\rtf1 broken");
        var nested = () => document.SetText(TextSetOptions.FormatRtf, @"{\rtf1 " + new string('{', 257) + "x" + new string('}', 258));

        unbalanced.Should().Throw<InvalidDataException>().WithMessage("*unbalanced*");
        nested.Should().Throw<InvalidDataException>().WithMessage("*nesting*");
    }

    [Test]
    public void SpellcheckSupportsEnglishSpanishIgnoreAndCustomWords()
    {
        var spellcheck = new SpellCheckService();

        spellcheck.Check("hello wurld", "en-US").Select(value => value.Word).Should().Contain("wurld");
        spellcheck.Check("hola pingüino", "es-ES").Should().BeEmpty();
        spellcheck.Ignore("wurld");
        spellcheck.Check("wurld", "en-US").Should().BeEmpty();
        spellcheck.AddWord("es-ES", "codificador");
        spellcheck.Check("codificador", "es-ES").Should().BeEmpty();
    }

    [Test]
    public void HeadingsAndLists_RoundTripThroughRtfAndSurviveEditing()
    {
        var source = new RichDocument();
        source.SetText(TextSetOptions.None, "Title\nBullet\nOne\nTwo");

        source.GetRange(0, 5).ParagraphFormat.HeadingLevel = RichTextHeadingLevel.Heading1;
        var bulletStart = source.Text.IndexOf("Bullet", StringComparison.Ordinal);
        source.GetRange(bulletStart, bulletStart + 6).ParagraphFormat.ListType = MarkerType.Bullet;
        var oneStart = source.Text.IndexOf("One", StringComparison.Ordinal);
        source.GetRange(oneStart, source.Length).ParagraphFormat.ListType = MarkerType.Arabic;
        source.GetRange(oneStart, source.Length).ParagraphFormat.ListStart = 3;

        source.GetRange(bulletStart + 6, bulletStart + 6).Text = " item";
        source.GetRange(bulletStart, bulletStart + 11).ParagraphFormat.ListType.Should().Be(MarkerType.Bullet);

        source.GetText(TextGetOptions.FormatRtf, out var rtf);
        rtf.Should().Contain(@"\outlinelevel0");
        rtf.Should().Contain(@"\pnlvlblt");
        rtf.Should().Contain(@"\pnlvlbody");

        var target = new RichDocument();
        target.SetText(TextSetOptions.FormatRtf, rtf);

        target.Text.Should().Be(source.Text);
        target.GetRange(0, 5).ParagraphFormat.HeadingLevel.Should().Be(RichTextHeadingLevel.Heading1);
        target.GetRange(bulletStart, bulletStart + 11).ParagraphFormat.ListType.Should().Be(MarkerType.Bullet);
        target.GetRange(target.Text.IndexOf("One", StringComparison.Ordinal), target.Length).ParagraphFormat.ListType.Should().Be(MarkerType.Arabic);
        target.GetRange(target.Text.IndexOf("One", StringComparison.Ordinal), target.Length).ParagraphFormat.ListStart.Should().Be(3);
    }

    [Test]
    public void ClearFormatting_ResetsCharacterAndParagraphStateWithoutChangingTextOrSelection()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "Heading");
        var selection = document.GetRange(0, document.Length);
        selection.CharacterFormat.Bold = FormatEffect.On;
        selection.CharacterFormat.Size = 30;
        selection.ParagraphFormat.HeadingLevel = RichTextHeadingLevel.Heading2;
        selection.ParagraphFormat.ListType = MarkerType.Bullet;
        var start = selection.StartPosition;
        var end = selection.EndPosition;

        selection.ClearFormatting();

        document.Text.Should().Be("Heading");
        selection.StartPosition.Should().Be(start);
        selection.EndPosition.Should().Be(end);
        selection.CharacterFormat.Bold.Should().Be(FormatEffect.Off);
        selection.CharacterFormat.Size.Should().Be(document.DefaultCharacterFormat.Size);
        selection.ParagraphFormat.HeadingLevel.Should().Be(RichTextHeadingLevel.None);
        selection.ParagraphFormat.ListType.Should().Be(MarkerType.None);

        document.Undo();
        selection.CharacterFormat.Bold.Should().Be(FormatEffect.On);
        selection.ParagraphFormat.HeadingLevel.Should().Be(RichTextHeadingLevel.Heading2);
        selection.ParagraphFormat.ListType.Should().Be(MarkerType.Bullet);
    }

    [Test]
    public void RendererListMarkers_UseBulletsAndSequentialArabicNumbers()
    {
        var counter = 0;
        var previousWasOrdered = false;

        RichTextCanvas.GetMarkerText(
            new ParagraphFormatState { ListType = MarkerType.Bullet },
            ref counter,
            ref previousWasOrdered).Should().Be("•");
        RichTextCanvas.GetMarkerText(
            new ParagraphFormatState { ListType = MarkerType.Arabic, ListStart = 3 },
            ref counter,
            ref previousWasOrdered).Should().Be("3.");
        RichTextCanvas.GetMarkerText(
            new ParagraphFormatState { ListType = MarkerType.Arabic, ListStart = 3 },
            ref counter,
            ref previousWasOrdered).Should().Be("4.");
    }
}
