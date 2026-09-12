using System.Collections;
using System.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class DataGridViewEditLifetimeTests
{
    [Fact]
    public void SortingWhileEditingCommitsOriginalCellIdentity()
    {
        using var grid = CreateGrid();
        var original = grid.Rows[0].Cells[0];
        grid.BeginEdit(0, 0);
        Editor(grid).Text = "edited";
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.True(grid.EndEdit());
        Assert.Equal("edited", original.Value);
        Assert.Equal("z", grid.Rows[0].Cells[0].Value);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void RemovingEditedRowNeverCommitsReplacementRow()
    {
        using var grid = CreateGrid();
        grid.BeginEdit(0, 0);
        Editor(grid).Text = "edited";
        grid.Rows.RemoveAt(0);
        Assert.False(grid.EndEdit());
        Assert.Equal("z", grid.Rows[0].Cells[0].Value);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void BeginCallbackCannotRetargetRemovedRow()
    {
        using var grid = CreateGrid();
        grid.CellBeginEdit += (_, _) => grid.Rows.RemoveAt(0);
        grid.BeginEdit(0, 0);
        Assert.False(grid.IsCurrentCellInEditMode);
        Assert.Empty(grid.Controls.GetAllControls(true).OfType<TextBox>());
    }

    [Fact]
    public void ThrowingEndCallbackStillRetiresAndDisposesEditor()
    {
        using var grid = CreateGrid();
        grid.BeginEdit(0, 0);
        var editor = Editor(grid);
        editor.Text = "edited";
        grid.CellEndEdit += (_, _) => throw new InvalidOperationException("application callback");
        Assert.Throws<InvalidOperationException>(() => grid.EndEdit());
        Assert.False(grid.IsCurrentCellInEditMode);
        Assert.True(editor.IsDisposed);
        Assert.DoesNotContain(editor, grid.Controls.GetAllControls(true));
    }

    [Fact]
    public void DisposePreservesEditorAndGridDisposedFailuresAfterRetiringTheEdit()
    {
        using var grid = CreateGrid();
        grid.BeginEdit(0, 0);
        var editor = Editor(grid);
        var editorFailure = new InvalidOperationException("editor disposed observer");
        var gridFailure = new ArgumentException("grid disposed observer");
        EventHandler disposeEditor = (_, _) => throw editorFailure;
        EventHandler disposeGrid = (_, _) => throw gridFailure;
        editor.Disposed += disposeEditor;
        grid.Disposed += disposeGrid;
        try
        {
            var failures = Assert.Throws<AggregateException>(grid.Dispose).Flatten().InnerExceptions;
            Assert.Equal(2, failures.Count);
            Assert.Same(editorFailure, failures[0]);
            Assert.Same(gridFailure, failures[1]);
            Assert.False(grid.IsCurrentCellInEditMode);
            Assert.True(editor.IsDisposed);
            Assert.True(grid.IsDisposed);
            Assert.DoesNotContain(editor, grid.Controls.GetAllControls(true));
        }
        finally
        {
            editor.Disposed -= disposeEditor;
            grid.Disposed -= disposeGrid;
        }
    }

    [Fact]
    public void BoundSortCommitsTheOriginalSourceItem()
    {
        var first = new BoundRow { Name = "a" };
        var last = new BoundRow { Name = "z" };
        using var grid = new VisibleGrid { Size = new Size(360, 180), DataSource = new ArrayList { first, last } };
        grid.BeginEdit(0, 0);
        Editor(grid).Text = "edited";
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.True(grid.EndEdit());
        Assert.Equal("edited", first.Name);
        Assert.Equal("z", last.Name);
        Assert.Equal("edited", grid.Rows[1].Cells[0].Value);
        Assert.Equal("z", grid.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void EndCallbackMayStartNewEditorWithoutOldCleanupRemovingIt()
    {
        using var grid = CreateGrid();
        grid.BeginEdit(0, 0);
        var old = Editor(grid);
        old.Text = "edited";
        grid.CellEndEdit += (_, _) => grid.BeginEdit(1, 0);
        Assert.True(grid.EndEdit());
        Assert.True(old.IsDisposed);
        Assert.True(grid.IsCurrentCellInEditMode);
        Assert.Equal("z", Editor(grid).Text);
        Assert.NotSame(old, Editor(grid));
    }

    [Fact]
    public void BeginCallbackCancelWithoutAnEditorRevokesOuterRequest()
    {
        using var grid = CreateGrid();
        grid.CellBeginEdit += (_, _) => grid.CancelEdit();
        grid.BeginEdit(0, 0);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void RemovedAndReinsertedRowDoesNotReuseEditLifetime()
    {
        using var grid = CreateGrid();
        var row = grid.Rows[0];
        grid.CellBeginEdit += (_, _) => { grid.Rows.Remove(row); grid.Rows.Insert(0, row); };
        grid.BeginEdit(0, 0);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void MissingCellIsCreatedOnlyWhenEditingBegins()
    {
        using var grid = CreateGrid();
        grid.Columns.Add("Second", 100);
        Assert.Single(grid.Rows[0].Cells);
        Assert.Equal(string.Empty, grid.AccessibilityObject.GridProvider!.GetItem(0, 1)!.Value);
        Assert.Single(grid.Rows[0].Cells);
        grid.BeginEdit(0, 1);
        Assert.True(grid.IsCurrentCellInEditMode);
        Editor(grid).Text = "new";
        Assert.True(grid.EndEdit());
        Assert.Equal("new", grid.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void BindingConversionFailureLeavesCellAndSourceUnchanged()
    {
        var model = new NumericRow { Count = 7 };
        using var grid = new VisibleGrid { Size = new Size(360, 180), DataSource = new ArrayList { model } };
        int changes = 0, ends = 0;
        grid.CellValueChanged += (_, _) => changes++;
        grid.CellEndEdit += (_, _) => ends++;
        grid.BeginEdit(0, 0);
        Editor(grid).Text = "invalid";
        Assert.False(grid.EndEdit());
        Assert.Equal(7, model.Count);
        Assert.Equal("7", grid.Rows[0].Cells[0].Value);
        Assert.Equal(0, changes);
        Assert.Equal(1, ends);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void ReplacingCellCancelsTheOriginalEdit()
    {
        using var grid = CreateGrid();
        grid.BeginEdit(0, 0);
        Editor(grid).Text = "edited";
        grid.Rows[0].Cells[0] = new("replacement");
        Assert.False(grid.IsCurrentCellInEditMode);
        Assert.False(grid.EndEdit());
        Assert.Equal("replacement", grid.Rows[0].Cells[0].Value);
    }

    public sealed class NumericRow
    {
        public int Count { get; set; }
    }

    private static VisibleGrid CreateGrid()
    {
        var grid = new VisibleGrid { Size = new Size(360, 180) };
        grid.Columns.Add("Name", 200);
        grid.Rows.Add("a");
        grid.Rows.Add("z");
        return grid;
    }

    private static TextBox Editor(DataGridView grid)
        => Assert.Single(grid.Controls.GetAllControls(true).OfType<TextBox>());

    public sealed class BoundRow
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class VisibleGrid : DataGridView
    {
        public override bool Visible { get => true; set => base.Visible = value; }
    }
}
