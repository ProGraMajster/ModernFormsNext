using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void NativeVirtualizedProviderKeepsFullLogicalNavigationWithoutMaterializing100kPeers()
    {
        using var list = new ListView { Size = new(240, 160), VirtualizationEnabled = true, ItemsLayout = ItemsLayout.Stack };
        list.SuspendLayout();
        for (int index = 0; index < 100_003; index++) list.Items.Add($"Entry {index}");
        list.ResumeLayout();
        using var surface = new SkiaControlSurface(list);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var provider = WindowsUiaRootProvider.Create(new IntPtr(42), root, new InlineDispatcher());
        var first = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        var last = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.LastChild));
        var previous = Assert.IsType<WindowsUiaProvider>(last.Navigate(NavigateDirection.PreviousSibling));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("Entry 0", first.PlatformObject.Name);
        Assert.Equal("Entry 100002", last.PlatformObject.Name);
        Assert.Equal("Entry 100001", previous.PlatformObject.Name);
        Assert.Same(last, previous.Navigate(NavigateDirection.NextSibling));
        Assert.Null(last.Navigate(NavigateDirection.NextSibling));
        Assert.True(allocated < 256 * 1024, $"Bounded native provider/navigation allocated {allocated} B.");

        list.SelectedIndex = 100_002;
        Assert.Same(last, Assert.Single(((ISelectionProvider)provider).GetSelection()));
        list.Items.Move(100_002, 50_000);
        Assert.Same(last, Assert.Single(((ISelectionProvider)provider).GetSelection()));
        Assert.Equal("Entry 50000", Assert.IsType<WindowsUiaProvider>(last.Navigate(NavigateDirection.NextSibling)).PlatformObject.Name);
        Assert.Equal(StructureChangeType.ChildrenInvalidated, provider.ConsumeStructureChange().ChangeType);
        list.Items.RemoveAt(50_000);
        Assert.Throws<WindowsUiaElementNotAvailableException>(() => last.GetRuntimeId());
    }
}
