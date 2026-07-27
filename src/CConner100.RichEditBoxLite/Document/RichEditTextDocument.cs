using System.Collections.ObjectModel;
using Microsoft.UI.Text;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace CConner100.RichEditBoxLite;

public sealed class RichEditTextDocument
{
    private readonly List<FormatRun> _runs = [];
    private readonly Dictionary<int, ParagraphFormatState> _paragraphFormats = [];
    private readonly Stack<DocumentSnapshot> _undo = [];
    private readonly Stack<DocumentSnapshot> _redo = [];
    private bool _inUndoGroup;
    private DocumentSnapshot? _groupStart;
    private string _text = string.Empty;

    public RichEditTextDocument()
    {
        Selection = new RichEditTextRange(this, 0, 0);
        DefaultCharacterFormat = new CharacterFormatState();
        DefaultParagraphFormat = new ParagraphFormatState();
    }

    public event EventHandler? Changed;
    internal event EventHandler<int>? ScrollIntoViewRequested;
    internal Func<int, Rect>? PositionRectProvider { get; set; }
    public RichEditTextRange Selection { get; }
    public uint UndoLimit { get; set; } = 100;
    public float DefaultTabStop { get; set; } = 36;
    public CaretType CaretType { get; set; } = CaretType.Normal;
    public CharacterFormatState DefaultCharacterFormat { get; private set; }
    public ParagraphFormatState DefaultParagraphFormat { get; private set; }
    public string Text => _text;
    public int Length => _text.Length;
    internal IReadOnlyList<FormatRun> Runs => _runs;
    internal bool IsInUndoGroup => _inUndoGroup;

    public bool CanCopy() => Selection.Length > 0;
    public bool CanPaste() => true;
    public bool CanUndo() => _undo.Count > 0;
    public bool CanRedo() => _redo.Count > 0;
    public int ApplyDisplayUpdates() => 0;
    public int BatchDisplayUpdates() => 0;
    public RichEditTextRange GetRange(int startPosition, int endPosition) => new(this, startPosition, endPosition);
    public RichEditTextRange GetRangeFromPoint(Point point, PointOptions options) => new(this, 0, 0);
    public void GetText(TextGetOptions options, out string value) => value = options.HasFlag(TextGetOptions.FormatRtf) ? RtfCodec.Export(this) : _text;
    public void SetText(TextSetOptions options, string value)
    {
        if (options.HasFlag(TextSetOptions.FormatRtf))
        {
            RtfCodec.Import(this, value);
        }
        else
        {
            Replace(0, Length, value ?? string.Empty);
        }
    }

    public void BeginUndoGroup()
    {
        if (!_inUndoGroup)
        {
            _groupStart = Capture();
            _inUndoGroup = true;
        }
    }

    public void EndUndoGroup()
    {
        if (_inUndoGroup && _groupStart is not null && !SnapshotEquals(_groupStart))
        {
            PushUndo(_groupStart);
        }
        _groupStart = null;
        _inUndoGroup = false;
    }

    public void Undo()
    {
        if (_undo.TryPop(out var snapshot))
        {
            _redo.Push(Capture());
            Restore(snapshot);
        }
    }

    public void Redo()
    {
        if (_redo.TryPop(out var snapshot))
        {
            _undo.Push(Capture());
            Restore(snapshot);
        }
    }

    public void ClearUndoRedoHistory() { _undo.Clear(); _redo.Clear(); }

    public void LoadFromStream(TextSetOptions options, IRandomAccessStream value)
    {
        using var stream = value.AsStreamForRead();
        using var reader = new StreamReader(stream, leaveOpen: true);
        SetText(options, reader.ReadToEnd());
    }

    public void SaveToStream(TextGetOptions options, IRandomAccessStream value)
    {
        using var stream = value.AsStreamForWrite();
        using var writer = new StreamWriter(stream, leaveOpen: true);
        GetText(options, out var text);
        writer.Write(text);
        writer.Flush();
    }

    public void SetDefaultCharacterFormat(CharacterFormatState value) => DefaultCharacterFormat = value;
    public void SetDefaultParagraphFormat(ParagraphFormatState value) => DefaultParagraphFormat = value;

    internal string GetText(int start, int length) => _text.Substring(Math.Clamp(start, 0, Length), Math.Clamp(length, 0, Length - Math.Clamp(start, 0, Length)));

    internal void Replace(int start, int length, string replacement)
    {
        start = Math.Clamp(start, 0, Length);
        length = Math.Clamp(length, 0, Length - start);
        RecordUndo();
        var inherited = GetCharacterFormat(Math.Max(0, start - 1));
        var inheritedParagraph = GetParagraphFormat(start);
        var previousParagraphs = _paragraphFormats.ToArray();
        _text = _text.Remove(start, length).Insert(start, replacement);
        RebuildRunsAfterReplace(start, length, replacement.Length, inherited);
        RebuildParagraphsAfterReplace(previousParagraphs, start, length, replacement.Length, inheritedParagraph);
        Selection.SetRange(start + replacement.Length, start + replacement.Length);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal CharacterFormatState GetCharacterFormat(int position)
    {
        position = Math.Clamp(position, 0, Math.Max(0, Length - 1));
        return _runs.FirstOrDefault(r => position >= r.Start && position < r.End)?.Format ?? DefaultCharacterFormat;
    }

    internal ParagraphFormatState GetParagraphFormat(int position)
    {
        var start = StartOfUnit(position, TextRangeUnit.Paragraph);
        return _paragraphFormats.TryGetValue(start, out var value) ? value : DefaultParagraphFormat;
    }

    internal void ApplyCharacterFormat(int start, int length, Func<CharacterFormatState, CharacterFormatState> change)
    {
        if (Length == 0)
        {
            DefaultCharacterFormat = change(DefaultCharacterFormat);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
        RecordUndo();
        start = Math.Clamp(start, 0, Length - 1);
        length = Math.Clamp(length, 1, Length - start);
        var perCharacter = Enumerable.Range(0, Length).Select(GetCharacterFormat).ToArray();
        for (var i = start; i < start + length; i++) perCharacter[i] = change(perCharacter[i]);
        RebuildRuns(perCharacter);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplyParagraphFormat(int start, int length, Func<ParagraphFormatState, ParagraphFormatState> change)
    {
        RecordUndo();
        var end = Math.Min(Length, start + length);
        for (var paragraphStart = StartOfUnit(start, TextRangeUnit.Paragraph); paragraphStart <= end;)
        {
            _paragraphFormats[paragraphStart] = change(GetParagraphFormat(paragraphStart));
            var next = _text.IndexOf('\n', paragraphStart);
            if (next < 0 || next + 1 <= paragraphStart) break;
            paragraphStart = next + 1;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal int StartOfUnit(int position, TextRangeUnit unit)
    {
        position = Math.Clamp(position, 0, Length);
        return unit switch
        {
            TextRangeUnit.Story => 0,
            TextRangeUnit.Paragraph or TextRangeUnit.Line => position == 0 ? 0 : _text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1,
            TextRangeUnit.Word => FindWordStart(position),
            _ => Math.Max(0, position - 1)
        };
    }

    internal int EndOfUnit(int position, TextRangeUnit unit)
    {
        position = Math.Clamp(position, 0, Length);
        return unit switch
        {
            TextRangeUnit.Story => Length,
            TextRangeUnit.Paragraph or TextRangeUnit.Line => _text.IndexOf('\n', position) is var e && e >= 0 ? e + 1 : Length,
            TextRangeUnit.Word => FindWordEnd(position),
            _ => Math.Min(Length, position + 1)
        };
    }

    internal Rect GetRectForPosition(int position) => PositionRectProvider?.Invoke(position) ?? Rect.Empty;
    internal void RequestScrollIntoView(int position) => ScrollIntoViewRequested?.Invoke(this, position);

    internal void InsertImage(int position, int width, int height, string alternateText, IRandomAccessStream stream)
    {
        Replace(position, 0, "\uFFFC");
    }

    internal void ReplaceFromCodec(
        string text,
        IEnumerable<FormatRun> runs,
        IReadOnlyDictionary<int, ParagraphFormatState>? paragraphs = null)
    {
        _text = text;
        _runs.Clear();
        _runs.AddRange(runs);
        _paragraphFormats.Clear();
        if (paragraphs is not null)
        {
            foreach (var paragraph in paragraphs)
            {
                _paragraphFormats[paragraph.Key] = paragraph.Value;
            }
        }
        Selection.SetRange(0, 0);
        ClearUndoRedoHistory();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private int FindWordStart(int position) { while (position > 0 && char.IsLetterOrDigit(_text[position - 1])) position--; return position; }
    private int FindWordEnd(int position) { while (position < Length && char.IsLetterOrDigit(_text[position])) position++; return position; }

    private void RebuildRunsAfterReplace(int start, int removed, int inserted, CharacterFormatState inherited)
    {
        var oldLength = Length - inserted + removed;
        var oldFormats = Enumerable.Range(0, oldLength).Select(i => GetCharacterFormat(Math.Min(i, Math.Max(0, oldLength - 1)))).ToList();
        if (removed > 0 && start < oldFormats.Count) oldFormats.RemoveRange(start, Math.Min(removed, oldFormats.Count - start));
        if (inserted > 0) oldFormats.InsertRange(start, Enumerable.Repeat(inherited, inserted));
        RebuildRuns(oldFormats.ToArray());
    }

    private void RebuildRuns(IReadOnlyList<CharacterFormatState> formats)
    {
        _runs.Clear();
        if (formats.Count == 0) return;
        var start = 0;
        var current = formats[0];
        for (var i = 1; i <= formats.Count; i++)
        {
            if (i == formats.Count || formats[i] != current)
            {
                _runs.Add(new FormatRun(start, i - start, current));
                if (i < formats.Count) { start = i; current = formats[i]; }
            }
        }
    }

    private void RebuildParagraphsAfterReplace(
        IReadOnlyList<KeyValuePair<int, ParagraphFormatState>> previous,
        int start,
        int removed,
        int inserted,
        ParagraphFormatState inherited)
    {
        _paragraphFormats.Clear();
        var removedEnd = start + removed;
        var delta = inserted - removed;
        foreach (var pair in previous)
        {
            var mapped = pair.Key <= start
                ? pair.Key
                : pair.Key >= removedEnd
                    ? pair.Key + delta
                    : -1;
            if (mapped >= 0 && mapped <= Length && (mapped == 0 || _text[mapped - 1] == '\n'))
            {
                _paragraphFormats[mapped] = pair.Value;
            }
        }

        var inheritedStart = StartOfUnit(Math.Clamp(start, 0, Length), TextRangeUnit.Paragraph);
        if (inherited != DefaultParagraphFormat)
        {
            _paragraphFormats[inheritedStart] = inherited;
        }
    }

    private void RecordUndo()
    {
        if (!_inUndoGroup) PushUndo(Capture());
        _redo.Clear();
    }

    private void PushUndo(DocumentSnapshot snapshot)
    {
        _undo.Push(snapshot);
        while (_undo.Count > UndoLimit && _undo.Count > 0)
        {
            var keep = _undo.Reverse().Take((int)UndoLimit).Reverse().ToArray();
            _undo.Clear();
            foreach (var item in keep) _undo.Push(item);
        }
    }

    private DocumentSnapshot Capture() => new(_text, _runs.ToArray(), new Dictionary<int, ParagraphFormatState>(_paragraphFormats), Selection.StartPosition, Selection.EndPosition);
    private bool SnapshotEquals(DocumentSnapshot snapshot) =>
        snapshot.Text == _text
        && snapshot.Runs.SequenceEqual(_runs)
        && snapshot.Paragraphs.Count == _paragraphFormats.Count
        && snapshot.Paragraphs.All(pair => _paragraphFormats.TryGetValue(pair.Key, out var value) && value == pair.Value);
    private void Restore(DocumentSnapshot snapshot)
    {
        _text = snapshot.Text;
        _runs.Clear();
        _runs.AddRange(snapshot.Runs);
        _paragraphFormats.Clear();
        foreach (var pair in snapshot.Paragraphs) _paragraphFormats[pair.Key] = pair.Value;
        Selection.SetRange(snapshot.SelectionStart, snapshot.SelectionEnd);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record DocumentSnapshot(string Text, FormatRun[] Runs, Dictionary<int, ParagraphFormatState> Paragraphs, int SelectionStart, int SelectionEnd);
}
