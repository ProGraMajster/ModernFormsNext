using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for <see cref="Switch.ValueChanged"/>.
    /// </summary>
    public class SwitchValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchValueChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldValue">The previous switch value.</param>
        /// <param name="newValue">The new switch value.</param>
        public SwitchValueChangedEventArgs(int oldValue, int newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// Gets the previous switch value.
        /// </summary>
        public int OldValue { get; }

        /// <summary>
        /// Gets the new switch value.
        /// </summary>
        public int NewValue { get; }
    }
}
