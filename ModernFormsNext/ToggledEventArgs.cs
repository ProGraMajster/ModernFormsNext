using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for <see cref="Switch.Toggled"/>.
    /// </summary>
    public class ToggledEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToggledEventArgs"/> class.
        /// </summary>
        /// <param name="value">The Boolean switch value.</param>
        public ToggledEventArgs(bool value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the Boolean switch value after the change.
        /// </summary>
        public bool Value { get; }
    }
}
