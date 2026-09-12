using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    private sealed partial class TestAccessibleObject : IPlatformAccessibilityScroll
    {
        public PlatformAccessibleScrollInfo? ScrollInfo { get; set; }
        public int? Orientation => null;
        public object? LastParameter { get; private set; }
    }
    private static TestAccessibleObject ScrollRoot() => new("viewport", 3) { SupportedActions = 256,
        ScrollInfo = new(new(25, 0, 100, 200, 5, 100), new(0, 0, 0, 100, 0, 0), new(0, 0, 200, 100)) };

    [Fact]
    public void ScrollPatternExposesActualMetricsAndTranslatesNativeSentinel()
    {
        var node = ScrollRoot(); using var provider = Create(node);
        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaScrollIds.Pattern));
        var scroll = (IScrollProvider)provider;
        Assert.Equal(25, scroll.HorizontalScrollPercent);
        Assert.Equal(200d / 300 * 100, scroll.HorizontalViewSize, 8);
        Assert.Equal(-1, scroll.VerticalScrollPercent); Assert.Equal(100, scroll.VerticalViewSize);
        Assert.True(scroll.HorizontallyScrollable); Assert.False(scroll.VerticallyScrollable);
        Assert.Equal(0, provider.AbiSetScrollPercent(75, -1));
        var request = Assert.IsType<PlatformAccessibleScrollRequest>(node.LastParameter);
        Assert.Equal(75, request.Horizontal); Assert.Null(request.Vertical);
        Assert.NotEqual(0, provider.AbiSetScrollPercent(25, 0));
        node.State = StateUnavailable;
        Assert.True(scroll.HorizontallyScrollable);
        Assert.Equal(unchecked((int)0x80040200), provider.AbiSetScrollPercent(75, -1));
    }

    [Theory]
    [InlineData(0, 3)] [InlineData(1, 1)] [InlineData(2, 0)] [InlineData(3, 4)] [InlineData(4, 2)]
    public void NativeScrollAmountOrderIsTranslatedExplicitly(int native, int canonical)
    {
        var node = ScrollRoot(); using var provider = Create(node);
        Assert.Equal(0, provider.AbiScroll((ScrollAmount)native, ScrollAmount.NoAmount));
        Assert.Equal(canonical, Assert.IsType<PlatformAccessibleScrollRequest>(node.LastParameter).HorizontalAmount);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-2d)] [InlineData(101d)]
    public void InvalidNativePercentCannotMutate(double value)
    {
        var node = ScrollRoot(); using var provider = Create(node);
        Assert.NotEqual(0, provider.AbiSetScrollPercent(value, -1)); Assert.Equal(0, node.LastAction);
    }

    [Fact]
    public void RetainedDescendantProviderRejectsDetachedAncestorWithNonNullImmediateParent()
    {
        var root = Root("root", 3);
        var parent = root.AddChild(new("parent", 3));
        var child = parent.AddChild(ScrollRoot());
        using var provider = Create(root);
        var childProvider = (WindowsUiaProvider)provider.Navigate(NavigateDirection.FirstChild)!.Navigate(NavigateDirection.FirstChild)!;
        parent.ParentObject = null; root.Children.Clear();
        Assert.Same(parent, child.Parent);
        Assert.Equal(unchecked((int)0x80040201), childProvider.AbiSetScrollPercent(50, -1));
        Assert.Equal(0, child.LastAction);
    }
}
