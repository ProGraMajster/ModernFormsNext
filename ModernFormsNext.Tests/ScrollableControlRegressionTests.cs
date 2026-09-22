using System.Drawing;
using System.Linq;
using System.Reflection;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ScrollableControlRegressionTests
{
    [Fact]
    public void NestedRevealKeepsBothScrollOriginsAndAccessibleBoundsAfterRelayout()
    {
        using var root = new VisibleRoot { Size = new Size(1000, 800) };
        var outer = root.Controls.Add(new Panel { AutoScroll = true, Size = new Size(300, 200) });
        var inner = outer.Controls.Add(new FlowLayoutPanel {
            AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Bounds = new Rectangle(400, 300, 240, 120)
        });
        for (int i = 0; i < 8; i++)
            inner.Controls.Add(new Control { Size = new Size(180, 80), Margin = Padding.Empty });
        outer.PerformLayout();
        inner.PerformLayout();
        Control target = inner.Controls[^1];
        var peer = target.AccessibilityObject;
        long identity = peer.RuntimeId;
        Assert.True(peer.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.True(outer.HorizontalScrollProperties.Value > 0);
        Assert.True(outer.VerticalScrollProperties.Value > 0);
        Assert.True(inner.VerticalScrollProperties.Value > 0);
        var outerPosition = outer.TouchScrollPosition;
        var innerPosition = inner.TouchScrollPosition;
        Rectangle accessibleBounds = peer.Bounds;
        Rectangle innerViewport = inner.AccessibilityObject.ScrollInfo!.Value.ViewportBounds;
        Assert.True(innerViewport.Contains(accessibleBounds));
        Assert.Equal(AccessibleStates.None, peer.State & AccessibleStates.Offscreen);
        for (int i = 0; i < 3; i++)
        {
            outer.Visible = false;
            outer.Visible = true;
            outer.PerformLayout();
            inner.PerformLayout();
            Assert.Equal(outerPosition, outer.TouchScrollPosition);
            Assert.Equal(innerPosition, inner.TouchScrollPosition);
            Assert.Equal(new Point(400 - outerPosition.X, 300 - outerPosition.Y), inner.Location);
            Assert.Equal(7 * 80 - innerPosition.Y, target.Top);
            Assert.Equal(accessibleBounds, peer.Bounds);
            Assert.Equal(innerViewport, inner.AccessibilityObject.ScrollInfo!.Value.ViewportBounds);
            Assert.Equal(identity, peer.RuntimeId);
            Assert.True(peer.PerformAction(AccessibleActions.ScrollIntoView));
            Assert.Equal(outerPosition, outer.TouchScrollPosition);
            Assert.Equal(innerPosition, inner.TouchScrollPosition);
        }
    }

    [Theory]
    [InlineData("scrollable")]
    [InlineData("panel")]
    [InlineData("usercontrol")]
    public void ScrolledDockAnchorAndAutoSizeMatchTheUnscrolledLayout(string kind)
    {
        using var root = new VisibleRoot { Size = new Size(1200, 900) };
        var reference = CreateContainer();
        var scrolled = CreateContainer();
        scrolled.HorizontalScrollProperties.Value = 40;
        scrolled.VerticalScrollProperties.Value = 50;
        Verify();
        scrolled.Visible = false;
        Verify();
        scrolled.Visible = true;
        scrolled.PerformLayout();
        Verify();
        reference.Size = scrolled.Size = new Size(460, 310);
        Verify();
        scrolled.PerformLayout();
        Verify();
        scrolled.HorizontalScrollProperties.Value = scrolled.HorizontalScrollProperties.Maximum;
        scrolled.VerticalScrollProperties.Value = scrolled.VerticalScrollProperties.Maximum;
        reference.Size = scrolled.Size = new Size(700, 650);
        Verify(new Point(scrolled.HorizontalScrollProperties.Maximum, scrolled.VerticalScrollProperties.Maximum));
        reference.Size = scrolled.Size = new Size(900, 800);
        Verify(Point.Empty);
        Assert.False(scrolled.Controls.GetAllControls(true).OfType<HorizontalScrollBar>().Single().Visible);
        Assert.False(GetVerticalScrollBar(scrolled).Visible);
        scrolled.PerformLayout();
        Verify(Point.Empty);

        ScrollableControl CreateContainer()
        {
            ScrollableControl panel = kind switch {
                "panel" => new Panel(), "usercontrol" => new UserControl(), _ => new ScrollableControl()
            };
            panel.AutoScroll = true;
            panel.Size = new Size(400, 250);
            root.Controls.Add(panel);
            panel.Controls.Add(new Control { Size = new Size(800, 700), Margin = Padding.Empty });
            foreach (var anchor in new[] { AnchorStyles.Top | AnchorStyles.Left, AnchorStyles.Top | AnchorStyles.Right,
                AnchorStyles.Bottom | AnchorStyles.Left, AnchorStyles.Bottom | AnchorStyles.Right,
                AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AnchorStyles.None })
                panel.Controls.Add(new Control { Bounds = new Rectangle(100, 100, 80, 40), Anchor = anchor });
            foreach (var dock in new[] { DockStyle.Fill, DockStyle.Top, DockStyle.Bottom, DockStyle.Left, DockStyle.Right })
                panel.Controls.Add(new Control { Size = new Size(30, 20), Dock = dock });
            panel.Controls.Add(new PositiveConstraintControl { AutoSize = true, Location = new Point(20, 30) });
            panel.PerformLayout();
            return panel;
        }

        void Verify(Point? expectedOffset = null)
        {
            Point offset = scrolled.TouchScrollPosition;
            Assert.Equal(expectedOffset ?? new Point(40, 50), offset);
            for (int i = 0; i < reference.Controls.Count; i++)
            {
                Rectangle expected = reference.Controls[i].Bounds;
                expected.Offset(-offset.X, -offset.Y);
                Assert.True(expected == scrolled.Controls[i].Bounds,
                    $"{kind} child {i} ({scrolled.Controls[i].Dock}/{scrolled.Controls[i].Anchor}): expected {expected}, actual {scrolled.Controls[i].Bounds}");
            }
        }
    }

    [Theory]
    [InlineData(FlowDirection.TopDown)]
    [InlineData(FlowDirection.LeftToRight)]
    [InlineData(FlowDirection.BottomUp)]
    [InlineData(FlowDirection.RightToLeft)]
    public void FlowRelayoutUsesTheScrolledOriginWithoutChangingTheExtent(FlowDirection direction)
    {
        using var root = new VisibleRoot { Size = new Size(400, 300) };
        var panel = root.Controls.Add(new FlowLayoutPanel {
            AutoScroll = true, FlowDirection = direction, WrapContents = false,
            Size = new Size(240, 120), Padding = new Padding(7, 9, 11, 13)
        });
        for (int i = 0; i < 8; i++)
            panel.Controls.Add(new Control { Size = new Size(100, 80), Margin = new Padding(2, 3, 4, 5) });
        panel.PerformLayout();
        Point origin = panel.DisplayRectangle.Location;
        Size extent = Layout.CommonProperties.GetLayoutBounds(panel);
        Rectangle[] initial = panel.Controls.Select(c => c.Bounds).ToArray();
        bool vertical = direction is FlowDirection.TopDown or FlowDirection.BottomUp;
        var bar = vertical ? panel.VerticalScrollProperties : panel.HorizontalScrollProperties;
        bar.Value = 60;
        for (int pass = 0; pass < 3; pass++)
        {
            panel.Visible = false;
            Verify();
            panel.Visible = true;
            panel.PerformLayout();
            Verify();
        }

        void Verify()
        {
            var offset = vertical ? new Point(0, 60) : new Point(60, 0);
            Assert.Equal(offset, panel.TouchScrollPosition);
            Assert.Equal(new Point(origin.X - offset.X, origin.Y - offset.Y), panel.DisplayRectangle.Location);
            Assert.Equal(extent, Layout.CommonProperties.GetLayoutBounds(panel));
            for (int i = 0; i < initial.Length; i++)
            {
                Rectangle expected = initial[i];
                expected.Offset(-offset.X, -offset.Y);
                Assert.Equal(expected, panel.Controls[i].Bounds);
            }
        }
    }

    [Fact]
    public void WrappingFlowKeepsItsViewportAndRowsWhileAnAncestorIsHidden()
    {
        using var root = new VisibleRoot();
        var view = root.Controls.Add(new Panel { Size = new Size(240, 120) });
        var panel = view.Controls.Add(new FlowLayoutPanel { AutoScroll = true, Dock = DockStyle.Fill });
        for (int i = 0; i < 8; i++)
            panel.Controls.Add(new Control { Size = new Size(105, 70), Margin = new Padding(3) });
        panel.PerformLayout();
        Rectangle[] initial = panel.Controls.Select(c => c.Bounds).ToArray();
        Size viewport = panel.DisplayRectangle.Size;
        panel.VerticalScrollProperties.Value = 60;
        view.Visible = false;
        Verify();
        view.Visible = true;
        panel.PerformLayout();
        Verify();

        void Verify()
        {
            Assert.Equal(viewport, panel.DisplayRectangle.Size);
            Assert.Equal(60, panel.VerticalScrollProperties.Value);
            for (int i = 0; i < initial.Length; i++)
            {
                Rectangle expected = initial[i];
                expected.Offset(0, -60);
                Assert.Equal(expected, panel.Controls[i].Bounds);
            }
        }
    }

    [Fact]
    public void NonWrappingFlowMeasuresAutoSizedChildrenWithPositiveConstraintsWhenScrolled()
    {
        using var root = new VisibleRoot();
        var panel = root.Controls.Add(new FlowLayoutPanel {
            AutoScroll = true, WrapContents = false, Size = new Size(120, 100)
        });
        for (int i = 0; i < 5; i++)
            panel.Controls.Add(new PositiveConstraintControl { AutoSize = true, Margin = Padding.Empty });
        panel.HorizontalScrollProperties.Value = 40;
        panel.PerformLayout();
        Assert.Equal(-40, panel.Controls[0].Left);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("custom")]
    [InlineData("default")]
    public void SharedScrollOriginPreservesBothAxesForOtherLayoutEngines(string engine)
    {
        using var root = new VisibleRoot();
        ScrollableControl panel;
        if (engine == "table")
        {
            var table = new TableLayoutPanel { ColumnCount = 1, RowCount = 1 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 700));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 600));
            panel = table;
        }
        else panel = engine == "custom" ? new CustomLayoutPanel() : new ScrollableControl();
        panel.AutoScroll = true;
        panel.Size = new Size(240, 120);
        root.Controls.Add(panel);
        var child = panel.Controls.Add(new Control { Size = new Size(700, 600), Margin = Padding.Empty });
        panel.PerformLayout();
        Rectangle initial = child.Bounds;
        panel.HorizontalScrollProperties.Value = 70;
        panel.VerticalScrollProperties.Value = 90;
        for (int pass = 0; pass < 3; pass++)
        {
            panel.Visible = false;
            Verify();
            panel.Visible = true;
            panel.PerformLayout();
            Verify();
        }

        void Verify()
        {
            Assert.Equal(new Point(70, 90), panel.TouchScrollPosition);
            Assert.Equal(new Rectangle(initial.X - 70, initial.Y - 90, initial.Width, initial.Height), child.Bounds);
        }
    }

    private sealed class PositiveConstraintControl : Control
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            Assert.True(proposedSize.Width > 0 && proposedSize.Height > 0, $"Invalid flow measurement: {proposedSize}");
            return new Size(80, 40);
        }
    }

    private sealed class CustomLayoutPanel : ScrollableControl
    {
        public override Layout.LayoutEngine LayoutEngine { get; } = new OriginLayout();

        // An external layout engine cannot publish internal CommonProperties.LayoutBounds.
        // The fallback extent calculation must work with its scrolled child bounds too.
        private sealed class OriginLayout : Layout.LayoutEngine
        {
            public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
            {
                var control = (Control)container;
                foreach (Control child in control.Controls)
                    child.Location = control.DisplayRectangle.Location;
                return false;
            }
        }
    }

    [Fact]
    public void FlowLayoutPanelUsesArrangedContentBoundsForVerticalScrollRange()
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Size = new Size(240, 120),
            Padding = new Padding(0, 0, 8, 0)
        };
        root.Controls.Add(panel);

        panel.Visible = false;
        for (var index = 0; index < 6; index++)
        {
            panel.Controls.Add(new Control
            {
                Size = new Size(220, 42),
                Margin = new Padding(0, 0, 0, 8)
            });
        }

        panel.Visible = true;
        panel.PerformLayout();

        var flowScrollBar = GetVerticalScrollBar(panel);
        Assert.True(
            flowScrollBar.Visible,
            $"visible={flowScrollBar.Visible}; maximum={flowScrollBar.Maximum}; large={flowScrollBar.LargeChange}; layout={Layout.CommonProperties.GetLayoutBounds(panel)}; client={panel.ClientSize}; last={panel.Controls[^1].Bounds}");
    }

    [Theory]
    [InlineData("Label")]
    [InlineData("Panel")]
    [InlineData("Button")]
    [InlineData("CheckBox")]
    [InlineData("TextBox")]
    [InlineData("ComboBox")]
    public void MouseWheelOverCommonChildScrollsScrollableAncestor(string childKind)
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var panel = new ScrollableControl
        {
            AutoScroll = true,
            Size = new Size(240, 120)
        };
        root.Controls.Add(panel);
        using var child = CreateCommonChild(childKind);
        using var filler = new Control
        {
            Bounds = new Rectangle(10, 360, 160, 28)
        };
        panel.Controls.AddRange(child, filler);
        panel.PerformLayout();
        var scrollBar = GetVerticalScrollBar(panel);
        Assert.True(
            scrollBar.Visible,
            $"visible={scrollBar.Visible}; maximum={scrollBar.Maximum}; large={scrollBar.LargeChange}; client={panel.ClientSize}; filler={filler.Bounds}");

        root.RaiseMouseWheel(new MouseEventArgs(
            MouseButtons.None,
            0,
            20,
            20,
            new Point(0, -1)));

        Assert.True(panel.VerticalScrollProperties.Value > 0);
    }

    [Fact]
    public void MouseWheelOverScrollableBackgroundScrollsControl()
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var panel = CreateOverflowingPanel(root);

        RaiseWheel(root, 210, 40, -1);

        Assert.True(panel.VerticalScrollProperties.Value > 0);
    }

    [Fact]
    public void NestedScrollableControlConsumesWheelBeforeOuterContainer()
    {
        var (root, outer, inner) = CreateNestedScrollableControls();
        using (root)
        using (outer)
        using (inner)
        {
            RaiseWheel(root, 20, 20, -1);

            Assert.True(inner.VerticalScrollProperties.Value > 0);
            Assert.Equal(0, outer.VerticalScrollProperties.Value);
        }
    }

    [Fact]
    public void NestedScrollableControlBubblesWheelAtItsScrollBoundary()
    {
        var (root, outer, inner) = CreateNestedScrollableControls();
        using (root)
        using (outer)
        using (inner)
        {
            inner.VerticalScrollProperties.Value = inner.VerticalScrollProperties.Maximum;

            RaiseWheel(root, 20, 20, -1);

            Assert.Equal(inner.VerticalScrollProperties.Maximum, inner.VerticalScrollProperties.Value);
            Assert.True(outer.VerticalScrollProperties.Value > 0);
        }
    }

    [Fact]
    public void WheelAwareChildPreventsOuterContainerFromScrollingWhenItChangesValue()
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var outer = CreateOverflowingPanel(root);
        using var trackBar = new TrackBar
        {
            Bounds = new Rectangle(10, 10, 160, 32),
            Minimum = 0,
            Maximum = 10,
            Value = 5
        };
        outer.Controls.Add(trackBar);
        outer.PerformLayout();

        RaiseWheel(root, 20, 20, -1);

        Assert.Equal(4, trackBar.Value);
        Assert.Equal(0, outer.VerticalScrollProperties.Value);
    }

    [Fact]
    public void WheelAwareChildBubblesAtItsValueBoundary()
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var outer = CreateOverflowingPanel(root);
        using var trackBar = new TrackBar
        {
            Bounds = new Rectangle(10, 10, 160, 32),
            Minimum = 0,
            Maximum = 10,
            Value = 0
        };
        outer.Controls.Add(trackBar);
        outer.PerformLayout();

        RaiseWheel(root, 20, 20, -1);

        Assert.Equal(0, trackBar.Value);
        Assert.True(outer.VerticalScrollProperties.Value > 0);
    }

    [Fact]
    public void HandledMouseWheelEventStopsBubbling()
    {
        using var root = new VisibleRoot { Size = new Size(300, 180) };
        using var outer = CreateOverflowingPanel(root);
        using var child = new Button { Bounds = new Rectangle(10, 10, 160, 32) };
        child.MouseWheel += (_, e) => e.Handled = true;
        outer.Controls.Add(child);
        outer.PerformLayout();

        RaiseWheel(root, 20, 20, -1);

        Assert.Equal(0, outer.VerticalScrollProperties.Value);
    }

    [Fact]
    public void FlowLayoutPanelUpdatesScrollbarForRuntimeCollectionAndResizeChanges()
    {
        using var root = new VisibleRoot { Size = new Size(700, 700) };
        using var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Size = new Size(260, 140)
        };
        root.Controls.Add(panel);
        var scrollBar = GetVerticalScrollBar(panel);

        panel.PerformLayout();
        Assert.False(scrollBar.Visible);

        var controls = Enumerable.Range(0, 30)
            .Select(_ => new Control
            {
                Size = new Size(230, 40),
                Margin = new Padding(0, 0, 0, 6)
            })
            .ToArray();
        panel.Controls.AddRange(controls);
        panel.PerformLayout();
        Assert.True(scrollBar.Visible);
        Assert.True(panel.VerticalScrollProperties.Maximum > panel.ClientSize.Height);

        panel.Height = 1600;
        panel.PerformLayout();
        Assert.False(scrollBar.Visible);

        panel.Height = 140;
        panel.PerformLayout();
        Assert.True(scrollBar.Visible);

        panel.VerticalScrollProperties.Value = panel.VerticalScrollProperties.Maximum;
        panel.Controls.Clear();
        foreach (var control in controls)
            control.Dispose();
        panel.Controls.Add(new Control { Size = new Size(230, 40) });
        panel.PerformLayout();
        Assert.False(scrollBar.Visible);
        Assert.Equal(0, panel.VerticalScrollProperties.Value);
    }

    [Fact]
    public void FlowLayoutPanelKeepsScrollbarHiddenForSeveralFittingItems()
    {
        using var root = new VisibleRoot { Size = new Size(500, 400) };
        using var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Size = new Size(260, 160)
        };
        root.Controls.Add(panel);
        for (var index = 0; index < 3; index++)
            panel.Controls.Add(new Control { Size = new Size(230, 36) });

        panel.PerformLayout();

        Assert.False(GetVerticalScrollBar(panel).Visible);
    }

    [Fact]
    public void FlowLayoutPanelCreatesVerticalScrollbarForQueueSizedUserControls()
    {
        using var root = new VisibleRoot { Size = new Size(900, 700) };
        using var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Size = new Size(680, 360),
            Padding = new Padding(0, 0, 8, 0)
        };
        root.Controls.Add(panel);
        for (var index = 0; index < 4; index++)
        {
            panel.Controls.Add(new UserControl
            {
                Size = new Size(640, 150),
                Margin = new Padding(0, 0, 0, 14)
            });
        }

        panel.PerformLayout();

        Assert.True(GetVerticalScrollBar(panel).Visible);
        Assert.False(panel.Controls.GetAllControls(true).OfType<HorizontalScrollBar>().Single().Visible);
    }

    [Theory]
    [InlineData(1d, 96)]
    [InlineData(1.25d, 120)]
    [InlineData(1.5d, 144)]
    public void FlowExtentAndWheelRoutingRemainStableAtWindowScale(double scale, int expectedDpi)
    {
        using var window = new DpiTestWindow(scale);
        using var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Size = new Size(240, 120)
        };
        window.Controls.Add(panel);
        for (var index = 0; index < 6; index++)
            panel.Controls.Add(new Label { Size = new Size(220, 42), Text = $"Item {index}" });
        panel.PerformLayout();

        Assert.Equal(expectedDpi, panel.DeviceDpi);
        Assert.True(GetVerticalScrollBar(panel).Visible);

        window.adapter.RaiseMouseWheel(new MouseEventArgs(
            MouseButtons.None,
            0,
            20,
            20,
            new Point(0, -1)));

        Assert.True(panel.VerticalScrollProperties.Value > 0);
    }

    private static Control CreateCommonChild(string childKind)
    {
        Control child = childKind switch
        {
            "Label" => new Label { Text = "Ordinary content" },
            "Panel" => new Panel(),
            "Button" => new Button { Text = "Action" },
            "CheckBox" => new CheckBox { Text = "Option" },
            "TextBox" => new TextBox { Text = "Input" },
            "ComboBox" => new ComboBox(),
            _ => throw new ArgumentOutOfRangeException(nameof(childKind))
        };
        child.Bounds = new Rectangle(10, 10, 160, 32);
        return child;
    }

    private static ScrollableControl CreateOverflowingPanel(VisibleRoot root)
    {
        var panel = new ScrollableControl
        {
            AutoScroll = true,
            Size = new Size(240, 120)
        };
        panel.Controls.Add(new Control { Bounds = new Rectangle(10, 420, 160, 28) });
        root.Controls.Add(panel);
        panel.PerformLayout();
        Assert.True(GetVerticalScrollBar(panel).Visible);
        return panel;
    }

    private static (VisibleRoot Root, ScrollableControl Outer, ScrollableControl Inner) CreateNestedScrollableControls()
    {
        var root = new VisibleRoot { Size = new Size(320, 200) };
        var outer = new ScrollableControl
        {
            AutoScroll = true,
            Size = new Size(260, 140)
        };
        var inner = new ScrollableControl
        {
            AutoScroll = true,
            Bounds = new Rectangle(10, 10, 220, 90)
        };
        inner.Controls.AddRange(
            new Label { Bounds = new Rectangle(5, 5, 160, 28), Text = "Inner content" },
            new Control { Bounds = new Rectangle(5, 300, 160, 28) });
        outer.Controls.AddRange(inner, new Control { Bounds = new Rectangle(10, 520, 160, 28) });
        root.Controls.Add(outer);
        inner.PerformLayout();
        outer.PerformLayout();
        Assert.True(GetVerticalScrollBar(inner).Visible);
        Assert.True(GetVerticalScrollBar(outer).Visible);
        return (root, outer, inner);
    }

    private static void RaiseWheel(Control root, int x, int y, int deltaY)
        => root.RaiseMouseWheel(new MouseEventArgs(
            MouseButtons.None,
            0,
            x,
            y,
            new Point(0, deltaY)));

    private static VerticalScrollBar GetVerticalScrollBar(ScrollableControl control)
        => control.Controls.GetAllControls(true).OfType<VerticalScrollBar>().Single();

    private sealed class VisibleRoot : Control
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }

    private sealed class DpiTestWindow : WindowBase
    {
        public DpiTestWindow(double renderScaling)
            : this(DispatchProxy.Create<IWindowBaseImpl, DpiWindowProxy>(), renderScaling)
        {
        }

        private DpiTestWindow(IWindowBaseImpl implementation, double renderScaling)
            : base(implementation)
            => ((DpiWindowProxy)implementation).RenderScaling = renderScaling;
    }

    private class DpiWindowProxy : DispatchProxy
    {
        public double RenderScaling { get; set; } = 1d;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_RenderScaling")
                return RenderScaling;
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;
            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
