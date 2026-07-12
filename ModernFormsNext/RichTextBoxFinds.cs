using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how <see cref="RichTextBox.Find(string, RichTextBoxFinds)"/> searches text.
    /// </summary>
    /// <remarks>
    /// The values intentionally mirror the Windows Forms names so migrated code can keep the
    /// same search intent. ModernFormsNext implements the text-search behavior in shared code
    /// and does not use a native RichEdit control.
    /// </remarks>
    [Flags]
    public enum RichTextBoxFinds
    {
        /// <summary>
        /// Searches using the default behavior.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        /// Matches only whole words.
        /// </summary>
        WholeWord = 0x00000002,

        /// <summary>
        /// Matches character casing exactly.
        /// </summary>
        MatchCase = 0x00000004,

        /// <summary>
        /// Returns the match without selecting it in the control.
        /// </summary>
        NoHighlight = 0x00000008,

        /// <summary>
        /// Searches backward from the supplied end position.
        /// </summary>
        Reverse = 0x00000010,
    }
}
