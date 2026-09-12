using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    private sealed partial class TestAccessibleObject
    {
        private string? privacyName, privacyHelp, privacyDescription, privacyAutomationId, privacyValue;
        private PlatformAccessibleRangeValue? privacyRange;
        internal Func<string?>? NameReader, HelpReader, DescriptionReader, AutomationIdReader, ValueReader;
        internal Func<PlatformAccessibleRangeValue?>? RangeReader;
    }

    [Fact]
    public void OwnPasswordRetainsExplicitLabelHelpAndIdWithoutReadingItsValue()
    {
        var password = Root("Password label", 10, ActionSetValue);
        password.IsSensitive = true;
        password.Help = "Enter the test password";
        password.AutomationId = "password-id";
        password.ValueReader = () => throw new InvalidOperationException("Password value getter was called.");
        using var provider = Create(password);
        Assert.Equal("Password label", provider.GetPropertyValue(WindowsUiaIds.NameProperty));
        Assert.Equal("Enter the test password", provider.GetPropertyValue(WindowsUiaIds.HelpTextProperty));
        Assert.Equal("password-id", provider.GetPropertyValue(WindowsUiaIds.AutomationIdProperty));
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IValueProvider)provider).Value);
        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaIds.ValuePattern));
    }

    [Fact]
    public void ProtectedAncestorSkipsAllPayloadGettersAndPreservesPasswordWriteOnlyPattern()
    {
        var root = Root("protected", 3); root.State |= unchecked((int)0x20000000);
        var child = root.AddChild(Root("child", 10, ActionSetValue));
        string? Forbidden() => throw new InvalidOperationException("Protected getter was called.");
        child.NameReader = Forbidden; child.HelpReader = Forbidden; child.DescriptionReader = Forbidden;
        child.AutomationIdReader = Forbidden; child.ValueReader = Forbidden;
        child.RangeReader = () => throw new InvalidOperationException("Protected range getter was called.");
        using var provider = Create(root);
        var native = (WindowsUiaProvider)provider.Navigate(NavigateDirection.FirstChild)!;
        Assert.Equal(string.Empty, native.GetPropertyValue(WindowsUiaIds.NameProperty));
        Assert.Equal(string.Empty, native.GetPropertyValue(WindowsUiaIds.HelpTextProperty));
        Assert.Equal(string.Empty, native.GetPropertyValue(WindowsUiaIds.AutomationIdProperty));
        Assert.Equal(true, native.GetPropertyValue(WindowsUiaIds.IsPasswordProperty));
        Assert.Null(native.GetPatternProvider(WindowsUiaIds.RangeValuePattern));
        Assert.Same(native, native.GetPatternProvider(WindowsUiaIds.ValuePattern));
        Assert.Throws<WindowsUiaAccessDeniedException>(() => native.GetPropertyValue(WindowsUiaIds.ValueProperty));
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IValueProvider)native).Value);
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IRangeValueProvider)native).Minimum);
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IRangeValueProvider)native).SetValue(5));
        // The fake action's normal mutation path inspects its own range; stop poisoning that
        // application-owned handler, while retaining proof that provider write policy permits it.
        child.RangeReader = null;
        ((IValueProvider)native).SetValue("synthetic replacement");
        Assert.Equal(ActionSetValue, child.LastAction);
        Assert.Equal("synthetic replacement", child.LastParameter);
    }

    [Theory]
    [InlineData(WindowsUiaIds.NameProperty)]
    [InlineData(WindowsUiaIds.AutomationIdProperty)]
    [InlineData(WindowsUiaIds.HelpTextProperty)]
    public void PayloadIsRedactedWhenGetterProtectsAncestor(int property)
    {
        var root = Root("root", 3); var child = root.AddChild(Root("child", 10));
        string? Mutate() { root.IsSensitive = true; return "must not escape"; }
        if (property == WindowsUiaIds.NameProperty) child.NameReader = Mutate;
        if (property == WindowsUiaIds.AutomationIdProperty) child.AutomationIdReader = Mutate;
        if (property == WindowsUiaIds.HelpTextProperty) child.HelpReader = Mutate;
        using var provider = Create(root);
        var native = (WindowsUiaProvider)provider.Navigate(NavigateDirection.FirstChild)!;
        Assert.Equal(string.Empty, native.GetPropertyValue(property));
    }

    [Fact]
    public void HelpFallbackDoesNotReadDescriptionAfterPrivacyChange()
    {
        var root = Root("root", 3); var child = root.AddChild(Root("child", 10));
        child.HelpReader = () => { root.IsSensitive = true; return null; };
        child.DescriptionReader = () => throw new InvalidOperationException("Late private fallback.");
        using var provider = Create(root);
        var native = (WindowsUiaProvider)provider.Navigate(NavigateDirection.FirstChild)!;
        Assert.Equal(string.Empty, native.GetPropertyValue(WindowsUiaIds.HelpTextProperty));
    }

    [Fact]
    public void RetainedValueAndRangeInterfacesRecheckPrivacyAfterPayloadGetter()
    {
        var root = Root("root", 3); var child = root.AddChild(Root("child", 10, ActionSetValue));
        using var provider = Create(root);
        var native = (WindowsUiaProvider)provider.Navigate(NavigateDirection.FirstChild)!;
        child.ValueReader = () => { root.IsSensitive = true; return "must not escape"; };
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IValueProvider)native).Value);
        root.IsSensitive = false;
        child.RangeReader = () => { root.IsSensitive = true; return new(5, 0, 10, 1, 2, false); };
        Assert.Throws<WindowsUiaAccessDeniedException>(() => ((IRangeValueProvider)native).Value);
        root.IsSensitive = false;
        Assert.Null(native.GetPatternProvider(WindowsUiaIds.RangeValuePattern));
    }
}
