using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace CConner100.RichEditBoxLite;

internal sealed class RichTextCanvas : SKCanvasElement
{
    private readonly List<GlyphLayout> _glyphs = [];
    private readonly List<MarkerLayout> _markers = [];
    private RichEditTextDocument? _document;
    private TextWrapping _textWrapping = TextWrapping.Wrap;
    private double _viewportWidth;

    public RichEditTextDocument? Document
    {
        get => _document;
        set
        {
            if (_document == value) return;
            if (_document is not null) _document.Changed -= OnDocumentChanged;
            _document = value;
            if (_document is not null)
            {
                _document.Changed += OnDocumentChanged;
                _document.PositionRectProvider = GetRectForPosition;
            }
            InvalidateMeasure();
            Invalidate();
        }
    }

    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public bool ShowCaret { get; set; }
    public IReadOnlyList<SpellingError> SpellingErrors { get; set; } = [];
    public SKColor SelectionColor { get; set; } = new(0, 120, 215, 110);
    public SKColor CaretColor { get; set; } = SKColors.Black;
    public SKColor DefaultTextColor { get; set; } = SKColors.Black;
    public float HorizontalPadding { get; set; } = 8;
    public float VerticalPadding { get; set; } = 6;

    public TextWrapping TextWrapping
    {
        get => _textWrapping;
        set
        {
            if (_textWrapping == value) return;
            _textWrapping = value;
            InvalidateMeasure();
            Invalidate();
        }
    }

    public double ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            var width = double.IsFinite(value) ? Math.Max(0, value) : 0;
            if (Math.Abs(_viewportWidth - width) < .5) return;
            _viewportWidth = width;
            InvalidateMeasure();
            Invalidate();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width;
        if (TextWrapping == TextWrapping.NoWrap)
        {
            Layout(float.PositiveInfinity);
            var contentRight = Math.Max(
                _glyphs.Count == 0 ? HorizontalPadding : _glyphs.Max(glyph => glyph.Rect.Right),
                _markers.Count == 0 ? HorizontalPadding : _markers.Max(marker => marker.Rect.Right));
            var contentWidth = contentRight + HorizontalPadding;
            width = double.IsFinite(availableSize.Width)
                ? Math.Max(Math.Max(1, availableSize.Width), contentWidth)
                : Math.Max(1, contentWidth);
        }
        else
        {
            width = ResolveMeasureWidth(availableSize.Width, ViewportWidth);
            Layout((float)width);
        }
        var height = _glyphs.Count == 0 ? 32 : _glyphs.Max(g => g.Rect.Bottom) + VerticalPadding;
        return new Size(width, Math.Max(32, height));
    }

    internal static double ResolveMeasureWidth(double availableWidth, double viewportWidth) =>
        double.IsFinite(availableWidth)
            ? Math.Max(1, availableWidth)
            : double.IsFinite(viewportWidth) && viewportWidth > 0
                ? viewportWidth
                : 1;

    internal int GetPositionFromPoint(Point point, double layoutWidth)
    {
        Layout((float)Math.Max(1, layoutWidth));
        return GetPositionFromPoint(_glyphs, point, _document?.Length ?? 0);
    }

    internal static int GetPositionFromPoint(RichEditTextDocument document, Point point, double layoutWidth)
    {
        var glyphs = new List<GlyphLayout>();
        var markers = new List<MarkerLayout>();
        Layout(document, (float)Math.Max(1, layoutWidth), TextWrapping.Wrap, 8, 6, SKColors.Black, glyphs, markers);
        return GetPositionFromPoint(glyphs, point, document.Length);
    }

    internal static Rect GetPositionRect(RichEditTextDocument document, int position, double layoutWidth)
    {
        var glyphs = new List<GlyphLayout>();
        var markers = new List<MarkerLayout>();
        Layout(document, (float)Math.Max(1, layoutWidth), TextWrapping.Wrap, 8, 6, SKColors.Black, glyphs, markers);
        return GetRectForPosition(glyphs, position, 8, 6);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        Layout((float)Math.Max(1, area.Width));
        var selectionEnd = SelectionStart + SelectionLength;
        using var selectionPaint = new SKPaint { Color = SelectionColor, IsAntialias = true };
        foreach (var glyph in _glyphs.Where(g => g.Index >= SelectionStart && g.Index < selectionEnd))
        {
            canvas.DrawRect(glyph.Rect, selectionPaint);
        }

        foreach (var marker in _markers)
        {
            using var paint = CreatePaint(marker.Format, DefaultTextColor);
            canvas.DrawText(marker.Text, marker.Rect.Left, GetBaseline(marker.Rect, paint), paint);
        }

        foreach (var glyph in _glyphs)
        {
            var format = glyph.Format;
            using var paint = CreatePaint(format, DefaultTextColor);
            if (format.BackgroundColor.A > 0)
            {
                using var background = new SKPaint { Color = ToSkColor(format.BackgroundColor) };
                canvas.DrawRect(glyph.Rect, background);
            }
            if (glyph.Character != '\n')
            {
                var baseline = GetBaseline(glyph.Rect, paint);
                if (format.Subscript) baseline += format.Size * .25f;
                if (format.Superscript) baseline -= format.Size * .35f;
                canvas.DrawText(glyph.Character.ToString(), glyph.Rect.Left, baseline, paint);
                if (format.Underline)
                {
                    canvas.DrawLine(glyph.Rect.Left, glyph.Rect.Bottom - 1, glyph.Rect.Right, glyph.Rect.Bottom - 1, paint);
                }
                if (format.Strikethrough)
                {
                    var y = glyph.Rect.Top + glyph.Rect.Height * .55f;
                    canvas.DrawLine(glyph.Rect.Left, y, glyph.Rect.Right, y, paint);
                }
            }
        }

        using var proofingPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, IsAntialias = true };
        foreach (var error in SpellingErrors)
        {
            foreach (var glyph in _glyphs.Where(g => g.Index >= error.Start && g.Index < error.Start + error.Length))
            {
                var y = glyph.Rect.Bottom + 1;
                canvas.DrawLine(glyph.Rect.Left, y, glyph.Rect.Right, y, proofingPaint);
            }
        }

        if (ShowCaret && SelectionLength == 0)
        {
            var rect = GetRectForPosition(SelectionStart);
            using var caretPaint = new SKPaint { Color = CaretColor, StrokeWidth = 1.5f };
            canvas.DrawLine((float)rect.X, (float)rect.Y, (float)rect.X, (float)rect.Bottom, caretPaint);
        }
    }

    private void Layout(float width)
    {
        _glyphs.Clear();
        _markers.Clear();
        if (_document is null) return;
        Layout(_document, width, TextWrapping, HorizontalPadding, VerticalPadding, DefaultTextColor, _glyphs, _markers);
    }

    private static void Layout(
        RichEditTextDocument document,
        float width,
        TextWrapping textWrapping,
        float horizontalPadding,
        float verticalPadding,
        SKColor defaultTextColor,
        List<GlyphLayout> glyphs,
        List<MarkerLayout> markers)
    {
        var x = horizontalPadding;
        var y = verticalPadding;
        var available = Math.Max(20, width - horizontalPadding * 2);
        var lineHeight = 22f;
        var lineStartX = horizontalPadding;
        var orderedListCounter = 0;
        var previousWasOrdered = false;
        for (var index = 0; index < document.Text.Length; index++)
        {
            var ch = document.Text[index];
            var paragraphStart = index == 0 || document.Text[index - 1] == '\n';
            var paragraphFormat = document.GetParagraphFormat(index);
            var format = ApplyParagraphStyle(document.GetCharacterFormat(index), paragraphFormat);
            if (paragraphStart)
            {
                lineHeight = Math.Max(22, format.Size * 1.45f);
                var paragraphIndent = Math.Max(0, paragraphFormat.LeftIndent);
                lineStartX = horizontalPadding + paragraphIndent;
                x = lineStartX;
                var markerText = GetMarkerText(paragraphFormat, ref orderedListCounter, ref previousWasOrdered);
                if (markerText is not null)
                {
                    using var markerPaint = CreatePaint(format, defaultTextColor);
                    var markerWidth = markerPaint.MeasureText(markerText);
                    markers.Add(new MarkerLayout(
                        markerText,
                        new SKRect(x, y, x + markerWidth, y + lineHeight),
                        format));
                    x += Math.Max(28, markerWidth + 8);
                    lineStartX = x;
                }
            }
            using var paint = CreatePaint(format, defaultTextColor);
            lineHeight = Math.Max(lineHeight, format.Size * 1.45f);
            var isLineBreak = ch is '\r' or '\n';
            if (ch == '\n' && index > 0 && document.Text[index - 1] == '\r')
            {
                continue;
            }
            var glyphWidth = ch switch
            {
                '\t' => paint.MeasureText("    "),
                '\r' or '\n' => 0,
                '\uFFFC' => Math.Max(48, format.Size * 3),
                _ => Math.Max(1, paint.MeasureText(ch.ToString()) + format.Spacing)
            };
            if (isLineBreak)
            {
                glyphs.Add(new GlyphLayout(index, ch, new SKRect(x, y, x + Math.Max(1, glyphWidth), y + lineHeight), format));
                x = lineStartX;
                y += lineHeight;
                lineHeight = 22;
                continue;
            }
            if (textWrapping != TextWrapping.NoWrap && x + glyphWidth > horizontalPadding + available)
            {
                x = lineStartX;
                y += lineHeight;
                lineHeight = Math.Max(22, format.Size * 1.45f);
            }
            glyphs.Add(new GlyphLayout(index, ch, new SKRect(x, y, x + glyphWidth, y + lineHeight), format));
            x += glyphWidth;
        }
    }

    private Rect GetRectForPosition(int position)
        => GetRectForPosition(_glyphs, position, HorizontalPadding, VerticalPadding);

    private static Rect GetRectForPosition(
        IReadOnlyList<GlyphLayout> glyphs,
        int position,
        float horizontalPadding,
        float verticalPadding)
    {
        if (glyphs.Count == 0) return new Rect(horizontalPadding, verticalPadding, 1, 22);
        position = Math.Max(0, position);
        var glyph = glyphs.FirstOrDefault(candidate => candidate.Index >= position);
        if (glyph is null)
        {
            var last = glyphs[^1].Rect;
            return new Rect(last.Right, last.Top, 1, last.Height);
        }
        var rect = glyph.Rect;
        return new Rect(rect.Left, rect.Top, 1, rect.Height);
    }

    private static int GetPositionFromPoint(IReadOnlyList<GlyphLayout> glyphs, Point point, int documentLength)
    {
        if (glyphs.Count == 0) return 0;

        var lines = glyphs
            .GroupBy(glyph => glyph.Rect.Top)
            .Select(group => group.OrderBy(glyph => glyph.Rect.Left).ToArray())
            .ToArray();
        var line = lines.MinBy(candidate => VerticalDistance(point.Y, candidate))!;

        foreach (var glyph in line)
        {
            var midpoint = glyph.Rect.Left + glyph.Rect.Width / 2;
            if (point.X < midpoint) return Math.Clamp(glyph.Index, 0, documentLength);
            if (point.X <= glyph.Rect.Right)
            {
                var position = glyph.Character is '\r' or '\n' ? glyph.Index : glyph.Index + 1;
                return Math.Clamp(position, 0, documentLength);
            }
        }

        var last = line[^1];
        var endPosition = last.Character is '\r' or '\n' ? last.Index : last.Index + 1;
        return Math.Clamp(endPosition, 0, documentLength);
    }

    private static double VerticalDistance(double y, IReadOnlyList<GlyphLayout> line)
    {
        var top = line.Min(glyph => glyph.Rect.Top);
        var bottom = line.Max(glyph => glyph.Rect.Bottom);
        return y < top ? top - y : y > bottom ? y - bottom : 0;
    }

    private static SKPaint CreatePaint(CharacterFormatState format, SKColor defaultTextColor)
    {
        var style = format.Bold && format.Italic ? SKFontStyle.BoldItalic
            : format.Bold ? SKFontStyle.Bold
            : format.Italic ? SKFontStyle.Italic
            : SKFontStyle.Normal;
        return new SKPaint
        {
            IsAntialias = true,
            Color = format.ForegroundColor.A == 0 ? defaultTextColor : ToSkColor(format.ForegroundColor),
            TextSize = format.Size,
            Typeface = SKTypeface.FromFamilyName(format.FontFamily, style)
        };
    }

    private static CharacterFormatState ApplyParagraphStyle(CharacterFormatState format, ParagraphFormatState paragraph) =>
        paragraph.HeadingLevel switch
        {
            RichTextHeadingLevel.Heading1 => format with { Bold = true, Size = Math.Max(32, format.Size) },
            RichTextHeadingLevel.Heading2 => format with { Bold = true, Size = Math.Max(24, format.Size) },
            _ => format
        };

    internal static string? GetMarkerText(
        ParagraphFormatState paragraph,
        ref int orderedListCounter,
        ref bool previousWasOrdered)
    {
        if (paragraph.ListType == MarkerType.Bullet)
        {
            previousWasOrdered = false;
            orderedListCounter = 0;
            return "•";
        }
        if (paragraph.ListType == MarkerType.Arabic)
        {
            orderedListCounter = previousWasOrdered
                ? orderedListCounter + 1
                : Math.Max(1, paragraph.ListStart);
            previousWasOrdered = true;
            return $"{orderedListCounter}.";
        }
        previousWasOrdered = false;
        orderedListCounter = 0;
        return null;
    }

    private static float GetBaseline(SKRect rect, SKPaint paint)
    {
        var metrics = paint.FontMetrics;
        var textHeight = metrics.Descent - metrics.Ascent;
        return rect.Top + (rect.Height - textHeight) / 2 - metrics.Ascent;
    }

    private static SKColor ToSkColor(Windows.UI.Color color) => new(color.R, color.G, color.B, color.A);
    private void OnDocumentChanged(object? sender, EventArgs e) { InvalidateMeasure(); Invalidate(); }
    private sealed record GlyphLayout(int Index, char Character, SKRect Rect, CharacterFormatState Format);
    private sealed record MarkerLayout(string Text, SKRect Rect, CharacterFormatState Format);
}
