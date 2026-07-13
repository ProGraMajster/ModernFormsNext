namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Represents the lifecycle state of an Android Skia surface host.
/// </summary>
public enum AndroidSurfaceLifecycleState
{
    /// <summary>The owning activity has not started the host.</summary>
    Uninitialized,

    /// <summary>The owning activity has started, but the surface is not yet accepting input.</summary>
    Started,

    /// <summary>The host is visible and may render or accept input.</summary>
    Resumed,

    /// <summary>The host is paused and no longer accepts pointer input.</summary>
    Paused,

    /// <summary>The host activity has stopped.</summary>
    Stopped,

    /// <summary>The host has released its resources permanently.</summary>
    Disposed
}
