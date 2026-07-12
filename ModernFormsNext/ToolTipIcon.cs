namespace ModernFormsNext
{
    /// <summary>
    /// Specifies the informational icon displayed by a <see cref="ToolTip"/>.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext renders tooltip icons with SkiaSharp instead of using native operating
    /// system tooltip windows. The values intentionally mirror the familiar Windows Forms names
    /// for source-level migration.
    /// </remarks>
    public enum ToolTipIcon
    {
        /// <summary>
        /// No icon is displayed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Displays an informational icon.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Displays a warning icon.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Displays an error icon.
        /// </summary>
        Error = 3
    }
}
