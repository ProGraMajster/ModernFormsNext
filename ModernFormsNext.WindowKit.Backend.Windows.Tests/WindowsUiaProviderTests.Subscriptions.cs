using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using System.Runtime.InteropServices;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void MsaaBatchPublishesAncestorVisibilityOnceAndPreservesOtherEvents()
    {
        var callbacks = new Queue<Action>();
        var delivered = new List<(int Event, int Child)>();
        var batch = new WindowsMsaaNotificationBatch(callbacks.Enqueue, (_, e, child) => delivered.Add((e, child)));
        var root = Root("Window", 2);
        var page = root.AddChild(new TestAccessibleObject("Page", 1));
        batch.Add(page, 0x8002, 1);
        batch.Add(page, 0x800A, 1);
        for (int i = 0; i < 800; i++)
        {
            var child = page.AddChild(new TestAccessibleObject("Child", 6));
            batch.Add(child, 0x8002, i + 2);
            batch.Add(child, 0x800A, i + 2);
        }
        var focused = page.Children[0];
        batch.Add(focused, 0x8005, 2);
        batch.Add(focused, 0x800C, 2);
        batch.Add(focused, 0x800C, 2);
        Assert.Single(callbacks); Assert.Empty(delivered);
        callbacks.Dequeue()();
        Assert.Equal(new[] { (0x8002, 1), (0x8005, 2), (0x800C, 2) }, delivered);
        delivered.Clear();
        batch.Add(focused, 0x800A, 2);
        callbacks.Dequeue()();
        Assert.Equal(new[] { (0x800A, 2) }, delivered);
    }

    [Fact]
    public void MsaaEventTargetIsTheDescendantAndSurvivesSiblingReordering()
    {
        var root = Root("Window", 2);
        var page = root.AddChild(new TestAccessibleObject("Page", 1));
        var button = page.AddChild(new TestAccessibleObject("Download", 6));
        var other = page.AddChild(new TestAccessibleObject("Other", 6));
        var msaa = WindowsMsaaAccessibleObject.CreateRoot(root);
        int id = msaa.GetEventChildId(button);
        Assert.NotEqual(0, id);
        Assert.Equal("Download", msaa.get_accName(id));
        Assert.Equal("Window", msaa.get_accName(0));
        Assert.Equal(0, msaa.GetEventChildId(root));
        page.Children.Reverse();
        Assert.Equal(id, msaa.GetEventChildId(button));
        Assert.Same(button, Assert.IsType<WindowsMsaaAccessibleObject>(msaa.get_accChild(id)).PlatformObject);
        Assert.Same(page, Assert.IsType<WindowsMsaaAccessibleObject>(msaa.get_accChild(1)).PlatformObject);
        page.Children.Remove(button); button.ParentObject = null;
        Assert.Throws<COMException>(() => msaa.get_accName(id));
        Assert.Equal("Other", msaa.get_accName(msaa.GetEventChildId(other)));
    }

    [Fact]
    public void NativeAdviseVtableBorrowsSafeArrayAndBalancesOverlappingListeners()
    {
        var sink = new RecordingEventSink();
        var node = Root("Window", 2);
        using var provider = WindowsUiaRootProvider.Create(new IntPtr(42), node, new InlineDispatcher(), sink);
        IntPtr unknown = WindowsUiaNativeMethods.GetUnknownPointer(provider), advise = IntPtr.Zero;
        IntPtr properties = WindowsUiaNativeMethods.CreateSafeArray(new[] { WindowsUiaIds.NameProperty });
        try
        {
            Guid iid = typeof(IRawElementProviderAdviseEventsAbi).GUID;
            Assert.Equal(0, Marshal.QueryInterface(unknown, in iid, out advise));
            IntPtr table = Marshal.ReadIntPtr(advise);
            var add = Marshal.GetDelegateForFunctionPointer<AdviseCallback>(Marshal.ReadIntPtr(table, 3 * IntPtr.Size));
            var remove = Marshal.GetDelegateForFunctionPointer<AdviseCallback>(Marshal.ReadIntPtr(table, 4 * IntPtr.Size));
            Assert.Equal(0, add(advise, 20004, properties));
            Assert.Equal(0, add(advise, 20004, properties));
            Assert.Equal(0, remove(advise, 20004, properties));
            provider.RaiseNotification(node, 0x800C);
            Assert.Single(sink.PropertyChanges);
            Assert.Equal(new[] { WindowsUiaIds.NameProperty }, WindowsUiaNativeMethods.ReadPropertyIds(properties));
            Assert.Equal(0, remove(advise, 20004, properties));
            provider.RaiseNotification(node, 0x800C);
            Assert.Single(sink.PropertyChanges);
            // Null property array denotes all properties, independent of specific counters.
            Assert.Equal(0, add(advise, 20004, IntPtr.Zero));
            provider.RaiseNotification(node, 0x800A);
            Assert.Contains(WindowsUiaIds.IsEnabledProperty, sink.PropertyChanges);
            Assert.Equal(0, remove(advise, 20004, IntPtr.Zero));
        }
        finally
        {
            _ = DestroySubscriptionSafeArray(properties);
            if (advise != IntPtr.Zero) Marshal.Release(advise);
            Marshal.Release(unknown);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdviseCallback(IntPtr self, int eventId, IntPtr properties);
    [DllImport("oleaut32.dll", EntryPoint = "SafeArrayDestroy")]
    private static extern int DestroySubscriptionSafeArray(IntPtr properties);

    [Fact]
    public void SubscriptionsArePerFragmentAndReferenceCountedPerProperty()
    {
        var sink = new RecordingEventSink();
        var node = Root("Window", 2);
        using var provider = WindowsUiaRootProvider.Create(new IntPtr(42), node, new InlineDispatcher(), sink);
        using var other = WindowsUiaRootProvider.Create(new IntPtr(43), node, new InlineDispatcher(), sink);
        int[] properties = [WindowsUiaIds.NameProperty];
        provider.AdviseEventAdded(20004, properties);
        provider.AdviseEventAdded(20004, properties);
        provider.AdviseEventRemoved(20004, properties);
        provider.RaiseNotification(node, 0x800C);
        provider.RaiseNotification(node, 0x800A);
        other.RaiseNotification(node, 0x800C);
        Assert.Equal(new[] { WindowsUiaIds.NameProperty }, sink.PropertyChanges);
        provider.AdviseEventRemoved(20004, properties);
        provider.RaiseNotification(node, 0x800C);
        Assert.Single(sink.PropertyChanges);
        // An unmatched removal cannot consume a different subscription.
        provider.AdviseEventRemoved(20004, properties);
        provider.AdviseEventAdded(20002, null);
        provider.RaiseNotification(node, 0x8002);
        Assert.Single(sink.StructureEvents);
        Assert.Single(sink.PropertyChanges);
        provider.Dispose();
        provider.RaiseNotification(node, 0x8002);
        Assert.Single(sink.StructureEvents);
    }

    [Fact]
    public void UnrelatedListeningClientDoesNotReceiveFragmentAttachmentEvents()
    {
        var sink = new RecordingEventSink(); // Global ClientsAreListening can concern another HWND.
        var root = Root("Window", 2);
        using var provider = WindowsUiaRootProvider.Create(new IntPtr(42), root, new InlineDispatcher(), sink);
        for (int i = 0; i < 800; i++)
        {
            var child = root.AddChild(new TestAccessibleObject("Child", 6));
            provider.RaiseNotification(child, 0x8002);
            provider.RaiseNotification(child, 0x800A);
        }
        Assert.Empty(sink.PropertyChanges);
        Assert.Empty(sink.StructureEvents);
    }
}
