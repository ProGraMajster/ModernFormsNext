using System;
using System.ComponentModel;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="CheckedListBox.ItemCheck"/> event.
    /// </summary>
    /// <remarks>
    /// The event is raised before the new value is committed. Handlers can assign
    /// <see cref="NewValue"/> to accept, reject, or alter the pending check-state transition.
    /// </remarks>
    public class ItemCheckEventArgs : EventArgs
    {
        private CheckState new_value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemCheckEventArgs"/> class.
        /// </summary>
        /// <param name="index">The zero-based index of the item whose check state is changing.</param>
        /// <param name="newValue">The proposed new check state.</param>
        /// <param name="currentValue">The current check state before the event is raised.</param>
        public ItemCheckEventArgs(int index, CheckState newValue, CheckState currentValue)
        {
            ValidateCheckState(newValue, nameof(newValue));
            ValidateCheckState(currentValue, nameof(currentValue));

            Index = index;
            new_value = newValue;
            CurrentValue = currentValue;
        }

        /// <summary>
        /// Gets the current check state before the event is applied.
        /// </summary>
        public CheckState CurrentValue { get; }

        /// <summary>
        /// Gets the zero-based index of the item whose check state is changing.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets or sets the check state that will be applied after event handlers run.
        /// </summary>
        /// <remarks>
        /// Assign this property inside an <see cref="CheckedListBox.ItemCheck"/> handler to
        /// cancel a change or replace it with another <see cref="CheckState"/> value.
        /// </remarks>
        public CheckState NewValue
        {
            get => new_value;
            set
            {
                ValidateCheckState(value, nameof(value));
                new_value = value;
            }
        }

        private static void ValidateCheckState(CheckState value, string parameterName)
        {
            if (!Enum.IsDefined(typeof(CheckState), value))
                throw new InvalidEnumArgumentException(parameterName, (int)value, typeof(CheckState));
        }
    }
}
