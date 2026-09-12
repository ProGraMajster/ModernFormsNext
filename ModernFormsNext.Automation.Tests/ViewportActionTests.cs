using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

public sealed class ViewportActionTests
{
    [Fact]
    public void TypedScrollUsesCurrentCanonicalViewportAndDetachedSnapshot()
    {
        using var f = new AutomationFixture();
        var panel = f.Add(new ScrollableControl { Bounds = new Rectangle(0, 0, 100, 100), AutoScroll = true });
        panel.Controls.Add(new Control { Bounds = new(0, 0, 300, 20) }); f.Host.LayoutUntilStable();
        var before = f.Inspect(panel).Value!;
        Assert.Equal(0, before.ScrollInfo!.Value.Horizontal.Offset);
        var result = f.Act(panel, AccessibleActions.Scroll, AutomationActionValue.FromScroll(AccessibleScrollRequest.ToPercent(50, null)));
        Assert.Equal(AutomationActionStatus.Accepted, result.Status);
        Assert.Equal(100, panel.HorizontalScrollProperties.Value);
        Assert.Equal(0, before.ScrollInfo!.Value.Horizontal.Offset);
        Assert.Equal(100, f.Inspect(panel).Value!.ScrollInfo!.Value.Horizontal.Offset);
    }
    [Fact]
    public void UntypedAndUnrelatedPayloadsCannotScroll()
    {
        using var f = new AutomationFixture();
        var panel = f.Add(new ScrollableControl { Bounds = new(0, 0, 100, 100), AutoScroll = true });
        panel.Controls.Add(new Control { Bounds = new(0, 0, 300, 20) }); f.Host.LayoutUntilStable();
        Assert.Equal(AutomationActionStatus.InvalidArgument, f.Act(panel, AccessibleActions.Scroll, AutomationActionValue.FromNumber(50)).Status);
        Assert.Equal(AutomationActionStatus.InvalidArgument, f.Act(panel, AccessibleActions.Scroll).Status);
        Assert.Equal(AutomationActionStatus.InvalidArgument, f.Act(panel, AccessibleActions.Invoke,
            AutomationActionValue.FromScroll(AccessibleScrollRequest.ToPercent(50, null))).Status);
        Assert.Equal(0, panel.HorizontalScrollProperties.Value);
    }

    [Fact]
    public void ProtectedAncestorPreventsPayloadGetterDuringSnapshotAndScrollAction()
    {
        using var f = new AutomationFixture();
        var parent = f.Add(new ProtectedContainer { Bounds = new(0, 0, 200, 200) });
        var probe = parent.Controls.Add(new ScrollProbe { Bounds = new(0, 0, 100, 100) });
        Assert.Null(f.Inspect(probe).Value!.ScrollInfo);
        Assert.Equal(AutomationActionStatus.Rejected, f.Act(probe, AccessibleActions.Scroll,
            AutomationActionValue.FromScroll(AccessibleScrollRequest.ToPercent(null, 50))).Status);
        Assert.Equal(0, probe.Reads);
        Assert.Equal(0, probe.Actions);
    }

    private sealed class ProtectedContainer : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(Control owner) : ControlAccessibleObject(owner)
        { public override bool IsSensitive => true; }
    }

    private sealed class ScrollProbe : Control
    {
        internal int Reads, Actions;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(ScrollProbe owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleActions SupportedActions => AccessibleActions.Scroll;
            public override AccessibleScrollInfo? ScrollInfo
            {
                get { owner.Reads++; return new(new(0, 0, 0, 100, 0, 0), new(0, 0, 100, 100, 5, 100), new(0, 0, 100, 100)); }
            }
            public override bool PerformAction(AccessibleActions action, object? parameter = null) { owner.Actions++; return true; }
        }
    }
}
