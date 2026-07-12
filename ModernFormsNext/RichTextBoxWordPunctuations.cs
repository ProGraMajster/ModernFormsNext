namespace ModernFormsNext
{
    /// <summary>
    /// Identifies word-break punctuation tables used by rich-edit implementations.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext currently uses the shared <see cref="TextMeasurer"/> word-separator logic.
    /// This enum is exposed for WinForms-style source compatibility.
    /// </remarks>
    public enum RichTextBoxWordPunctuations
    {
        /// <summary>
        /// Uses the default level 1 punctuation table.
        /// </summary>
        Level1 = 0x080,

        /// <summary>
        /// Uses the default level 2 punctuation table.
        /// </summary>
        Level2 = 0x100,

        /// <summary>
        /// Uses a custom punctuation table when supported.
        /// </summary>
        Custom = 0x200,

        /// <summary>
        /// Masks all punctuation table values.
        /// </summary>
        All = Level1 | Level2 | Custom,
    }
}
