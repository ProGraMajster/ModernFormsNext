using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DataGridView
{
    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new GridAccessibleObject(this);

    private sealed partial class GridAccessibleObject : ControlAccessibleObject
    {
        private readonly ConditionalWeakTable<DataGridViewRow, RowPeer> rows = new();
        private readonly ConditionalWeakTable<DataGridViewColumn, ColumnHeaderPeer> columns = new();
        private readonly GridHeadersPeer headers;
        private readonly GridProvider provider;

        internal GridAccessibleObject(DataGridView owner) : base(owner)
        {
            headers = new GridHeadersPeer(this);
            provider = new GridProvider(this);
        }

        internal DataGridView? Grid => Owner is DataGridView { IsDisposed: false, Disposing: false } grid && !grid.retiringGrid ? grid : null;
        public override AccessibleControlType ControlType => Grid?.AccessibleControlType is { } type && type != AccessibleControlType.Default ? type : AccessibleControlType.DataGrid;
        public override AccessibleGridProvider? GridProvider => Grid is not null && !IsSensitive ? provider : null;

        internal RowPeer GetRow(DataGridViewRow row)
        {
            if (rows.TryGetValue(row, out var peer) && peer.Attached) return peer;
            rows.Remove(row);
            return rows.GetValue(row, value => new RowPeer(this, value));
        }

        internal ColumnHeaderPeer GetColumn(DataGridViewColumn column)
        {
            if (columns.TryGetValue(column, out var peer) && peer.Attached) return peer;
            columns.Remove(column);
            return columns.GetValue(column, value => new ColumnHeaderPeer(this, headers, value));
        }

        internal DataGridViewColumn? ColumnAt(int semanticIndex)
        {
            if (Grid is not { } grid || semanticIndex < 0) return null;
            foreach (var column in grid.Columns)
                if (column.Visible && semanticIndex-- == 0) return column;
            return null;
        }

        internal int SemanticIndex(DataGridViewColumn column)
        {
            if (Grid is not { } grid) return -1;
            int index = 0;
            foreach (var candidate in grid.Columns)
            {
                if (!candidate.Visible) continue;
                if (ReferenceEquals(candidate, column)) return index;
                ++index;
            }
            return -1;
        }

        internal int ColumnCount => Grid?.Columns.Count(column => column.Visible) ?? 0;

        // Direct indexed enumeration creates only requested peers; taking a bounded snapshot of a
        // large managed grid does not first allocate every logical cell or realize any editor.
        public override int GetChildCount() => Grid is { } grid && View != AccessibilityView.Hidden
            ? (grid.ColumnHeadersVisible ? 1 : 0) + grid.Rows.Count + base.GetChildCount()
                + (grid.vscrollbar.Visible ? 1 : 0) + (grid.hscrollbar.Visible ? 1 : 0) : 0;

        public override AccessibleObject? GetChild(int index)
        {
            if (Grid is not { } grid || View == AccessibilityView.Hidden || index < 0) return null;
            if (grid.ColumnHeadersVisible)
            {
                if (index == 0) return headers;
                --index;
            }
            if (index < grid.Rows.Count) return GetRow(grid.Rows[index]);
            index -= grid.Rows.Count;
            int visuals = base.GetChildCount();
            if (index < visuals) return base.GetChild(index);
            index -= visuals;
            if (grid.vscrollbar.Visible) { if (index == 0) return grid.vscrollbar.AccessibilityObject; --index; }
            return grid.hscrollbar.Visible && index == 0 ? grid.hscrollbar.AccessibilityObject : null;
        }

        public override AccessibleObject? GetSelected()
        {
            if (Grid is not { CurrentRow: { } row } grid) return null;
            if (grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect) return GetRow(row);
            return grid.SelectedColumnIndex >= 0 && grid.SelectedColumnIndex < grid.Columns.Count
                && grid.Columns[grid.SelectedColumnIndex].Visible
                && GetRow(row).GetCell(grid.Columns[grid.SelectedColumnIndex]) is { Attached: true } cell ? cell : null;
        }

        public override AccessibleObject? GetFocused()
        {
            if (Grid is not { } grid) return null;
            if (grid.edit_textbox is { Focused: true } editor) return editor.AccessibilityObject;
            return grid.Focused ? GetSelected() ?? this : base.GetFocused();
        }

        public override AccessibleObject? HitTest(int x, int y)
        {
            if (Grid is not { } grid || !Bounds.Contains(x, y)) return null;
            // Visual children (including the live editor and bars) take precedence over cell peers.
            for (int i = 0; i < base.GetChildCount(); ++i)
                if (base.GetChild(i)?.HitTest(x, y) is { } hit) return hit;
            if (grid.vscrollbar.Visible && grid.vscrollbar.AccessibilityObject.HitTest(x, y) is { } vertical) return vertical;
            if (grid.hscrollbar.Visible && grid.hscrollbar.AccessibilityObject.HitTest(x, y) is { } horizontal) return horizontal;
            if (grid.ColumnHeadersVisible && headers.HitTest(x, y) is { } header) return header;
            for (int row = grid.top_index; row < grid.Rows.Count; ++row)
            {
                var localBounds = grid.GetAccessibleRowBounds(row);
                if (localBounds.IsEmpty) break;
                if (GridScreenBounds(grid, localBounds).Contains(x, y))
                    return GetRow(grid.Rows[row]).HitTest(x, y) ?? this;
            }
            return this;
        }
    }

    private sealed class GridProvider(GridAccessibleObject root) : AccessibleGridProvider
    {
        public override int RowCount => root.Grid?.Rows.Count ?? 0;
        public override int ColumnCount => root.ColumnCount;
        public override bool IsTable => root.Grid is { } grid && (grid.ColumnHeadersVisible || grid.RowHeadersVisible);
        public override AccessibleObject? GetItem(int row, int column)
            => root.Grid is { } grid && row >= 0 && row < grid.Rows.Count && root.ColumnAt(column) is { } col
                ? root.GetRow(grid.Rows[row]).GetCell(col) : null;
        public override IReadOnlyList<AccessibleObject> GetColumnHeaders()
            => root.Grid is { ColumnHeadersVisible: true } grid
                ? grid.Columns.Where(column => column.Visible).Select(root.GetColumn).Cast<AccessibleObject>().ToArray() : [];
        public override IReadOnlyList<AccessibleObject> GetRowHeaders()
            => root.Grid is { RowHeadersVisible: true } grid
                ? grid.Rows.Select(row => root.GetRow(row).Header).Cast<AccessibleObject>().ToArray() : [];
    }

    private abstract class GridPeer(GridAccessibleObject root) : AccessibleObject
    {
        protected GridAccessibleObject Root { get; } = root;
        protected DataGridView? Grid => Root.Grid;
        internal abstract bool Attached { get; }
        protected abstract Rectangle LocalBounds { get; }
        public override Rectangle Bounds => Grid is { } grid && Attached ? GridScreenBounds(grid, LocalBounds) : Rectangle.Empty;
        public override AccessibilityView View => Attached && Root.View != AccessibilityView.Hidden ? AccessibilityView.Control : AccessibilityView.Hidden;
        public override bool IsSensitive => Root.IsSensitive;
        public override AccessibleStates State
        {
            get
            {
                if (!Attached || Grid is not { } grid) return AccessibleStates.Unavailable | AccessibleStates.Invisible | AccessibleStates.Offscreen;
                var state = AccessibleStates.None;
                if (!grid.Enabled) state |= AccessibleStates.Unavailable;
                if (!grid.Visible) state |= AccessibleStates.Invisible;
                if (LocalBounds.IsEmpty) state |= AccessibleStates.Offscreen;
                return state;
            }
        }
        protected bool Available => Attached && Grid is { Enabled: true, Visible: true } grid
            && grid.FindWindow()?.InputBindingsClosed != true && Root.View != AccessibilityView.Hidden && !IsSensitive;
        public override AccessibleObject? Navigate(AccessibleNavigation direction)
        {
            if (!Attached) return null;
            if (direction == AccessibleNavigation.FirstChild) return GetChild(0);
            if (direction == AccessibleNavigation.LastChild) return GetChild(GetChildCount() - 1);
            if (direction is not (AccessibleNavigation.Next or AccessibleNavigation.Previous) || Parent is not { } parent) return null;
            for (int i = 0; i < parent.GetChildCount(); i++)
                if (ReferenceEquals(parent.GetChild(i), this)) return parent.GetChild(i + (direction == AccessibleNavigation.Next ? 1 : -1));
            return null;
        }
        public override AccessibleObject? HitTest(int x, int y)
        {
            if (!Attached || !Bounds.Contains(x, y)) return null;
            for (int i = 0; i < GetChildCount(); i++)
                if (GetChild(i)?.HitTest(x, y) is { } child) return child;
            return this;
        }
    }

    private sealed class RowPeer : GridPeer
    {
        private readonly WeakReference<DataGridViewRow> row;
        private readonly long version;
        private readonly ConditionalWeakTable<DataGridViewColumn, CellPeer> cells = new();
        internal RowHeaderPeer Header { get; }
        internal RowPeer(GridAccessibleObject root, DataGridViewRow value) : base(root)
        {
            row = new(value); version = value.OwnershipVersion; Header = new(this, root);
        }
        internal DataGridViewRow? Row => row.TryGetTarget(out var value) ? value : null;
        internal override bool Attached => Grid is { } grid && Row is { } value
            && value.OwnershipVersion == version && value.DataGridView == grid && value.Index >= 0;
        public override AccessibleObject? Parent => Attached ? Root : null;
        public override AccessibleRole Role => AccessibleRole.Row;
        public override AccessibleControlType ControlType => AccessibleControlType.DataItem;
        public override string? Name { get => Attached ? (Row!.Index + 1).ToString(CultureInfo.CurrentCulture) : null; set { } }
        protected override Rectangle LocalBounds => Attached ? Grid!.GetAccessibleRowBounds(Row!.Index) : Rectangle.Empty;
        public override AccessibleStates State => base.State | AccessibleStates.Selectable
            | (Attached && Grid!.CurrentRow == Row && Grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect ? AccessibleStates.Selected : 0)
            | (Attached && Grid!.Focused && Grid.CurrentRow == Row && Grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect ? AccessibleStates.Focused : 0);
        internal CellPeer GetCell(DataGridViewColumn column)
        {
            if (cells.TryGetValue(column, out var peer) && peer.Attached) return peer;
            cells.Remove(column);
            return cells.GetValue(column, value => new CellPeer(this, Root, value));
        }
        public override int GetChildCount() => Attached ? Root.ColumnCount + (Grid!.RowHeadersVisible ? 1 : 0) : 0;
        public override AccessibleObject? GetChild(int index)
        {
            if (!Attached || index < 0) return null;
            if (Grid!.RowHeadersVisible) { if (index == 0) return Header; --index; }
            return Root.ColumnAt(index) is { } column ? GetCell(column) : null;
        }
        public override AccessibleActions SupportedActions => Available ? AccessibleActions.Select | AccessibleActions.ScrollIntoView | (Grid!.CanSelect ? AccessibleActions.Focus : 0) : 0;
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (parameter is not null || !Available || (SupportedActions & action) == 0 || Grid is not { } grid) return false;
            var lifetime = new AccessibilityControlLifetime(grid);
            switch (action)
            {
                case AccessibleActions.Select: grid.SetCurrentCoordinates(Row!.Index, grid.SelectedColumnIndex); break;
                case AccessibleActions.Focus: grid.SetCurrentCoordinates(Row!.Index, grid.SelectedColumnIndex); if (lifetime.IsCurrent && Attached) grid.Select(); break;
                case AccessibleActions.ScrollIntoView: grid.EnsureRowVisible(Row!.Index); break;
                default: return false;
            }
            return lifetime.IsCurrent && Attached;
        }
        public override AccessibleObject? GetSelected() => (State & AccessibleStates.Selected) != 0 ? this : null;
        public override AccessibleObject? GetFocused() => (State & AccessibleStates.Focused) != 0 ? this : null;
        public override void Select(AccessibleSelection flags)
        {
            if ((flags & AccessibleSelection.TakeSelection) != 0) PerformAction(AccessibleActions.Select);
            if ((flags & AccessibleSelection.TakeFocus) != 0) PerformAction(AccessibleActions.Focus);
        }
    }

    private sealed class CellPeer : GridPeer
    {
        private readonly RowPeer parent;
        private readonly WeakReference<DataGridViewColumn> column;
        private readonly WeakReference<DataGridViewCell>? cell;
        private readonly long columnVersion, cellVersion;
        internal CellPeer(RowPeer parent, GridAccessibleObject root, DataGridViewColumn column) : base(root)
        {
            this.parent = parent; this.column = new(column); columnVersion = column.OwnershipVersion;
            if (parent.Row is { } row && column.Index >= 0 && column.Index < row.Cells.Count)
            {
                cell = new(row.Cells[column.Index]); cellVersion = row.Cells[column.Index].OwnershipVersion;
            }
        }
        private DataGridViewColumn? Column => column.TryGetTarget(out var value) ? value : null;
        private DataGridViewCell? Cell => cell is not null && cell.TryGetTarget(out var value) ? value : null;
        internal override bool Attached => parent.Attached && Column is { Visible: true } col && col.OwnershipVersion == columnVersion
            && col.DataGridView == Grid && col.Index >= 0 && parent.Row is { } row
            && (cell is null ? row.Cells.Count <= col.Index : Cell is { } value && value.OwnershipVersion == cellVersion
                && value.OwningRow == row && value.ColumnIndex == col.Index);
        public override AccessibleObject? Parent => Attached ? parent : null;
        public override AccessibleRole Role => AccessibleRole.Cell;
        public override AccessibleControlType ControlType => AccessibleControlType.DataItem;
        public override string? Name { get => Attached && !IsSensitive ? Column!.HeaderText : null; set { } }
        public override string? Value { get => Attached && !IsSensitive ? Cell?.Value ?? string.Empty : null; set { if (value is not null) PerformAction(AccessibleActions.SetValue, value); } }
        protected override Rectangle LocalBounds => Attached ? Grid!.GetAccessibleCellBounds(parent.Row!.Index, Column!.Index) : Rectangle.Empty;
        private bool IsCurrent => Attached && Grid!.CurrentRow == parent.Row && Grid.SelectedColumnIndex == Column!.Index;
        public override AccessibleStates State => base.State | AccessibleStates.Selectable
            | (IsCurrent ? AccessibleStates.Selected : 0) | (IsCurrent && Grid!.Focused ? AccessibleStates.Focused : 0)
            | (Grid?.ReadOnly == true ? AccessibleStates.ReadOnly : 0);
        public override AccessibleGridCellInfo? GridCell => Attached && !IsSensitive
            ? new(Root, parent.Row!.Index, Root.SemanticIndex(Column!), rowHeaders: Grid!.RowHeadersVisible ? [parent.Header] : [],
                columnHeaders: Grid.ColumnHeadersVisible ? [Root.GetColumn(Column!)] : []) : null;
        public override AccessibleActions SupportedActions => Available ? AccessibleActions.Select | AccessibleActions.ScrollIntoView
            | (Grid!.CanSelect ? AccessibleActions.Focus : 0) | (!Grid.ReadOnly ? AccessibleActions.SetValue : 0) : 0;
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (!Available || (SupportedActions & action) == 0 || Grid is not { } grid) return false;
            if (action == AccessibleActions.SetValue)
                return parameter is string value && grid.SetAccessibleCellValue(parent.Row!, Column!, Cell, value);
            if (parameter is not null) return false;
            var lifetime = new AccessibilityControlLifetime(grid);
            switch (action)
            {
                case AccessibleActions.Select: grid.SetCurrentCoordinates(parent.Row!.Index, Column!.Index); break;
                case AccessibleActions.Focus:
                    grid.SetCurrentCoordinates(parent.Row!.Index, Column!.Index);
                    if (lifetime.IsCurrent && Attached) grid.Select();
                    break;
                case AccessibleActions.ScrollIntoView: grid.RevealAccessibleCell(parent.Row!, Column!); break;
                default: return false;
            }
            return lifetime.IsCurrent && Attached;
        }
        public override AccessibleObject? GetSelected() => IsCurrent ? this : null;
        public override AccessibleObject? GetFocused() => IsCurrent && Grid!.Focused ? this : null;
        public override void Select(AccessibleSelection flags)
        {
            if ((flags & AccessibleSelection.TakeSelection) != 0) PerformAction(AccessibleActions.Select);
            if ((flags & AccessibleSelection.TakeFocus) != 0) PerformAction(AccessibleActions.Focus);
        }
    }

    private sealed class GridHeadersPeer(GridAccessibleObject root) : GridPeer(root)
    {
        internal override bool Attached => Grid is { ColumnHeadersVisible: true };
        public override AccessibleObject? Parent => Attached ? Root : null;
        public override AccessibleRole Role => AccessibleRole.Grouping;
        public override AccessibleControlType ControlType => AccessibleControlType.Header;
        protected override Rectangle LocalBounds => Attached ? Grid!.GetAccessibleHeaderBounds() : Rectangle.Empty;
        public override int GetChildCount() => Attached ? Root.ColumnCount : 0;
        public override AccessibleObject? GetChild(int index) => Attached && Root.ColumnAt(index) is { } column ? Root.GetColumn(column) : null;
    }

    private sealed class ColumnHeaderPeer : GridPeer
    {
        private readonly GridHeadersPeer parent;
        private readonly WeakReference<DataGridViewColumn> column;
        private readonly long version;
        internal ColumnHeaderPeer(GridAccessibleObject root, GridHeadersPeer parent, DataGridViewColumn column) : base(root)
        { this.parent = parent; this.column = new(column); version = column.OwnershipVersion; }
        private DataGridViewColumn? Column => column.TryGetTarget(out var value) ? value : null;
        internal override bool Attached => Grid is { ColumnHeadersVisible: true } grid && Column is { Visible: true } col
            && col.OwnershipVersion == version && col.DataGridView == grid && col.Index >= 0;
        public override AccessibleObject? Parent => Attached ? parent : null;
        public override AccessibleRole Role => AccessibleRole.ColumnHeader;
        public override AccessibleControlType ControlType => AccessibleControlType.HeaderItem;
        public override string? Name { get => Attached && !IsSensitive ? Column!.HeaderText : null; set { } }
        protected override Rectangle LocalBounds => Attached ? Grid!.GetAccessibleColumnHeaderBounds(Column!.Index) : Rectangle.Empty;
        public override AccessibleActions SupportedActions => Available && Column!.Sortable ? AccessibleActions.Invoke : 0;
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action != AccessibleActions.Invoke || parameter is not null || SupportedActions == 0 || Grid is not { } grid) return false;
            var lifetime = new AccessibilityControlLifetime(grid);
            grid.OnColumnHeaderClick(Column!.Index);
            return lifetime.IsCurrent && Attached;
        }
        public override void DoDefaultAction() => PerformAction(AccessibleActions.Invoke);
    }

    private sealed class RowHeaderPeer(RowPeer parent, GridAccessibleObject root) : GridPeer(root)
    {
        internal override bool Attached => parent.Attached && Grid is { RowHeadersVisible: true };
        public override AccessibleObject? Parent => Attached ? parent : null;
        public override AccessibleRole Role => AccessibleRole.RowHeader;
        public override AccessibleControlType ControlType => AccessibleControlType.HeaderItem;
        public override string? Name { get => Attached && !IsSensitive ? parent.Name : null; set { } }
        protected override Rectangle LocalBounds
        {
            get
            {
                if (!Attached) return Rectangle.Empty;
                var row = Grid!.GetAccessibleRowBounds(parent.Row!.Index);
                return row.IsEmpty ? row : new(row.Left, row.Top, Math.Min(row.Width, Grid.ScaledRowHeadersWidth), row.Height);
            }
        }
        // The header activates its row; it is not a second independently selectable item nested
        // inside that row. This keeps native SelectionItem containers at the actual grid.
        public override AccessibleActions SupportedActions => !Attached ? 0
            : (parent.SupportedActions & ~AccessibleActions.Select)
                | ((parent.SupportedActions & AccessibleActions.Select) != 0 ? AccessibleActions.Invoke : 0);
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
            => Attached && (SupportedActions & action) != 0
                && parent.PerformAction(action == AccessibleActions.Invoke ? AccessibleActions.Select : action, parameter);
        public override void DoDefaultAction() => PerformAction(AccessibleActions.Invoke);
    }

    private Rectangle GetAccessibleRowBounds(int row)
    {
        if (row < top_index || row < 0 || row >= Rows.Count) return Rectangle.Empty;
        var content = GetContentArea();
        int top = content.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);
        for (int i = top_index; i < row && top < content.Bottom; ++i) top += LogicalToDeviceUnits(Rows[i].Height);
        return top >= content.Bottom ? Rectangle.Empty : Rectangle.Intersect(content,
            new Rectangle(content.Left, top, content.Width, LogicalToDeviceUnits(Rows[row].Height)));
    }

    private Rectangle GetAccessibleCellBounds(int row, int column)
    {
        if (column < 0 || column >= Columns.Count || !Columns[column].Visible) return Rectangle.Empty;
        var content = GetContentArea();
        int left = content.Left + (RowHeadersVisible ? ScaledRowHeadersWidth : 0);
        int top = content.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);
        return Rectangle.Intersect(Rectangle.FromLTRB(left, top, Math.Max(left, content.Right), Math.Max(top, content.Bottom)), GetCellBounds(row, column));
    }

    private Rectangle GetAccessibleHeaderBounds()
    {
        var content = GetContentArea();
        return Rectangle.Intersect(content, new(content.Left, content.Top, content.Width, ScaledHeaderHeight));
    }

    private Rectangle GetAccessibleColumnHeaderBounds(int column)
    {
        var content = GetAccessibleHeaderBounds();
        int left = content.Left + (RowHeadersVisible ? ScaledRowHeadersWidth : 0);
        int x = left - horizontal_scroll_offset;
        for (int i = 0; i < column; i++) if (Columns[i].Visible) x += LogicalToDeviceUnits(Columns[i].Width);
        return Rectangle.Intersect(Rectangle.FromLTRB(left, content.Top, Math.Max(left, content.Right), content.Bottom),
            new Rectangle(x, content.Top, LogicalToDeviceUnits(Columns[column].Width), content.Height));
    }

    private static Rectangle GridScreenBounds(DataGridView grid, Rectangle local)
    {
        if (local.Width <= 0 || local.Height <= 0) return Rectangle.Empty;
        Point a = grid.PointToScreen(local.Location), b = grid.PointToScreen(new(local.Right, local.Top)),
            c = grid.PointToScreen(new(local.Left, local.Bottom)), d = grid.PointToScreen(new(local.Right, local.Bottom));
        return Rectangle.FromLTRB(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }

    private void RevealAccessibleCell(DataGridViewRow row, DataGridViewColumn column)
    {
        var lifetime = new AccessibilityControlLifetime(this);
        long rowVersion = row.OwnershipVersion, columnVersion = column.OwnershipVersion;
        EnsureRowVisible(row.Index);
        // A vertical scroll observer may detach and reinsert these same model objects. Such
        // objects have new peers; the retired request must not continue on the horizontal axis.
        if (!lifetime.IsCurrent || row.OwnershipVersion != rowVersion || column.OwnershipVersion != columnVersion
            || row.DataGridView != this || column.DataGridView != this || !column.Visible || !Enabled || !Visible) return;
        var content = GetContentArea();
        var bounds = GetCellBounds(row.Index, column.Index);
        if (bounds.IsEmpty) return;
        int left = content.Left + (RowHeadersVisible ? ScaledRowHeadersWidth : 0);
        long offset = horizontal_scroll_offset;
        if (bounds.Left < left) offset += bounds.Left - left;
        else if (bounds.Right > content.Right) offset += bounds.Right - content.Right;
        hscrollbar.Value = (int)Math.Clamp(offset, hscrollbar.Minimum, hscrollbar.Maximum);
    }

    private bool SetAccessibleCellValue(DataGridViewRow row, DataGridViewColumn column, DataGridViewCell? cell, string value)
    {
        if (!CanEditCell(row, column, cell)) return false;
        var lifetime = new AccessibilityControlLifetime(this);
        long rowVersion = row.OwnershipVersion, columnVersion = column.OwnershipVersion;
        RevealAccessibleCell(row, column);
        if (!lifetime.IsCurrent || rowVersion != row.OwnershipVersion || columnVersion != column.OwnershipVersion
            || !CanEditCell(row, column, cell)) return false;
        var edit = BeginCellEdit(row.Index, column.Index);
        if (edit is null || !ReferenceEquals(active_edit, edit) || edit.Row != row || edit.Column != column
            || (cell is not null && edit.Cell != cell) || !IsEditAttached(edit)) return false;
        edit.Editor.Text = value;
        return ReferenceEquals(active_edit, edit) && IsEditAttached(edit) && EndEdit();
    }
}
