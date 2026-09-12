using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class AccessibilityNotificationTests
{
    [Fact]
    public void PeerDeliversTheOriginalSubscriberSnapshotBeforeAggregatingFailures()
    {
        var peer = new AccessibleObject();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var calls = new List<int>();
        EventHandler<AccessibleObjectNotificationEventArgs> later = (_, _) => { calls.Add(2); throw second; };
        peer.ClientNotification += (_, _) => { calls.Add(1); peer.ClientNotification -= later; throw first; };
        peer.ClientNotification += later;
        peer.ClientNotification += (sender, args) => {
            Assert.Same(peer, sender);
            Assert.Equal(AccessibleEvents.TextChanged, args.EventId);
            Assert.Equal(7, args.ObjectId);
            Assert.Equal(9, args.ChildId);
            calls.Add(3);
        };
        var error = Assert.Throws<AggregateException>(() => peer.NotifyClients(AccessibleEvents.TextChanged, 7, 9));
        Assert.Equal(new[] { 1, 2, 3 }, calls);
        Assert.Equal(new Exception[] { first, second }, error.InnerExceptions);
    }

    [Fact]
    public void SingleFailureKeepsItsIdentityAfterSuccessfulLaterDelivery()
    {
        var peer = new AccessibleObject();
        var failure = new InvalidOperationException("observer");
        bool delivered = false;
        peer.ClientNotification += (_, _) => throw failure;
        peer.ClientNotification += (_, _) => delivered = true;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => peer.NotifyClients(AccessibleEvents.NameChange)));
        Assert.True(delivered);
    }

    [Fact]
    public void ControlCompletesPlatformDeliveryAndPreservesBothObserverAndPlatformFailures()
    {
        using var root = new NotificationRoot();
        var control = root.Controls.Add(new Control());
        var observerFailure = new InvalidOperationException("observer");
        var platformFailure = new ArgumentException("platform");
        EventHandler<AccessibleObjectNotificationEventArgs> observer = (_, _) => throw observerFailure;
        control.AccessibilityObject.ClientNotification += observer;
        root.Callback = (_, _, _, _) => throw platformFailure;
        root.Deliveries = 0;
        var error = Assert.Throws<AggregateException>(() => control.NotifyAccessibilityClients(AccessibleEvents.NameChange));
        Assert.Equal(new Exception[] { observerFailure, platformFailure }, error.InnerExceptions);
        Assert.Equal(1, root.Deliveries);
        control.AccessibilityObject.ClientNotification -= observer;
        root.Callback = null;
    }

    [Fact]
    public void CommittedTextReachesLaterObserversAndPlatformEvenWhenAnObserverThrows()
    {
        using var root = new NotificationRoot();
        var editor = root.Controls.Add(new TextBox { Text = "before" });
        var provider = editor.AccessibilityObject.TextProvider!;
        _ = provider.DocumentRange;
        var failure = new InvalidOperationException("text observer");
        var delivered = new List<string>();
        EventHandler<AccessibleObjectNotificationEventArgs> throwing = (_, args) => {
            if (args.EventId == AccessibleEvents.TextChanged) throw failure;
        };
        editor.AccessibilityObject.ClientNotification += throwing;
        editor.AccessibilityObject.ClientNotification += (_, args) => {
            if (args.EventId == AccessibleEvents.TextChanged) delivered.Add(provider.DocumentRange.GetText());
        };
        var nativeEvents = new List<int>();
        root.Callback = (_, eventId, _, _) => nativeEvents.Add(eventId);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => editor.Text = "after"));
        Assert.Equal("after", editor.Text);
        Assert.Equal(new[] { "after" }, delivered);
        Assert.Single(nativeEvents, e => e == (int)AccessibleEvents.TextChanged);
        editor.AccessibilityObject.ClientNotification -= throwing;
        root.Callback = null;
    }

    private sealed class NotificationRoot : Control, IControlSurfaceAccessibilitySink
    {
        internal int Deliveries;
        internal Action<IPlatformAccessibleObject, int, int, int>? Callback;
        public void NotifyAccessibility(IPlatformAccessibleObject source, int eventId, int objectId, int childId)
        { Deliveries++; Callback?.Invoke(source, eventId, objectId, childId); }
    }
}
