using System;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a collection of ListViewItems.
    /// </summary>
    public class ListViewItemCollection : Collection<ListViewItem>
    {
        private readonly ListView owner;

        internal ListViewItemCollection (ListView owner)
        {
            this.owner = owner;
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
            bool selectionChanged = Items.Any(item => item.Selected);

            foreach (var item in Items)
                item.Parent = null;

            base.ClearItems ();

            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (selectionChanged)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, ListViewItem item)
        {
            base.InsertItem (index, item);

            item.Parent = owner;
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (item.Selected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Selection);
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            var item = this[index];
            bool selectionChanged = item.Selected;

            base.RemoveItem (index);

            item.Parent = null;
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (selectionChanged)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, ListViewItem item)
        {
            var old_item = this.ElementAtOrDefault (index);
            bool oldSelected = old_item?.Selected == true;

            if (old_item != null)
                old_item.Parent = null;

            base.SetItem (index, item);

            item.Parent = owner;
            owner.Invalidate ();
            owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Reorder);
            if (oldSelected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.SelectionRemove);
            if (item.Selected)
                owner.NotifyAccessibilityClients(Accessibility.AccessibleEvents.Selection);
        }
    }
}
