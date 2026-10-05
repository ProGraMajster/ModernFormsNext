using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

/// <summary>An immutable, payload-free snapshot of the Android window backend.</summary>
/// <param name="Active">Whether the initialized backend has not exited.</param>
/// <param name="HostGeneration">Identity epoch; changes whenever an Activity presentation is retired or attached.</param>
/// <param name="Attached">Whether an Activity presentation is currently attached.</param>
/// <param name="ActivityState">The existing native Activity lifecycle state, independent of Form visibility.</param>
/// <param name="Windows">Framework windows, including hidden Forms and reusable popups.</param>
/// <remarks>Obtain on the UI thread. Contains no Activity, View, control, text, or activation payload.</remarks>
public sealed record AndroidWindowingDiagnostics(bool Active, long HostGeneration, bool Attached,
    PlatformApplicationLifecycleState ActivityState, IReadOnlyList<AndroidWindowDiagnostics> Windows)
{
    /// <summary>Gets the supported top-level and descendant presentation policy.</summary>
    public string WindowPolicy => "One main Form per Activity; owner-bound modal Forms and reusable in-Activity popups.";
    /// <summary>Gets supported native capabilities; rendering remains software Skia.</summary>
    public string SupportedCapabilities => "Application.Run, Show/Hide/Close, cancellable Back, modal ownership, popup positioning, confirmed geometry, density, safe area, IME, touch, hardware keys, accessibility.";
    /// <summary>Gets desktop operations explicitly rejected by this backend.</summary>
    public string UnsupportedOperations => "Concurrent Activity hosts, independent top-level Forms, arbitrary top-level position, minimize/maximize, topmost, taskbar visibility, per-Form icons, min/max constraints, desktop move/resize dragging, custom cursors.";
}

/// <summary>Immutable native presentation facts for one framework window.</summary>
/// <param name="Main">Whether this is the designated main Form.</param>
/// <param name="Popup">Whether this is a temporary popup.</param>
/// <param name="Modal">Whether this is an owner-bound modal Form.</param>
/// <param name="Visible">Requested framework visibility, retained across recreation.</param>
/// <param name="Attached">Whether native layout has confirmed a presentation.</param>
/// <param name="Active">Whether actual native input focus is confirmed.</param>
/// <param name="LogicalSize">Last confirmed native size in logical pixels; initial 400 by 300 before first layout.</param>
/// <param name="Density">Physical pixels per logical pixel.</param>
/// <param name="ScaledDensity">Native density multiplied by the system font scale, for diagnostics only.</param>
/// <param name="Insets">The last confirmed safe-area and informational IME snapshot.</param>
/// <param name="PaintCount">Completed native software paint callbacks across this Form's presentations.</param>
/// <param name="ActivePointers">Active native touch identities in the current presentation.</param>
public sealed record AndroidWindowDiagnostics(bool Main, bool Popup, bool Modal, bool Visible, bool Attached,
    bool Active, Size LogicalSize, double Density, double ScaledDensity, WindowInsets Insets,
    long PaintCount, int ActivePointers);
