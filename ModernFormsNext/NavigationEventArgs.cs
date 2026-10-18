using System.ComponentModel;

namespace ModernFormsNext;

/// <summary>Identifies an explicit navigation-stack operation.</summary>
public enum NavigationKind
{
    /// <summary>Add a detached page above the current entry.</summary>
    Push,
    /// <summary>Remove the current entry and reveal its predecessor.</summary>
    Pop,
    /// <summary>Remove entries above the retained root.</summary>
    PopToRoot
}

/// <summary>Describes a proposed stack change before validation or ownership commits.</summary>
/// <remarks>Cancel retains the existing stack. Handlers run on the UI thread and may enqueue later operations.</remarks>
public sealed class NavigationChangingEventArgs : CancelEventArgs
{
    internal NavigationChangingEventArgs(NavigationKind kind, Page? previous, Page? next)
    { Kind = kind; PreviousPage = previous; NextPage = next; }
    /// <summary>Gets the requested operation.</summary>
    public NavigationKind Kind { get; }
    /// <summary>Gets the previously current page, including null for an empty stack.</summary>
    public Page? PreviousPage { get; }
    /// <summary>Gets the proposed current page.</summary>
    public Page? NextPage { get; }
}

/// <summary>Describes a committed stack operation after its presentation settles.</summary>
/// <remarks>Borrowed pages are not snapshots of current ownership; later queued work can change the stack.</remarks>
public sealed class NavigationChangedEventArgs : EventArgs
{
    internal NavigationChangedEventArgs(NavigationKind kind, Page? previous, Page? next, IReadOnlyList<Page> removed)
    { Kind = kind; PreviousPage = previous; NextPage = next; RemovedPages = removed; }
    /// <summary>Gets the committed operation.</summary>
    public NavigationKind Kind { get; }
    /// <summary>Gets the former page, possibly transferred by PopAsync.</summary>
    public Page? PreviousPage { get; }
    /// <summary>Gets the page made current by this operation.</summary>
    public Page? NextPage { get; }
    /// <summary>Gets the read-only snapshot of removed entries in former root-to-current order.</summary>
    /// <remarks>The operation detached these pages without disposal. Application callbacks may subsequently transfer them again.</remarks>
    public IReadOnlyList<Page> RemovedPages { get; }
}
