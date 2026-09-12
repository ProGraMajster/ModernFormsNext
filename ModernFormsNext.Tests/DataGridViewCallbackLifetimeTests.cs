using System.Drawing;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class DataGridViewCallbackLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevealingRetiredCellCannotContinueHorizontalScrollAfterVerticalCallback(bool replaceColumn)
    {
        using var grid = new VisibleGrid { Size = new(180, 120) };
        grid.Columns.Add("First", 200);
        var column = grid.Columns.Add("Target", 200);
        for (int i = 0; i < 20; ++i) grid.Rows.Add("first", "target");
        var row = grid.Rows[19];
        var root = grid.AccessibilityObject;
        var cell = root.GridProvider!.GetItem(19, 1)!;
        bool mutate = true;
        root.ClientNotification += (_, e) =>
        {
            if (!mutate || e.EventId != AccessibleEvents.ScrollChanged) return;
            mutate = false;
            if (replaceColumn) { grid.Columns.Remove(column); grid.Columns.Add(column); }
            else { grid.Rows.Remove(row); grid.Rows.Add(row); }
        };
        Assert.True(root.ScrollInfo!.Value.Vertical.IsScrollable);
        Assert.False(cell.PerformAction(AccessibleActions.ScrollIntoView));
        Assert.False(mutate);
        Assert.Null(cell.GridCell);
        Assert.Equal(0, root.ScrollInfo!.Value.Horizontal.Offset);
    }

    [Fact]
    public void EditorDisposalObserverCannotCreateReplacementDuringGridDisposal()
    {
        using var grid = Create();
        grid.BeginEdit(0, 0);
        int added = 0;
        grid.ControlAdded += (_, e) => { if (e.Value is TextBox) added++; };
        Editor(grid).Disposed += (_, _) => grid.BeginEdit(1, 0);
        grid.Dispose();
        Assert.Equal(0, added);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void SparseCellCallbackCannotResumeEditingAfterRowRemovalAndReinsertion()
    {
        using var grid = new VisibleGrid { Size = new(360, 180) };
        grid.Columns.Add("Name", 180);
        var row = new DataGridViewRow();
        grid.Rows.Add(row);
        bool mutate = true;
        int editorsAdded = 0;
        grid.ControlAdded += (_, e) => { if (e.Value is TextBox) editorsAdded++; };
        grid.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId != AccessibleEvents.Reorder || !mutate || row.Cells.Count == 0) return;
            mutate = false;
            grid.Rows.Remove(row);
            grid.Rows.Add(row);
        };
        grid.BeginEdit(0, 0);
        Assert.Equal(0, editorsAdded);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void EndObserverCannotStartBeginNotificationForReinsertedTarget()
    {
        using var grid = Create();
        var target = grid.Rows[1];
        grid.BeginEdit(0, 0);
        int begins = 0;
        grid.CellBeginEdit += (_, _) => begins++;
        grid.CellEndEdit += (_, _) => { grid.Rows.Remove(target); grid.Rows.Add(target); };
        grid.BeginEdit(1, 0);
        Assert.Equal(0, begins);
        Assert.False(grid.IsCurrentCellInEditMode);
    }

    [Fact]
    public void SemanticSetValueCannotTakeOverReplacementEditorForTheSameCell()
    {
        using var grid = Create();
        var cell = grid.AccessibilityObject.GridProvider!.GetItem(0, 0)!;
        TextBox? replacement = null;
        int ended = 0;
        grid.CellEndEdit += (_, _) => ended++;
        EventHandler<DataGridViewCellEditEventArgs>? begin = null;
        begin = (_, _) =>
        {
            // The callback owns this new edit, even though its coordinates equal the outer
            // semantic request. Retire the observer before entering the canonical edit path.
            grid.CellBeginEdit -= begin;
            grid.BeginEdit(0, 0);
            replacement = Editor(grid);
            replacement.Text = "callback";
        };
        grid.CellBeginEdit += begin;

        Assert.False(cell.PerformAction(AccessibleActions.SetValue, "outer"));

        Assert.NotNull(replacement);
        Assert.Same(replacement, Editor(grid));
        Assert.Equal("callback", replacement.Text);
        Assert.Equal("a", grid.Rows[0].Cells[0].Value);
        Assert.True(grid.IsCurrentCellInEditMode);
        Assert.Equal(0, ended);
    }

    [Fact]
    public void ThrowingSelectionObserverCannotPreventDetachedEditorCleanup()
    {
        using var grid = Create();
        grid.SelectedRowIndex = 0;
        grid.SelectedColumnIndex = 0;
        grid.BeginEdit(0, 0);
        var editor = Editor(grid);
        bool reordered = false;
        grid.AccessibilityObject.ClientNotification += (_, e) => { if (e.EventId == AccessibleEvents.Reorder) reordered = true; };
        grid.SelectionChanged += (_, _) => throw new InvalidOperationException("selection");
        Assert.Throws<InvalidOperationException>(() => grid.Rows.RemoveAt(0));
        Assert.False(grid.IsCurrentCellInEditMode);
        Assert.True(editor.IsDisposed);
        Assert.True(reordered);
    }

    [Fact]
    public void NewlyPublishedBoundRowAlreadyCommitsToItsModel()
    {
        using var grid = new VisibleGrid { Size = new(360, 180) };
        var model = new Model { Name = "original" };
        bool edit = true;
        grid.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId != AccessibleEvents.Reorder || !edit || grid.Rows.Count != 1) return;
            edit = false;
            grid.BeginEdit(0, 0);
            Editor(grid).Text = "edited";
            Assert.True(grid.EndEdit());
        };
        grid.DataSource = new List<Model> { model };
        Assert.Equal("edited", model.Name);
        Assert.Equal("edited", grid.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void BindingGetterCannotAppendOldRowsAfterReplacingDataSource()
    {
        using var grid = new VisibleGrid { Size = new(360, 180) };
        var replacement = new List<Model> { new() { Name = "replacement" } };
        var original = new RedirectingModel();
        original.OnRead = () => { original.OnRead = null; grid.DataSource = replacement; };
        grid.DataSource = new List<RedirectingModel> { original };
        Assert.Same(replacement, grid.DataSource);
        Assert.Single(grid.Rows);
        Assert.Equal("replacement", grid.Rows[0].Cells[0].Value);
    }

    public sealed class Model { public string Name { get; set; } = string.Empty; }
    public sealed class RedirectingModel
    {
        internal Action? OnRead;
        public string Name { get { OnRead?.Invoke(); return "original"; } }
    }
    private static VisibleGrid Create()
    {
        var grid = new VisibleGrid { Size = new Size(360, 180) };
        grid.Columns.Add("Name", 200);
        grid.Rows.Add("a"); grid.Rows.Add("z");
        return grid;
    }
    private static TextBox Editor(DataGridView grid) => Assert.Single(grid.Controls.GetAllControls(true).OfType<TextBox>());
    private sealed class VisibleGrid : DataGridView { public override bool Visible { get => true; set => base.Visible = value; } }
}
