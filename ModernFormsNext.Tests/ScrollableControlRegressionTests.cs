using System.Drawing;
using System.Linq;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ScrollableControlRegressionTests
{
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
