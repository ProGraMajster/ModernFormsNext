using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ViewportHardeningTests
{
    [Theory]
    [InlineData(1d, false)] [InlineData(1.5d, false)] [InlineData(2d, false)]
    [InlineData(1d, true)] [InlineData(1.5d, true)] [InlineData(2d, true)]
    public void PaddingAndBorderRemainLogicalDuringLayoutAndRangeCalculation(double scale, bool styledPadding)
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new ScrollableControl { AutoScroll = true };
        if (styledPadding) panel.Style.Padding = new Padding(5, 0, 7, 0);
        else panel.Padding = new Padding(5, 0, 7, 0);
        panel.Style.Border.Width = 2;
        var child = panel.Controls.Add(new Control { Bounds = new(7, 2, 110, 20) });
        host.Show(panel, new TestViewport(100, 100, scale)); host.LayoutUntilStable();
        Assert.Equal(7, panel.DisplayRectangle.X);
        Assert.Equal(84, panel.DisplayRectangle.Width);
        var info = panel.AccessibilityObject.ScrollInfo!.Value;
        Assert.Equal(96, info.Horizontal.ViewportLength);
        Assert.Equal(26, info.Horizontal.Maximum);
        Assert.True(panel.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, null)));
        Assert.Equal(-19, child.Left);
    }

    [Fact]
    public void MinimumChangeCorrectsRelativeChildPositionWithoutFalseValueChanged()
    {
        using var host = ModernFormsTestHost.Create();
        var panel = new ScrollableControl { AutoScroll = true };
        var child = panel.Controls.Add(new Control { Bounds = new(0, 0, 200, 20) });
        host.Show(panel, 100, 100); host.LayoutUntilStable();
        panel.HorizontalScrollProperties.Value = 20;
        Assert.Equal(-20, child.Left);
        int notifications = 0;
        panel.AccessibilityObject.ClientNotification += (_, e) => { if (e.EventId == AccessibleEvents.ScrollChanged) notifications++; };
        panel.HorizontalScrollProperties.Minimum = 10;
        Assert.Equal(20, panel.HorizontalScrollProperties.Value);
        Assert.Equal(-10, child.Left);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ListReconcilesReentrantCollectionChangeDuringRangeClamp()
    {
        using var host = ModernFormsTestHost.Create();
        var list = new ListBox { ItemHeight = 20 };
        for (int i = 0; i < 20; i++) list.Items.Add("item " + i);
        host.Show(list, 100, 100); host.LayoutUntilStable();
        list.FirstVisibleIndex = 15;
        bool changed = false;
        list.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId != AccessibleEvents.ScrollChanged || changed) return;
            changed = true;
            list.Items.Clear();
        };
        while (list.Items.Count > 5) list.Items.RemoveAt(list.Items.Count - 1);
        Assert.True(changed);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.AccessibilityObject.ScrollInfo!.Value.Vertical.Offset);
        Assert.False(list.AccessibilityObject.ScrollInfo!.Value.Vertical.IsScrollable);
    }

    [Fact]
    public void OrdinaryTreeItemRevealUsesItsActualOwnerAndPreservesSelection()
    {
        using var host = ModernFormsTestHost.Create();
        var tree = new TreeView();
        for (int i = 0; i < 30; i++) tree.Items.Add("node " + i);
        host.Show(tree, 150, 100); host.LayoutUntilStable();
        var selected = tree.SelectedItem;
        var last = tree.AccessibilityObject.GetChild(29)!;
        Assert.True(last.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.Equal(100, tree.AccessibilityObject.ScrollInfo!.Value.Vertical.Percent);
        Assert.Same(selected, tree.SelectedItem);
    }
}
