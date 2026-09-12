using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ViewportAccessibilityTests
{
    [Theory]
    [InlineData(1d)] [InlineData(1.5d)] [InlineData(2d)]
    public void SingleAxisUsesActualLogicalViewportAndInclusiveEndpoint(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 300, scale));
        var form = new Form();
        var panel = form.Controls.Add(new ScrollableControl { Bounds = new(10, 10, 100, 100), AutoScroll = true });
        var child = panel.Controls.Add(new Control { Bounds = new(0, 0, 110, 20) });
        host.Show(form); host.LayoutUntilStable();
        var peer = panel.AccessibilityObject;
        var info = Assert.IsType<AccessibleScrollInfo>(peer.ScrollInfo);
        Assert.Equal(100, info.Horizontal.ViewportLength);
        Assert.Equal(10, info.Horizontal.Maximum);
        Assert.False(info.Vertical.IsScrollable);
        Assert.Equal(100 * scale, info.ViewportBounds.Width);
        Assert.Equal(100d / 110 * 100, info.Horizontal.ViewPercent, 8);
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, null)));
        Assert.Equal(10, panel.HorizontalScrollProperties.Value);
        Assert.Equal(-10, child.Left);
        Assert.Equal(100, peer.ScrollInfo!.Value.Horizontal.Percent);
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(0, null)));
        Assert.Equal(0, child.Left);
    }

    [Fact]
    public void InvalidSecondAxisDoesNotPartiallyMoveFirstAndDisabledMetadataSurvives()
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new ScrollableControl { AutoScroll = true };
        panel.Controls.Add(new Control { Bounds = new(0, 0, 150, 20) });
        host.Show(panel, 100, 100); host.LayoutUntilStable();
        var peer = panel.AccessibilityObject;
        Assert.False(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, 100)));
        Assert.Equal(0, panel.HorizontalScrollProperties.Value);
        panel.Enabled = false;
        Assert.True(peer.ScrollInfo!.Value.Horizontal.IsScrollable);
        Assert.False(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, null)));
    }

    [Fact]
    public void RevealKeepsIdentityAndFocusAndChangesOffscreenState()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var panel = form.Controls.Add(new ScrollableControl { Bounds = new(0, 0, 100, 100), AutoScroll = true });
        var button = panel.Controls.Add(new Button { Bounds = new(0, 200, 70, 25) });
        var focus = form.Controls.Add(new Button { Bounds = new(130, 0, 80, 30) });
        host.Show(form); focus.Select(); host.LayoutUntilStable();
        var peer = button.AccessibilityObject;
        long id = peer.RuntimeId;
        Assert.True((peer.State & AccessibleStates.Offscreen) != 0);
        Assert.True(peer.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.Equal(AccessibleStates.None, peer.State & AccessibleStates.Offscreen);
        Assert.Equal(id, peer.RuntimeId);
        Assert.True(focus.Focused);
        Assert.True(peer.PerformAction(AccessibleActions.ScrollIntoView));
    }

    [Fact]
    public void TwoAxisActionStopsAfterAncestorRemovalAndReinsertion()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var parent = form.Controls.Add(new Panel { Bounds = new(0, 0, 200, 200) });
        var viewport = parent.Controls.Add(new ScrollableControl { Bounds = new(0, 0, 100, 100), AutoScroll = true });
        viewport.Controls.Add(new Control { Bounds = new(0, 0, 300, 300) });
        host.Show(form); host.LayoutUntilStable();
        bool once = false;
        viewport.Scroll += (_, _) => {
            if (once) return; once = true;
            form.Controls.Remove(parent); form.Controls.Add(parent);
        };
        Assert.False(viewport.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(50, 50)));
        Assert.True(viewport.HorizontalScrollProperties.Value > 0);
        Assert.Equal(0, viewport.VerticalScrollProperties.Value);
    }

    [Fact]
    public void ReentrantScrollUsesCommittedOffsetAndPublishesOnlyFinalViewport()
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new ScrollableControl { AutoScroll = true };
        var first = panel.Controls.Add(new Control { Bounds = new(0, 0, 200, 20) });
        var second = panel.Controls.Add(new Control { Bounds = new(30, 30, 20, 20) });
        host.Show(panel, 100, 100); host.LayoutUntilStable();
        var offsets = new List<double>();
        panel.AccessibilityObject.ClientNotification += (_, e) => {
            if (e.EventId == AccessibleEvents.ScrollChanged) offsets.Add(panel.AccessibilityObject.ScrollInfo!.Value.Horizontal.Offset);
        };
        bool once = false;
        first.LocationChanged += (_, _) => { if (!once) { once = true; panel.HorizontalScrollProperties.Value = 20; } };
        panel.HorizontalScrollProperties.Value = 10;
        Assert.Equal(-20, first.Left); Assert.Equal(10, second.Left);
        Assert.Equal(new[] { 20d }, offsets);
        offsets.Clear();
        Assert.True(panel.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(20, null)));
        Assert.Empty(offsets);
    }

    [Fact]
    public void ViewportFractionsClampBeforeIntegerConversionAndResizeResetsExtent()
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new ScrollableControl { AutoScroll = true };
        panel.Controls.Add(new Control { Bounds = new(0, 0, 200, 20) });
        var window = host.Show(panel, 100, 100); host.LayoutUntilStable();
        Assert.True(panel.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ByViewport(double.MaxValue, 0)));
        Assert.Equal(panel.HorizontalScrollProperties.Maximum, panel.HorizontalScrollProperties.Value);
        window.Resize(300, 200); host.LayoutUntilStable();
        Assert.False(panel.AccessibilityObject.ScrollInfo!.Value.Horizontal.IsScrollable);
        Assert.Equal(0, panel.HorizontalScrollProperties.Value);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)] [InlineData(-1d)] [InlineData(101d)]
    public void PercentRequestsRejectMalformedNumbers(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => AccessibleScrollRequest.ToPercent(value, null));
}
