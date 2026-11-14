using System.Drawing;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ScrollControlIntoViewTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2)]
    public void RevealUsesLogicalPaddedViewportAndPreservesFocus(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(600, 500, scale));
        using var form = new Form();
        var panel = form.Controls.Add(new ScrollableControl { Size = new Size(200, 160), AutoScroll = true,
            Padding = new Padding(12, 8, 23, 17) });
        panel.Style.Border.Width = 2;
        var target = panel.Controls.Add(new Button { Bounds = new Rectangle(260, 220, 40, 24), Enabled = false });
        var focus = form.Controls.Add(new TextBox { Bounds = new Rectangle(280, 25, 150, 30) });
        host.Show(form);
        host.LayoutUntilStable();
        focus.Select();
        panel.HorizontalScrollProperties.Minimum = 30;
        panel.VerticalScrollProperties.Minimum = 50;
        panel.PerformLayout();
        var notifications = Observe(panel);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(new Rectangle(120, 102, 40, 24), target.Bounds);
        Assert.Equal(170, panel.HorizontalScrollProperties.Value);
        Assert.Equal(168, panel.VerticalScrollProperties.Value);
        Assert.Equal(new[] { (ScrollOrientation.HorizontalScroll, 0, 140, ScrollEventType.ThumbPosition),
            (ScrollOrientation.VerticalScroll, 0, 118, ScrollEventType.ThumbPosition) }, notifications);
        Assert.True(focus.Focused);
        Assert.False(target.Focused);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(2, notifications.Count);
    }

    [Fact]
    public void NestedRevealRunsInnerFirstAndStopsAtReceiver()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var above = form.Controls.Add(new ScrollableControl { Size = new Size(220, 180), AutoScroll = true });
        var outer = above.Controls.Add(new ScrollableControl { Bounds = new Rectangle(375, 320, 180, 140), AutoScroll = true });
        var inner = outer.Controls.Add(new ScrollableControl { Bounds = new Rectangle(250, 210, 120, 100), AutoScroll = true });
        var target = inner.Controls.Add(new Button { Bounds = new Rectangle(150, 140, 40, 25) });
        host.Show(form);
        host.LayoutUntilStable();
        var events = new List<(ScrollableControl, ScrollOrientation)>();
        foreach (var viewport in new[] { above, outer, inner })
            viewport.Scroll += (_, e) => events.Add((viewport, e.ScrollOrientation));

        Assert.True(outer.ScrollControlIntoView(target));
        Assert.Equal(new[] { (inner, ScrollOrientation.HorizontalScroll), (inner, ScrollOrientation.VerticalScroll),
            (outer, ScrollOrientation.HorizontalScroll), (outer, ScrollOrientation.VerticalScroll) }, events);
        Assert.Equal(new Point(65, 60), target.Location);
        Assert.Equal(new Point(60, 40), inner.Location);
        Assert.Equal(0, above.HorizontalScrollProperties.Value);
        Assert.Equal(0, above.VerticalScrollProperties.Value);
        Assert.Equal(new[] { 85, 80, 190, 170 }, new[] { inner.HorizontalScrollProperties.Value,
            inner.VerticalScrollProperties.Value, outer.HorizontalScrollProperties.Value, outer.VerticalScrollProperties.Value });
        outer.PerformLayout();
        inner.PerformLayout();
        Assert.True(outer.ScrollControlIntoView(target));
        Assert.Equal(4, events.Count);
    }

    [Theory]
    [InlineData(0, 170)]
    [InlineData(370, 170)]
    [InlineData(220, 220)]
    public void OversizedTargetSettlesAtLeadingEdgeOrKeepsExistingCoverage(int start, int end)
    {
        using var host = ModernFormsTestHost.Create();
        using var panel = new ScrollableControl { AutoScroll = true };
        var target = panel.Controls.Add(new Control { Bounds = new Rectangle(170, 0, 250, 30) });
        panel.Controls.Add(new Control { Bounds = new Rectangle(0, 30, 700, 10) });
        host.Show(panel, 110, 100);
        host.LayoutUntilStable();
        panel.HorizontalScrollProperties.Value = start;
        var notifications = Observe(panel);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(end, panel.HorizontalScrollProperties.Value);
        Assert.Equal(start == end ? 0 : 1, notifications.Count);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(start == end ? 0 : 1, notifications.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingOrReinsertingTargetDuringScrollRetiresRequestBeforeLaterAxes(bool reinsert)
    {
        using var host = ModernFormsTestHost.Create();
        using var outer = new ScrollableControl { AutoScroll = true };
        var inner = outer.Controls.Add(new ScrollableControl { Bounds = new Rectangle(300, 200, 100, 80), AutoScroll = true });
        using var target = new Control { Bounds = new Rectangle(170, 160, 24, 24) };
        inner.Controls.Add(target);
        inner.Controls.Add(new Control { Bounds = new Rectangle(350, 350, 10, 10) });
        host.Show(outer, 180, 130);
        host.LayoutUntilStable();
        var notifications = Observe(inner);
        inner.Scroll += (_, e) => {
            if (e.ScrollOrientation != ScrollOrientation.HorizontalScroll) return;
            inner.Controls.Remove(target);
            if (reinsert) inner.Controls.Add(target);
        };
        Assert.False(outer.ScrollControlIntoView(target));
        Assert.Single(notifications);
        Assert.Equal(0, inner.VerticalScrollProperties.Value);
        Assert.Equal(0, outer.HorizontalScrollProperties.Value);
        Assert.Equal(0, outer.VerticalScrollProperties.Value);
    }

    [Fact]
    public void InvalidTargetsDoNotScrollAndNoMovementCanSucceed()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        var panel = form.Controls.Add(new ScrollableControl { AutoScroll = true, Size = new Size(120, 100) });
        var other = form.Controls.Add(new Panel { Bounds = new Rectangle(140, 0, 100, 100) });
        var foreign = other.Controls.Add(new Button());
        var hidden = panel.Controls.Add(new Button { Visible = false });
        var inside = panel.Controls.Add(new Button { Bounds = new Rectangle(12, 18, 52, 25) });
        var disposed = panel.Controls.Add(new Button());
        using var detached = new Button();
        host.Show(form);
        host.LayoutUntilStable();
        disposed.Dispose();
        var notifications = Observe(panel);
        Assert.Throws<ArgumentNullException>(() => panel.ScrollControlIntoView(null!));
        foreach (var target in new Control[] { panel, foreign, hidden, disposed, detached })
            Assert.False(panel.ScrollControlIntoView(target));
        Assert.True(panel.ScrollControlIntoView(inside));
        Assert.Empty(notifications);
        panel.AutoScroll = false;
        Assert.False(panel.ScrollControlIntoView(inside));
        Assert.Empty(notifications);
    }

    [Fact]
    public void ClampedAxisAndLayoutAfterResizeUseTheSameScrollPosition()
    {
        using var host = ModernFormsTestHost.Create();
        using var panel = new ScrollableControl { AutoScroll = true, Padding = new Padding(4, 8, 12, 16) };
        var contents = panel.Controls.Add(new Panel { Bounds = new Rectangle(150, 180, 60, 55) });
        var target = contents.Controls.Add(new Button { Dock = DockStyle.Right, Width = 20 });
        var anchor = contents.Controls.Add(new Control { Bounds = new Rectangle(10, 10, 14, 18),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom });
        host.Show(panel, 124, 110);
        host.LayoutUntilStable();
        var notifications = Observe(panel);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(panel.HorizontalScrollProperties.Maximum, panel.HorizontalScrollProperties.Value);
        Assert.Equal(panel.VerticalScrollProperties.Maximum, panel.VerticalScrollProperties.Value);
        Assert.Equal(2, notifications.Count);
        var docked = target.Bounds;
        var anchored = anchor.Bounds;
        var old = contents.Location;
        panel.Size = new Size(164, 142);
        panel.PerformLayout();
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(docked, target.Bounds);
        Assert.Equal(anchored, anchor.Bounds);
        Assert.Equal(new Point(old.X + 40, old.Y + 32), contents.Location);
        Assert.Equal(4, notifications.Count);
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(4, notifications.Count);
    }

    [Fact]
    public void SecondAxisUsesTargetGeometryAfterTheFirstObserver()
    {
        using var host = ModernFormsTestHost.Create();
        using var panel = new ScrollableControl { AutoScroll = true };
        var target = panel.Controls.Add(new Button { Bounds = new Rectangle(180, 180, 24, 24) });
        panel.Controls.Add(new Control { Bounds = new Rectangle(300, 300, 10, 10) });
        host.Show(panel, 100, 100);
        host.LayoutUntilStable();
        var notifications = Observe(panel);
        panel.Scroll += (_, e) => {
            if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll) target.Top = 20;
        };
        Assert.True(panel.ScrollControlIntoView(target));
        Assert.Equal(new Point(61, 20), target.Location);
        Assert.Equal(0, panel.VerticalScrollProperties.Value);
        Assert.Single(notifications);
    }

    private static List<(ScrollOrientation Axis, int Old, int New, ScrollEventType Cause)> Observe(ScrollableControl panel)
    {
        var changes = new List<(ScrollOrientation, int, int, ScrollEventType)>();
        panel.Scroll += (_, e) => changes.Add((e.ScrollOrientation, e.OldValue, e.NewValue, e.Type));
        return changes;
    }
}
