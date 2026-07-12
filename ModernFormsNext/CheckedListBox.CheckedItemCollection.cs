using System;
using System.Collections;
using System.Collections.Generic;

namespace ModernFormsNext
{
    public partial class CheckedListBox
    {
        /// <summary>
        /// Represents the collection of checked items in a <see cref="CheckedListBox"/>.
        /// </summary>
        /// <remarks>
        /// This collection is a read-only live view over <see cref="ListBox.Items"/>. It returns
        /// the same item objects that were added to the list; check state remains owned by the
        /// <see cref="CheckedListBox"/>.
        /// </remarks>
        public sealed class CheckedItemCollection : IReadOnlyList<object>
        {
            private readonly CheckedListBox owner;

            internal CheckedItemCollection(CheckedListBox owner)
            {
                this.owner = owner;
            }

            /// <inheritdoc/>
            public int Count => owner.CheckedIndices.Count;

            /// <summary>
            /// Gets the checked item at the specified position in this view.
            /// </summary>
            /// <param name="index">The zero-based position in the checked-item view.</param>
            /// <returns>The checked item.</returns>
            public object this[int index] => owner.Items[owner.CheckedIndices[index]];

            /// <summary>
            /// Determines whether the specified item is checked.
            /// </summary>
            /// <param name="item">The item to find.</param>
            /// <returns><see langword="true"/> if the item is checked; otherwise, <see langword="false"/>.</returns>
            public bool Contains(object? item) => IndexOf(item) >= 0;

            /// <summary>
            /// Copies checked items to an array.
            /// </summary>
            /// <param name="array">The destination array.</param>
            /// <param name="arrayIndex">The destination array index where copying begins.</param>
            public void CopyTo(object[] array, int arrayIndex)
            {
                ArgumentNullException.ThrowIfNull(array);

                if (arrayIndex < 0 || arrayIndex > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));

                if (array.Length - arrayIndex < Count)
                    throw new ArgumentException("The destination array does not have enough space.", nameof(array));

                foreach (var item in this)
                    array[arrayIndex++] = item;
            }

            /// <inheritdoc/>
            public IEnumerator<object> GetEnumerator()
            {
                foreach (var index in owner.CheckedIndices)
                    yield return owner.Items[index];
            }

            /// <summary>
            /// Gets the position of the specified item in this checked-item view.
            /// </summary>
            /// <param name="item">The item to find.</param>
            /// <returns>The position in this view, or -1 when the item is not checked.</returns>
            public int IndexOf(object? item)
            {
                var checked_position = 0;

                foreach (var index in owner.CheckedIndices)
                {
                    if (Equals(owner.Items[index], item))
                        return checked_position;

                    checked_position++;
                }

                return -1;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
