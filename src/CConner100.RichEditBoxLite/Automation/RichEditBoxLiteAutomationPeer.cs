using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace CConner100.RichEditBoxLite;

internal sealed class RichEditBoxLiteAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
{
    private readonly RichEditBoxLite _owner;

    internal RichEditBoxLiteAutomationPeer(RichEditBoxLite owner) : base(owner) => _owner = owner;

    protected override string GetClassNameCore() => nameof(RichEditBoxLite);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Value ? this : base.GetPatternCore(patternInterface);

    public bool IsReadOnly => _owner.IsReadOnly;
    public string Value => _owner.Document.Text;

    public void SetValue(string value)
    {
        if (IsReadOnly) throw new InvalidOperationException("The RichEditBoxLite is read-only.");
        _owner.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, value);
    }
}
