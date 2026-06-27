using System.ComponentModel;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for print lifecycle events.
    /// </summary>
    public class PrintEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrintEventArgs"/> class.
        /// </summary>
        public PrintEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrintEventArgs"/> class.
        /// </summary>
        /// <param name="cancel">Whether the print operation should be canceled.</param>
        public PrintEventArgs(bool cancel)
            : base(cancel)
        {
        }
    }
}
