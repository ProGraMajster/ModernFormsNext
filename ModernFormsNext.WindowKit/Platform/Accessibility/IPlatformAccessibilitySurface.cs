namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>
/// Routes canonical notifications from an existing windowless control surface. This internal
/// transport supplements the window service; it does not define another semantic tree.
/// </summary>
internal interface IPlatformAccessibilitySurface : IPlatformAccessibilityHost
{
    event Action<IPlatformAccessibleObject, int, int, int>? AccessibilityNotification;
}

/// <summary>Internal canonical notifications absent from the legacy MSAA event enumeration.</summary>
internal static class PlatformAccessibilitySurfaceEvents
{
    internal const int Invoked = -1;
}

/// <summary>Observes notifications from an on-demand logical peer, including custom children.</summary>
internal interface IPlatformAccessibilityNotifications
{
    event Action<int, int, int>? AccessibilityNotification;
}

/// <summary>Confirms that the legacy canonical selection-removal operation is implemented.</summary>
internal interface IPlatformAccessibilitySelection
{
    bool CanClearSelection { get; }
}

/// <summary>Answers canonical child membership without enumerating a large logical collection.</summary>
/// <remarks>
/// An internal optional query on the existing peer adapter, not another semantic tree. Return
/// false when the peer cannot answer safely; providers must retain their ordinary projection
/// checks for custom peers. A true return makes contains authoritative for the current UI state.
/// </remarks>
internal interface IPlatformAccessibilityChildMembership
{
    bool TryContainsChild(IPlatformAccessibleObject child, out bool contains);
}

/// <summary>Projects the currently realized canonical children for a native virtualized node query.</summary>
/// <remarks>
/// The returned references are the same canonical peers used by focus, selection and actions.
/// This per-query projection is not cached as a semantic tree. Custom projections may decline.
/// </remarks>
internal interface IPlatformAccessibilityVirtualizedChildren
{
    bool TryGetVirtualizedChildren(out PlatformAccessibleVirtualizedChildren children);
    bool TryGetVirtualizedItemIndex(IPlatformAccessibleObject child, out int index);
    // Native fragments can navigate the full flat logical source without enumerating all
    // preceding siblings. A supported out-of-range index returns true with a null child.
    bool TryGetVirtualizedChild(int index, out IPlatformAccessibleObject? child);
}

/// <summary>Borrowed canonical peers and optional complete flat-list count for one native query.</summary>
/// <param name="Children">Realized/cache peers and retained selected/focused peers, plus explicitly visible visual children.</param>
/// <param name="LogicalItemCount">The entire flat source count, or null when additional non-list children prevent flat collection metadata.</param>
internal readonly record struct PlatformAccessibleVirtualizedChildren(
    IReadOnlyList<IPlatformAccessibleObject> Children, int? LogicalItemCount);
