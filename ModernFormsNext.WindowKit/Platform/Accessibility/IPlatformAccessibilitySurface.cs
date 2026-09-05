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
