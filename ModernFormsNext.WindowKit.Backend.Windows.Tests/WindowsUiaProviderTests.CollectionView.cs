using System.Collections.ObjectModel;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void CollectionViewNativeProviderNavigatesAndSelectsFullSourceWithoutScanningPeers()
    {
        var source = new ObservableCollection<object>(Enumerable.Range(0, 100_003).Select(index => (object)$"Entry {index}"));
        using var view = new CollectionView { Size = new(240, 160), ItemSize = new(200, 24), ItemsSource = source };
        using var surface = new SkiaControlSurface(view);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var provider = WindowsUiaRootProvider.Create(new IntPtr(42), root, new InlineDispatcher());
        var first = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        var last = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.LastChild));
        var previous = Assert.IsType<WindowsUiaProvider>(last.Navigate(NavigateDirection.PreviousSibling));
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(bytes < 256 * 1024, $"Indexed CollectionView provider navigation allocated {bytes} B.");
        Assert.Equal("Entry 0", first.PlatformObject.Name);
        Assert.Equal("Entry 100002", last.PlatformObject.Name);
        Assert.Equal("Entry 100001", previous.PlatformObject.Name);
        Assert.Same(last, previous.Navigate(NavigateDirection.NextSibling));
        Assert.Null(last.Navigate(NavigateDirection.NextSibling));
        view.SelectedIndex = 100_002;
        Assert.Same(last, Assert.Single(((ISelectionProvider)provider).GetSelection()));
        source.Move(100_002, 50_000);
        Assert.Same(last, Assert.Single(((ISelectionProvider)provider).GetSelection()));
        Assert.Equal("Entry 50000", Assert.IsType<WindowsUiaProvider>(last.Navigate(NavigateDirection.NextSibling)).PlatformObject.Name);
        Assert.Equal(StructureChangeType.ChildrenInvalidated, provider.ConsumeStructureChange().ChangeType);
        source.RemoveAt(50_000);
        Assert.Throws<WindowsUiaElementNotAvailableException>(() => last.GetRuntimeId());
    }
}
