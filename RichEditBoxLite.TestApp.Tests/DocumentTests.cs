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
}
