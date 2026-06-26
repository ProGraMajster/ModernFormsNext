namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how a masked text box interprets typed input when text already exists at the caret position.
    /// </summary>
    /// <remarks>
    /// The values match WinForms so migration code can keep the same configuration. <see cref="Default"/> uses insert
    /// mode until the user toggles overwrite mode with the Insert key while the control has focus.
    /// </remarks>
    public enum InsertKeyMode
    {
        /// <summary>
        /// Uses the control's default insert behavior.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Inserts new input and shifts existing editable characters to the right when the mask allows it.
        /// </summary>
        Insert = 1,

        /// <summary>
        /// Replaces the editable character at the caret position.
        /// </summary>
        Overwrite = 2
    }
}
