using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidVirtualizationAccessibilityTests(ITestOutputHelper output)
{
    [Fact]
    public void RegisteringOneLogicalRowDoesNotMaterializeAllSiblingPeers()
    {
        using var list = new ListView { Size = new(200, 120), VirtualizationEnabled = true, ItemsLayout = ItemsLayout.Stack };
        list.SuspendLayout();
        for (int index = 0; index < 10_000; index++) list.Items.Add($"Entry {index}");
        list.ResumeLayout();
        using var surface = new SkiaControlSurface(list);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        list.SelectedIndex = 0;
        var peer = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!.GetSelected()!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        int id = session.Register(peer);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"One native membership query: {allocated} B; source count={list.ItemCount}.");
        Assert.True(id > 0);
        Assert.Same(peer, session.Find(id));
        Assert.Equal(2, session.CachedNodeCount);
        Assert.True(allocated < 128 * 1024, $"One membership query allocated {allocated} B, suggesting sibling realization.");
        list.Items.Move(0, 9_999);
        Assert.Same(peer, session.Find(id));
        list.Items.RemoveAt(9_999);
        Assert.Null(session.Find(id));
        Assert.Equal(1, session.CachedNodeCount); // Private scrollbar infrastructure did not enter the semantic tree.
    }

    [Fact]
    public void NativeChildrenAndCollectionCountUseBoundedCanonicalProjectionFor100kItems()
    {
        using var list = new ListView { Size = new(240, 160), VirtualizationEnabled = true, ItemsLayout = ItemsLayout.Stack };
        list.SuspendLayout();
        for (int index = 0; index < 100_003; index++) list.Items.Add($"Entry {index}");
        list.ResumeLayout();
        using var surface = new SkiaControlSurface(list);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        list.SelectedIndex = 90_000;
        list.ScrollToItem(1_000);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var children = session.Children(root);
        var collection = AndroidAccessibilityMapper.Collection(root)!.Value;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Native children={children.Count}; logical rows={collection.Rows}; query={allocated} B.");
        Assert.InRange(children.Count, 1, list.Controls.Count + 2);
        Assert.Equal(100_003, collection.Rows);
        Assert.Equal(1, collection.Columns);
        Assert.True(allocated < 128 * 1024, $"Bounded native projection allocated {allocated} B.");
        var peer = root.GetSelected()!;
        int id = session.Register(peer);
        Assert.Contains(id, children);
        Assert.Equal(90_000, AndroidAccessibilityMapper.CollectionItemIndex(peer));
        list.ScrollToItem(90_000);
        Assert.Contains(id, session.Children(root));
        Assert.Same(peer, session.Find(id));
        var bounds = list.Items[90_000].Bounds;
        var point = list.PointToScreen(new(bounds.Left + 5, bounds.Top + 5));
        long beforeHit = GC.GetAllocatedBytesForCurrentThread();
        Assert.Same(peer, root.HitTest(point.X, point.Y));
        long hitBytes = GC.GetAllocatedBytesForCurrentThread() - beforeHit;
        Assert.True(hitBytes < 128 * 1024, $"One hit test allocated {hitBytes} B.");
        list.Items.Move(90_000, 50_000);
        list.ScrollToItem(50_000);
        Assert.Contains(id, session.Children(root));
        Assert.Same(peer, session.Find(id));
        Assert.Equal(50_000, AndroidAccessibilityMapper.CollectionItemIndex(peer));
    }

    [Fact]
    public void NativeRegistryRetiresUnselectedRowsOutsideCurrentProjection()
    {
        using var list = new ListView { Size = new(240, 160), VirtualizationEnabled = true, ItemsLayout = ItemsLayout.Stack };
        list.SuspendLayout();
        for (int index = 0; index < 1_000; index++) list.Items.Add($"Entry {index}");
        list.ResumeLayout();
        using var surface = new SkiaControlSurface(list);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        for (int step = 0; step < 100; step++)
        {
            list.ScrollToItem(step * 73 % 1_000);
            session.Children(root);
        }
        Assert.InRange(session.CachedNodeCount, 1, list.Controls.Count + 1);
    }

    [Fact]
    public void NestedCanonicalMembershipDoesNotRepeatedlyEnumerateAncestorSiblings()
    {
        using var rootControl = new Panel();
        for (int index = 0; index < 50; index++) rootControl.Controls.Add(new Panel { Visible = false });
        var parent = rootControl.Controls.Add(new Panel());
        var list = parent.Controls.Add(new ListView { Size = new(240, 160), VirtualizationEnabled = true });
        list.Items.Add("Visible row");
        list.SelectedIndex = 0;
        using var surface = new SkiaControlSurface(rootControl);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        var peer = list.AccessibilityObject.GetSelected()!;
        var adapter = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!.GetChild(0)!.GetChild(0)!.GetSelected()!;
        int id = session.Register(adapter);
        Assert.True(id > 0);
        bool allMatched = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int query = 0; query < 1_000; query++) allMatched &= id == session.Register(adapter);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allMatched);
        output.WriteLine($"1,000 nested membership queries: {allocated} B; canonical peer={peer.RuntimeId}.");
        Assert.True(allocated < 128 * 1024, $"Repeated canonical membership allocated {allocated} B.");
    }

    [Fact]
    public void CustomIndependentInvisibleStateStillRejectsNativeMembership()
    {
        using var list = new InvisibleProjectionList { VirtualizationEnabled = true };
        using var surface = new SkiaControlSurface(list);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        Assert.False(((IPlatformUiaAccessibleObject)root).VisibilityFollowsView);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        Assert.Null(session.Find(AndroidAccessibilitySession.HostId));
    }

    [Fact]
    public void CustomListProjectionStillDecidesActualMembership()
    {
        using var list = new EmptyProjectionList { VirtualizationEnabled = true };
        list.Items.Add("Not projected");
        using var surface = new SkiaControlSurface(list);
        using var session = new AndroidAccessibilitySession((IPlatformAccessibilityHost)surface);
        session.Attach();
        list.SelectedIndex = 0;
        var peer = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!.GetSelected()!;
        Assert.Equal(AndroidAccessibilitySession.InvalidId, session.Register(peer));
    }

    private sealed class EmptyProjectionList : ListView
    {
        protected override ModernFormsNext.Accessibility.AccessibleObject CreateAccessibilityInstance() => new EmptyPeer(this);
        private sealed class EmptyPeer(Control owner) : ControlAccessibleObject(owner)
        {
            protected override IEnumerable<ModernFormsNext.Accessibility.AccessibleObject> GetAccessibilityChildren() => [];
        }
    }

    private sealed class InvisibleProjectionList : ListView
    {
        protected override ModernFormsNext.Accessibility.AccessibleObject CreateAccessibilityInstance() => new InvisiblePeer(this);
        private sealed class InvisiblePeer(Control owner) : ControlAccessibleObject(owner)
        {
            public override ModernFormsNext.Accessibility.AccessibleStates State => base.State | ModernFormsNext.Accessibility.AccessibleStates.Invisible;
        }
    }
}
