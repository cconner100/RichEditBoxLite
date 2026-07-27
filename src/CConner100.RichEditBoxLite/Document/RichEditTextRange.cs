using Microsoft.UI.Text;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace CConner100.RichEditBoxLite;

public sealed class RichEditTextRange
{
    private int _start;
    private int _end;

    internal RichEditTextRange(RichEditTextDocument document, int start, int end)
    {
        Document = document;
        SetRange(start, end);
        CharacterFormat = new RichTextCharacterFormat(this);
        ParagraphFormat = new RichTextParagraphFormat(this);
    }

    internal RichEditTextDocument Document { get; }
    public RichTextCharacterFormat CharacterFormat { get; }
    public RichTextParagraphFormat ParagraphFormat { get; }
    public int StartPosition { get => _start; set => SetRange(value, _end); }
    public int EndPosition { get => _end; set => SetRange(_start, value); }
    public int Length => Math.Abs(_end - _start);
    public int StoryLength => Document.Length;
    public string Text { get => Document.GetText(NormalizedStart, Length); set => SetText(TextSetOptions.None, value); }
    public string Link { get; set; } = string.Empty;
    internal int NormalizedStart => Math.Min(_start, _end);

    public void SetRange(int startPosition, int endPosition)
    {
        _start = Math.Clamp(startPosition, 0, Document.Length);
        _end = Math.Clamp(endPosition, 0, Document.Length);
    }

    public RichEditTextRange GetClone() => new(Document, _start, _end);
    public void Collapse(bool start) => SetRange(start ? NormalizedStart : Math.Max(_start, _end), start ? NormalizedStart : Math.Max(_start, _end));
    public bool InRange(RichEditTextRange range) => range.Document == Document && NormalizedStart >= range.NormalizedStart && Math.Max(_start, _end) <= Math.Max(range._start, range._end);
    public bool InStory(RichEditTextRange range) => range.Document == Document;
    public bool IsEqual(RichEditTextRange range) => range.Document == Document && range._start == _start && range._end == _end;

    public void GetText(TextGetOptions options, out string value) => value = Text;
    public void SetText(TextSetOptions options, string value)
    {
        var start = NormalizedStart;
        Document.Replace(start, Length, value ?? string.Empty);
        SetRange(start + (value?.Length ?? 0), start + (value?.Length ?? 0));
    }

    public int FindText(string value, int scanLength, FindOptions options)
    {
        var comparison = options.HasFlag(FindOptions.Case) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var start = NormalizedStart;
        var available = Math.Min(Math.Abs(scanLength), Document.Length - start);
        var index = Document.Text.IndexOf(value, start, available, comparison);
        if (index < 0)
        {
            return 0;
        }
        SetRange(index, index + value.Length);
        return value.Length;
    }

    public void ChangeCase(LetterCase value)
    {
        Text = value switch
        {
            LetterCase.Lower => Text.ToLowerInvariant(),
            LetterCase.Upper => Text.ToUpperInvariant(),
            _ => Text
        };
    }

    public int Delete(TextRangeUnit unit, int count)
    {
        if (Length > 0)
        {
            var removed = Length;
            SetText(TextSetOptions.None, string.Empty);
            return removed;
        }
        var amount = Math.Min(Math.Abs(count), count < 0 ? NormalizedStart : Document.Length - NormalizedStart);
        var start = count < 0 ? NormalizedStart - amount : NormalizedStart;
        Document.Replace(start, amount, string.Empty);
        SetRange(start, start);
        return amount;
    }

    public int Move(TextRangeUnit unit, int count)
    {
        var target = Math.Clamp(_end + count, 0, Document.Length);
        SetRange(target, target);
        return target;
    }

    public int MoveStart(TextRangeUnit unit, int count) { var before = _start; StartPosition += count; return _start - before; }
    public int MoveEnd(TextRangeUnit unit, int count) { var before = _end; EndPosition += count; return _end - before; }
    public int StartOf(TextRangeUnit unit, bool extend) { var p = Document.StartOfUnit(NormalizedStart, unit); SetRange(extend ? _start : p, p); return p; }
    public int EndOf(TextRangeUnit unit, bool extend) { var p = Document.EndOfUnit(Math.Max(_start, _end), unit); SetRange(extend ? _start : p, p); return p; }
    public int Expand(TextRangeUnit unit) { var s = Document.StartOfUnit(NormalizedStart, unit); var e = Document.EndOfUnit(Math.Max(_start, _end), unit); SetRange(s, e); return e - s; }

    public void GetRect(PointOptions options, out Rect rect, out int hit)
    {
        rect = Document.GetRectForPosition(NormalizedStart);
        hit = rect.IsEmpty ? 0 : 1;
    }

    public void GetPoint(HorizontalCharacterAlignment horizontalAlign, VerticalCharacterAlignment verticalAlign, PointOptions options, out Point point)
    {
        var rect = Document.GetRectForPosition(NormalizedStart);
        point = new Point(rect.X, rect.Y);
    }

    public void ScrollIntoView(PointOptions value) => Document.RequestScrollIntoView(NormalizedStart);

    public void InsertImage(int width, int height, int ascent, VerticalCharacterAlignment verticalAlign, string alternateText, IRandomAccessStream value) =>
        Document.InsertImage(NormalizedStart, width, height, alternateText, value);

    public void ClearFormatting() => ClearFormatting(RichTextClearFormattingOptions.All);

    public void ClearFormatting(RichTextClearFormattingOptions options)
    {
        var ownsUndoGroup = !Document.IsInUndoGroup;
        if (ownsUndoGroup)
        {
            Document.BeginUndoGroup();
        }
        try
        {
            if (options.HasFlag(RichTextClearFormattingOptions.Character))
            {
                ResetCharacterFormat();
            }
            if (options.HasFlag(RichTextClearFormattingOptions.Paragraph))
            {
                ResetParagraphFormat();
            }
        }
        finally
        {
            if (ownsUndoGroup)
            {
                Document.EndUndoGroup();
            }
        }
    }

    internal void ApplyCharacterFormat(Func<CharacterFormatState, CharacterFormatState> change) =>
        Document.ApplyCharacterFormat(NormalizedStart, Math.Max(Length, 1), change);

    internal void ApplyParagraphFormat(Func<ParagraphFormatState, ParagraphFormatState> change) =>
        Document.ApplyParagraphFormat(NormalizedStart, Math.Max(Length, 1), change);

    internal void ResetCharacterFormat() =>
        Document.ApplyCharacterFormat(NormalizedStart, Math.Max(Length, 1), _ => Document.DefaultCharacterFormat);

    internal void ResetParagraphFormat() =>
        Document.ApplyParagraphFormat(NormalizedStart, Math.Max(Length, 1), _ => Document.DefaultParagraphFormat);
}
