using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a collection of ListViewItems.
    /// </summary>
    public class ListViewItemCollection : Collection<ListViewItem>, INotifyCollectionChanged
    {
        private readonly ListView owner;

        internal ListViewItemCollection (ListView owner)
        {
            this.owner = owner;
        }

        /// <summary>Occurs after an indexed collection mutation has committed on the UI thread.</summary>
        /// <remarks>Used by the shared virtualization host. Values remain ListView-owned metadata; no containers are created by this event itself.</remarks>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>Moves one occurrence without replacing its logical identity.</summary>
        /// <param name="oldIndex">The current occurrence index.</param>
        /// <param name="newIndex">The occurrence's final index.</param>
        /// <remarks>Preserves selection and measured metadata in virtualized mode. Call on the UI thread.</remarks>
        public void Move (int oldIndex, int newIndex)
        {
            owner.VerifyItemMutationAccess ();
            if ((uint)oldIndex >= (uint)Count) throw new ArgumentOutOfRangeException (nameof(oldIndex));
            if ((uint)newIndex >= (uint)Count) throw new ArgumentOutOfRangeException (nameof(newIndex));
            if (oldIndex == newIndex) return;
            var item = this[oldIndex];
            Items.RemoveAt (oldIndex);
            Items.Insert (newIndex, item);
            CollectionChanged?.Invoke (this, new (NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
            owner.Invalidate ();
            owner.NotifyAccessibilityClients (Accessibility.AccessibleEvents.Reorder);
        }

        /// <summary>
        /// Adds a new ListViewItem to the collection with the specified text.
        /// </summary>
        public ListViewItem Add (string text)
        {
            var item = new ListViewItem {
                Text = text
            };

            Add (item);

            return item;
        }

        /// <summary>
        /// Adds a new ListViewItem to the collection with the specified text and image.
        /// </summary>
        public ListViewItem Add (string text, SKBitmap image)
        {
            var item = new ListViewItem {
                Text = text,
                Image = image
            };

            Add (item);

            return item;
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            owner.VerifyItemMutationAccess ();
            bool selectionChanged = Items.Any(item => item.Selected);

            foreach (var item in Items)
                item.Parent = null;

            base.ClearItems ();

            CollectionChanged?.Invoke (this, new (NotifyCollectionChangedAction.Reset));

            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (selectionChanged)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, ListViewItem item)
        {
            owner.VerifyItemMutationAccess ();
            ArgumentNullException.ThrowIfNull (item);
            base.InsertItem (index, item);

            item.Parent = owner;
            CollectionChanged?.Invoke (this, new (NotifyCollectionChangedAction.Add, item, index));
            if (item.Selected) owner.OnItemSelectionChanged (item);
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (item.Selected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Selection);
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            owner.VerifyItemMutationAccess ();
            var item = this[index];
            bool selectionChanged = item.Selected;

            base.RemoveItem (index);

            item.Parent = null;
            CollectionChanged?.Invoke (this, new (NotifyCollectionChangedAction.Remove, item, index));
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (selectionChanged)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, ListViewItem item)
        {
            owner.VerifyItemMutationAccess ();
            ArgumentNullException.ThrowIfNull (item);
            var old_item = this.ElementAtOrDefault (index);
            bool oldSelected = old_item?.Selected == true;

            if (old_item != null)
                old_item.Parent = null;

            base.SetItem (index, item);

            item.Parent = owner;
            CollectionChanged?.Invoke (this, new (NotifyCollectionChangedAction.Replace, item, old_item, index));
            if (item.Selected) owner.OnItemSelectionChanged (item);
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (oldSelected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
            if (item.Selected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Selection);
        }
    }
}
