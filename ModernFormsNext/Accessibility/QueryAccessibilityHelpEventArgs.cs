using System;

namespace ModernFormsNext.Accessibility;

/// <summary>
/// Provides data for the <see cref="Control.QueryAccessibilityHelp"/> event.
/// </summary>
/// <remarks>
/// Controls and extender providers use this event to supply help metadata to
/// <see cref="AccessibleObject"/> instances and, through platform backends, to
/// assistive technologies.
/// </remarks>
public class QueryAccessibilityHelpEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAccessibilityHelpEventArgs"/> class.
    /// </summary>
    public QueryAccessibilityHelpEventArgs()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAccessibilityHelpEventArgs"/> class.
    /// </summary>
    /// <param name="helpNamespace">The help file, URI, or namespace associated with the object.</param>
    /// <param name="helpString">The help text associated with the object.</param>
    /// <param name="helpKeyword">The keyword or topic identifier associated with the object.</param>
    public QueryAccessibilityHelpEventArgs(string? helpNamespace, string? helpString, string? helpKeyword)
    {
        HelpNamespace = helpNamespace;
        HelpString = helpString;
        HelpKeyword = helpKeyword;
    }

    /// <summary>
    /// Gets or sets the help file, URI, or namespace associated with the object.
    /// </summary>
    public string? HelpNamespace { get; set; }

    /// <summary>
    /// Gets or sets the help text associated with the object.
    /// </summary>
    public string? HelpString { get; set; }

    /// <summary>
    /// Gets or sets the keyword or topic identifier associated with the object.
    /// </summary>
    public string? HelpKeyword { get; set; }
}
