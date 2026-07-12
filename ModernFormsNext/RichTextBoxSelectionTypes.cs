using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Describes the kind of content selected in a <see cref="RichTextBox"/>.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext currently supports text selections only. Object-related values exist
    /// for API compatibility and are not produced by the shared renderer.
    /// </remarks>
    [Flags]
    public enum RichTextBoxSelectionTypes
    {
        /// <summary>
        /// The current selection is empty.
        /// </summary>
        Empty = 0,

        /// <summary>
        /// The current selection contains text.
        /// </summary>
        Text = 1,

        /// <summary>
        /// The current selection contains an embedded object.
        /// </summary>
        Object = 2,

        /// <summary>
        /// The current selection contains more than one character.
        /// </summary>
        MultiChar = 4,

        /// <summary>
        /// The current selection contains more than one embedded object.
        /// </summary>
        MultiObject = 8,
    }
}
