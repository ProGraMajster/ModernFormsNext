using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DataGridView
{
    private bool retiringGrid;
    // An index is a coordinate, not an editing identity: sorting and callbacks may replace it.
    private sealed record GridEditSession(TextBox Editor, DataGridViewRow Row,
        DataGridViewColumn Column, DataGridViewCell Cell, AccessibilityControlLifetime Lifetime,
        long RowVersion, long ColumnVersion, long CellVersion);

    private bool CanEditCell(DataGridViewRow row, DataGridViewColumn column, DataGridViewCell? cell)
        => !retiringGrid && !IsDisposed && !Disposing && !read_only && Enabled && Visible
            && FindWindow()?.InputBindingsClosed != true
            && ReferenceEquals(row.DataGridView, this) && Rows.Contains(row)
            && ReferenceEquals(column.DataGridView, this) && Columns.Contains(column) && column.Visible
            && (cell is null ? row.Cells.Count <= column.Index
                : ReferenceEquals(cell.OwningRow, row) && cell.ColumnIndex == column.Index);

    private bool IsEditAttached(GridEditSession edit)
        => edit.Lifetime.IsCurrent && edit.RowVersion == edit.Row.OwnershipVersion
            && edit.ColumnVersion == edit.Column.OwnershipVersion && edit.CellVersion == edit.Cell.OwnershipVersion
            && CanEditCell(edit.Row, edit.Column, edit.Cell);

    private GridEditSession? BeginCellEdit(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Columns.Count)
            return null;
        var row = Rows[rowIndex];
        var column = Columns[columnIndex];
        var cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
        if (!CanEditCell(row, column, cell)) return null;
        var lifetime = new AccessibilityControlLifetime(this);
        var rowVersion = row.OwnershipVersion;
        var columnVersion = column.OwnershipVersion;
        var cellVersion = cell?.OwnershipVersion;

        EndEdit();
        // An EndEdit callback can begin a replacement session. The outer request must not own it.
        if (active_edit is not null || !lifetime.IsCurrent || rowVersion != row.OwnershipVersion
            || columnVersion != column.OwnershipVersion || cellVersion != cell?.OwnershipVersion
            || !CanEditCell(row, column, cell)) return null;
        long request = ++edit_version;
        var args = new DataGridViewCellEditEventArgs(row.Index, column.Index);
        OnCellBeginEdit(args);
        if (args.Cancel || request != edit_version || !lifetime.IsCurrent
            || rowVersion != row.OwnershipVersion || columnVersion != column.OwnershipVersion
            || cellVersion != cell?.OwnershipVersion || !CanEditCell(row, column, cell)) return null;

        // Missing cells are materialized only by accepted editing, never by semantic enumeration.
        while (row.Cells.Count <= column.Index)
        {
            int appendIndex = row.Cells.Count;
            var added = row.Cells.Add(string.Empty);
            if (row.Cells.Count > column.Index) cell = row.Cells[column.Index];
            if (request != edit_version || !lifetime.IsCurrent || rowVersion != row.OwnershipVersion
                || columnVersion != column.OwnershipVersion || appendIndex >= row.Cells.Count
                || !ReferenceEquals(row.Cells[appendIndex], added) || !CanEditCell(row, column, cell)) return null;
        }
        cell ??= row.Cells[column.Index];
        var bounds = GetCellBounds(row.Index, column.Index);
        if (bounds.IsEmpty) return null;
        var editor = new TextBox
        {
            Left = DeviceToLogicalUnits(bounds.Left) + 1,
            Top = DeviceToLogicalUnits(bounds.Top) + 1,
            Width = Math.Max(0, DeviceToLogicalUnits(bounds.Width) - 2),
            Height = Math.Max(0, DeviceToLogicalUnits(bounds.Height) - 2),
            Text = cell.Value
        };
        editor.Style.Border.Width = 0;
        var edit = new GridEditSession(editor, row, column, cell, lifetime,
            rowVersion, columnVersion, cell.OwnershipVersion);
        active_edit = edit;
        editor.KeyDown += EditTextBox_KeyDown;
        editor.LostFocus += EditTextBox_LostFocus;
        try
        {
            Controls.Add(editor);
            if (!ReferenceEquals(active_edit, edit) || !IsEditAttached(edit))
            {
                RetireEdit(edit);
                CleanupEdit(editor);
                return null;
            }
            editor.Select();
            if (ReferenceEquals(active_edit, edit) && IsEditAttached(edit)) editor.SelectAll();
            // Return only the session accepted by this invocation. Reentrant callbacks can
            // create another editor for exactly the same cell; matching coordinates or model
            // objects would incorrectly transfer ownership of that replacement to the caller.
            return ReferenceEquals(active_edit, edit) && IsEditAttached(edit) ? edit : null;
        }
        catch (Exception failure)
        {
            RetireEdit(edit);
            CleanupEdit(editor, failure);
            return null;
        }
    }

    private bool EndCellEdit()
    {
        if (active_edit is not { } edit) return false;
        // Retire before committing or removing controls: both can invoke application callbacks.
        RetireEdit(edit);
        long request = edit_version;
        Exception? failure = null;
        bool committed = false;
        try
        {
            if (!IsEditAttached(edit)) return false;
            string text = edit.Editor.Text;
            string oldValue = edit.Cell.Value;
            int rowIndex = edit.Row.Index;
            int columnIndex = edit.Column.Index;
            committed = oldValue == text || CommitBoundValue(edit, text);
            if (!IsEditAttached(edit) || request != edit_version) return false;
            if (committed && oldValue != text)
            {
                edit.Cell.Value = text;
                if (IsEditAttached(edit) && request == edit_version)
                    OnCellValueChanged(new DataGridViewCellEditEventArgs(edit.Row.Index, edit.Column.Index));
            }
            if (!IsDisposed && !Disposing)
                OnCellEndEdit(new DataGridViewCellEditEventArgs(rowIndex, columnIndex));
            return committed;
        }
        catch (Exception error) { failure = error; throw; }
        finally { CleanupEdit(edit.Editor, failure); }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Data binding reflects user-provided model properties, as in DataSource initialization.")]
    private bool CommitBoundValue(GridEditSession edit, string text)
    {
        if (edit.Row.BoundSource is null) return true;
        if (!ReferenceEquals(edit.Row.BoundSource, data_source) || edit.Row.BoundItem is not { } item)
            return false;
        // Sorting only changes the view. Locate the captured model object in its original source;
        // never infer the model object from the row's current visual index.
        bool found = false;
        foreach (object? candidate in edit.Row.BoundSource)
            if (ReferenceEquals(candidate, item)) { found = true; break; }
        if (!found || !IsEditAttached(edit)) return false;
        var property = item.GetType().GetProperty(edit.Column.HeaderText,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite != true || property.GetIndexParameters().Length != 0) return false;
        object? converted;
        try
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            converted = text.Length == 0 && Nullable.GetUnderlyingType(property.PropertyType) is not null
                ? null : Convert.ChangeType(text, targetType, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            return false;
        }
        if (!IsEditAttached(edit)) return false;
        try { property.SetValue(item, converted); }
        catch (Exception error) when (error is TargetInvocationException or ArgumentException or MethodAccessException)
        {
            // A rejecting model setter leaves the displayed value intact. No mutation is retried.
            return false;
        }
        return true;
    }

    private void RetireEdit(GridEditSession edit)
    {
        if (ReferenceEquals(active_edit, edit)) { active_edit = null; ++edit_version; }
        edit.Editor.KeyDown -= EditTextBox_KeyDown;
        edit.Editor.LostFocus -= EditTextBox_LostFocus;
    }

    private void CancelCellEdit()
    {
        ++edit_version; // Also invalidates a pending CellBeginEdit callback without an editor yet.
        if (active_edit is not { } edit) return;
        RetireEdit(edit);
        CleanupEdit(edit.Editor);
    }

    private void CleanupEdit(TextBox editor, Exception? precedingFailure = null)
    {
        List<Exception>? failures = precedingFailure is null ? null : [precedingFailure];
        try { if (ReferenceEquals(editor.Parent, this)) Controls.Remove(editor); }
        catch (Exception error) { (failures ??= []).Add(error); }
        try { editor.Dispose(); }
        catch (Exception error) { (failures ??= []).Add(error); }
        try { if (!IsDisposed && !Disposing) Invalidate(); }
        catch (Exception error) { (failures ??= []).Add(error); }
        if (failures?.Count > 1) throw new AggregateException("Grid editing and cleanup callbacks failed.", failures);
        if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }

    private void UpdateEditTextBoxPosition()
    {
        if (active_edit is not { } edit) return;
        if (!IsEditAttached(edit)) { CancelEdit(); return; }
        var bounds = GetCellBounds(edit.Row.Index, edit.Column.Index);
        if (bounds.IsEmpty) { CancelEdit(); return; }
        // SetBounds is atomic; a BoundsChanged callback cannot make us resize a replacement editor.
        edit.Editor.SetBounds(DeviceToLogicalUnits(bounds.Left) + 1, DeviceToLogicalUnits(bounds.Top) + 1,
            Math.Max(0, DeviceToLogicalUnits(bounds.Width) - 2), Math.Max(0, DeviceToLogicalUnits(bounds.Height) - 2));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        retiringGrid = true;
        Exception? failure = null;
        try { CancelEdit(); }
        catch (Exception error) { failure = error; }
        // Retiring the captured editor must not skip the grid's remaining cleanup, nor may
        // a grid disposal observer hide the original editor-cleanup exception.
        try { base.Dispose(true); }
        catch (Exception cleanup) when (failure is not null)
        { throw new AggregateException("Grid editor and control disposal failed.", failure, cleanup); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
