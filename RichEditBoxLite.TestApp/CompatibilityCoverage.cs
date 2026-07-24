namespace RichEditBoxLite.TestApp;

public static class CompatibilityCoverage
{
    public static IReadOnlyList<string> DependencyProperties { get; } =
    [
        "AcceptsReturn", "CharacterCasing", "ClipboardCopyFormat", "Description",
        "DesiredCandidateWindowAlignment", "DisabledFormattingAccelerators", "Header",
        "HeaderTemplate", "HorizontalTextAlignment", "InputScope", "IsColorFontEnabled",
        "IsReadOnly", "IsSpellCheckEnabled", "IsTextPredictionEnabled", "MaxLength",
        "PlaceholderText", "PreventKeyboardDisplayOnProgrammaticFocus", "ProofingLanguage",
        "ProofingMenuFlyout", "SelectionFlyout", "SelectionHighlightColor",
        "SelectionHighlightColorWhenNotFocused", "TextAlignment", "TextReadingOrder",
        "TextWrapping"
    ];

    public static IReadOnlyList<string> Events { get; } =
    [
        "CandidateWindowBoundsChanged", "ContextMenuOpening", "CopyingToClipboard",
        "CuttingToClipboard", "Paste", "SelectionChanged", "SelectionChanging",
        "TextChanged", "TextChanging", "TextCompositionChanged", "TextCompositionEnded",
        "TextCompositionStarted"
    ];

    public static IReadOnlyList<string> UnsupportedProperties { get; } =
    [
        "TextReadingOrder — retained; rendering remains left-to-right",
        "DesiredCandidateWindowAlignment — retained; host controls candidate placement",
        "IsColorFontEnabled — retained; native color-glyph behavior varies by Skia host",
        "ProofingMenuFlyout — retained; automatic native placement is not implemented",
        "SelectionFlyout — retained; custom flyout may be supplied",
        "Pagination paragraph flags — round-tripped without continuous-layout effect",
        "MathML, handwriting, OLE and advanced Word destinations — NotSupportedException"
    ];
}
