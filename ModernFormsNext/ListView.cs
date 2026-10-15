using System;
using System.Drawing;
using System.Linq;
using System.ComponentModel;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a ListView control.
    /// Note the ListView control has not been fully developed, and probably does not contain enough functionality to be useful yet.
    /// </summary>
    public class ListView : ItemsControl
    {
        /// <summary>
        /// Initializes a new instance of the ListView class.
        /// </summary>
        public ListView ()
        {
            VirtualizationEnabled = false;
            Items = new ListViewItemCollection (this);
            ItemSize = new Size (76, 76);
            ItemsLayout = ItemsLayout.Wrap;
            SetItemsSource (Items);
        }

        private ListViewItem? logicalSelectedItem;
        private bool updatingItemSelection;
        private long logicalSelectionVersion;

        /// <summary>Gets or sets bounded shared item virtualization for this list.</summary>
        /// <remarks>
        /// Opt in for large collections. Existing Items, SelectedItem and renderer overrides remain available;
        /// logical single selection, canonical scrollbars and keyboard navigation replace the legacy drawn grid.
        /// ItemSize includes spacing. Distinct ListViewItem instances represent distinct rows; use an ItemsControl
        /// template when equal source values or repeated object references represent separate occurrences.
        /// Turning this off retains the original non-scrolling renderer behavior.
        /// </remarks>
        [DefaultValue(false)]
        public override bool VirtualizationEnabled
        {
            get => base.VirtualizationEnabled;
            set
            {
                VerifyItemMutationAccess();
                if (base.VirtualizationEnabled == value) return;
                // Legacy lists may have several selected flags. Snapshot only those flags once
                // on opt-in; normal scrolling never scans the source.
                var legacySelection = value && Items is not null
                    ? Items.Where (item => item.Selected).ToArray () : Array.Empty<ListViewItem> ();
                var selected = legacySelection.Length > 0 ? legacySelection[0] : null;
                long selectionVersion = logicalSelectionVersion;
                base.VirtualizationEnabled = value;
                if (value && base.VirtualizationEnabled && !IsDisposed && selectionVersion == logicalSelectionVersion)
                    SelectedIndex = selected is { Selected: true } && ReferenceEquals (selected.Parent, this)
                        ? Items!.IndexOf (selected) : -1;
                foreach (var item in legacySelection) {
                    if (!base.VirtualizationEnabled || IsDisposed) break;
                    // A template or selection notification may have committed a newer selection
                    // or moved an item to another owner. Preserve that live state while retiring
                    // stale legacy flags, including when the selected item was already true.
                    if (!ReferenceEquals (item, logicalSelectedItem) && ReferenceEquals (item.Parent, this))
                        item.Selected = false;
                }
                if (value && base.VirtualizationEnabled && !IsDisposed && logicalSelectedItem is { } current &&
                    ReferenceEquals (current.Parent, this)) current.Selected = true;
            }
        }

        /// <inheritdoc/>
        [DefaultValue(typeof(Size), "76, 76")]
        public override Size ItemSize { get => base.ItemSize; set => base.ItemSize = value; }

        /// <inheritdoc/>
        [DefaultValue(ItemsLayout.Wrap)]
        public override ItemsLayout ItemsLayout { get => base.ItemsLayout; set => base.ItemsLayout = value; }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (450, 450);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle, 
            (style) => style.BackgroundColor = Theme.ControlLowColor);

        /// <summary>
        /// Raised when a list view item is double-clicked.
        /// </summary>
        public event EventHandler<EventArgs<ListViewItem>>? ItemDoubleClicked;

        /// <summary>
        /// Gets the collection of items contained by this ListView.
        /// </summary>
        public ListViewItemCollection Items { get; }

        // Lays out the ListViewItems.
        private void LayoutItems ()
        {
            var bounds = PaddedClientRectangle;
            var item_size = LogicalToDeviceUnits (70);
            var item_margin = LogicalToDeviceUnits (6);

            var x = bounds.Left;
            var y = bounds.Top;

            foreach (var item in Items) {
                item.SetBounds (x, y, item_size, item_size);
                x += item_size + item_margin;

                if (x + item_size > bounds.Width) {
                    x = bounds.Left;
                    y += item_size + item_margin;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            base.OnClick (e);

            var clicked_item = FindItemAt (e.Location);

            SelectedItem = clicked_item;
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            var clicked_item = FindItemAt (e.Location);

            if (clicked_item != null)
                ItemDoubleClicked?.Invoke (this, new EventArgs<ListViewItem> (clicked_item));
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            if (!VirtualizationEnabled) LayoutItems ();
            // ScrollableControl dispatches the most-derived renderer after painting children.
            // Legacy geometry must be ready first; a second dispatch would composite images twice.
            base.OnPaint (e);
        }

        /// <summary>
        /// Gets or sets the currently selected item, if any. If there are multiple selected items, the first selected item will be returned.
        /// </summary>
        public ListViewItem? SelectedItem {
            get => VirtualizationEnabled ? logicalSelectedItem : Items.FirstOrDefault (i => i.Selected);
            set {
                if (VirtualizationEnabled) {
                    VerifyItemMutationAccess ();
                    if (value is not null && !Items.Contains (value))
                        throw new ArgumentException ("The selected item must belong to this ListView.", nameof(value));
                    // An explicit same-index choice during a mode/template callback still owns
                    // the current selection; an older opt-in must not restore its legacy snapshot.
                    logicalSelectionVersion++;
                    SelectedIndex = value is null ? -1 : Items.IndexOf (value);
                    return;
                }
                var current_item = Items.FirstOrDefault (i => i.Selected);

                if (current_item == value)
                    return;

                if (current_item != null)
                    current_item.Selected = false;

                if (value != null)
                    value.Selected = true;

                Invalidate ();
                NotifyAccessibilityClients(Accessibility.AccessibleEvents.ValueChange);
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        private ListViewItem? FindItemAt (Point point)
        {
            if (!VirtualizationEnabled) return Items.FirstOrDefault (item => item.Bounds.Contains (point));
            var range = RealizedRange;
            for (int index = range.StartIndex; index < range.EndIndex; index++)
                if (index < Items.Count && Items[index].Bounds.Contains (point)) return Items[index];
            return null;
        }

        /// <inheritdoc/>
        protected override Control CreateItemContainer () => new ListItemContainer ();

        /// <inheritdoc/>
        protected override void PrepareItemContainer (Control container, object? item, int index) { }

        /// <inheritdoc/>
        protected override void ClearItemContainer (Control container, object? item, int index)
        {
            if (item is ListViewItem oldItem) oldItem.SetBounds (0, 0, 0, 0);
        }

        /// <inheritdoc/>
        protected override void OnItemContainerArranged (Control container, object? item, int index)
        {
            if (item is not ListViewItem listItem) return;
            var logical = container.Bounds;
            var bounds = new Rectangle (LogicalToDeviceUnits (logical.X), LogicalToDeviceUnits (logical.Y),
                LogicalToDeviceUnits (logical.Width), LogicalToDeviceUnits (logical.Height));
            int spacing = LogicalToDeviceUnits (6);
            listItem.SetBounds (bounds.X, bounds.Y, Math.Max (0, bounds.Width - spacing), Math.Max (0, bounds.Height - spacing));
        }

        /// <inheritdoc/>
        protected override void OnItemInvoked (int index, bool doubleClick)
        {
            base.OnItemInvoked (index, doubleClick);
            if (doubleClick && index < Items.Count)
                ItemDoubleClicked?.Invoke (this, new EventArgs<ListViewItem> (Items[index]));
        }

        /// <inheritdoc/>
        protected override void OnSelectedIndexChanged (EventArgs e)
        {
            logicalSelectionVersion++;
            var previous = logicalSelectedItem;
            logicalSelectedItem = SelectedIndex >= 0 && Items is not null && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;
            updatingItemSelection = true;
            try {
                if (previous is not null && !ReferenceEquals (previous, logicalSelectedItem)) previous.Selected = false;
                if (logicalSelectedItem is not null) logicalSelectedItem.Selected = true;
            } finally { updatingItemSelection = false; }
            base.OnSelectedIndexChanged (e);
            NotifyAccessibilityClients (Accessibility.AccessibleEvents.ValueChange);
        }

        internal void OnItemSelectionChanged (ListViewItem item)
        {
            if (!VirtualizationEnabled || updatingItemSelection || IsDisposed) return;
            logicalSelectionVersion++;
            int index = Items.IndexOf (item);
            if (item.Selected && index >= 0) SelectedIndex = index;
            else if (!item.Selected && ReferenceEquals (logicalSelectedItem, item)) SelectedIndex = -1;
        }

        private sealed class ListItemContainer : Control
        {
            internal ListItemContainer ()
            {
                SetControlBehavior (ControlBehaviors.Transparent, true);
                SetControlBehavior (ControlBehaviors.Selectable, true);
                Margin = Padding.Empty;
                // ListView already exposes stable logical item peers. The recycled hit/focus control
                // is implementation detail, not a second public accessibility item.
                AccessibilityView = Accessibility.AccessibilityView.Hidden;
            }
        }
    }
}
