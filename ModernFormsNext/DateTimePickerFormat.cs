namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how the <see cref="DateTimePicker"/> displays its value.
    /// </summary>
    public enum DateTimePickerFormat
    {
        /// <summary>
        /// Displays the date using the current culture long date pattern.
        /// </summary>
        Long = 1,

        /// <summary>
        /// Displays the date using the current culture short date pattern.
        /// </summary>
        Short = 2,

        /// <summary>
        /// Displays the time using the current culture short time pattern.
        /// </summary>
        Time = 4,

        /// <summary>
        /// Displays the value using <see cref="DateTimePicker.CustomFormat"/>.
        /// </summary>
        Custom = 8
    }
}
