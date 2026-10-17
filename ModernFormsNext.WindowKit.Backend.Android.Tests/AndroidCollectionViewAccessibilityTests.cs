using System.Collections.ObjectModel;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidCollectionViewAccessibilityTests
{
    [Fact]
    public void ActualCollectionViewUsesBoundedNativeProjectionAndStableMutatedSelection()
    {
        var source = new ObservableCollection<object>(Enumerable.Range(0, 100_003).Select(index => (object)$"Entry {index}"));
        using var view = new CollectionView { Size = new(240, 160), ItemSize = new(200, 24), ItemsSource = source };
        using var surface = new SkiaControlSurface(view);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        view.SelectedIndex = 90_000;
        view.ScrollToItem(1_000);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var children = session.Children(root);
        var collection = AndroidAccessibilityMapper.Collection(root)!.Value;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(children.Count, 1, view.Controls.Count + 2);
        Assert.Equal(100_003, collection.Rows);
        Assert.True(bytes < 128 * 1024, $"One bounded CollectionView query allocated {bytes} B.");
        var selected = root.GetSelected()!;
        int id = session.Register(selected);
        Assert.Contains(id, children);
        Assert.Equal(90_000, AndroidAccessibilityMapper.CollectionItemIndex(selected));
        source.Move(90_000, 50_000);
        Assert.Same(selected, session.Find(id));
        Assert.Equal(50_000, AndroidAccessibilityMapper.CollectionItemIndex(selected));
        view.ScrollToItem(50_000);
        Assert.Contains(id, session.Children(root));
        var row = selected.GetChild(0)!;
        Assert.Same(selected, row.Parent);
        Assert.True(session.Register(row) > 0);
        source.RemoveAt(50_000);
        Assert.Null(session.Find(id));
    }

    [Fact]
    public void NativeRegistryRetiresVisualRowsAndUnselectedOccurrencesWhileRecycling()
    {
        using var view = new CollectionView
        {
            Size = new(240, 160), ItemSize = new(200, 24),
            ItemsSource = Enumerable.Range(0, 100_003).Cast<object>().ToArray()
        };
        using var surface = new SkiaControlSurface(view);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        for (int step = 0; step < 100; step++)
        {
            view.ScrollToItem(step * 7919 % 100_003);
            foreach (int id in session.Children(root))
                if (session.Find(id) is { } peer) session.Children(peer);
        }
        Assert.InRange(session.CachedNodeCount, 1, view.Controls.Count * 2 + 1);
    }
}
