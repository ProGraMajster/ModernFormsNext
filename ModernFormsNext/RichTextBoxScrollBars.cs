namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how a <see cref="RichTextBox"/> displays scroll bars.
    /// </summary>
    /// <remarks>
    /// Forced values are mapped to the existing ModernFormsNext scroll bar controls. They are
    /// retained even though the current shared text engine only performs automatic vertical
    /// scrolling.
    /// </remarks>
    public enum RichTextBoxScrollBars
    {
        /// <summary>
        /// No scroll bars are shown.
        /// </summary>
        None = 0,

        /// <summary>
        /// Shows a horizontal scroll bar when supported.
        /// </summary>
        Horizontal = 0x0001,

        /// <summary>
        /// Shows a vertical scroll bar.
        /// </summary>
        Vertical = 0x0002,

        /// <summary>
        /// Shows both scroll bars when supported.
        /// </summary>
        Both = Horizontal | Vertical,

        /// <summary>
        /// Requests an always-visible horizontal scroll bar.
        /// </summary>
        ForcedHorizontal = 0x0010 | Horizontal,

        /// <summary>
        /// Requests an always-visible vertical scroll bar.
        /// </summary>
        ForcedVertical = 0x0010 | Vertical,

        /// <summary>
        /// Requests both scroll bars to be always visible.
        /// </summary>
        ForcedBoth = ForcedHorizontal | ForcedVertical,
    }
}
