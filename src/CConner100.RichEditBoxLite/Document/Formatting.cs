using Microsoft.UI.Text;
using Windows.UI;
using FontStyle = Windows.UI.Text.FontStyle;

namespace CConner100.RichEditBoxLite;

public sealed record CharacterFormatState
{
    public string FontFamily { get; init; } = "Open Sans";
    public float Size { get; init; } = 14;
    public int Weight { get; init; } = 400;
    public FontStyle FontStyle { get; init; } = FontStyle.Normal;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool Strikethrough { get; init; }
    public bool Subscript { get; init; }
    public bool Superscript { get; init; }
    public Color ForegroundColor { get; init; } = Color.FromArgb(0, 0, 0, 0);
    public Color BackgroundColor { get; init; } = Color.FromArgb(0, 0, 0, 0);
    public float Spacing { get; init; }
    public string LanguageTag { get; init; } = "en-US";
}

public sealed record ParagraphFormatState
{
    public ParagraphAlignment Alignment { get; init; } = ParagraphAlignment.Left;
    public float FirstLineIndent { get; init; }
    public float LeftIndent { get; init; }
    public float RightIndent { get; init; }
    public float LineSpacing { get; init; }
    public float SpaceBefore { get; init; }
    public float SpaceAfter { get; init; }
    public MarkerType ListType { get; init; } = MarkerType.None;
    public MarkerStyle ListStyle { get; init; } = MarkerStyle.Undefined;
    public int ListStart { get; init; } = 1;
    public bool RightToLeft { get; init; }
}

internal sealed record FormatRun(int Start, int Length, CharacterFormatState Format)
{
    public int End => Start + Length;
}

public sealed class RichTextCharacterFormat
{
    private readonly RichEditTextRange _range;

    internal RichTextCharacterFormat(RichEditTextRange range) => _range = range;

    private CharacterFormatState Current => _range.Document.GetCharacterFormat(_range.StartPosition);
    private void Apply(Func<CharacterFormatState, CharacterFormatState> change) => _range.ApplyCharacterFormat(change);

    public FormatEffect Bold { get => Current.Bold ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { Bold = value == FormatEffect.On }); }
    public FormatEffect Italic { get => Current.Italic ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { Italic = value == FormatEffect.On }); }
    public FormatEffect Strikethrough { get => Current.Strikethrough ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { Strikethrough = value == FormatEffect.On }); }
    public FormatEffect Subscript { get => Current.Subscript ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { Subscript = value == FormatEffect.On }); }
    public FormatEffect Superscript { get => Current.Superscript ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { Superscript = value == FormatEffect.On }); }
    public UnderlineType Underline { get => Current.Underline ? UnderlineType.Single : UnderlineType.None; set => Apply(f => f with { Underline = value != UnderlineType.None }); }
    public Color ForegroundColor { get => Current.ForegroundColor; set => Apply(f => f with { ForegroundColor = value }); }
    public Color BackgroundColor { get => Current.BackgroundColor; set => Apply(f => f with { BackgroundColor = value }); }
    public FontStyle FontStyle { get => Current.FontStyle; set => Apply(f => f with { FontStyle = value }); }
    public string Name { get => Current.FontFamily; set => Apply(f => f with { FontFamily = value }); }
    public float Size { get => Current.Size; set => Apply(f => f with { Size = Math.Max(1, value) }); }
    public int Weight { get => Current.Weight; set => Apply(f => f with { Weight = Math.Clamp(value, 1, 999) }); }
    public float Spacing { get => Current.Spacing; set => Apply(f => f with { Spacing = value }); }
    public string LanguageTag { get => Current.LanguageTag; set => Apply(f => f with { LanguageTag = value }); }

    public RichTextCharacterFormat GetClone() => new(_range.GetClone());
}

public sealed class RichTextParagraphFormat
{
    private readonly RichEditTextRange _range;

    internal RichTextParagraphFormat(RichEditTextRange range) => _range = range;

    private ParagraphFormatState Current => _range.Document.GetParagraphFormat(_range.StartPosition);
    private void Apply(Func<ParagraphFormatState, ParagraphFormatState> change) => _range.ApplyParagraphFormat(change);

    public ParagraphAlignment Alignment { get => Current.Alignment; set => Apply(f => f with { Alignment = value }); }
    public float FirstLineIndent => Current.FirstLineIndent;
    public float LeftIndent => Current.LeftIndent;
    public float RightIndent { get => Current.RightIndent; set => Apply(f => f with { RightIndent = value }); }
    public float LineSpacing => Current.LineSpacing;
    public float SpaceBefore { get => Current.SpaceBefore; set => Apply(f => f with { SpaceBefore = value }); }
    public float SpaceAfter { get => Current.SpaceAfter; set => Apply(f => f with { SpaceAfter = value }); }
    public MarkerType ListType { get => Current.ListType; set => Apply(f => f with { ListType = value }); }
    public MarkerStyle ListStyle { get => Current.ListStyle; set => Apply(f => f with { ListStyle = value }); }
    public int ListStart { get => Current.ListStart; set => Apply(f => f with { ListStart = value }); }
    public FormatEffect RightToLeft { get => Current.RightToLeft ? FormatEffect.On : FormatEffect.Off; set => Apply(f => f with { RightToLeft = value == FormatEffect.On }); }

    public void SetIndents(float start, float left, float right) =>
        Apply(f => f with { FirstLineIndent = start, LeftIndent = left, RightIndent = right });

    public void SetLineSpacing(LineSpacingRule rule, float spacing) =>
        Apply(f => f with { LineSpacing = spacing });
}
