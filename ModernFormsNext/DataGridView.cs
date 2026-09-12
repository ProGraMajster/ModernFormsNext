using ModernFormsNext.Renderers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a DataGridView control for displaying tabular data.
    /// </summary>
    public partial class DataGridView : Control
    {
        private int header_height = 30;
        private int row_height = 25;
        private int row_headers_width = 40;
        private bool row_headers_visible;
        private int top_index;
        private int horizontal_scroll_offset;
        private int selected_row_index = -1;
        private int selected_column_index = -1;
        private int hovered_row_index = -1;
        private int resize_column_index = -1;
        private int resize_row_index = -1;
        private int resize_start_x;
        private int resize_start_y;
        private int resize_start_width;
        private int resize_start_height;
        private bool is_resizing_column;
        private bool is_resizing_row;
        private bool column_headers_visible = true;
        private DataGridViewSelectionMode selection_mode = DataGridViewSelectionMode.FullRowSelect;
        private bool read_only;
        private IList? data_source;
        private long data_source_version;
        private GridEditSession? active_edit;
        private long edit_version;
        private TextBox? edit_textbox => active_edit?.Editor;

        private readonly VerticalScrollBar vscrollbar;
        private readonly HorizontalScrollBar hscrollbar;

        /// <summary>
        /// Initializes a new instance of the DataGridView class.
        /// </summary>
        public DataGridView()
        {
            Columns = new DataGridViewColumnCollection(this);
            Rows = new DataGridViewRowCollection(this);

            vscrollbar = new VerticalScrollBar
            {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Right
            };

            vscrollbar.ValueChanged += (o, e) => {
                top_index = Math.Max(vscrollbar.Value, 0);
                UpdateEditTextBoxPosition();
                Invalidate();
                NotifyAccessibleScrollChanged();
            };

            hscrollbar = new HorizontalScrollBar
            {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 10,
                LargeChange = 50,
                Visible = false,
                Dock = DockStyle.Bottom
            };

            hscrollbar.ValueChanged += (o, e) => {
                horizontal_scroll_offset = Math.Max(hscrollbar.Value, 0);
                UpdateEditTextBoxPosition();
                Invalidate();
                NotifyAccessibleScrollChanged();
            };

            Controls.AddImplicitControl(vscrollbar);
            Controls.AddImplicitControl(hscrollbar);
        }

        /// <summary>
        /// Begins editing the specified cell.
        /// </summary>
        public void BeginEdit(int rowIndex, int columnIndex) => BeginCellEdit(rowIndex, columnIndex);

        /// <summary>
        /// Raised when a cell begins editing.
        /// </summary>
        public event EventHandler<DataGridViewCellEditEventArgs>? CellBeginEdit;

        /// <summary>
        /// Raised when a cell ends editing.
        /// </summary>
        public event EventHandler<DataGridViewCellEditEventArgs>? CellEndEdit;

        /// <summary>
        /// Raised when a cell value has changed.
        /// </summary>
        public event EventHandler<DataGridViewCellEditEventArgs>? CellValueChanged;

        /// <summary>
        /// Gets or sets whether column headers are visible.
        /// </summary>
        public bool ColumnHeadersVisible
        {
            get => column_headers_visible;
            set
            {
                if (column_headers_visible != value)
                {
                    column_headers_visible = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets the collection of columns in the DataGridView.
        /// </summary>
        public DataGridViewColumnCollection Columns { get; }

        /// <summary>
        /// Gets or sets the data source for the DataGridView.
        /// Setting this property auto-generates columns from the item type's public properties
        /// and populates the rows from the collection.
        /// </summary>
        public IList? DataSource
        {
            get => data_source;
            set
            {
                data_source = value;
                OnDataSourceChanged();
            }
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(450, 300);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;
            });

        /// <summary>
        /// Gets or sets whether the user can resize columns by dragging column header borders.
        /// </summary>
        public bool AllowUserToResizeColumns { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the user can resize rows by dragging row header borders.
        /// </summary>
        public bool AllowUserToResizeRows { get; set; } = true;

        /// <summary>
        /// Gets the default cell style applied to alternating rows.
        /// </summary>
        public ControlStyle AlternatingRowsDefaultCellStyle { get; } = new ControlStyle(DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets the default cell style applied to cells in the DataGridView.
        /// </summary>
        public ControlStyle DefaultCellStyle { get; } = new ControlStyle(DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets the default cell style applied to column header cells.
        /// </summary>
        public ControlStyle ColumnHeadersDefaultCellStyle { get; } = new ControlStyle(DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets the default cell style applied to row header cells.
        /// </summary>
        public ControlStyle RowHeadersDefaultCellStyle { get; } = new ControlStyle(DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Commits the current edit and hides the edit TextBox.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        public bool EndEdit() => EndCellEdit();

        // Handle key events during editing.
        private void EditTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                EndEdit();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                EndEdit();

                if (e.Shift)
                    NavigateToPreviousCell();
                else
                    NavigateToNextCell();

                // Begin editing the newly selected cell
                if (selected_row_index >= 0 && selected_column_index >= 0)
                    BeginEdit(selected_row_index, selected_column_index);

                e.Handled = true;
            }
        }

        // Handle lost focus during editing.
        private void EditTextBox_LostFocus(object? sender, EventArgs e)
        {
            EndEdit();
        }

        /// <summary>
        /// Cancels the current edit without committing changes.
        /// </summary>
        public void CancelEdit() => CancelCellEdit();

        /// <summary>
        /// Gets or sets the index of the first row displayed on the DataGridView.
        /// </summary>
        public int FirstDisplayedScrollingRowIndex
        {
            get => top_index;
            set
            {
                if (top_index == value)
                    return;

                if (value < 0 || value >= Rows.Count)
                    return;

                vscrollbar.Value = Math.Min(value, vscrollbar.Maximum);
            }
        }

        /// <summary>
        /// Gets the bounding rectangle for a cell.
        /// </summary>
        public Rectangle GetCellBounds(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Columns.Count)
                return Rectangle.Empty;

            if (rowIndex < top_index || !Columns[columnIndex].Visible)
                return Rectangle.Empty;

            var client = GetContentArea();
            var row_top = client.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);

            // Accumulate y by summing individual row heights from the first visible row
            var y = row_top;

            for (var i = top_index; i < rowIndex; i++)
                y += LogicalToDeviceUnits(Rows[i].Height);

            var scaled_row_height = LogicalToDeviceUnits(Rows[rowIndex].Height);

            // Row is below the visible area
            if (y >= client.Bottom)
                return Rectangle.Empty;

            var row_header_offset = row_headers_visible ? ScaledRowHeadersWidth : 0;
            var x = client.Left + row_header_offset - horizontal_scroll_offset;

            for (var i = 0; i < columnIndex; i++)
            {
                if (Columns[i].Visible)
                    x += LogicalToDeviceUnits(Columns[i].Width);
            }

            var col_width = LogicalToDeviceUnits(Columns[columnIndex].Width);
            return new Rectangle(x, y, col_width, scaled_row_height);
        }

        /// <summary>
        /// Gets the content area, accounting for scrollbars.
        /// Use Math.Ceiling to avoid fractional DPI rounding artifacts.
        /// </summary>
        internal Rectangle GetContentArea()
        {
            var client = ClientRectangle;
            var w = client.Width - (vscrollbar.Visible ? (int)Math.Ceiling(vscrollbar.Width * ScaleFactor.Width) : 0);
            var h = client.Height - (hscrollbar.Visible ? (int)Math.Ceiling(hscrollbar.Height * ScaleFactor.Height) : 0);
            return new Rectangle(client.Left, client.Top, Math.Max(0, w), Math.Max(0, h));
        }

        /// <summary>
        /// Gets the column index at the specified location.
        /// </summary>
        internal int GetColumnAtLocation(Point location)
        {
            var client = GetContentArea();
            var row_header_offset = row_headers_visible ? ScaledRowHeadersWidth : 0;
            var x = client.Left + row_header_offset - horizontal_scroll_offset;

            for (var i = 0; i < Columns.Count; i++)
            {
                if (!Columns[i].Visible)
                    continue;

                var col_width = LogicalToDeviceUnits(Columns[i].Width);

                if (location.X >= x && location.X < x + col_width)
                    return i;

                x += col_width;
            }

            return -1;
        }

        /// <summary>
        /// Gets the resize column index if the mouse is near a column border.
        /// </summary>
        private int GetResizeColumnAtLocation(Point location)
        {
            var client = GetContentArea();
            var header_rect = new Rectangle(client.Left, client.Top, client.Width, ScaledHeaderHeight);

            if (!header_rect.Contains(location))
                return -1;

            var row_header_offset = row_headers_visible ? ScaledRowHeadersWidth : 0;
            var x = client.Left + row_header_offset - horizontal_scroll_offset;
            var resize_zone = LogicalToDeviceUnits(4);

            for (var i = 0; i < Columns.Count; i++)
            {
                if (!Columns[i].Visible)
                    continue;

                x += LogicalToDeviceUnits(Columns[i].Width);

                if (Math.Abs(location.X - x) <= resize_zone)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the row index if the mouse is near a row border in the row header area.
        /// </summary>
        private int GetResizeRowAtLocation(Point location)
        {
            if (!row_headers_visible)
                return -1;

            var client = GetContentArea();
            var header_offset = ColumnHeadersVisible ? ScaledHeaderHeight : 0;
            var row_header_rect = new Rectangle(client.Left, client.Top + header_offset, ScaledRowHeadersWidth, Math.Max(0, client.Height - header_offset));

            if (!row_header_rect.Contains(location))
                return -1;

            var row_top = client.Top + header_offset;
            var resize_zone = LogicalToDeviceUnits(4);

            for (var i = top_index; i < Rows.Count; i++)
            {
                var scaled_row_height = LogicalToDeviceUnits(Rows[i].Height);
                row_top += scaled_row_height;

                if (row_top > client.Bottom)
                    break;

                if (Math.Abs(location.Y - row_top) <= resize_zone)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the row index at the specified location.
        /// </summary>
        internal int GetRowAtLocation(Point location)
        {
            var client = GetContentArea();
            var row_top = client.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);

            if (location.Y < row_top)
                return -1;

            var y = row_top;

            for (var i = top_index; i < Rows.Count; i++)
            {
                var h = LogicalToDeviceUnits(Rows[i].Height);

                if (location.Y >= y && location.Y < y + h)
                    return i;

                y += h;

                if (y >= client.Bottom)
                    break;
            }

            return -1;
        }

        /// <summary>
        /// Gets or sets the height, in pixels, of the column headers row.
        /// </summary>
        public int ColumnHeadersHeight
        {
            get => header_height;
            set
            {
                if (header_height != value)
                {
                    header_height = Math.Max(value, 10);
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets the currently selected cell, or null if no cell is selected.
        /// </summary>
        public DataGridViewCell? CurrentCell
        {
            get
            {
                if (selected_row_index < 0 || selected_row_index >= Rows.Count)
                    return null;

                if (selected_column_index < 0 || selected_column_index >= Rows[selected_row_index].Cells.Count)
                    return null;

                return Rows[selected_row_index].Cells[selected_column_index];
            }
        }

        /// <summary>
        /// Gets the row and column indices of the currently selected cell.
        /// </summary>
        public Point CurrentCellAddress => new Point(selected_column_index, selected_row_index);

        /// <summary>
        /// Gets the row containing the currently selected cell, or null if no row is selected.
        /// </summary>
        public DataGridViewRow? CurrentRow
        {
            get
            {
                if (selected_row_index >= 0 && selected_row_index < Rows.Count)
                    return Rows[selected_row_index];

                return null;
            }
        }

        /// <summary>
        /// Gets the horizontal scroll offset.
        /// </summary>
        internal int HorizontalScrollOffset => horizontal_scroll_offset;

        /// <summary>
        /// Gets or sets the index of the currently hovered row.
        /// </summary>
        internal int HoveredRowIndex
        {
            get => hovered_row_index;
            set
            {
                if (hovered_row_index != value)
                {
                    hovered_row_index = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets whether a cell is currently being edited.
        /// </summary>
        public bool IsCurrentCellInEditMode => edit_textbox is not null;

        /// <summary>
        /// Raises the CellBeginEdit event.
        /// </summary>
        protected virtual void OnCellBeginEdit(DataGridViewCellEditEventArgs e) => CellBeginEdit?.Invoke(this, e);

        /// <summary>
        /// Raises the CellEndEdit event.
        /// </summary>
        protected virtual void OnCellEndEdit(DataGridViewCellEditEventArgs e) => CellEndEdit?.Invoke(this, e);

        /// <summary>
        /// Raises the CellValueChanged event.
        /// </summary>
        protected virtual void OnCellValueChanged(DataGridViewCellEditEventArgs e)
        {
            CellValueChanged?.Invoke(this, e);
            NotifyAccessibilityClients(AccessibleEvents.ValueChange);
        }

        /// <summary>
        /// Handles a column header click for sorting.
        /// </summary>
        private void OnColumnHeaderClick(int columnIndex)
        {
            var column = Columns[columnIndex];

            // Toggle sort order
            var new_order = column.SortOrder == SortOrder.Ascending
                ? SortOrder.Descending
                : SortOrder.Ascending;

            // Reset all other columns
            foreach (var col in Columns)
                col.SortOrder = SortOrder.None;

            column.SortOrder = new_order;

            // Sort the data
            SortByColumn(columnIndex, new_order);

            // Raise the event
            ColumnHeaderClick?.Invoke(this, new EventArgs<DataGridViewColumn>(column));

            Invalidate();
        }

        /// <summary>
        /// Raised when a column header is clicked.
        /// </summary>
        public event EventHandler<EventArgs<DataGridViewColumn>>? ColumnHeaderClick;

        // Populates rows and columns from the DataSource.
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private void OnDataSourceChanged()
        {
            long request = ++data_source_version;
            var source = data_source;
            var lifetime = new AccessibilityControlLifetime(this);
            bool IsCurrent() => request == data_source_version && ReferenceEquals(source, data_source) && lifetime.IsCurrent;
            Columns.Clear();
            if (!IsCurrent()) return;
            Rows.Clear();
            if (!IsCurrent() || source is null || source.Count == 0)
                return;

            // Get the element type
            var element_type = GetElementType(source);

            if (!IsCurrent() || element_type is null)
                return;

            // Auto-generate columns from public readable properties
            var properties = element_type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();

            foreach (var prop in properties)
            {
                Columns.Add(prop.Name, EstimateColumnWidth(prop.Name));
                if (!IsCurrent()) return;
            }

            // Populate rows
            foreach (var item in source)
            {
                if (!IsCurrent()) return;
                if (item is null)
                    continue;

                var values = new string[properties.Length];

                for (var i = 0; i < properties.Length; i++)
                {
                    values[i] = properties[i].GetValue(item)?.ToString() ?? string.Empty;
                    // Getters/ToString can replace DataSource, including A -> B -> A. The old
                    // request must never append rows into the replacement's completed view.
                    if (!IsCurrent()) return;
                }

                // Publish a fully bound row: Reorder callbacks may immediately edit this cell.
                var row = new DataGridViewRow { BoundSource = source, BoundItem = item };
                foreach (string text in values) row.Cells.Add(text);
                Rows.Add(row);
                if (!IsCurrent()) return;
            }
        }

        // Gets the element type from an IList.
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private static Type? GetElementType(IList list)
        {
            var list_type = list.GetType();

            // Check for generic IList<T>
            foreach (var iface in list_type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                    return iface.GetGenericArguments()[0];
            }

            // Fallback: use type of first item
            if (list.Count > 0 && list[0] is not null)
                return list[0]!.GetType();

            return null;
        }

        // Estimates a column width based on header text length.
        private static int EstimateColumnWidth(string headerText)
        {
            return Math.Max(80, headerText.Length * 10 + 20);
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick(MouseEventArgs e)
        {
            base.OnDoubleClick(e);

            if (read_only || !Enabled)
                return;

            var row = GetRowAtLocation(e.Location);
            var col = GetColumnAtLocation(e.Location);

            if (row >= 0 && col >= 0)
                BeginEdit(row, col);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || !e.Button.HasFlag(MouseButtons.Left))
                return;

            // If editing, end edit when clicking outside the editor
            if (edit_textbox is not null)
            {
                var edit_bounds = edit_textbox.ScaledBounds;

                if (!edit_bounds.Contains(e.Location))
                    EndEdit();
            }

            // Check for column resize
            if (ColumnHeadersVisible && AllowUserToResizeColumns)
            {
                var resize_col = GetResizeColumnAtLocation(e.Location);

                if (resize_col >= 0)
                {
                    is_resizing_column = true;
                    resize_column_index = resize_col;
                    resize_start_x = e.Location.X;
                    resize_start_width = LogicalToDeviceUnits(Columns[resize_col].Width);
                    return;
                }
            }

            // Check for row resize
            if (row_headers_visible && AllowUserToResizeRows)
            {
                var resize_row = GetResizeRowAtLocation(e.Location);

                if (resize_row >= 0)
                {
                    is_resizing_row = true;
                    resize_row_index = resize_row;
                    resize_start_y = e.Location.Y;
                    resize_start_height = LogicalToDeviceUnits(Rows[resize_row].Height);
                    return;
                }
            }

            // Check for header click (sorting)
            if (ColumnHeadersVisible)
            {
                var client = GetContentArea();
                var header_rect = new Rectangle(client.Left, client.Top, client.Width, ScaledHeaderHeight);

                if (header_rect.Contains(e.Location))
                {
                    var col = GetColumnAtLocation(e.Location);

                    if (col >= 0 && Columns[col].Sortable)
                        OnColumnHeaderClick(col);

                    return;
                }
            }

            // Select row/cell
            var row = GetRowAtLocation(e.Location);

            if (row >= 0)
            {
                if (selection_mode == DataGridViewSelectionMode.FullRowSelect)
                {
                    SelectedRowIndex = row;
                }
                else
                {
                    var col = GetColumnAtLocation(e.Location);
                    SetCurrentCoordinates(row, col);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoveredRowIndex = -1;

            if (!is_resizing_column && !is_resizing_row)
                SetCursorDirect(Cursors.Arrow);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (is_resizing_column)
            {
                var delta = e.Location.X - resize_start_x;
                var new_width = DeviceToLogicalUnits(resize_start_width + delta);
                Columns[resize_column_index].Width = new_width;
                UpdateScrollBars();
                return;
            }

            if (is_resizing_row)
            {
                var delta = e.Location.Y - resize_start_y;
                var new_height = DeviceToLogicalUnits(resize_start_height + delta);
                Rows[resize_row_index].Height = Math.Max(new_height, 10);
                UpdateScrollBars();
                return;
            }

            // Update cursor for column resize zones
            if (ColumnHeadersVisible && AllowUserToResizeColumns)
            {
                var resize_col = GetResizeColumnAtLocation(e.Location);

                if (resize_col >= 0)
                {
                    if (Cursor != Cursors.SizeWestEast)
                        SetCursorDirect(Cursors.SizeWestEast);

                    // Update hovered row
                    HoveredRowIndex = GetRowAtLocation(e.Location);
                    return;
                }
            }

            // Update cursor for row resize zones
            if (row_headers_visible && AllowUserToResizeRows)
            {
                var resize_row = GetResizeRowAtLocation(e.Location);

                if (resize_row >= 0)
                {
                    if (Cursor != Cursors.SizeNorthSouth)
                        SetCursorDirect(Cursors.SizeNorthSouth);

                    HoveredRowIndex = GetRowAtLocation(e.Location);
                    return;
                }
            }

            if (Cursor != Cursors.Arrow)
                SetCursorDirect(Cursors.Arrow);

            // Update hovered row
            var row = GetRowAtLocation(e.Location);
            HoveredRowIndex = row;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (is_resizing_column)
            {
                is_resizing_column = false;
                resize_column_index = -1;
                SetCursorDirect(Cursors.Arrow);
            }

            if (is_resizing_row)
            {
                is_resizing_row = false;
                resize_row_index = -1;
                SetCursorDirect(Cursors.Arrow);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (!e.Handled && vscrollbar.Visible)
                vscrollbar.RaiseMouseWheel(e);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            RenderManager.Render(this, e);

            base.OnPaint(e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            // F2 begins editing
            if (e.KeyCode == Keys.F2 && !read_only && selected_row_index >= 0 && selected_column_index >= 0)
            {
                BeginEdit(selected_row_index, selected_column_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                if (selected_row_index < Rows.Count - 1)
                {
                    SelectedRowIndex = selected_row_index + 1;
                    EnsureRowVisible(selected_row_index);
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.Up)
            {
                if (selected_row_index > 0)
                {
                    SelectedRowIndex = selected_row_index - 1;
                    EnsureRowVisible(selected_row_index);
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.PageDown)
            {
                var new_index = Math.Min(selected_row_index + DisplayedRowCount, Rows.Count - 1);
                SelectedRowIndex = new_index;
                EnsureRowVisible(new_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.PageUp)
            {
                var new_index = Math.Max(selected_row_index - DisplayedRowCount, 0);
                SelectedRowIndex = new_index;
                EnsureRowVisible(new_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                SelectedRowIndex = 0;
                EnsureRowVisible(0);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.End)
            {
                SelectedRowIndex = Rows.Count - 1;
                EnsureRowVisible(Rows.Count - 1);
                e.Handled = true;
                return;
            }

            if (selection_mode != DataGridViewSelectionMode.FullRowSelect)
            {
                if (e.KeyCode == Keys.Left && selected_column_index > 0)
                {
                    SelectedColumnIndex = selected_column_index - 1;
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Right && selected_column_index < Columns.Count - 1)
                {
                    SelectedColumnIndex = selected_column_index + 1;
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Tab)
                {
                    if (e.Shift)
                        NavigateToPreviousCell();
                    else
                        NavigateToNextCell();

                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyUp(e);
        }

        /// <summary>
        /// Called when the row collection changes.
        /// </summary>
        internal void OnRowsChanged()
        {
            OnGridStructureChanged(updateScrollBars: true);
        }

        /// <summary>
        /// Called when the column collection changes.
        /// </summary>
        internal void OnColumnsChanged()
        {
            OnGridStructureChanged(updateScrollBars: true);
        }

        /// <summary>
        /// Gets or sets whether the DataGridView is read-only.
        /// </summary>
        public bool ReadOnly
        {
            get => read_only;
            set
            {
                if (read_only != value)
                {
                    read_only = value;

                    if (read_only)
                        CancelEdit();
                }
            }
        }

        /// <summary>
        /// Gets the collection of rows in the DataGridView.
        /// </summary>
        public DataGridViewRowCollection Rows { get; }

        /// <summary>
        /// Gets or sets the default height, in pixels, of each row.
        /// </summary>
        public int RowHeight
        {
            get => row_height;
            set
            {
                if (row_height != value)
                {
                    row_height = Math.Max(value, 10);
                    UpdateScrollBars();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the row header column is displayed.
        /// </summary>
        public bool RowHeadersVisible
        {
            get => row_headers_visible;
            set
            {
                if (row_headers_visible != value)
                {
                    row_headers_visible = value;
                    UpdateScrollBars();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the width, in pixels, of the row header column.
        /// </summary>
        public int RowHeadersWidth
        {
            get => row_headers_width;
            set
            {
                if (row_headers_width != value)
                {
                    row_headers_width = Math.Max(value, 10);
                    UpdateScrollBars();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets how cells in the DataGridView can be selected.
        /// </summary>
        public DataGridViewSelectionMode SelectionMode
        {
            get => selection_mode;
            set
            {
                if (selection_mode != value)
                {
                    selection_mode = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets the scaled height of the header row.
        /// </summary>
        internal int ScaledHeaderHeight => LogicalToDeviceUnits(header_height);

        /// <summary>
        /// Gets the scaled height of each data row.
        /// </summary>
        internal int ScaledRowHeight => LogicalToDeviceUnits(row_height);

        /// <summary>
        /// Gets the scaled width of the row header column.
        /// </summary>
        internal int ScaledRowHeadersWidth => LogicalToDeviceUnits(row_headers_width);

        /// <summary>
        /// Gets or sets the index of the currently selected column.
        /// </summary>
        public int SelectedColumnIndex
        {
            get => selected_column_index;
            set => SetCurrentCoordinates(selected_row_index, value);
        }

        /// <summary>
        /// Gets or sets the index of the currently selected row.
        /// </summary>
        public int SelectedRowIndex
        {
            get => selected_row_index;
            set => SetCurrentCoordinates(value, selected_column_index);
        }

        /// <summary>
        /// Raises the SelectionChanged event.
        /// </summary>
        protected virtual void OnSelectionChanged(EventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
            NotifyAccessibilityClients(AccessibleEvents.Selection);
        }

        /// <summary>
        /// Raised when the selection changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        // Sets the cursor and immediately updates the OS cursor.
        // Setting Cursor alone only takes effect on next OnMouseEnter, so we
        // must call SetCursor directly to update the cursor during mouse move.
        private void SetCursorDirect(Cursor cursor)
        {
            Cursor = cursor;
            FindForm()?.SetCursor(cursor);
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore(x, y, width, height, specified);

            UpdateScrollBars();
        }

        /// <summary>
        /// Sorts the rows by the specified column.
        /// </summary>
        public void SortByColumn(int columnIndex, SortOrder order)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count || order == SortOrder.None || Rows.Count == 0)
                return;

            // Sort the rows in-place (note: List.Sort is not guaranteed to be stable)
            var sorted = Rows.ToList();

            sorted.Sort((a, b) => {
                var val_a = columnIndex < a.Cells.Count ? a.Cells[columnIndex].Value : string.Empty;
                var val_b = columnIndex < b.Cells.Count ? b.Cells[columnIndex].Value : string.Empty;

                // Try numeric comparison first
                if (double.TryParse(val_a, out var num_a) && double.TryParse(val_b, out var num_b))
                {
                    var cmp = num_a.CompareTo(num_b);
                    return order == SortOrder.Descending ? -cmp : cmp;
                }

                // Fall back to string comparison
                var result = string.Compare(val_a, val_b, StringComparison.CurrentCultureIgnoreCase);
                return order == SortOrder.Descending ? -result : result;
            });

            // Replace rows without triggering per-item change notifications
            Rows.ReplaceAll(sorted);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets the total width needed to display all columns.
        /// </summary>
        internal int TotalColumnsWidth
        {
            get
            {
                var total = 0;

                foreach (var col in Columns)
                    if (col.Visible)
                        total += LogicalToDeviceUnits(col.Width);

                return total;
            }
        }

        /// <summary>
        /// Updates the scrollbars based on the current content.
        /// </summary>
        private void UpdateScrollBars()
        {
            // Start from the full client once. The two bars can force each other to appear;
            // using GetContentArea here would subtract the previously visible bars twice.
            if (updatingGridScrollBars) { gridScrollBarsPending = true; return; }
            updatingGridScrollBars = true;
            gridScrollUpdateDepth++;
            try
            {
                int passCount = 0;
                do
                {
                if (++passCount > 64) throw new InvalidOperationException("Grid scrollbar layout did not stabilize after 64 changes.");
                gridScrollBarsPending = false;
                var client = ClientRectangle;
                long rowsHeight = 0;
                foreach (var row in Rows) rowsHeight += Math.Max(0, LogicalToDeviceUnits(row.Height));
                int headerHeight = ColumnHeadersVisible ? ScaledHeaderHeight : 0;
                int headerWidth = RowHeadersVisible ? ScaledRowHeadersWidth : 0;
                int verticalWidth = (int)Math.Ceiling(vscrollbar.Width * ScaleFactor.Width);
                int horizontalHeight = (int)Math.Ceiling(hscrollbar.Height * ScaleFactor.Height);
                bool horizontal = false, vertical = false;
                int width = 0, height = 0;
                for (int pass = 0; pass < 3; pass++)
                {
                    width = Math.Max(0, client.Width - headerWidth - (vertical ? verticalWidth : 0));
                    height = Math.Max(0, client.Height - headerHeight - (horizontal ? horizontalHeight : 0));
                    horizontal |= TotalColumnsWidth > width;
                    vertical |= rowsHeight > height;
                }
                width = Math.Max(0, client.Width - headerWidth - (vertical ? verticalWidth : 0));
                height = Math.Max(0, client.Height - headerHeight - (horizontal ? horizontalHeight : 0));

                // The last page is aligned to an actual row boundary, including when no full
                // row fits. A single oversized row remains a documented row-scrolling limit.
                int lastTop = Math.Max(0, Rows.Count - 1);
                long trailingHeight = Rows.Count == 0 ? 0 : Math.Max(0, LogicalToDeviceUnits(Rows[lastTop].Height));
                while (lastTop > 0)
                {
                    int preceding = Math.Max(0, LogicalToDeviceUnits(Rows[lastTop - 1].Height));
                    if (trailingHeight + preceding > height) break;
                    trailingHeight += preceding;
                    lastTop--;
                }
                vscrollbar.Visible = vertical;
                vscrollbar.Maximum = vertical ? lastTop : 0;
                vscrollbar.Value = Math.Clamp(vscrollbar.Value, 0, vscrollbar.Maximum);
                top_index = vscrollbar.Value;
                vscrollbar.LargeChange = Math.Max(1, CountDisplayedRows(top_index, height));
                hscrollbar.Visible = horizontal;
                hscrollbar.Maximum = horizontal ? Math.Max(0, TotalColumnsWidth - width) : 0;
                hscrollbar.Value = Math.Clamp(hscrollbar.Value, 0, hscrollbar.Maximum);
                horizontal_scroll_offset = hscrollbar.Value;
                hscrollbar.LargeChange = Math.Max(1, width);
                } while (gridScrollBarsPending && !IsDisposed && !Disposing);
            }
            finally { updatingGridScrollBars = false; gridScrollUpdateDepth--; NotifyAccessibleScrollChanged(); }
        }

        /// <summary>
        /// Gets the number of full rows that can be displayed at a time.
        /// </summary>
        public int DisplayedRowCount
        {
            get
            {
                var content = GetContentArea();
                var available = content.Height - (ColumnHeadersVisible ? ScaledHeaderHeight : 0);
                return CountDisplayedRows(top_index, Math.Max(0, available));
            }
        }

        /// <summary>
        /// Ensures the specified row is visible by scrolling if necessary.
        /// </summary>
        private void EnsureRowVisible(int index)
        {
            if (index < 0 || index >= Rows.Count) return;
            if (index < top_index) { FirstDisplayedScrollingRowIndex = index; return; }
            int available = GridScrollViewport.Height;
            long height = 0;
            for (int row = top_index; row <= index; row++) height += Math.Max(0, LogicalToDeviceUnits(Rows[row].Height));
            if (height <= available) return;
            int target = index;
            height = Math.Max(0, LogicalToDeviceUnits(Rows[index].Height));
            while (target > 0)
            {
                int preceding = Math.Max(0, LogicalToDeviceUnits(Rows[target - 1].Height));
                if (height + preceding > available) break;
                height += preceding;
                target--;
            }
            FirstDisplayedScrollingRowIndex = target;
        }

        // Moves the selection to the next cell, wrapping to the next row.
        private void NavigateToNextCell()
        {
            if (Columns.Count == 0 || Rows.Count == 0)
                return;

            if (selected_column_index < Columns.Count - 1)
            {
                SelectedColumnIndex = selected_column_index + 1;
            }
            else if (selected_row_index < Rows.Count - 1)
            {
                SetCurrentCoordinates(selected_row_index + 1, 0);
                EnsureRowVisible(selected_row_index);
            }
        }

        // Moves the selection to the previous cell, wrapping to the previous row.
        private void NavigateToPreviousCell()
        {
            if (Columns.Count == 0 || Rows.Count == 0)
                return;

            if (selected_column_index > 0)
            {
                SelectedColumnIndex = selected_column_index - 1;
            }
            else if (selected_row_index > 0)
            {
                SetCurrentCoordinates(selected_row_index - 1, Columns.Count - 1);
                EnsureRowVisible(selected_row_index);
            }
        }

        /// <summary>
        /// Converts device units to logical units.
        /// </summary>
        internal int DeviceToLogicalUnits(int value)
        {
            var factor = Scaling;
            return factor > 0 ? (int)(value / factor) : value;
        }
    }

    /// <summary>
    /// Provides data for cell editing events.
    /// </summary>
    public class DataGridViewCellEditEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the DataGridViewCellEditEventArgs class.
        /// </summary>
        public DataGridViewCellEditEventArgs(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        /// <summary>
        /// Gets or sets whether the editing operation should be canceled.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Gets the column index of the cell.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets the row index of the cell.
        /// </summary>
        public int RowIndex { get; }
    }
}
