using System.ComponentModel;
using System.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class RightToLeftLayoutTests
{
    [Fact]
    public void DirectionInheritsAndExplicitNoSurvivesAncestorChangesAndReparenting()
    {
        using var first = new VisibleRoot { RightToLeft = RightToLeft.Yes };
        using var second = new VisibleRoot();
        var inherited = first.Controls.Add(new Panel());
        var leaf = inherited.Controls.Add(new Control());
        var boundary = inherited.Controls.Add(new Panel { RightToLeft = RightToLeft.No });
        var protectedLeaf = boundary.Controls.Add(new Control());
        Assert.Equal(RightToLeft.Yes, leaf.RightToLeft);
        Assert.Equal(RightToLeft.No, protectedLeaf.RightToLeft);
        int changes = 0;
        leaf.RightToLeftChanged += (_, _) => changes++;
        inherited.Parent = second;
        Assert.Equal(RightToLeft.No, leaf.RightToLeft);
        Assert.Equal(1, changes);
        second.RightToLeft = RightToLeft.Yes;
        Assert.Equal(RightToLeft.Yes, leaf.RightToLeft);
        Assert.Equal(2, changes);
        Assert.Equal(RightToLeft.No, protectedLeaf.RightToLeft);
        boundary.RightToLeft = RightToLeft.Inherit;
        Assert.Equal(RightToLeft.Yes, protectedLeaf.RightToLeft);
        Assert.Throws<InvalidEnumArgumentException>(() => leaf.RightToLeft = (RightToLeft)9);
        Assert.Equal(RightToLeft.Yes, leaf.RightToLeft);
    }

    [Fact]
    public void ParentObserverCanChooseAnExplicitChildValueBeforeItsInheritedNotification()
    {
        using var root = new VisibleRoot();
        var child = root.Controls.Add(new Panel());
        var leaf = child.Controls.Add(new Control());
        int childEvents = 0, leafEvents = 0;
        child.RightToLeftChanged += (_, _) => childEvents++;
        leaf.RightToLeftChanged += (_, _) => leafEvents++;
        root.RightToLeftChanged += (_, _) => child.RightToLeft = RightToLeft.Yes;
        root.RightToLeft = RightToLeft.Yes;
        Assert.Equal(RightToLeft.Yes, leaf.RightToLeft);
        Assert.Equal(1, childEvents);
        Assert.Equal(1, leafEvents);
    }

    [Fact]
    public void SameDirectionReparentDuringNotificationCompletesPendingDescendantsOnce()
    {
        using var first = new VisibleRoot();
        using var second = new VisibleRoot { RightToLeft = RightToLeft.Yes };
        var child = first.Controls.Add(new Panel());
        var leaf = child.Controls.Add(new Control());
        int changes = 0;
        leaf.RightToLeftChanged += (_, _) => changes++;
        child.RightToLeftChanged += (_, _) => child.Parent = second;
        first.RightToLeft = RightToLeft.Yes;
        Assert.Same(second, child.Parent);
        Assert.Equal(RightToLeft.Yes, leaf.RightToLeft);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void PropertyDescriptorResetRestoresInheritanceWithoutPersistingAResolvedDefault()
    {
        using var root = new VisibleRoot { RightToLeft = RightToLeft.Yes };
        var child = root.Controls.Add(new Control());
        var property = TypeDescriptor.GetProperties(child)[nameof(Control.RightToLeft)]!;
        Assert.False(property.ShouldSerializeValue(child));
        property.SetValue(child, RightToLeft.No);
        Assert.True(property.ShouldSerializeValue(child));
        property.ResetValue(child);
        Assert.Equal(RightToLeft.Yes, child.RightToLeft);
        Assert.False(property.ShouldSerializeValue(child));
    }

    [Fact]
    public void InheritanceHonorsADerivedParentsDirectionContract()
    {
        using var root = new VisibleRoot();
        var boundary = root.Controls.Add(new DirectionBoundary());
        var child = boundary.Controls.Add(new Control());
        Assert.Equal(RightToLeft.Yes, child.RightToLeft);
        child.RightToLeft = RightToLeft.No;
        Assert.Equal(RightToLeft.No, child.RightToLeft);
    }

    [Fact]
    public void LayoutCallbackCanSupersedeAnOlderDirectionNotification()
    {
        using var root = new VisibleRoot();
        var child = root.Controls.Add(new Control());
        int notifications = 0;
        root.RightToLeftChanged += (_, _) => notifications++;
        root.Layout += (_, _) => { if (root.RightToLeft == RightToLeft.Yes) root.RightToLeft = RightToLeft.No; };
        root.RightToLeft = RightToLeft.Yes;
        Assert.Equal(RightToLeft.No, root.RightToLeft);
        Assert.Equal(RightToLeft.No, child.RightToLeft);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeadingPaddingAndBorderRemainInsideTheCanonicalScrollRange(bool rtl)
    {
        using var root = new VisibleRoot { Size = new Size(800, 600) };
        var flow = root.Controls.Add(new FlowLayoutPanel { Size = new Size(170, 110), AutoScroll = true,
            WrapContents = false, Padding = new Padding(23, 19, 1, 2),
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No });
        flow.Style.Border.Width = 4;
        for (int i = 0; i < 5; i++) flow.Controls.Add(new Control { Size = new Size(70, 35), Margin = new Padding(3) });
        flow.PerformLayout();
        foreach (Control child in flow.Controls)
        {
            Assert.True(child.AccessibilityObject.PerformAction(Accessibility.AccessibleActions.ScrollIntoView));
            Assert.True(flow.AccessibilityObject.ScrollInfo!.Value.ViewportBounds.Contains(child.AccessibilityObject.Bounds));
        }
    }

    [Fact]
    public void LocalizedSemanticNamesHitTestingAndAuthoredTraversalKeepTheirIdentity()
    {
        using var root = new VisibleRoot { Size = new Size(800, 600) };
        var flow = root.Controls.Add(new FlowLayoutPanel { Size = new Size(400, 100) });
        var first = flow.Controls.Add(new Button { Size = new Size(90, 35), Text = "مرحبا", TabIndex = 0 });
        var second = flow.Controls.Add(new Button { Size = new Size(90, 35), Text = "שלום", TabIndex = 1 });
        var firstPeer = first.AccessibilityObject;
        long identity = firstPeer.RuntimeId;
        flow.RightToLeft = RightToLeft.Yes;
        Assert.True(first.Left > second.Left);
        Assert.Same(second, flow.GetNextControl(first));
        Assert.Same(first, flow.GetNextControl(second, forward: false));
        Assert.Equal("مرحبا", firstPeer.Name);
        Assert.Equal("שלום", second.AccessibilityObject.Name);
        var bounds = firstPeer.Bounds;
        Assert.Same(firstPeer, flow.AccessibilityObject.HitTest(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
        Assert.Same(second.AccessibilityObject, firstPeer.Navigate(Accessibility.AccessibleNavigation.Next));
        flow.RightToLeft = RightToLeft.No;
        Assert.True(first.Left < second.Left);
        Assert.Equal(identity, firstPeer.RuntimeId);
        Assert.Same(second, flow.GetNextControl(first));
    }

    [Fact]
    public void ReentrantLatestDirectionAndThrowingObserversLeaveOtherDescendantsReconciled()
    {
        using var root = new VisibleRoot();
        var first = root.Controls.Add(new Panel());
        var second = root.Controls.Add(new Panel());
        int firstEvents = 0, secondEvents = 0;
        first.RightToLeftChanged += (_, _) => firstEvents++;
        second.RightToLeftChanged += (_, _) => secondEvents++;
        bool redirected = false;
        root.RightToLeftChanged += (_, _) =>
        {
            if (!redirected) { redirected = true; root.RightToLeft = RightToLeft.No; }
        };
        root.RightToLeft = RightToLeft.Yes;
        Assert.Equal(RightToLeft.No, first.RightToLeft);
        Assert.Equal(RightToLeft.No, second.RightToLeft);
        Assert.Equal(0, firstEvents);
        Assert.Equal(0, secondEvents);
        first.RightToLeftChanged += (_, _) => throw new InvalidOperationException("Representative observer failure.");
        Assert.Throws<InvalidOperationException>(() => root.RightToLeft = RightToLeft.Yes);
        Assert.Equal(RightToLeft.Yes, first.RightToLeft);
        Assert.Equal(RightToLeft.Yes, second.RightToLeft);
        Assert.Equal(1, firstEvents);
        Assert.Equal(1, secondEvents);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, false)]
    [InlineData(FlowDirection.LeftToRight, true)]
    [InlineData(FlowDirection.RightToLeft, false)]
    [InlineData(FlowDirection.RightToLeft, true)]
    public void HorizontalDirectionComposesWithAmbientRtlAndAsymmetricMargins(FlowDirection direction, bool rtl)
    {
        using var root = new VisibleRoot();
        var flow = root.Controls.Add(new FlowLayoutPanel {
            Size = new Size(260, 160), Padding = new Padding(7, 11, 13, 17),
            FlowDirection = direction, RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
        });
        var child = flow.Controls.Add(new Control { Size = new Size(35, 20), Margin = new Padding(3, 5, 9, 7) });
        flow.PerformLayout();
        Rectangle display = flow.DisplayRectangle;
        int expected = direction == FlowDirection.LeftToRight
            ? rtl ? display.Right - child.Width - child.Margin.Left : display.Left + child.Margin.Left
            : rtl ? display.Left + child.Margin.Right : display.Right - child.Width - child.Margin.Right;
        Assert.Equal(expected, child.Left);
        Assert.Equal(display.Top + child.Margin.Top, child.Top);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight)]
    [InlineData(FlowDirection.RightToLeft)]
    [InlineData(FlowDirection.TopDown)]
    [InlineData(FlowDirection.BottomUp)]
    public void AmbientRtlMirrorsHorizontalPlacementAndPreservesWrapAndVerticalOrder(FlowDirection direction)
    {
        using var root = new VisibleRoot();
        var flow = root.Controls.Add(new FlowLayoutPanel {
            Size = new Size(130, 110), Padding = new Padding(7, 11, 13, 17), FlowDirection = direction
        });
        for (int i = 0; i < 5; i++) flow.Controls.Add(new Control { Size = new Size(45, 25), Margin = new Padding(3) });
        flow.PerformLayout();
        var before = flow.Controls.Select(child => child.Bounds).ToArray();
        Rectangle display = flow.DisplayRectangle;
        flow.RightToLeft = RightToLeft.Yes;
        for (int i = 0; i < before.Length; i++)
        {
            Assert.Equal(display.Left + display.Right - before[i].Right, flow.Controls[i].Left);
            Assert.Equal(before[i].Y, flow.Controls[i].Top);
            Assert.Equal(before[i].Size, flow.Controls[i].Size);
        }
    }

    [Fact]
    public void TableMirrorsLogicalColumnsSpansMarginsAndAnchorsWithoutChangingCellIdentity()
    {
        using var root = new VisibleRoot();
        var table = root.Controls.Add(new TableLayoutPanel {
            Size = new Size(330, 170), Padding = new Padding(7, 11, 13, 17), ColumnCount = 3, RowCount = 2
        });
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var first = new Control { Size = new Size(31, 23), Margin = new Padding(3, 5, 9, 7), Anchor = AnchorStyles.Top | AnchorStyles.Left };
        var last = new Control { Size = new Size(29, 21), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        var span = new Control { Dock = DockStyle.Fill, Margin = new Padding(5, 3, 11, 7) };
        table.Controls.Add(first, 0, 0);
        table.Controls.Add(last, 2, 0);
        table.Controls.Add(span, 0, 1);
        table.SetColumnSpan(span, 2);
        table.PerformLayout();
        var before = new[] { first.Bounds, last.Bounds, span.Bounds };
        Rectangle display = table.DisplayRectangle;
        table.RightToLeft = RightToLeft.Yes;
        var controls = new[] { first, last, span };
        for (int i = 0; i < controls.Length; i++)
        {
            Assert.Equal(display.Left + display.Right - before[i].Right, controls[i].Left);
            Assert.Equal(before[i].Y, controls[i].Top);
            Assert.Equal(before[i].Size, controls[i].Size);
        }
        Assert.Same(first, table.GetControlFromPosition(0, 0));
        Assert.Same(span, table.GetControlFromPosition(1, 1));
        Assert.True(first.Left > last.Left);
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, false)]
    [InlineData(FlowDirection.LeftToRight, true)]
    [InlineData(FlowDirection.RightToLeft, false)]
    [InlineData(FlowDirection.RightToLeft, true)]
    [InlineData(FlowDirection.TopDown, false)]
    [InlineData(FlowDirection.TopDown, true)]
    [InlineData(FlowDirection.BottomUp, false)]
    [InlineData(FlowDirection.BottomUp, true)]
    public void EveryOverflowingFlowChildCanBeRevealedAndKeepsItsScrollPosition(FlowDirection direction, bool rtl)
    {
        using var root = new VisibleRoot { Size = new Size(800, 600) };
        var flow = root.Controls.Add(new FlowLayoutPanel {
            Size = new Size(170, 110), Padding = new Padding(7, 11, 13, 17),
            AutoScroll = true, WrapContents = false, FlowDirection = direction,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
        });
        for (int i = 0; i < 5; i++) flow.Controls.Add(new Control { Size = new Size(70, 35), Margin = new Padding(3) });
        flow.PerformLayout();
        foreach (Control child in flow.Controls)
        {
            Assert.True(child.AccessibilityObject.PerformAction(Accessibility.AccessibleActions.ScrollIntoView));
            var viewport = flow.AccessibilityObject.ScrollInfo!.Value.ViewportBounds;
            Assert.True(viewport.Contains(child.AccessibilityObject.Bounds),
                $"{direction}/{rtl}: target {child.Bounds} is outside {viewport}; offset={flow.TouchScrollPosition}.");
            Point offset = flow.TouchScrollPosition;
            flow.PerformLayout();
            Assert.Equal(offset, flow.TouchScrollPosition);
            Assert.True(flow.AccessibilityObject.ScrollInfo!.Value.ViewportBounds.Contains(child.AccessibilityObject.Bounds));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryOverflowingTableCellCanBeRevealed(bool rtl)
    {
        using var root = new VisibleRoot { Size = new Size(800, 600) };
        var table = root.Controls.Add(new TableLayoutPanel {
            Size = new Size(170, 110), AutoScroll = true, ColumnCount = 3, RowCount = 1,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No
        });
        for (int i = 0; i < 3; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.Controls.Add(new Control { Size = new Size(70, 35) }, i, 0);
        }
        table.PerformLayout();
        foreach (Control child in table.Controls)
        {
            Assert.True(child.AccessibilityObject.PerformAction(Accessibility.AccessibleActions.ScrollIntoView));
            Assert.True(table.AccessibilityObject.ScrollInfo!.Value.ViewportBounds.Contains(child.AccessibilityObject.Bounds),
                $"RTL={rtl}: unreachable logical cell {table.GetColumn(child)} at {child.Bounds}; offset={table.TouchScrollPosition}.");
        }
    }

    [Fact]
    public void ExistingReverseFlowCanRevealItsLastChild()
    {
        // This scenario uses only pre-#89 APIs so the same driver can compare the exact baseline.
        using var root = new VisibleRoot { Size = new Size(800, 600) };
        var flow = root.Controls.Add(new FlowLayoutPanel {
            Size = new Size(170, 110), AutoScroll = true, WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft
        });
        for (int i = 0; i < 5; i++) flow.Controls.Add(new Control { Size = new Size(70, 35), Margin = new Padding(3) });
        flow.PerformLayout();
        var last = flow.Controls[^1];
        Assert.True(last.AccessibilityObject.PerformAction(Accessibility.AccessibleActions.ScrollIntoView));
        Assert.True(flow.AccessibilityObject.ScrollInfo!.Value.ViewportBounds.Contains(last.AccessibilityObject.Bounds),
            $"Last child remains at {last.Bounds}; offset={flow.TouchScrollPosition}.");
    }

    private sealed class VisibleRoot : Control
    {
        public override bool Visible { get => true; set { } }
    }

    private sealed class DirectionBoundary : Panel
    {
        public override RightToLeft RightToLeft { get => RightToLeft.Yes; set => base.RightToLeft = value; }
    }
}
