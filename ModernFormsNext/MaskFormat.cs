namespace ModernFormsNext
{
    /// <summary>
    /// Specifies whether prompt characters and literal characters are included when masked text is returned.
    /// </summary>
    /// <remarks>
    /// This enum follows the WinForms <c>MaskFormat</c> values so code that configures masked text formatting can be
    /// migrated with minimal changes.
    /// </remarks>
    public enum MaskFormat
    {
        /// <summary>
        /// Excludes prompt characters and literal characters from the returned text.
        /// </summary>
        ExcludePromptAndLiterals = 0,

        /// <summary>
        /// Includes prompt characters and excludes literal characters from the returned text.
        /// </summary>
        IncludePrompt = 1,

        /// <summary>
        /// Includes literal characters and excludes prompt characters from the returned text.
        /// </summary>
        IncludeLiterals = 2,

        /// <summary>
        /// Includes both prompt characters and literal characters in the returned text.
        /// </summary>
        IncludePromptAndLiterals = 3
    }
}
