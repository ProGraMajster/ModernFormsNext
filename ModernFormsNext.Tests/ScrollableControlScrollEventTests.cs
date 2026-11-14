using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ScrollableControlScrollEventTests
{
    public static IEnumerable<object[]> MovementPaths()
    {
        foreach (bool vertical in new[] { false, true })
            foreach (string path in new[] { "value", "wheel+", "wheel-", "arrow+", "arrow-",
                "page+", "page-", "thumb", "touch", "accessible-small", "accessible-page",
                "accessible-percent", "accessible-viewport" })
                yield return new object[] { vertical, path };
    }

    [Theory]
    [MemberData(nameof(MovementPaths))]
    public void EveryMovementPublishesCommittedLogicalGeometry(bool vertical, string path)
    {
        using var root = new VisibleRoot { Size = new Size(1000, 800) };
        var panel = root.Controls.Add(new ViewportProbe { AutoScroll = true, Size = new Size(300, 200) });
        var marker = panel.Controls.Add(new Control { Bounds = new Rectangle(700, 700, 30, 30) });
        panel.Controls.Add(new Control { Bounds = new Rectangle(1200, 1200, 1, 1) });
        var bar = Bar(panel, vertical);
        bar.Minimum = 40;
        bar.Value = 340;
        Assert.Equal(vertical ? new Point(0, 300) : new Point(300, 0), panel.TouchScrollPosition);
        var expectedType = path switch {
            "wheel+" or "arrow+" or "accessible-small" => ScrollEventType.SmallIncrement,
            "wheel-" or "arrow-" => ScrollEventType.SmallDecrement,
            "page+" or "accessible-page" or "accessible-viewport" => ScrollEventType.LargeIncrement,
            "page-" => ScrollEventType.LargeDecrement,
            "thumb" or "touch" => ScrollEventType.ThumbTrack,
            _ => ScrollEventType.ThumbPosition
        };
        int events = 0;
        panel.Scroll += (_, e) => {
            events++;
            Assert.Equal(vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll, e.ScrollOrientation);
            Assert.Equal(expectedType, e.Type);
            Assert.Equal(300, e.OldValue);
            Assert.Equal(bar.Value - bar.Minimum, e.NewValue);
            Assert.NotEqual(e.OldValue, e.NewValue);
            Assert.Equal(panel.TouchScrollPosition, panel.RefreshedPosition);
            Assert.Equal(new Point(700 - panel.TouchScrollPosition.X, 700 - panel.TouchScrollPosition.Y), marker.Location);
            Assert.Equal(new Point(-panel.TouchScrollPosition.X, -panel.TouchScrollPosition.Y), panel.DisplayRectangle.Location);
        };

        switch (path) {
            case "value": bar.Value += 7; break;
            case "wheel+": case "wheel-":
                int delta = path.EndsWith('+') ? -1 : 1;
                panel.RaiseMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 20, 20,
                    vertical ? new Point(0, delta) : new Point(delta, 0)));
                break;
            case "touch": Assert.True(panel.ScrollByTouchDelta(vertical ? new Point(0, -7) : new Point(-7, 0))); break;
            case "accessible-small": case "accessible-page":
                var amount = path == "accessible-small" ? AccessibleScrollAmount.SmallIncrement : AccessibleScrollAmount.LargeIncrement;
                Assert.True(panel.PerformAccessibleScroll(AccessibleScrollRequest.ByAmount(
                    vertical ? AccessibleScrollAmount.None : amount, vertical ? amount : AccessibleScrollAmount.None)));
                break;
            case "accessible-percent":
                Assert.True(panel.PerformAccessibleScroll(AccessibleScrollRequest.ToPercent(vertical ? null : 70, vertical ? 70 : null)));
                break;
            case "accessible-viewport":
                Assert.True(panel.PerformAccessibleScroll(AccessibleScrollRequest.ByViewport(vertical ? 0 : .5, vertical ? .5 : 0)));
                break;
            default:
                var renderer = RenderManager.GetRenderer<ScrollBarRenderer>()!;
                Rectangle bounds = path switch {
                    "arrow+" => renderer.GetIncrementArrowBounds(bar),
                    "arrow-" => renderer.GetDecrementArrowBounds(bar),
                    "page+" => renderer.GetIncrementTrackBounds(bar),
                    "page-" => renderer.GetDecrementTrackBounds(bar),
                    _ => renderer.GetThumbDragBounds(bar)
                };
                var point = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                bar.RaiseMouseDown(Mouse(point));
                if (path == "thumb") {
                    point.Offset(vertical ? 0 : 10, vertical ? 10 : 0);
                    bar.RaiseMouseMove(Mouse(point));
                }
                bar.RaiseMouseUp(Mouse(point));
                break;
        }
        Assert.Equal(1, events);
        bar.Value = bar.Value;
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(false, "minimum")]
    [InlineData(true, "minimum")]
    [InlineData(false, "clamp")]
    [InlineData(true, "clamp")]
    [InlineData(false, "resize")]
    [InlineData(true, "resize")]
    [InlineData(false, "hidden-range")]
    [InlineData(true, "hidden-range")]
    public void RangeAndResizeReportActualOffsetsEvenUnderHiddenAncestor(bool vertical, string operation)
    {
        var (root, panel, marker) = CreatePanel();
        using (root) {
            var bar = Bar(panel, vertical);
            bar.Minimum = 40;
            bar.Value = 340;
            if (operation == "hidden-range") root.Visible = false;
            var events = Observe(panel);
            switch (operation) {
                case "minimum": case "hidden-range": bar.Minimum = 50; break;
                case "clamp": bar.Maximum = 315; break;
                case "resize": panel.Size = new Size(1100, 1100); break;
            }
            var change = Assert.Single(events);
            Assert.Equal(vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll, change.ScrollOrientation);
            Assert.Equal(300, change.OldValue);
            Assert.Equal(bar.Value - bar.Minimum, change.NewValue);
            Assert.Equal(ScrollEventType.ThumbPosition, change.Type);
            Assert.Equal(new Point(700 - panel.TouchScrollPosition.X, 700 - panel.TouchScrollPosition.Y), marker.Location);
            Assert.Equal(panel.TouchScrollPosition, panel.RefreshedPosition);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoOpsAndHideShowDoNotInventMovement(bool vertical)
    {
        var (root, panel, _) = CreatePanel();
        using (root) {
            var bar = Bar(panel, vertical);
            var events = Observe(panel);
            bar.Value = bar.Value;
            bar.SmallChange += 1;
            bar.LargeChange += 1;
            bar.Maximum += 1;
            panel.ScrollByTouchDelta(vertical ? new Point(0, 10) : new Point(10, 0));
            Assert.Empty(events);
            // Restore the layout-owned range before testing hide/show at its real endpoint.
            panel.PerformLayout();
            bar.Value = bar.Maximum;
            events.Clear();
            panel.ScrollByTouchDelta(vertical ? new Point(0, -int.MaxValue) : new Point(-int.MaxValue, 0));
            panel.Visible = false;
            panel.Visible = true;
            panel.PerformLayout();
            Assert.Empty(events);
        }
    }

    [Fact]
    public void DisablingAutoScrollPublishesBothResetsAfterGeometryIsRestored()
    {
        var (root, panel, marker) = CreatePanel();
        using (root) {
            Bar(panel, false).Value = 100;
            Bar(panel, true).Value = 150;
            var events = Observe(panel);
            panel.Scroll += (_, _) => {
                Assert.Equal(Point.Empty, panel.TouchScrollPosition);
                Assert.Equal(new Point(700, 700), marker.Location);
                Assert.Equal(Point.Empty, panel.RefreshedPosition);
            };
            panel.AutoScroll = false;
            Assert.Collection(events,
                e => Check(e, ScrollOrientation.HorizontalScroll, 100, 0, ScrollEventType.ThumbPosition),
                e => Check(e, ScrollOrientation.VerticalScroll, 150, 0, ScrollEventType.ThumbPosition));
        }
    }

    [Fact]
    public void ReentrantTwoAxisChangesRetireStaleSubscribersAndRefreshBeforeNextEvent()
    {
        var (root, panel, marker) = CreatePanel();
        using (root) {
            var events = Observe(panel);
            bool changed = false;
            panel.Scroll += (_, e) => {
                Assert.Equal(panel.TouchScrollPosition, panel.RefreshedPosition);
                Assert.Equal(new Point(700 - panel.TouchScrollPosition.X, 700 - panel.TouchScrollPosition.Y), marker.Location);
                if (!changed) {
                    changed = true;
                    Assert.Equal(new Point(10, 20), panel.TouchScrollPosition);
                    Bar(panel, false).Value = 40;
                    Bar(panel, true).Value = 60;
                }
            };
            var later = Observe(panel);
            Assert.True(panel.ScrollByTouchDelta(new Point(-10, -20)));
            Assert.Collection(events,
                e => Check(e, ScrollOrientation.HorizontalScroll, 0, 10, ScrollEventType.ThumbTrack),
                e => Check(e, ScrollOrientation.HorizontalScroll, 10, 40, ScrollEventType.ThumbPosition),
                e => Check(e, ScrollOrientation.VerticalScroll, 0, 60, ScrollEventType.ThumbPosition));
            Assert.Equal(2, later.Count);
            Assert.Equal(40, later[0].NewValue);
        }
    }

    [Fact]
    public void FailuresDoNotSuppressOtherObserversOrLeavePublicationActive()
    {
        var (root, panel, _) = CreatePanel();
        using (root) {
            panel.Refreshed = () => throw new InvalidOperationException("derived");
            EventHandler<ScrollEventArgs> throwing = (_, _) => throw new InvalidOperationException("public");
            panel.Scroll += throwing;
            var events = Observe(panel);
            var failure = Assert.Throws<AggregateException>(() => panel.ScrollByTouchDelta(new Point(-10, -20)));
            Assert.Equal(new[] { "derived", "public", "public" }, failure.Flatten().InnerExceptions.Select(e => e.Message));
            Assert.Equal(2, events.Count);
            panel.Refreshed = null;
            panel.Scroll -= throwing;
            Bar(panel, true).Value = 30;
            Check(events[^1], ScrollOrientation.VerticalScroll, 20, 30, ScrollEventType.ThumbPosition);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovalOrDisposalStopsObsoleteCallbacks(bool dispose)
    {
        var (root, panel, _) = CreatePanel();
        using (root)
        using (panel) {
            panel.Scroll += (_, _) => { if (dispose) panel.Dispose(); else root.Controls.Remove(panel); };
            int later = 0;
            panel.Scroll += (_, _) => later++;
            panel.ScrollByTouchDelta(new Point(-10, -20));
            Assert.Equal(0, later);
        }
    }

    [Fact]
    public void SubscriberRemovalUsesSnapshotAndMutableNewValueCannotReplaceCommittedOffset()
    {
        var (root, panel, _) = CreatePanel();
        using (root) {
            int later = 0;
            EventHandler<ScrollEventArgs> removed = (_, e) => { later++; Assert.Equal(10, e.NewValue); };
            panel.Scroll += (_, e) => { panel.Scroll -= removed; e.NewValue = 999; };
            panel.Scroll += removed;
            Bar(panel, true).Value = 10;
            Assert.Equal(10, panel.TouchScrollPosition.Y);
            Assert.Equal(1, later);
            Bar(panel, true).Value = 20;
            Assert.Equal(20, panel.TouchScrollPosition.Y);
            Assert.Equal(1, later);
        }
    }

    [Fact]
    public void NonStabilizingObserverIsBoundedAndNextOperationStillWorks()
    {
        var (root, panel, _) = CreatePanel();
        using (root) {
            EventHandler<ScrollEventArgs> loop = (_, _) => Bar(panel, true).Value = Bar(panel, true).Value == 1 ? 2 : 1;
            panel.Scroll += loop;
            var failure = Assert.Throws<AggregateException>(() => Bar(panel, true).Value = 1);
            Assert.Contains(failure.Flatten().InnerExceptions, e => e.Message.Contains("64 changes"));
            panel.Scroll -= loop;
            var events = Observe(panel);
            Bar(panel, true).Value = 30;
            Assert.Equal(30, Assert.Single(events).NewValue);
        }
    }

    [Theory]
    [InlineData("value")]
    [InlineData("touch")]
    [InlineData("accessible")]
    [InlineData("range")]
    [InlineData("reset")]
    public void ChildAndPublicationFailuresAreBothRetained(string path)
    {
        var (root, panel, marker) = CreatePanel();
        using (root) {
            Bar(panel, true).Value = 100;
            var other = panel.Controls[^1];
            EventHandler childFailure = (_, _) => throw new InvalidOperationException("child");
            marker.LocationChanged += childFailure;
            panel.Scroll += (_, _) => throw new InvalidOperationException("scroll");
            var events = Observe(panel);
            var error = Assert.ThrowsAny<Exception>(() => {
                switch (path) {
                    case "value": Bar(panel, true).Value = 120; break;
                    case "touch": panel.ScrollByTouchDelta(new Point(0, -20)); break;
                    case "accessible": panel.PerformAccessibleScroll(AccessibleScrollRequest.ToPercent(null, 50)); break;
                    case "range": Bar(panel, true).Maximum = 80; break;
                    default: panel.AutoScroll = false; break;
                }
            });
            var messages = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.Select(e => e.Message).ToArray() : [error.Message];
            Assert.Contains("child", messages);
            Assert.Contains("scroll", messages);
            Assert.Single(events);
            Assert.Equal(700 - panel.TouchScrollPosition.Y, marker.Top);
            Assert.Equal(1200 - panel.TouchScrollPosition.Y, other.Top);
            marker.LocationChanged -= childFailure;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProgrammaticChangeInsideInputObserverUsesItsOwnCause(bool vertical)
    {
        var (root, panel, _) = CreatePanel();
        using (root) {
            var bar = Bar(panel, vertical);
            bool changed = false;
            panel.Scroll += (_, _) => { if (!changed) { changed = true; bar.Value = 30; } };
            var events = Observe(panel);
            bar.RaiseMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 1, 1, new Point(0, -1)));
            var change = Assert.Single(events);
            Check(change, vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll,
                5, 30, ScrollEventType.ThumbPosition);
        }
    }

    [Fact]
    public void NestedWheelPublishesOnlyTheViewportThatActuallyMoved()
    {
        using var root = new VisibleRoot { Size = new Size(1000, 800) };
        var outer = root.Controls.Add(new ScrollableControl { AutoScroll = true, Size = new Size(400, 300) });
        var inner = outer.Controls.Add(new ScrollableControl { AutoScroll = true, Bounds = new Rectangle(10, 10, 200, 150) });
        inner.Controls.Add(new Control { Bounds = new Rectangle(10, 500, 20, 20) });
        outer.Controls.Add(new Control { Bounds = new Rectangle(10, 800, 20, 20) });
        var outerEvents = Observe(outer);
        var innerEvents = Observe(inner);
        root.RaiseMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 20, 20, new Point(0, -1)));
        Check(Assert.Single(innerEvents), ScrollOrientation.VerticalScroll, 0, 5, ScrollEventType.SmallIncrement);
        Assert.Empty(outerEvents);
        Bar(inner, true).Value = Bar(inner, true).Maximum;
        innerEvents.Clear();
        root.RaiseMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 20, 20, new Point(0, -1)));
        Assert.Empty(innerEvents);
        Check(Assert.Single(outerEvents), ScrollOrientation.VerticalScroll, 0, 5, ScrollEventType.SmallIncrement);
    }

    private static (VisibleRoot Root, ViewportProbe Panel, Control Marker) CreatePanel()
    {
        var root = new VisibleRoot { Size = new Size(1400, 1400) };
        var panel = root.Controls.Add(new ViewportProbe { AutoScroll = true, Size = new Size(300, 200) });
        var marker = panel.Controls.Add(new Control { Bounds = new Rectangle(700, 700, 30, 30) });
        panel.Controls.Add(new Control { Bounds = new Rectangle(1200, 1200, 1, 1) });
        return (root, panel, marker);
    }

    private static List<ScrollEventArgs> Observe(ScrollableControl panel)
    {
        var events = new List<ScrollEventArgs>();
        panel.Scroll += (_, e) => events.Add(new ScrollEventArgs(e.Type, e.OldValue, e.NewValue, e.ScrollOrientation));
        return events;
    }

    private static void Check(ScrollEventArgs e, ScrollOrientation axis, int oldValue, int newValue, ScrollEventType type)
    {
        Assert.Equal(axis, e.ScrollOrientation);
        Assert.Equal(oldValue, e.OldValue);
        Assert.Equal(newValue, e.NewValue);
        Assert.Equal(type, e.Type);
    }

    private static MouseEventArgs Mouse(Point point)
        => new(MouseButtons.Left, 1, point.X, point.Y, Point.Empty);

    private static ScrollBar Bar(ScrollableControl panel, bool vertical)
        => panel.Controls.GetAllControls(true).OfType<ScrollBar>()
            .Single(bar => (bar is VerticalScrollBar) == vertical);

    private sealed class VisibleRoot : Control
    {
        private bool visible = true;
        public override bool Visible { get => visible; set { visible = value; base.Visible = value; } }
    }

    private sealed class ViewportProbe : ScrollableControl
    {
        public Point RefreshedPosition { get; private set; }
        public Action? Refreshed { get; set; }
        protected override void OnScrollViewportChanged(EventArgs e)
        {
            base.OnScrollViewportChanged(e);
            RefreshedPosition = TouchScrollPosition;
            Refreshed?.Invoke();
        }
    }
}
