using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a collection of <see cref="NotifyIconMenuItem"/> instances.
    /// </summary>
    public class NotifyIconMenuItemCollection : Collection<NotifyIconMenuItem>
    {
        private readonly NotifyIconMenuItem? owner;

        internal NotifyIconMenuItemCollection (NotifyIconMenuItem? owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds an existing item to the collection.
        /// </summary>
        /// <typeparam name="T">The concrete menu item type.</typeparam>
        /// <param name="item">The item to add.</param>
        /// <returns>The added item.</returns>
        public T Add<T> (T item) where T : NotifyIconMenuItem
        {
            ArgumentNullException.ThrowIfNull (item);

            base.Add (item);
            return item;
        }

        /// <summary>
        /// Adds a new clickable item with the specified text.
        /// </summary>
        /// <param name="text">The text displayed by the tray context menu.</param>
        /// <param name="onClick">The optional handler invoked when the item is selected.</param>
        /// <returns>The newly created item.</returns>
        public NotifyIconMenuItem Add (string text, EventHandler? onClick = null)
            => Add (new NotifyIconMenuItem (text, onClick));

        /// <summary>
        /// Adds a separator item to the collection.
        /// </summary>
        /// <returns>The newly created separator item.</returns>
        public NotifyIconMenuSeparatorItem AddSeparator ()
            => Add (new NotifyIconMenuSeparatorItem ());

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var item in this)
                item.Parent = null;

            base.ClearItems ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, NotifyIconMenuItem item)
        {
            ArgumentNullException.ThrowIfNull (item);

            base.InsertItem (index, item);
            item.Parent = owner;
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            var item = this[index];

            base.RemoveItem (index);
            item.Parent = null;
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, NotifyIconMenuItem item)
        {
            ArgumentNullException.ThrowIfNull (item);

            var old_item = this.ElementAtOrDefault (index);

            if (old_item is not null)
                old_item.Parent = null;

            base.SetItem (index, item);
            item.Parent = owner;
        }
    }
}
