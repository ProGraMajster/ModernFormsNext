using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Specifies style information applied to a <see cref="Font"/>.
    /// </summary>
    /// <remarks>
    /// Values can be combined to describe the style selected by APIs such as
    /// <see cref="FontDialog"/>. <see cref="Underline"/> and <see cref="Strikeout"/>
    /// are text effects rather than typeface variants; rendering code must opt in
    /// to drawing those effects when it supports them.
    /// </remarks>
    [Flags]
    public enum FontStyle
    {
        /// <summary>
        /// Normal text without bold, italic, underline, or strikeout effects.
        /// </summary>
        Regular = 0,

        /// <summary>
        /// Bold text.
        /// </summary>
        Bold = 1,

        /// <summary>
        /// Italic text.
        /// </summary>
        Italic = 2,

        /// <summary>
        /// Underlined text.
        /// </summary>
        Underline = 4,

        /// <summary>
        /// Text drawn with a strikeout line.
        /// </summary>
        Strikeout = 8
    }
}
