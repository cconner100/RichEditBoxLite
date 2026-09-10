using CConner100.RichEditBoxLite;
using Microsoft.UI.Text;
using Windows.UI;
using RichDocument = CConner100.RichEditBoxLite.RichEditTextDocument;

namespace RichEditBoxLite.TestApp.Tests;

public class HtmlCodecTests
{
    [TestCase("  - body")]
    [TestCase("a  b ")]
    [TestCase("\tindented\ttext")]
    [TestCase("a\n\nend\n")]
    public void HtmlRoundTrip_PreservesSignificantWhitespace(string text)
    {
        var source = new RichDocument();
        source.SetText(TextSetOptions.None, text);
        // Split whitespace across differently formatted runs, too.
        source.GetRange(0, 1).CharacterFormat.Bold = FormatEffect.On;
        var html = source.GetHtml();
        var target = new RichDocument();
        target.SetHtml(html);
        target.Text.Should().Be(text);
        target.GetHtml().Should().Be(html);
        if (text.Contains(' ') || text.Contains('\t')) html.Should().Contain("white-space: pre-wrap");
        if (text.Contains("\n\n")) html.Should().Contain("<p><br></p>");
    }

    [Test]
    public void SetHtml_WhitespaceModeIsInheritedAndRestoredAfterNestedElements()
    {
        var document = new RichDocument();
        document.SetHtml("<p style=\"white-space: pre-wrap\">  a<strong>  b</strong>  c</p><p>  collapsed   spaces </p>");
        document.Text.Should().Be("  a  b  c\ncollapsed spaces");
        document.GetRange(5, 6).CharacterFormat.Bold.Should().Be(FormatEffect.On);

        document.SetHtml("<p style=\"white-space: pre-wrap\">a\r\nb\rc\nd</p>");
        document.Text.Should().Be("a\nb\nc\nd");
    }

    [Test]
    public void SetHtml_ParagraphIndentsConvertPointsAndIgnoreNonFiniteValues()
    {
        var document = new RichDocument();
        document.SetHtml("<p style=\"margin-left: 18pt; margin-right: Infinitypx; text-indent: -3pt\">body</p>");
        var format = document.GetRange(0, 4).ParagraphFormat;
        format.LeftIndent.Should().Be(24);
        format.RightIndent.Should().Be(0);
        format.FirstLineIndent.Should().Be(-4);
    }

    [Test]
    public void HtmlRoundTrip_PreservesParagraphIndents()
    {
        var source = new RichDocument();
        source.SetText(TextSetOptions.None, "Title\nbody\nlast");
        source.GetRange(6, 10).ParagraphFormat.SetIndents(-6, 24, 12);
        source.GetRange(6, 10).ParagraphFormat.Alignment = ParagraphAlignment.Center;
        var html = source.GetHtml();
        html.Should().Contain("margin-left: 24px").And.Contain("text-indent: -6px");
        var target = new RichDocument();
        target.SetHtml(html);
        var format = target.GetRange(6, 10).ParagraphFormat;
        format.LeftIndent.Should().Be(24);
        format.RightIndent.Should().Be(12);
        format.FirstLineIndent.Should().Be(-6);
        format.Alignment.Should().Be(ParagraphAlignment.Center);
        target.GetRange(0, 1).ParagraphFormat.LeftIndent.Should().Be(0);
        target.GetRange(11, 12).ParagraphFormat.LeftIndent.Should().Be(0);
        target.GetHtml().Should().Be(html);
    }

    [Test]
    public void SetHtml_ImportsSupportedBlocksAndInlineFormatting()
    {
        var document = new RichDocument();
        document.SetHtml("<h1>Title</h1><p>Hello <strong>bold</strong> and <em>italic</em>.</p><ul><li>One</li><li>Two</li></ul>");

        document.Text.Should().Be("Title\nHello bold and italic.\nOne\nTwo");
        document.GetRange(0, 5).ParagraphFormat.HeadingLevel.Should().Be(RichTextHeadingLevel.Heading1);
        var bold = document.Text.IndexOf("bold", StringComparison.Ordinal);
        document.GetRange(bold, bold + 4).CharacterFormat.Bold.Should().Be(FormatEffect.On);
        var italic = document.Text.IndexOf("italic", StringComparison.Ordinal);
        document.GetRange(italic, italic + 6).CharacterFormat.Italic.Should().Be(FormatEffect.On);
        var one = document.Text.IndexOf("One", StringComparison.Ordinal);
        document.GetRange(one, one + 3).ParagraphFormat.ListType.Should().Be(MarkerType.Bullet);
    }

    [Test]
    public void HtmlRoundTrip_PreservesCanonicalProfileAndIsDeterministic()
    {
        var source = new RichDocument();
        source.SetText(TextSetOptions.None, "Title\nHello world\nItem one\nItem two");
        source.GetRange(0, 5).ParagraphFormat.HeadingLevel = RichTextHeadingLevel.Heading1;
        source.GetRange(0, 5).ParagraphFormat.Alignment = ParagraphAlignment.Center;
        source.GetRange(6, 11).CharacterFormat.Bold = FormatEffect.On;
        source.GetRange(6, 11).CharacterFormat.ForegroundColor = Color.FromArgb(255, 230, 0, 0);
        var items = source.Text.IndexOf("Item one", StringComparison.Ordinal);
        source.GetRange(items, source.Length).ParagraphFormat.ListType = MarkerType.Bullet;

        var html = source.GetHtml();
        var target = new RichDocument();
        target.SetHtml(html);

        target.Text.Should().Be(source.Text);
        target.GetRange(0, 5).ParagraphFormat.HeadingLevel.Should().Be(RichTextHeadingLevel.Heading1);
        target.GetRange(0, 5).ParagraphFormat.Alignment.Should().Be(ParagraphAlignment.Center);
        target.GetRange(6, 11).CharacterFormat.Bold.Should().Be(FormatEffect.On);
        target.GetRange(6, 11).CharacterFormat.ForegroundColor.Should().Be(Color.FromArgb(255, 230, 0, 0));
        target.GetRange(items, items + 8).ParagraphFormat.ListType.Should().Be(MarkerType.Bullet);
        target.GetHtml().Should().Be(html, "canonical HTML must be stable across round trips");
    }

    [Test]
    public void GetHtml_ProducesCanonicalOutput()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "Hello\nWorld");
        document.GetRange(0, 5).CharacterFormat.Bold = FormatEffect.On;

        document.GetHtml().Should().Be("<p><strong>Hello</strong></p><p>World</p>");
        new RichDocument().GetHtml().Should().Be("<p><br></p>");
    }

    [Test]
    public void SetHtml_DecodesEntitiesAndCollapsesWhitespace()
    {
        var document = new RichDocument();
        document.SetHtml("<p>Caf&eacute; &amp; more&nbsp;&#65;</p>");
        document.Text.Should().Be("Café & more\u00A0A");

        document.SetHtml("<p>  Multiple   spaces\n  and newlines  </p>");
        document.Text.Should().Be("Multiple spaces and newlines");
    }

    [Test]
    public void SetHtml_DiscardsUnsafeContentAndKeepsVisibleTextOfUnknownElements()
    {
        var document = new RichDocument();
        document.SetHtml(
            "<p>keep</p><script>alert(\"x\")</script><style>p { color: red }</style>" +
            "<iframe><p>hidden</p></iframe><object>data</object><img src=\"https://example.com/x.png\" />" +
            "<p><custom>visible</custom> text</p>");

        document.Text.Should().Be("keep\nvisible text");
    }

    [Test]
    public void SetHtml_EnforcesSizeAndDepthBoundsWithoutHangingOnMalformedInput()
    {
        var document = new RichDocument();
        var oversized = () => document.SetHtml("<p>" + new string('a', 16 * 1024 * 1024));
        oversized.Should().Throw<InvalidDataException>().WithMessage("*safety limit*");

        var nested = () => document.SetHtml(string.Concat(Enumerable.Repeat("<div>", 257)) + "x");
        nested.Should().Throw<InvalidDataException>().WithMessage("*nesting*");

        document.SetHtml("<p><strong>never closed");
        document.Text.Should().Be("never closed");
        document.GetRange(0, 5).CharacterFormat.Bold.Should().Be(FormatEffect.On);

        var repeatedParagraphs = () => document.SetHtml(string.Concat(Enumerable.Repeat("<p>", 300)));
        repeatedParagraphs.Should().NotThrow("unclosed sibling paragraphs are implied-closed, not treated as nesting");
    }

    [Test]
    public void SetHtml_ImportsRepresentativeLegacyStoredHtml()
    {
        var document = new RichDocument();
        document.SetHtml(
            "<div class=\"ql-editor\"><p>Dear <strong>Customer</strong>,</p><p><br></p>" +
            "<p style=\"color: rgb(230, 0, 0);\">Payment is due.</p>" +
            "<ol><li>First</li><li>Second</li></ol></div>");

        document.Text.Should().Be("Dear Customer,\n\nPayment is due.\nFirst\nSecond");
        var customer = document.Text.IndexOf("Customer", StringComparison.Ordinal);
        document.GetRange(customer, customer + 8).CharacterFormat.Bold.Should().Be(FormatEffect.On);
        var payment = document.Text.IndexOf("Payment", StringComparison.Ordinal);
        document.GetRange(payment, payment + 7).CharacterFormat.ForegroundColor.Should().Be(Color.FromArgb(255, 230, 0, 0));
        var first = document.Text.IndexOf("First", StringComparison.Ordinal);
        document.GetRange(first, first + 5).ParagraphFormat.ListType.Should().Be(MarkerType.Arabic);
    }

    [Test]
    public void PasteHtml_UsesTheSameCodecInsertsAtSelectionAndIsUndoable()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "before after");
        document.ClearUndoRedoHistory();
        document.Selection.SetRange(7, 7);

        document.PasteHtml("<strong>X</strong><script>alert(1)</script>");

        document.Text.Should().Be("before Xafter");
        document.GetRange(7, 8).CharacterFormat.Bold.Should().Be(FormatEffect.On);
        document.Selection.StartPosition.Should().Be(8);

        document.Undo();
        document.Text.Should().Be("before after");

        document.Selection.SetRange(0, document.Length);
        document.PasteHtml("<p>1</p><p>2</p>");
        document.Text.Should().Be("1\n2");
    }

    [Test]
    public void PasteHtml_HonorsMaxLength()
    {
        var document = new RichDocument();
        document.SetText(TextSetOptions.None, "12345");
        document.Selection.SetRange(5, 5);

        document.PasteHtml("<p>67890</p>", maxLength: 7);

        document.Text.Should().Be("1234567");
    }

    [Test]
    public void ExtractClipboardFragment_UnwrapsCfHtmlPayloads()
    {
        var cfHtml =
            "Version:0.9\r\nStartHTML:00000097\r\nEndHTML:00000200\r\nStartFragment:00000133\r\nEndFragment:00000167\r\n" +
            "<html><body><!--StartFragment--><p>Hi <b>there</b></p><!--EndFragment--></body></html>";

        var fragment = HtmlCodec.ExtractClipboardFragment(cfHtml);
        fragment.Should().Be("<p>Hi <b>there</b></p>");

        var document = new RichDocument();
        document.SetHtml(fragment);
        document.Text.Should().Be("Hi there");
        var there = document.Text.IndexOf("there", StringComparison.Ordinal);
        document.GetRange(there, there + 5).CharacterFormat.Bold.Should().Be(FormatEffect.On);

        HtmlCodec.ExtractClipboardFragment("<p>plain</p>").Should().Be("<p>plain</p>");
    }

    [Test]
    public void SetHtml_MapsSpanStylesAndFontAttributes()
    {
        var document = new RichDocument();
        document.SetHtml(
            "<p><span style=\"font-weight: bold; text-decoration: underline line-through; " +
            "font-size: 16px; font-family: 'Segoe UI', sans-serif; background-color: #ffff00\">styled</span>" +
            "<font color=\"#0066cc\">link-ish</font></p>");

        var format = document.GetRange(0, 6).CharacterFormat;
        format.Bold.Should().Be(FormatEffect.On);
        format.Underline.Should().Be(UnderlineType.Single);
        format.Strikethrough.Should().Be(FormatEffect.On);
        format.Size.Should().Be(12); // 16px == 12pt
        format.Name.Should().Be("Segoe UI");
        format.BackgroundColor.Should().Be(Color.FromArgb(255, 255, 255, 0));
        var tail = document.Text.IndexOf("link-ish", StringComparison.Ordinal);
        document.GetRange(tail, tail + 8).CharacterFormat.ForegroundColor.Should().Be(Color.FromArgb(255, 0, 102, 204));
    }

    [Test]
    public void SetHtml_TreatsLineBreaksLikeBrowsers()
    {
        var document = new RichDocument();
        document.SetHtml("<p>a<br>b</p><p>trailing<br></p>");
        document.Text.Should().Be("a\nb\ntrailing");

        document.SetHtml("a<br><br>b");
        document.Text.Should().Be("a\n\nb");
    }
}
