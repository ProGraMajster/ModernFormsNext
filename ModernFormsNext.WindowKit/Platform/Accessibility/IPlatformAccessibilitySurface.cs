namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>
/// Routes canonical notifications from an existing windowless control surface. This internal
/// transport supplements the window service; it does not define another semantic tree.
/// </summary>
internal interface IPlatformAccessibilitySurface : IPlatformAccessibilityHost
{
    event Action<IPlatformAccessibleObject, int, int, int>? AccessibilityNotification;
}

/// <summary>Observes notifications from an on-demand logical peer, including custom children.</summary>
internal interface IPlatformAccessibilityNotifications
{
    event Action<int, int, int>? AccessibilityNotification;
}
