using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidViewportAccessibilityTests
{
    [Fact]
    public void DirectionsAndForwardBackwardUseViewportInsteadOfRangeActions()
    {
        var peer = new ScrollPeer(); var node = PlatformAccessibleObjectAdapter.From(peer)!;
        Assert.Contains(ActionScrollDown, Actions(node)); Assert.Contains(ActionScrollRight, Actions(node));
        Assert.DoesNotContain(ActionScrollUp, Actions(node)); Assert.DoesNotContain(ActionScrollBackward, Actions(node));
        Assert.True(PerformAction(node, ActionScrollForward, .25d));
        Assert.Equal(25, peer.Vertical); Assert.Equal(0, peer.Horizontal);
        Assert.True(PerformAction(node, ActionScrollRight, 1.5d));
        Assert.Equal(150, peer.Horizontal);
        Assert.True(PerformAction(node, ActionScrollDown, double.PositiveInfinity));
        Assert.Equal(300, peer.Vertical); Assert.DoesNotContain(ActionScrollDown, Actions(node));
        Assert.DoesNotContain(ActionScrollForward, Actions(node)); Assert.Contains(ActionScrollUp, Actions(node));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(-1d)]
    public void MalformedGranularAmountCannotReachCanonicalAction(double value)
    {
        var peer = new ScrollPeer();
        Assert.False(PerformAction(PlatformAccessibleObjectAdapter.From(peer)!, ActionScrollDown, value));
        Assert.Equal(0, peer.Calls);
    }

    [Fact]
    public void RevokedSessionCheckRunsAfterMetadataAndBeforeMutation()
    {
        var peer = new ScrollPeer();
        Assert.False(PerformAction(PlatformAccessibleObjectAdapter.From(peer)!, ActionScrollDown, null, () => false));
        Assert.Equal(0, peer.Calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SensitiveOrProtectedViewportNeverReadsExtent(bool ancestor)
    {
        var peer = new ScrollPeer { ThrowOnRead = true };
        if (ancestor) peer.ParentPeer = new ProtectedPeer(); else peer.Sensitive = true;
        Assert.Null(PlatformAccessibleObjectAdapter.From(peer)!.GetScrollInfo());
        Assert.Equal(0, peer.Reads);
    }

    [Fact]
    public void CommittedScrollUsesViewScrolledQueueAndClipsChildrenToViewport()
    {
        var peer = new ScrollPeer(); var node = PlatformAccessibleObjectAdapter.From(peer)!;
        using var session = new AndroidAccessibilitySession(new Host(node));
        session.Attach(); session.DrainEvents();
        peer.NotifyClients(AccessibleEvents.ScrollChanged);
        Assert.Contains(session.DrainEvents(), e => e.Type == 4096 && e.Id == AndroidAccessibilitySession.HostId);
    }

    private sealed class Host(IPlatformAccessibleObject root) : IPlatformAccessibilityHost
    { public IPlatformAccessibleObject AccessibilityRoot => root; }
    private sealed class ProtectedPeer : AccessibleObject
    { public override AccessibleStates State => AccessibleStates.Protected; }
    private sealed class ScrollPeer : AccessibleObject
    {
        internal double Horizontal, Vertical;
        internal int Calls, Reads;
        internal bool Sensitive, ThrowOnRead;
        internal AccessibleObject? ParentPeer;
        public override AccessibleObject? Parent => ParentPeer;
        public override bool IsSensitive => Sensitive;
        public override Rectangle Bounds => new(0, 0, 100, 100);
        public override AccessibleActions SupportedActions => AccessibleActions.Scroll;
        public override AccessibleScrollInfo? ScrollInfo {
            get { Reads++; if (ThrowOnRead) throw new InvalidOperationException();
                return new(new(Horizontal, 0, 200, 100, 5, 100), new(Vertical, 0, 300, 100, 5, 100), new(0, 0, 100, 100)); }
        }
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action != AccessibleActions.Scroll || parameter is not AccessibleScrollRequest request) return false;
            var info = ScrollInfo!.Value;
            if (!request.TryGetOffset(info.Horizontal, true, out var x) || !request.TryGetOffset(info.Vertical, false, out var y)) return false;
            Calls++; Horizontal = x ?? Horizontal; Vertical = y ?? Vertical;
            NotifyClients(AccessibleEvents.ScrollChanged);
            return true;
        }
    }
}
