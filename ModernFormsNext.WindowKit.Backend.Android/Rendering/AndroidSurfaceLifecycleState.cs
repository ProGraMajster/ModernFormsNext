namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Represents the lifecycle state of an Android Skia surface host.
/// </summary>
public enum AndroidSurfaceLifecycleState
{
    /// <summary>The host has not yet been attached to an activity.</summary>
    Uninitialized,

    /// <summary>The host is visible and may render or accept input.</summary>
    Resumed,

    /// <summary>The host is paused and no longer accepts pointer input.</summary>
    Paused,

    /// <summary>The host activity has stopped.</summary>
    Stopped,

    /// <summary>The host has released its resources permanently.</summary>
    Disposed
}
