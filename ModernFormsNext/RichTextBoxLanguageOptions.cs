using System;

namespace ModernFormsNext
{
    /// <summary>
    /// Describes language and IME-related options for <see cref="RichTextBox"/>.
    /// </summary>
    /// <remarks>
    /// The current ModernFormsNext implementation stores this value for source compatibility.
    /// Platform-specific IME behavior remains the responsibility of the backend input layer.
    /// </remarks>
    [Flags]
    public enum RichTextBoxLanguageOptions
    {
        /// <summary>
        /// Automatically changes the keyboard layout when font or insertion point changes.
        /// </summary>
        AutoKeyboard = 0x0001,

        /// <summary>
        /// Automatically changes fonts when the keyboard layout changes.
        /// </summary>
        AutoFont = 0x0002,

        /// <summary>
        /// Keeps the IME composition string when composition is canceled.
        /// </summary>
        ImeCancelComplete = 0x0004,

        /// <summary>
        /// Sends notifications while IME composition is still pending.
        /// </summary>
        ImeAlwaysSendNotify = 0x0008,

        /// <summary>
        /// Scales font-bound sizes according to script.
        /// </summary>
        AutoFontSizeAdjust = 0x0010,

        /// <summary>
        /// Uses framework default UI fonts.
        /// </summary>
        UIFonts = 0x0020,

        /// <summary>
        /// Uses separate Latin and Asian font choices when supported.
        /// </summary>
        DualFont = 0x0080,
    }
}
