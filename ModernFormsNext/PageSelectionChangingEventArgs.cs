using System.ComponentModel;

namespace ModernFormsNext;

/// <summary>Describes a proposed tab selection before canonical focus admission or presentation changes.</summary>
/// <remarks>Handlers run on the UI thread. Cancel retains the previous selection; a newer selection wins.</remarks>
public sealed class PageSelectionChangingEventArgs : CancelEventArgs
{
    internal PageSelectionChangingEventArgs(Page? previous, Page? next) { PreviousPage = previous; NextPage = next; }
    /// <summary>Gets the previously selected page, including null for an empty selection.</summary>
    public Page? PreviousPage { get; }
    /// <summary>Gets the proposed retained page, or null when clearing selection.</summary>
    public Page? NextPage { get; }
}
