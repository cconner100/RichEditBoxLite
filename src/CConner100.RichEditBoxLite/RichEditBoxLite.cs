using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace CConner100.RichEditBoxLite;

[TemplatePart(Name = InputBridgePartName, Type = typeof(TextBox))]
[TemplatePart(Name = CanvasPartName, Type = typeof(RichTextCanvas))]
public sealed class RichEditBoxLite : Control
{
    private const string InputBridgePartName = "PART_InputBridge";
    private const string CanvasPartName = "PART_Canvas";
    private TextBox? _inputBridge;
    private RichTextCanvas? _canvas;
    private bool _updatingBridge;
    private bool _updatingDocument;
    private string _lastText = string.Empty;
    private int _lastSelectionStart;
    private int _lastSelectionLength;
    private readonly SpellCheckService _spellCheck = new();

    public RichEditBoxLite()
    {
        DefaultStyleKey = typeof(RichEditBoxLite);
        Document = new RichEditTextDocument();
        Document.Changed += OnDocumentChanged;
        IsTabStop = true;
    }

    public RichEditTextDocument Document { get; }
    public RichEditTextDocument TextDocument => Document;
    public SpellCheckService SpellCheck => _spellCheck;

    public static DependencyProperty AcceptsReturnProperty { get; } = Register(nameof(AcceptsReturn), true);
    public static DependencyProperty CharacterCasingProperty { get; } = Register(nameof(CharacterCasing), CharacterCasing.Normal);
    public static DependencyProperty ClipboardCopyFormatProperty { get; } = Register(nameof(ClipboardCopyFormat), RichEditClipboardFormat.AllFormats);
    public static DependencyProperty DescriptionProperty { get; } = Register<object?>(nameof(Description), null);
    public static DependencyProperty DesiredCandidateWindowAlignmentProperty { get; } = Register(nameof(DesiredCandidateWindowAlignment), CandidateWindowAlignment.Default);
    public static DependencyProperty DisabledFormattingAcceleratorsProperty { get; } = Register(nameof(DisabledFormattingAccelerators), DisabledFormattingAccelerators.None);
    public static DependencyProperty HeaderProperty { get; } = Register<object?>(nameof(Header), null);
    public static DependencyProperty HeaderTemplateProperty { get; } = Register<DataTemplate?>(nameof(HeaderTemplate), null);
    public static DependencyProperty HorizontalTextAlignmentProperty { get; } = Register(nameof(HorizontalTextAlignment), TextAlignment.Left);
    public static DependencyProperty InputScopeProperty { get; } = Register<InputScope?>(nameof(InputScope), null);
    public static DependencyProperty IsColorFontEnabledProperty { get; } = Register(nameof(IsColorFontEnabled), true);
    public static DependencyProperty IsReadOnlyProperty { get; } = Register(nameof(IsReadOnly), false);
    public static DependencyProperty IsSpellCheckEnabledProperty { get; } = Register(nameof(IsSpellCheckEnabled), true);
    public static DependencyProperty IsTextPredictionEnabledProperty { get; } = Register(nameof(IsTextPredictionEnabled), true);
    public static DependencyProperty MaxLengthProperty { get; } = Register(nameof(MaxLength), 0);
    public static DependencyProperty PlaceholderTextProperty { get; } = Register(nameof(PlaceholderText), string.Empty);
    public static DependencyProperty PreventKeyboardDisplayOnProgrammaticFocusProperty { get; } = Register(nameof(PreventKeyboardDisplayOnProgrammaticFocus), false);
    public static DependencyProperty ProofingLanguageProperty { get; } = Register(nameof(ProofingLanguage), "en-US");
    public static DependencyProperty ProofingMenuFlyoutProperty { get; } = Register<FlyoutBase?>(nameof(ProofingMenuFlyout), null);
    public static DependencyProperty SelectionFlyoutProperty { get; } = Register<FlyoutBase?>(nameof(SelectionFlyout), null);
    public static DependencyProperty SelectionHighlightColorProperty { get; } = Register<SolidColorBrush?>(nameof(SelectionHighlightColor), null);
    public static DependencyProperty SelectionHighlightColorWhenNotFocusedProperty { get; } = Register<SolidColorBrush?>(nameof(SelectionHighlightColorWhenNotFocused), null);
    public static DependencyProperty TextAlignmentProperty { get; } = Register(nameof(TextAlignment), TextAlignment.Left);
    public static DependencyProperty TextReadingOrderProperty { get; } = Register(nameof(TextReadingOrder), TextReadingOrder.Default);
    public static DependencyProperty TextWrappingProperty { get; } = Register(nameof(TextWrapping), TextWrapping.Wrap);

    public bool AcceptsReturn { get => (bool)GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
    public CharacterCasing CharacterCasing { get => (CharacterCasing)GetValue(CharacterCasingProperty); set => SetValue(CharacterCasingProperty, value); }
    public RichEditClipboardFormat ClipboardCopyFormat { get => (RichEditClipboardFormat)GetValue(ClipboardCopyFormatProperty); set => SetValue(ClipboardCopyFormatProperty, value); }
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public CandidateWindowAlignment DesiredCandidateWindowAlignment { get => (CandidateWindowAlignment)GetValue(DesiredCandidateWindowAlignmentProperty); set => SetValue(DesiredCandidateWindowAlignmentProperty, value); }
    public DisabledFormattingAccelerators DisabledFormattingAccelerators { get => (DisabledFormattingAccelerators)GetValue(DisabledFormattingAcceleratorsProperty); set => SetValue(DisabledFormattingAcceleratorsProperty, value); }
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public DataTemplate? HeaderTemplate { get => (DataTemplate?)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    public TextAlignment HorizontalTextAlignment { get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }
    public InputScope? InputScope { get => (InputScope?)GetValue(InputScopeProperty); set => SetValue(InputScopeProperty, value); }
    public bool IsColorFontEnabled { get => (bool)GetValue(IsColorFontEnabledProperty); set => SetValue(IsColorFontEnabledProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool IsSpellCheckEnabled { get => (bool)GetValue(IsSpellCheckEnabledProperty); set => SetValue(IsSpellCheckEnabledProperty, value); }
    public bool IsTextPredictionEnabled { get => (bool)GetValue(IsTextPredictionEnabledProperty); set => SetValue(IsTextPredictionEnabledProperty, value); }
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, Math.Max(0, value)); }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value ?? string.Empty); }
    public bool PreventKeyboardDisplayOnProgrammaticFocus { get => (bool)GetValue(PreventKeyboardDisplayOnProgrammaticFocusProperty); set => SetValue(PreventKeyboardDisplayOnProgrammaticFocusProperty, value); }
    public string ProofingLanguage { get => (string)GetValue(ProofingLanguageProperty); set => SetValue(ProofingLanguageProperty, value is "es-ES" ? value : "en-US"); }
    public FlyoutBase? ProofingMenuFlyout { get => (FlyoutBase?)GetValue(ProofingMenuFlyoutProperty); set => SetValue(ProofingMenuFlyoutProperty, value); }
    public FlyoutBase? SelectionFlyout { get => (FlyoutBase?)GetValue(SelectionFlyoutProperty); set => SetValue(SelectionFlyoutProperty, value); }
    public SolidColorBrush? SelectionHighlightColor { get => (SolidColorBrush?)GetValue(SelectionHighlightColorProperty); set => SetValue(SelectionHighlightColorProperty, value); }
    public SolidColorBrush? SelectionHighlightColorWhenNotFocused { get => (SolidColorBrush?)GetValue(SelectionHighlightColorWhenNotFocusedProperty); set => SetValue(SelectionHighlightColorWhenNotFocusedProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public TextReadingOrder TextReadingOrder { get => (TextReadingOrder)GetValue(TextReadingOrderProperty); set => SetValue(TextReadingOrderProperty, value); }
    public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    public event EventHandler<RichEditBoxLiteCandidateWindowBoundsChangedEventArgs>? CandidateWindowBoundsChanged;
    public event EventHandler<RichEditBoxLiteContextMenuOpeningEventArgs>? ContextMenuOpening;
    public event EventHandler<RichEditBoxLiteClipboardEventArgs>? CopyingToClipboard;
    public event EventHandler<RichEditBoxLiteClipboardEventArgs>? CuttingToClipboard;
    public event EventHandler<RichEditBoxLitePasteEventArgs>? Paste;
    public event RoutedEventHandler? SelectionChanged;
    public event EventHandler<RichEditBoxLiteSelectionChangingEventArgs>? SelectionChanging;
    public event RoutedEventHandler? TextChanged;
    public event EventHandler<RichEditBoxLiteTextChangingEventArgs>? TextChanging;
    public event EventHandler<RichEditBoxLiteCompositionEventArgs>? TextCompositionChanged;
    public event EventHandler<RichEditBoxLiteCompositionEventArgs>? TextCompositionEnded;
    public event EventHandler<RichEditBoxLiteCompositionEventArgs>? TextCompositionStarted;

    protected override void OnApplyTemplate()
    {
        DetachInputBridge();
        base.OnApplyTemplate();
        _inputBridge = GetTemplateChild(InputBridgePartName) as TextBox;
        _canvas = GetTemplateChild(CanvasPartName) as RichTextCanvas;
        if (_canvas is not null)
        {
            _canvas.Document = Document;
        }
        UpdateBridgeProperties();
        AttachInputBridge();
        UpdateVisuals();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RichEditBoxLiteAutomationPeer(this);

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        var control = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        if (!control) return;
        switch (e.Key)
        {
            case VirtualKey.Z: Document.Undo(); e.Handled = true; break;
            case VirtualKey.Y: Document.Redo(); e.Handled = true; break;
            case VirtualKey.B when !DisabledFormattingAccelerators.HasFlag(DisabledFormattingAccelerators.Bold):
                ToggleFormat(format => format with { Bold = !format.Bold }); e.Handled = true; break;
            case VirtualKey.I when !DisabledFormattingAccelerators.HasFlag(DisabledFormattingAccelerators.Italic):
                ToggleFormat(format => format with { Italic = !format.Italic }); e.Handled = true; break;
            case VirtualKey.U when !DisabledFormattingAccelerators.HasFlag(DisabledFormattingAccelerators.Underline):
                ToggleFormat(format => format with { Underline = !format.Underline }); e.Handled = true; break;
        }
    }

    public IAsyncOperation<IReadOnlyList<string>> GetLinguisticAlternativesAsync()
    {
        var range = Document.Selection;
        var wordRange = range.GetClone();
        wordRange.Expand(Microsoft.UI.Text.TextRangeUnit.Word);
        IReadOnlyList<string> suggestions = _spellCheck.Suggest(wordRange.Text, ProofingLanguage);
        return AsyncInfo.Run<IReadOnlyList<string>>(_ => Task.FromResult(suggestions));
    }

    internal IReadOnlyList<SpellingError> GetSpellingErrors() =>
        IsSpellCheckEnabled ? _spellCheck.Check(Document.Text, ProofingLanguage) : [];

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(RichEditBoxLite),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.None, OnCompatibilityPropertyChanged));

    private static void OnCompatibilityPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((RichEditBoxLite)sender).OnCompatibilityPropertyChanged(args.Property);

    private void OnCompatibilityPropertyChanged(DependencyProperty property)
    {
        UpdateBridgeProperties();
        UpdateVisuals();
    }

    private void AttachInputBridge()
    {
        if (_inputBridge is null) return;
        _updatingBridge = true;
        _inputBridge.Text = Document.Text;
        _inputBridge.Select(Document.Selection.StartPosition, Document.Selection.Length);
        _updatingBridge = false;
        _inputBridge.TextChanged += OnInputTextChanged;
        _inputBridge.SelectionChanged += OnInputSelectionChanged;
        _inputBridge.GotFocus += OnInputFocusChanged;
        _inputBridge.LostFocus += OnInputFocusChanged;
        _inputBridge.Paste += OnInputPaste;
        _inputBridge.CopyingToClipboard += OnInputCopying;
        _inputBridge.CuttingToClipboard += OnInputCutting;
        _lastText = Document.Text;
    }

    private void DetachInputBridge()
    {
        if (_inputBridge is null) return;
        _inputBridge.TextChanged -= OnInputTextChanged;
        _inputBridge.SelectionChanged -= OnInputSelectionChanged;
        _inputBridge.GotFocus -= OnInputFocusChanged;
        _inputBridge.LostFocus -= OnInputFocusChanged;
        _inputBridge.Paste -= OnInputPaste;
        _inputBridge.CopyingToClipboard -= OnInputCopying;
        _inputBridge.CuttingToClipboard -= OnInputCutting;
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingBridge || _inputBridge is null) return;
        var next = ApplyCharacterCasing(_inputBridge.Text);
        if (MaxLength > 0 && next.Length > MaxLength) next = next[..MaxLength];
        var prefix = CommonPrefix(_lastText, next);
        var suffix = CommonSuffix(_lastText, next, prefix);
        TextChanging?.Invoke(this, new RichEditBoxLiteTextChangingEventArgs(true));
        _updatingDocument = true;
        Document.Replace(prefix, _lastText.Length - prefix - suffix, next.Substring(prefix, next.Length - prefix - suffix));
        _updatingDocument = false;
        _lastText = next;
        if (_inputBridge.Text != next)
        {
            _updatingBridge = true;
            _inputBridge.Text = next;
            _updatingBridge = false;
        }
        TextChanged?.Invoke(this, new RoutedEventArgs());
        UpdateVisuals();
    }

    private void OnInputSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_inputBridge is null) return;
        var changing = new RichEditBoxLiteSelectionChangingEventArgs(_inputBridge.SelectionStart, _inputBridge.SelectionLength);
        SelectionChanging?.Invoke(this, changing);
        if (changing.Cancel)
        {
            _inputBridge.Select(_lastSelectionStart, _lastSelectionLength);
            return;
        }
        _lastSelectionStart = _inputBridge.SelectionStart;
        _lastSelectionLength = _inputBridge.SelectionLength;
        Document.Selection.SetRange(_lastSelectionStart, _lastSelectionStart + _lastSelectionLength);
        SelectionChanged?.Invoke(this, new RoutedEventArgs());
        UpdateVisuals();
    }

    private void OnInputFocusChanged(object sender, RoutedEventArgs e) => UpdateVisuals();
    private void OnInputPaste(object sender, TextControlPasteEventArgs e) { var args = new RichEditBoxLitePasteEventArgs(); Paste?.Invoke(this, args); e.Handled = args.Handled; }
    private void OnInputCopying(object sender, TextControlCopyingToClipboardEventArgs e) { var args = new RichEditBoxLiteClipboardEventArgs(); CopyingToClipboard?.Invoke(this, args); e.Handled = args.Handled; }
    private void OnInputCutting(object sender, TextControlCuttingToClipboardEventArgs e) { var args = new RichEditBoxLiteClipboardEventArgs(); CuttingToClipboard?.Invoke(this, args); e.Handled = args.Handled; }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (_updatingDocument) return;
        TextChanging?.Invoke(this, new RichEditBoxLiteTextChangingEventArgs(true));
        if (_inputBridge is not null && _inputBridge.Text != Document.Text)
        {
            _updatingBridge = true;
            _inputBridge.Text = Document.Text;
            _inputBridge.Select(Document.Selection.StartPosition, Document.Selection.Length);
            _updatingBridge = false;
        }
        _lastText = Document.Text;
        TextChanged?.Invoke(this, new RoutedEventArgs());
        UpdateVisuals();
    }

    private void UpdateBridgeProperties()
    {
        if (_inputBridge is null) return;
        _inputBridge.AcceptsReturn = AcceptsReturn;
        _inputBridge.IsReadOnly = IsReadOnly;
        _inputBridge.IsSpellCheckEnabled = false;
        _inputBridge.IsTextPredictionEnabled = IsTextPredictionEnabled;
        _inputBridge.MaxLength = MaxLength;
        _inputBridge.TextWrapping = TextWrapping;
        _inputBridge.TextAlignment = TextAlignment;
        _inputBridge.InputScope = InputScope;
        _inputBridge.PreventKeyboardDisplayOnProgrammaticFocus = PreventKeyboardDisplayOnProgrammaticFocus;
    }

    private void UpdateVisuals()
    {
        var focused = _inputBridge?.FocusState is not FocusState.Unfocused;
        VisualStateManager.GoToState(this, IsEnabled ? focused ? "Focused" : "Normal" : "Disabled", true);
        VisualStateManager.GoToState(this, Document.Length == 0 ? "PlaceholderVisible" : "PlaceholderHidden", true);
        if (_canvas is null) return;
        if (Foreground is SolidColorBrush foreground)
        {
            _canvas.DefaultTextColor = new SkiaSharp.SKColor(foreground.Color.R, foreground.Color.G, foreground.Color.B, foreground.Color.A);
            _canvas.CaretColor = _canvas.DefaultTextColor;
        }
        _canvas.SelectionStart = _inputBridge?.SelectionStart ?? Document.Selection.StartPosition;
        _canvas.SelectionLength = _inputBridge?.SelectionLength ?? Document.Selection.Length;
        _canvas.ShowCaret = focused && !IsReadOnly;
        _canvas.SpellingErrors = GetSpellingErrors();
        if (SelectionHighlightColor?.Color is { } color)
        {
            _canvas.SelectionColor = new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
        }
        _canvas.Invalidate();
    }

    private void ToggleFormat(Func<CharacterFormatState, CharacterFormatState> change) =>
        Document.ApplyCharacterFormat(Document.Selection.NormalizedStart, Math.Max(1, Document.Selection.Length), change);

    private string ApplyCharacterCasing(string value) => CharacterCasing switch
    {
        CharacterCasing.Upper => value.ToUpperInvariant(),
        CharacterCasing.Lower => value.ToLowerInvariant(),
        _ => value
    };

    private static int CommonPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < length && left[index] == right[index]) index++;
        return index;
    }

    private static int CommonSuffix(string left, string right, int prefix)
    {
        var length = Math.Min(left.Length, right.Length) - prefix;
        var index = 0;
        while (index < length && left[left.Length - 1 - index] == right[right.Length - 1 - index]) index++;
        return index;
    }
}
