using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class CompositeViewportAccessibilityTests
{
    [Theory]
    [InlineData(1d)] [InlineData(1.5d)] [InlineData(2d)]
    public void GridSingleAxisSubtractsNoPhantomVerticalBar(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(300, 200, scale));
        var grid = new DataGridView { ColumnHeadersVisible = false, RowHeadersVisible = false };
        grid.Style.Border.Width = 0;
        grid.Columns.Add("Column", 110);
        grid.Rows.Add("synthetic");
        host.Show(grid, new TestViewport(100, 100, scale)); host.LayoutUntilStable();
        var info = grid.AccessibilityObject.ScrollInfo!.Value;
        Assert.Equal(100, info.Horizontal.ViewportLength);
        Assert.Equal(10, info.Horizontal.Maximum);
        Assert.False(info.Vertical.IsScrollable);
        Assert.True(grid.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, null)));
        Assert.Equal(-10 * scale, grid.GetCellBounds(0, 0).Left);
    }

    [Fact]
    public void GridLastPageUsesActualTrailingHeightsAndCurrentFirstRow()
    {
        using var host = ModernFormsTestHost.Create();
        var grid = MakeGrid(20, 20, 60, 60);
        host.Show(grid, 100, 100); host.LayoutUntilStable();
        Assert.Equal(3, grid.DisplayedRowCount);
        var peer = grid.AccessibilityObject;
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 100)));
        Assert.Equal(3, grid.FirstDisplayedScrollingRowIndex);
        Assert.Equal(1, grid.DisplayedRowCount);
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.Offset);
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.Percent);
    }

    [Fact]
    public void GridReachesLaterRowsWhenNoWholeRowFits()
    {
        using var host = ModernFormsTestHost.Create();
        var grid = MakeGrid(150, 150, 150);
        host.Show(grid, 100, 100); host.LayoutUntilStable();
        Assert.Equal(0, grid.DisplayedRowCount);
        Assert.True(grid.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 100)));
        Assert.Equal(2, grid.FirstDisplayedScrollingRowIndex);
        Assert.False(grid.GetCellBounds(2, 0).IsEmpty);
        Assert.Equal(0, grid.DisplayedRowCount);
    }

    [Fact]
    public void GridDoesNotRetargetVerticalRequestAfterHorizontalCallbackClearsRows()
    {
        using var host = ModernFormsTestHost.Create();
        var grid = MakeGrid(50, 50, 50, 50);
        grid.Columns[0].Width = 300;
        host.Show(grid, 100, 100); host.LayoutUntilStable();
        var peer = grid.AccessibilityObject;
        var bar = Enumerable.Range(0, peer.GetChildCount()).Select(peer.GetChild)
            .OfType<Control.ControlAccessibleObject>().Select(child => child.Owner).OfType<HorizontalScrollBar>().Single();
        bar.ValueChanged += (_, _) => grid.Rows.Clear();
        Assert.False(grid.AccessibilityObject.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(100, 100)));
        Assert.Empty(grid.Rows);
        Assert.Equal(0, grid.FirstDisplayedScrollingRowIndex);
    }

    [Theory]
    [InlineData(1d)] [InlineData(1.5d)] [InlineData(2d)]
    public void ListUsesActualDeviceViewportAndResetsWhenContentFits(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(300, 200, scale));
        var list = new ListBox { ItemHeight = 20 };
        list.Style.Border.Width = 0;
        for (int i = 0; i < 10; i++) list.Items.Add("item " + i);
        host.Show(list, new TestViewport(100, 100, scale)); host.LayoutUntilStable();
        var peer = list.AccessibilityObject;
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.ViewportLength);
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.Maximum);
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 100)));
        Assert.Equal(5, list.FirstVisibleIndex);
        list.Items.Clear();
        Assert.False(peer.ScrollInfo!.Value.Vertical.IsScrollable);
        Assert.Equal(0, peer.ScrollInfo!.Value.Vertical.Offset);
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void TreeScrollPreservesSelectionAndStopsAtCanonicalItemEndpoint()
    {
        using var host = ModernFormsTestHost.Create();
        var tree = new TreeView();
        for (int i = 0; i < 30; i++) tree.Items.Add("node " + i);
        host.Show(tree, 150, 100); host.LayoutUntilStable();
        var selected = tree.SelectedItem;
        var peer = tree.AccessibilityObject;
        Assert.True(peer.ScrollInfo!.Value.Vertical.IsScrollable);
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 100)));
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.Percent);
        Assert.Same(selected, tree.SelectedItem);
        tree.Items.Clear(); host.LayoutUntilStable();
        Assert.False(peer.ScrollInfo!.Value.Vertical.IsScrollable);
    }

    [Theory]
    [InlineData(1d)] [InlineData(1.5d)] [InlineData(2d)]
    public void EditorScrollUsesShapedDocumentAndScaledVisibleBars(double scale)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 300, scale));
        var text = new TextBox { MultiLine = true, ScrollBars = ScrollBars.Vertical, Padding = Padding.Empty,
            Text = string.Join('\n', Enumerable.Range(0, 50).Select(i => "synthetic " + i)) };
        text.Style.Border.Width = 0;
        host.Show(text, new TestViewport(200, 100, scale)); host.LayoutUntilStable();
        var peer = text.AccessibilityObject;
        var info = peer.ScrollInfo!.Value;
        Assert.Equal(text.PaddedClientRectangle.Width / scale, info.Horizontal.ViewportLength);
        Assert.Equal(text.ClientRectangle.Width - text.VerticalScrollBar.ScaledWidth, text.PaddedClientRectangle.Width);
        Assert.True(info.Vertical.IsScrollable);
        int selection = text.SelectionStart;
        Assert.True(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 100)));
        Assert.Equal(100, peer.ScrollInfo!.Value.Vertical.Percent);
        Assert.Equal(selection, text.SelectionStart);
        text.PasswordCharacter = '*';
        Assert.Null(peer.ScrollInfo);
        Assert.False(peer.PerformAction(AccessibleActions.Scroll, AccessibleScrollRequest.ToPercent(null, 0)));
    }

    private static DataGridView MakeGrid(params int[] heights)
    {
        var grid = new DataGridView { ColumnHeadersVisible = false, RowHeadersVisible = false };
        grid.Style.Border.Width = 0;
        grid.Columns.Add("Column", 40);
        foreach (int height in heights)
        {
            var row = grid.Rows.Add("synthetic");
            row.Height = height;
        }
        return grid;
    }
}
