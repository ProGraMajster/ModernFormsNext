using System;
using System.Collections;
using System.Collections.Generic;

namespace ModernFormsNext
{
    public partial class CheckedListBox
    {
        /// <summary>
        /// Represents the collection of indexes whose items are checked in a
        /// <see cref="CheckedListBox"/>.
        /// </summary>
        /// <remarks>
        /// This collection is a read-only live view. It includes items whose state is
        /// <see cref="CheckState.Checked"/> or <see cref="CheckState.Indeterminate"/>.
        /// </remarks>
        public sealed class CheckedIndexCollection : IReadOnlyList<int>
        {
            private readonly CheckedListBox owner;

            internal CheckedIndexCollection(CheckedListBox owner)
            {
                this.owner = owner;
            }

            /// <inheritdoc/>
            public int Count
            {
                get
                {
                    var count = 0;

                    for (var i = 0; i < owner.Items.Count; i++)
                    {
                        if (owner.GetItemChecked(i))
                            count++;
                    }

                    return count;
                }
            }

            /// <summary>
            /// Gets the checked item index at the specified position in this view.
            /// </summary>
            /// <param name="index">The zero-based position in the checked-index view.</param>
            /// <returns>The item index in the owning <see cref="CheckedListBox"/>.</returns>
            public int this[int index]
            {
                get
                {
                    var checked_index = GetCheckedItemIndex(index);

                    if (checked_index < 0)
                        throw new ArgumentOutOfRangeException(nameof(index), "Index out of range.");

                    return checked_index;
                }
            }

            /// <summary>
            /// Determines whether the specified item index is checked.
            /// </summary>
            /// <param name="index">The item index to find.</param>
            /// <returns><see langword="true"/> if the item is checked; otherwise, <see langword="false"/>.</returns>
            public bool Contains(int index)
                => index >= 0 && index < owner.Items.Count && owner.GetItemChecked(index);

            /// <summary>
            /// Copies checked item indexes to an array.
            /// </summary>
            /// <param name="array">The destination array.</param>
            /// <param name="arrayIndex">The destination array index where copying begins.</param>
            public void CopyTo(int[] array, int arrayIndex)
            {
                ArgumentNullException.ThrowIfNull(array);

                if (arrayIndex < 0 || arrayIndex > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));

                if (array.Length - arrayIndex < Count)
                    throw new ArgumentException("The destination array does not have enough space.", nameof(array));

                foreach (var index in this)
                    array[arrayIndex++] = index;
            }

            /// <inheritdoc/>
            public IEnumerator<int> GetEnumerator()
            {
                for (var i = 0; i < owner.Items.Count; i++)
                {
                    if (owner.GetItemChecked(i))
                        yield return i;
                }
            }

            /// <summary>
            /// Gets the position of the specified item index in this checked-index view.
            /// </summary>
            /// <param name="index">The item index to find.</param>
            /// <returns>The position in this view, or -1 when the item is not checked.</returns>
            public int IndexOf(int index)
            {
                var checked_position = 0;

                for (var i = 0; i < owner.Items.Count; i++)
                {
                    if (!owner.GetItemChecked(i))
                        continue;

                    if (i == index)
                        return checked_position;

                    checked_position++;
                }

                return -1;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private int GetCheckedItemIndex(int checkedViewIndex)
            {
                if (checkedViewIndex < 0)
                    return -1;

                var checked_position = 0;

                for (var i = 0; i < owner.Items.Count; i++)
                {
                    if (!owner.GetItemChecked(i))
                        continue;

                    if (checked_position == checkedViewIndex)
                        return i;

                    checked_position++;
                }

                return -1;
            }
        }
    }
}
