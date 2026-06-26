using System;

namespace ModernFormsNext.Help
{
    /// <summary>
    /// Specifies the location or topic that should be opened by the help system.
    /// </summary>
    public enum HelpNavigator
    {
        /// <summary>
        ///  Displays a specific topic in the help target.
        /// </summary>
        Topic = unchecked((int)0x80000001),

        /// <summary>
        ///  Displays the table of contents for the help target.
        /// </summary>
        TableOfContents = unchecked((int)0x80000002),

        /// <summary>
        ///  Displays the index for the help target.
        /// </summary>
        Index = unchecked((int)0x80000003),

        /// <summary>
        ///  Displays the search page for the help target.
        /// </summary>
        Find = unchecked((int)0x80000004),

        /// <summary>
        ///  Displays a topic associated with the supplied keyword.
        /// </summary>
        AssociateIndex = unchecked((int)0x80000005),

        /// <summary>
        ///  Displays the keyword index for the help target.
        /// </summary>
        KeywordIndex = unchecked((int)0x80000006),

        /// <summary>
        ///  Displays a topic referenced by a topic identifier.
        /// </summary>
        TopicId = unchecked((int)0x80000007)
    }
}
