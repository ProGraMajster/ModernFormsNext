namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>
/// Describes the platform application's ability to perform foreground UI work.
/// </summary>
public enum PlatformApplicationLifecycleState
{
    /// <summary>The backend has not observed a definitive lifecycle state.</summary>
    Unknown,

    /// <summary>The application has an active foreground UI host.</summary>
    Foreground,

    /// <summary>The application is paused or stopped in the background.</summary>
    Background,

    /// <summary>The application currently has no usable UI host.</summary>
    NoHost
}
