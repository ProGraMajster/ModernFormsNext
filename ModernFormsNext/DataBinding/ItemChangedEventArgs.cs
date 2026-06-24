using System;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Provides data for the <see cref="CurrencyManager.ItemChanged"/> event.
    /// </summary>
    /// <remarks>
    ///  <see cref="Index"/> is the zero-based index of the changed item. A value of -1 represents
    ///  a reset where listeners should refresh their view of the whole list.
    /// </remarks>
    public class ItemChangedEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="ItemChangedEventArgs"/> class.
        /// </summary>
        /// <param name="index">The zero-based item index, or -1 for a reset notification.</param>
        public ItemChangedEventArgs(int index)
        {
            Index = index;
        }

        /// <summary>
        ///  Gets the zero-based index of the changed item, or -1 when the whole list should be refreshed.
        /// </summary>
        public int Index { get; }
    }
}
