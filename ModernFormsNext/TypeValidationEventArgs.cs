using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="MaskedTextBox.TypeValidationCompleted"/> event.
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="MaskedTextBox.ValidateText"/> after the current formatted text has been parsed
    /// using <see cref="MaskedTextBox.ValidatingType"/>. Set <see cref="Cancel"/> in an event handler when a caller should
    /// treat the validation result as rejected.
    /// </remarks>
    public class TypeValidationEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeValidationEventArgs"/> class.
        /// </summary>
        /// <param name="validatingType">The type used to validate the text.</param>
        /// <param name="isValidInput"><see langword="true"/> when conversion succeeded; otherwise, <see langword="false"/>.</param>
        /// <param name="returnValue">The converted value, or <see langword="null"/> when conversion failed.</param>
        /// <param name="message">The conversion error message, or an empty string when conversion succeeded.</param>
        public TypeValidationEventArgs (Type? validatingType, bool isValidInput, object? returnValue, string? message)
        {
            ValidatingType = validatingType;
            IsValidInput = isValidInput;
            ReturnValue = returnValue;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether validation should be treated as canceled by the caller.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current text was converted successfully.
        /// </summary>
        public bool IsValidInput { get; }

        /// <summary>
        /// Gets the conversion error message, or an empty string when conversion succeeded.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the converted value, or <see langword="null"/> when conversion failed.
        /// </summary>
        public object? ReturnValue { get; }

        /// <summary>
        /// Gets the type used to validate the text.
        /// </summary>
        public Type? ValidatingType { get; }
    }
}
