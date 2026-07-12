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
}
