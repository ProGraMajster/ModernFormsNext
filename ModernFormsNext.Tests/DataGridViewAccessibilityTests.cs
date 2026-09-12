using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class DataGridViewAccessibilityTests
{
    [Fact]
    public void GridUsesCanonicalRowsCellsAndHeadersWithoutCreatingControls()
    {
        using var grid = Create();
        var root = grid.AccessibilityObject;
        var provider = root.GridProvider!;
        int controls = grid.Controls.GetAllControls(true).Count();
        Assert.Equal(AccessibleControlType.DataGrid, root.ControlType);
        Assert.Equal(2, provider.RowCount);
        Assert.Equal(2, provider.ColumnCount);
        var cell = provider.GetItem(1, 1)!;
        Assert.Same(cell, root.GetChild(2)!.GetChild(2));
        Assert.Equal("Value", cell.Name);
        Assert.Equal("last", cell.Value);
        var info = cell.GridCell!;
        Assert.Same(root, info.Grid);
        Assert.Equal(1, info.Row);
        Assert.Equal(1, info.Column);
        Assert.Same(provider.GetColumnHeaders()[1], Assert.Single(info.ColumnHeaders));
        Assert.Same(provider.GetRowHeaders()[1], Assert.Single(info.RowHeaders));
        Assert.Equal(controls, grid.Controls.GetAllControls(true).Count());
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void SortingRetainsPeerIdentityAndUpdatesCoordinates()
    {
        using var grid = Create();
        var provider = grid.AccessibilityObject.GridProvider!;
        var cell = provider.GetItem(0, 0)!;
        long id = cell.RuntimeId;
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.Same(cell, provider.GetItem(1, 0));
        Assert.Equal(id, cell.RuntimeId);
        Assert.Equal(1, cell.GridCell!.Row);
    }

    [Fact]
    public void SelectionPublishesBothCoordinatesInOneCallbackAndSurvivesSort()
    {
        using var grid = Create();
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        var peer = grid.AccessibilityObject.GridProvider!.GetItem(1, 1)!;
        var observed = new List<Point>();
        grid.SelectionChanged += (_, _) => observed.Add(grid.CurrentCellAddress);
        Assert.True(peer.PerformAction(AccessibleActions.Select));
        Assert.Equal(new[] { new Point(1, 1) }, observed);
        Assert.Same(peer, grid.AccessibilityObject.GetSelected());
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.Equal(new Point(1, 0), grid.CurrentCellAddress);
        Assert.Same(peer, grid.AccessibilityObject.GetSelected());
    }

    [Fact]
    public void RemovedPeerCannotActAfterReinsertionOrOwnerTransfer()
    {
        using var grid = Create();
        using var other = Create();
        var row = grid.Rows[0];
        var old = grid.AccessibilityObject.GridProvider!.GetItem(0, 0)!;
        grid.Rows.Remove(row);
        Assert.Null(old.Parent);
        Assert.Null(old.GridCell);
        Assert.False(old.PerformAction(AccessibleActions.SetValue, "unsafe"));
        grid.Rows.Add(row);
        var current = grid.AccessibilityObject.GridProvider!.GetItem(1, 0)!;
        Assert.NotEqual(old.RuntimeId, current.RuntimeId);
        Assert.False(old.PerformAction(AccessibleActions.Select));
        grid.Rows.Remove(row);
        other.Rows.Add(row);
        Assert.False(current.PerformAction(AccessibleActions.SetValue, "unsafe"));
        Assert.Equal("a", row.Cells[0].Value);
    }

    [Fact]
    public void HiddenColumnIsOmittedAndReadOnlyCellStillExposesValue()
    {
        using var grid = Create();
        var provider = grid.AccessibilityObject.GridProvider!;
        var first = provider.GetItem(0, 0)!;
        grid.Columns[0].Visible = false;
        Assert.Equal(1, provider.ColumnCount);
        Assert.Equal("Value", provider.GetItem(0, 0)!.Name);
        Assert.Null(first.GridCell);
        grid.ReadOnly = true;
        var cell = provider.GetItem(0, 0)!;
        Assert.Equal("first", cell.Value);
        Assert.True(cell.State.HasFlag(AccessibleStates.ReadOnly));
        Assert.False(cell.PerformAction(AccessibleActions.SetValue, "changed"));
        Assert.True(cell.PerformAction(AccessibleActions.Select));
    }

    [Fact]
    public void SelectionInHiddenColumnDoesNotExposeARetiredPeer()
    {
        using var grid = Create();
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        var root = grid.AccessibilityObject;
        var cell = root.GridProvider!.GetItem(0, 0)!;
        Assert.True(cell.PerformAction(AccessibleActions.Select));
        grid.Columns[0].Visible = false;
        Assert.Null(root.GetSelected());
        Assert.NotSame(cell, root.GetFocused());
        Assert.Equal(new Point(0, 0), grid.CurrentCellAddress);
    }

    [Fact]
    public void CellValueUsesNormalBeginCommitEventsExactlyOnce()
    {
        using var grid = Create();
        var events = new List<string>();
        grid.CellBeginEdit += (_, _) => events.Add("begin");
        grid.CellValueChanged += (_, _) => events.Add("value");
        grid.CellEndEdit += (_, _) => events.Add("end");
        Assert.True(grid.AccessibilityObject.GridProvider!.GetItem(0, 1)!.PerformAction(AccessibleActions.SetValue, "replacement"));
        Assert.Equal(new[] { "begin", "value", "end" }, events);
        Assert.Equal("replacement", grid.Rows[0].Cells[1].Value);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void BoundsAndHitTestingWorkBeforePainting()
    {
        using var grid = Create();
        var cell = grid.AccessibilityObject.GridProvider!.GetItem(0, 0)!;
        Assert.False(cell.Bounds.IsEmpty);
        var bounds = cell.Bounds;
        Assert.Same(cell, grid.AccessibilityObject.HitTest(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
    }

    [Fact]
    public void CollectionsRejectDuplicateAndCrossOwnerItemsWithoutChangingOwners()
    {
        using var grid = Create();
        using var other = Create();
        var row = grid.Rows[0];
        var column = grid.Columns[0];
        var cell = row.Cells[0];
        Assert.Throws<ArgumentException>(() => grid.Rows.Add(row));
        Assert.Throws<ArgumentException>(() => other.Rows.Add(row));
        Assert.Throws<ArgumentException>(() => other.Columns.Add(column));
        Assert.Throws<ArgumentException>(() => grid.Rows[1].Cells.Add(cell));
        Assert.Same(grid, row.DataGridView);
        Assert.Same(grid, column.DataGridView);
        Assert.Same(row, cell.OwningRow);
    }

    private static VisibleGrid Create()
    {
        var grid = new VisibleGrid { Size = new Size(400, 200), RowHeadersVisible = true, AccessibleName = "Records" };
        grid.Columns.Add("Name", 140);
        grid.Columns.Add("Value", 140);
        grid.Rows.Add("a", "first");
        grid.Rows.Add("z", "last");
        return grid;
    }

    private sealed class VisibleGrid : DataGridView
    {
        public override bool Visible { get => true; set => base.Visible = value; }
    }
}
