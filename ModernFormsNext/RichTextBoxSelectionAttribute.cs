namespace ModernFormsNext
{
    /// <summary>
    /// Describes whether all, none, or some of the selected characters share an attribute.
    /// </summary>
    public enum RichTextBoxSelectionAttribute
    {
        /// <summary>
        /// Some but not all selected characters have the attribute.
        /// </summary>
        Mixed = -1,

        /// <summary>
        /// No selected characters have the attribute.
        /// </summary>
        None = 0,

        /// <summary>
        /// All selected characters have the attribute.
        /// </summary>
        All = 1,
    }
}
