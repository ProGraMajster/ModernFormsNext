using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DataGridView
{
    private DataGridViewRow? selected_row;
    private DataGridViewColumn? selected_column;
    private DataGridViewCell? selected_cell;

    // Commit both coordinates and identities before raising SelectionChanged. Pointer and semantic
    // callers use this one transition; handlers never observe an old column in a newly selected row.
    private void SetCurrentCoordinates(int rowIndex, int columnIndex)
    {
        var row = rowIndex >= 0 && rowIndex < Rows.Count ? Rows[rowIndex] : null;
        var column = columnIndex >= 0 && columnIndex < Columns.Count ? Columns[columnIndex] : null;
        var cell = row is not null && column is not null && columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
        bool changed = selected_row_index != rowIndex || selected_column_index != columnIndex
            || !ReferenceEquals(selected_row, row) || !ReferenceEquals(selected_column, column)
            || !ReferenceEquals(selected_cell, cell);
        if (!changed) return;
        if (selected_row is not null) selected_row.Selected = false;
        if (selected_cell is not null) selected_cell.Selected = false;
        selected_row_index = rowIndex;
        selected_column_index = columnIndex;
        selected_row = row;
        selected_column = column;
        selected_cell = cell;
        if (row is not null) row.Selected = true;
        if (cell is not null) cell.Selected = true;
        OnSelectionChanged(EventArgs.Empty);
        if (!IsDisposed && !Disposing) Invalidate();
    }

    private void ReconcileCurrentCoordinates()
    {
        int row = selected_row?.DataGridView == this ? selected_row.Index : -1;
        int column = selected_column?.DataGridView == this ? selected_column.Index : -1;
        SetCurrentCoordinates(row, column);
    }

    internal void OnCellsChanged()
    {
        OnGridStructureChanged(updateScrollBars: false);
    }

    private void OnGridStructureChanged(bool updateScrollBars)
    {
        // Collection ownership is already committed. Every derived state must now reconcile,
        // even if editor focus cleanup or a public selection observer throws. Each step reads
        // the current collection again so a callback's replacement remains authoritative.
        List<Exception>? failures = null;
        void Complete(Action action)
        {
            try { action(); }
            catch (Exception error) { (failures ??= []).Add(error); }
        }
        Complete(UpdateEditTextBoxPosition);
        Complete(ReconcileCurrentCoordinates);
        if (updateScrollBars) Complete(UpdateScrollBars);
        Complete(Invalidate);
        Complete(() => NotifyAccessibilityClients(AccessibleEvents.Reorder));
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Grid structure reconciliation failed.", failures);
    }
}
