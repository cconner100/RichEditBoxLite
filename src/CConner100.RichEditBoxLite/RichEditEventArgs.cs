using Windows.Foundation;

namespace CConner100.RichEditBoxLite;

public sealed class RichEditBoxLiteTextChangingEventArgs : EventArgs
{
    public RichEditBoxLiteTextChangingEventArgs(bool isContentChanging) =>
        IsContentChanging = isContentChanging;

    public bool IsContentChanging { get; }
}

public sealed class RichEditBoxLiteSelectionChangingEventArgs : EventArgs
{
    public RichEditBoxLiteSelectionChangingEventArgs(int selectionStart, int selectionLength)
    {
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
    }

    public bool Cancel { get; set; }
    public int SelectionStart { get; }
    public int SelectionLength { get; }
}

public sealed class RichEditBoxLiteClipboardEventArgs : EventArgs
{
    public bool Handled { get; set; }
}

public sealed class RichEditBoxLitePasteEventArgs : EventArgs
{
    public bool Handled { get; set; }
}

public sealed class RichEditBoxLiteCompositionEventArgs : EventArgs
{
    public RichEditBoxLiteCompositionEventArgs(string text, int start, int length)
    {
        Text = text;
        Start = start;
        Length = length;
    }

    public string Text { get; }
    public int Start { get; }
    public int Length { get; }
}

public sealed class RichEditBoxLiteContextMenuOpeningEventArgs : EventArgs
{
    public bool Handled { get; set; }
    public Point Position { get; init; }
}

public sealed class RichEditBoxLiteCandidateWindowBoundsChangedEventArgs : EventArgs
{
    public Rect Bounds { get; init; }
}
