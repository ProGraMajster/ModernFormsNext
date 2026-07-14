namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Stores user-visible state independently of a Windows window or Android activity.
/// </summary>
/// <remarks>
/// Android keeps the owning <see cref="App"/> in its process application object, so ordinary
/// activity recreation does not reset these values.
/// </remarks>
public sealed class SampleAppState
{
    /// <summary>Gets the number of shared button activations.</summary>
    public int ClickCount { get; internal set; }

    /// <summary>Gets the number of dispatcher callbacks completed by the shared application.</summary>
    public int DispatcherCount { get; internal set; }

    /// <summary>Gets the number of root paint passes observed by the shared page.</summary>
    public long RenderCount { get; internal set; }

    /// <summary>Gets the most recent logical surface width.</summary>
    public int SurfaceWidth { get; internal set; }

    /// <summary>Gets the most recent logical surface height.</summary>
    public int SurfaceHeight { get; internal set; }

    /// <summary>Gets the most recent permission result shown by the sample.</summary>
    public string PermissionStatus { get; internal set; } = "Not requested";

    /// <summary>Gets the latest lifecycle transition reported by the current native host.</summary>
    public string LifecycleStatus { get; internal set; } = "Application created";

    /// <summary>Gets the current Android logical-pixel density, or <c>1</c> on Windows.</summary>
    public float Density { get; internal set; } = 1f;

    /// <summary>Gets the current Android scaled density, or <c>1</c> on Windows.</summary>
    public float ScaledDensity { get; internal set; } = 1f;

    /// <summary>Gets a value indicating whether the native rendering surface is attached.</summary>
    public bool SurfaceAttached { get; internal set; }

    /// <summary>Gets the number of pointers currently tracked by the platform surface.</summary>
    public int ActivePointerCount { get; internal set; }

    /// <summary>Gets the number of render passes completed by the current native surface.</summary>
    public long NativeRenderCount { get; internal set; }

    /// <summary>Gets a short description of the most recent input transition.</summary>
    public string LastInput { get; internal set; } = "None";

    /// <summary>Gets the most recent shared control action received by the application.</summary>
    public string LastAction { get; internal set; } = "None";

    /// <summary>Gets the most recent platform service operation invoked by shared code.</summary>
    public string LastServiceInvocation { get; internal set; } = "None";

    /// <summary>Gets the completion status or controlled failure of the last service operation.</summary>
    public string LastServiceResult { get; internal set; } = "None";

    /// <summary>Gets the name of the last shared control that received focus.</summary>
    public string FocusedControl { get; internal set; } = "None";
}
