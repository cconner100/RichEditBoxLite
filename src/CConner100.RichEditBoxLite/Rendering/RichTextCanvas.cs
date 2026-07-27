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

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : Math.Max(1, availableSize.Width);
        Layout((float)width);
        var height = _glyphs.Count == 0 ? 32 : _glyphs.Max(g => g.Rect.Bottom) + VerticalPadding;
        return new Size(width, Math.Max(32, height));
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
            using var paint = CreatePaint(marker.Format);
            canvas.DrawText(marker.Text, marker.Rect.Left, GetBaseline(marker.Rect, paint), paint);
        }

        foreach (var glyph in _glyphs)
        {
            var format = glyph.Format;
            using var paint = CreatePaint(format);
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
        var x = HorizontalPadding;
        var y = VerticalPadding;
        var available = Math.Max(20, width - HorizontalPadding * 2);
        var lineHeight = 22f;
        var lineStartX = HorizontalPadding;
        var orderedListCounter = 0;
        var previousWasOrdered = false;
        for (var index = 0; index < _document.Text.Length; index++)
        {
            var ch = _document.Text[index];
            var paragraphStart = index == 0 || _document.Text[index - 1] == '\n';
            var paragraphFormat = _document.GetParagraphFormat(index);
            var format = ApplyParagraphStyle(_document.GetCharacterFormat(index), paragraphFormat);
            if (paragraphStart)
            {
                lineHeight = Math.Max(22, format.Size * 1.45f);
                var paragraphIndent = Math.Max(0, paragraphFormat.LeftIndent);
                lineStartX = HorizontalPadding + paragraphIndent;
                x = lineStartX;
                var markerText = GetMarkerText(paragraphFormat, ref orderedListCounter, ref previousWasOrdered);
                if (markerText is not null)
                {
                    using var markerPaint = CreatePaint(format);
                    var markerWidth = markerPaint.MeasureText(markerText);
                    _markers.Add(new MarkerLayout(
                        markerText,
                        new SKRect(x, y, x + markerWidth, y + lineHeight),
                        format));
                    x += Math.Max(28, markerWidth + 8);
                    lineStartX = x;
                }
            }
            using var paint = CreatePaint(format);
            lineHeight = Math.Max(lineHeight, format.Size * 1.45f);
            var isLineBreak = ch is '\r' or '\n';
            if (ch == '\n' && index > 0 && _document.Text[index - 1] == '\r')
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
            if (isLineBreak || x + glyphWidth > HorizontalPadding + available)
            {
                _glyphs.Add(new GlyphLayout(index, ch, new SKRect(x, y, x + Math.Max(1, glyphWidth), y + lineHeight), format));
                x = lineStartX;
                y += lineHeight;
                lineHeight = 22;
                if (isLineBreak) continue;
            }
            _glyphs.Add(new GlyphLayout(index, ch, new SKRect(x, y, x + glyphWidth, y + lineHeight), format));
            x += glyphWidth;
        }
    }

    private Rect GetRectForPosition(int position)
    {
        if (_glyphs.Count == 0) return new Rect(HorizontalPadding, VerticalPadding, 1, 22);
        if (position >= _glyphs.Count)
        {
            var last = _glyphs[^1].Rect;
            return new Rect(last.Right, last.Top, 1, last.Height);
        }
        var rect = _glyphs[Math.Max(0, position)].Rect;
        return new Rect(rect.Left, rect.Top, 1, rect.Height);
    }

    private SKPaint CreatePaint(CharacterFormatState format)
    {
        var style = format.Bold && format.Italic ? SKFontStyle.BoldItalic
            : format.Bold ? SKFontStyle.Bold
            : format.Italic ? SKFontStyle.Italic
            : SKFontStyle.Normal;
        return new SKPaint
        {
            IsAntialias = true,
            Color = format.ForegroundColor.A == 0 ? DefaultTextColor : ToSkColor(format.ForegroundColor),
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
