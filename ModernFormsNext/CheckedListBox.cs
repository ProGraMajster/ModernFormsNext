using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a list box that displays a check box next to each item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CheckedListBox"/> reuses the normal <see cref="ListBox.Items"/> collection,
    /// scrolling, hit testing, focus, and selection behavior. Check state is stored separately
    /// from the objects in <see cref="ListBox.Items"/>, so callers can add strings or arbitrary
    /// model objects without wrapping them in a special item type.
    /// </para>
    /// <para>
    /// The control is platform-neutral and rendered by ModernFormsNext through SkiaSharp. It
    /// does not create native WinForms controls.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var checkedListBox = new CheckedListBox();
    ///
    /// checkedListBox.Items.Add("Read");
    /// checkedListBox.Items.Add("Write");
    /// checkedListBox.Items.Add("Admin");
    ///
    /// checkedListBox.SetItemChecked(0, true);
    /// checkedListBox.SetItemCheckState(2, CheckState.Indeterminate);
    /// </code>
    /// </example>
    public partial class CheckedListBox : ListBox
    {
        internal const int CheckBoxGlyphSize = 15;
        internal const int CheckBoxHorizontalPadding = 4;
        internal const int CheckBoxTextPadding = 5;

        private readonly List<CheckState> check_states = new List<CheckState>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckedListBox"/> class.
        /// </summary>
        public CheckedListBox()
        {
            CheckedItems = new CheckedItemCollection(this);
            CheckedIndices = new CheckedIndexCollection(this);

            Items.CollectionChanged += Items_CollectionChanged;
        }

        /// <summary>
        /// Gets the collection of checked item indexes.
        /// </summary>
        /// <remarks>
        /// The collection is a live view over <see cref="ListBox.Items"/>. It includes items
        /// whose state is <see cref="CheckState.Checked"/> or
        /// <see cref="CheckState.Indeterminate"/>, matching
        /// <see cref="GetItemChecked(int)"/>.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CheckedIndexCollection CheckedIndices { get; }

        /// <summary>
        /// Gets the collection of checked items.
        /// </summary>
        /// <remarks>
        /// The collection is a live view over <see cref="ListBox.Items"/>. It includes items
        /// whose state is <see cref="CheckState.Checked"/> or
        /// <see cref="CheckState.Indeterminate"/>. The original item objects are returned
        /// unchanged; check state is stored by the control, not by the items.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CheckedItemCollection CheckedItems { get; }

        /// <summary>
        /// Gets or sets a value indicating whether clicking an item toggles its check state.
        /// </summary>
        /// <remarks>
        /// When this property is <see langword="false"/>, clicking the check box glyph toggles
        /// the state, while clicking item text first follows the normal <see cref="ListBox"/>
        /// selection behavior. Re-clicking the selected item toggles its state, matching the
        /// familiar WinForms interaction. When this property is <see langword="true"/>, clicking
        /// anywhere on an item toggles the check state after the normal item hit test succeeds.
        /// Touch input is routed through the same pointer path as mouse input.
        /// </remarks>
        [DefaultValue(false)]
        public bool CheckOnClick { get; set; }

        /// <summary>
        /// Occurs before an item's check state changes.
        /// </summary>
        /// <remarks>
        /// The event is raised for programmatic changes and user interaction. Handlers can
        /// inspect <see cref="ItemCheckEventArgs.CurrentValue"/> and assign
        /// <see cref="ItemCheckEventArgs.NewValue"/> before the control commits the state.
        /// </remarks>
        public event ItemCheckEventHandler? ItemCheck;

        /// <summary>
        /// Gets a value indicating whether the item at the specified index is checked.
        /// </summary>
        /// <param name="index">The zero-based index of the item to inspect.</param>
        /// <returns>
        /// <see langword="true"/> when the item is <see cref="CheckState.Checked"/> or
        /// <see cref="CheckState.Indeterminate"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public bool GetItemChecked(int index) => GetItemCheckState(index) != CheckState.Unchecked;

        /// <summary>
        /// Gets the check state of the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to inspect.</param>
        /// <returns>The item's current <see cref="CheckState"/>.</returns>
        public CheckState GetItemCheckState(int index)
        {
            ValidateItemIndex(index);
            EnsureCheckStateCount();

            return check_states[index];
        }

        /// <summary>
        /// Sets whether the item at the specified index is checked.
        /// </summary>
        /// <param name="index">The zero-based index of the item to change.</param>
        /// <param name="value">
        /// <see langword="true"/> to set the item to <see cref="CheckState.Checked"/>;
        /// <see langword="false"/> to set it to <see cref="CheckState.Unchecked"/>.
        /// </param>
        public void SetItemChecked(int index, bool value)
            => SetItemCheckState(index, value ? CheckState.Checked : CheckState.Unchecked);

        /// <summary>
        /// Sets the check state of the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to change.</param>
        /// <param name="value">The new <see cref="CheckState"/> to apply.</param>
        /// <remarks>
        /// This method raises <see cref="ItemCheck"/> before the value is stored. If a handler
        /// changes <see cref="ItemCheckEventArgs.NewValue"/>, the handler-supplied value is
        /// committed instead.
        /// </remarks>
        public void SetItemCheckState(int index, CheckState value)
        {
            ValidateItemIndex(index);
            ValidateCheckState(value, nameof(value));
            EnsureCheckStateCount();

            var current_value = check_states[index];

            if (current_value == value)
                return;

            var args = new ItemCheckEventArgs(index, value, current_value);
            OnItemCheck(args);

            if (args.NewValue == current_value)
                return;

            check_states[index] = args.NewValue;

            NotifyAccessibilityClients(AccessibleEvents.StateChange);
            NotifyAccessibilityClients(AccessibleEvents.ValueChange);
            Invalidate();
        }

        internal Rectangle GetItemCheckRectangle(int index)
        {
            var bounds = GetItemRectangle(index);
            var glyph_size = LogicalToDeviceUnits(CheckBoxGlyphSize);
            var left_padding = LogicalToDeviceUnits(CheckBoxHorizontalPadding);
            var top = bounds.Top + Math.Max(0, (ScaledItemHeight - glyph_size) / 2);

            return new Rectangle(bounds.Left + left_padding, top, glyph_size, glyph_size);
        }

        internal Rectangle GetItemTextRectangle(int index)
        {
            var bounds = GetItemRectangle(index);
            var check_bounds = GetItemCheckRectangle(index);
            var text_padding = LogicalToDeviceUnits(CheckBoxTextPadding);
            var text_left = check_bounds.Right + text_padding;

            bounds.Height = ScaledItemHeight;
            bounds.X = text_left;
            bounds.Width = Math.Max(0, bounds.Right - text_left - LogicalToDeviceUnits(CheckBoxHorizontalPadding));

            return bounds;
        }

        /// <inheritdoc/>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && Items.Count > 0)
            {
                var index = GetKeyboardCheckIndex();

                if (index >= 0)
                {
                    ToggleItemCheckState(index);
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyUp(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var check_index = GetPointerCheckIndex(e);

            base.OnMouseDown(e);

            if (!SelectItemOnMouseUp && check_index >= 0)
                ToggleItemCheckState(check_index);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            var check_index = GetPointerCheckIndex(e);

            base.OnMouseUp(e);

            if (SelectItemOnMouseUp && check_index >= 0)
                ToggleItemCheckState(check_index);
        }

        /// <summary>
        /// Raises the <see cref="ItemCheck"/> event.
        /// </summary>
        /// <param name="e">The event data describing the pending check-state change.</param>
        protected virtual void OnItemCheck(ItemCheckEventArgs e) => ItemCheck?.Invoke(this, e);

        private static void ValidateCheckState(CheckState value, string parameterName)
        {
            if (!Enum.IsDefined(typeof(CheckState), value))
                throw new InvalidEnumArgumentException(parameterName, (int)value, typeof(CheckState));
        }

        private void EnsureCheckStateCount()
        {
            while (check_states.Count < Items.Count)
                check_states.Add(CheckState.Unchecked);

            while (check_states.Count > Items.Count)
                check_states.RemoveAt(check_states.Count - 1);
        }

        private int GetKeyboardCheckIndex()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
                return SelectedIndex;

            var focused_index = Items.FocusedIndex;
            return focused_index >= 0 && focused_index < Items.Count ? focused_index : -1;
        }

        private int GetPointerCheckIndex(MouseEventArgs e)
        {
            if (!Enabled || !e.Button.HasFlag(MouseButtons.Left))
                return -1;

            var index = GetIndexAtLocation(e.Location);

            if (index < 0)
                return -1;

            if (CheckOnClick || GetItemCheckRectangle(index).Contains(e.Location) || Items.SelectedIndexes.Contains(index))
                return index;

            return -1;
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Check state is index-backed, but it represents the item occupying that slot before
            // the collection mutation. Mirror each item mutation so inserted items begin
            // unchecked, removed items drop their state, and moved items carry their state with
            // them instead of leaking it to a different item after indexes shift.
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertDefaultStates(e.NewStartingIndex, e.NewItems?.Count ?? 0);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveStates(e.OldStartingIndex, e.OldItems?.Count ?? 0);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveStates(e.OldStartingIndex, e.OldItems?.Count ?? 0);
                    InsertDefaultStates(e.NewStartingIndex, e.NewItems?.Count ?? 0);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MoveStates(e.OldStartingIndex, e.NewStartingIndex, e.OldItems?.Count ?? 0);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    check_states.Clear();
                    InsertDefaultStates(0, Items.Count);
                    break;
            }

            EnsureCheckStateCount();
            Invalidate();
        }

        private void InsertDefaultStates(int index, int count)
        {
            if (count <= 0)
                return;

            index = Math.Clamp(index, 0, check_states.Count);

            for (var i = 0; i < count; i++)
                check_states.Insert(index + i, CheckState.Unchecked);
        }

        private void MoveStates(int oldIndex, int newIndex, int count)
        {
            if (count <= 0 || oldIndex < 0 || oldIndex >= check_states.Count)
                return;

            count = Math.Min(count, check_states.Count - oldIndex);
            var moved_states = check_states.GetRange(oldIndex, count);
            check_states.RemoveRange(oldIndex, count);

            if (newIndex > oldIndex)
                newIndex -= count;

            newIndex = Math.Clamp(newIndex, 0, check_states.Count);
            check_states.InsertRange(newIndex, moved_states);
        }

        private void RemoveStates(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= check_states.Count)
                return;

            check_states.RemoveRange(index, Math.Min(count, check_states.Count - index));
        }

        private void ToggleItemCheckState(int index)
        {
            var current_value = GetItemCheckState(index);
            var new_value = current_value == CheckState.Unchecked ? CheckState.Checked : CheckState.Unchecked;

            SetItemCheckState(index, new_value);
        }

        private void ValidateItemIndex(int index)
        {
            if (index < 0 || index >= Items.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range.");
        }
    }
}
