using System;
using System.ComponentModel;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="MaskedTextBox.MaskInputRejected"/> event.
    /// </summary>
    /// <remarks>
    /// The event is raised when typed or pasted input cannot be accepted by the current mask. The
    /// <see cref="RejectionHint"/> value comes from <see cref="MaskedTextProvider"/> and identifies the validation rule
    /// that failed.
    /// </remarks>
    public class MaskInputRejectedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaskInputRejectedEventArgs"/> class.
        /// </summary>
        /// <param name="position">The mask position at which input was rejected.</param>
        /// <param name="rejectionHint">The provider result that explains why input was rejected.</param>
        public MaskInputRejectedEventArgs (int position, MaskedTextResultHint rejectionHint)
        {
            Position = position;
            RejectionHint = rejectionHint;
        }

        /// <summary>
        /// Gets the mask position at which input was rejected.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// Gets the provider result that explains why input was rejected.
        /// </summary>
        public MaskedTextResultHint RejectionHint { get; }
    }
}
