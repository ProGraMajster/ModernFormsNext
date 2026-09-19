namespace ModernFormsNext.Diagnostics;

/// <summary>Selects the amount of detail in the optional performance display.</summary>
public enum PerformanceOverlayMode
{
    /// <summary>Shows one concise row for each selected metric group.</summary>
    Compact,
    /// <summary>Also shows available counter, source and backend details.</summary>
    Expanded
}

/// <summary>Positions a performance display inside the rendered client viewport.</summary>
public enum PerformanceOverlayCorner
{
    /// <summary>Aligns the display with the top left corner.</summary>
    TopLeft,
    /// <summary>Aligns the display with the top right corner.</summary>
    TopRight,
    /// <summary>Aligns the display with the bottom left corner.</summary>
    BottomLeft,
    /// <summary>Aligns the display with the bottom right corner.</summary>
    BottomRight
}

/// <summary>Selects groups of recorded metrics to display without enabling their collection.</summary>
/// <remarks>Unavailable measurements are labelled unavailable. Selecting a group does not enable allocation or detailed-control tracking.</remarks>
[Flags]
public enum PerformanceOverlayMetrics
{
    /// <summary>Shows only the display heading and optional graph.</summary>
    None = 0,
    /// <summary>Shows the previous completed frame duration and observed source interval FPS.</summary>
    Frame = 1,
    /// <summary>Shows executed layout and preferred-size work.</summary>
    Layout = 2,
    /// <summary>Shows shared render and diagnostics rendering work.</summary>
    Render = 4,
    /// <summary>Shows input and animation work.</summary>
    InputAndAnimation = 8,
    /// <summary>Shows controls visited, repainted, composited and reused.</summary>
    Controls = 16,
    /// <summary>Shows invalidation requests and the actual root redraw policy.</summary>
    Invalidation = 32,
    /// <summary>Shows optional managed allocation and collection deltas.</summary>
    Memory = 64,
    /// <summary>Shows backend, acceleration, scale and available surface information.</summary>
    Backend = 128,
    /// <summary>Shows framework-owned shader creation and disposal counts.</summary>
    Shaders = 256,
    /// <summary>Selects the compact display's usual metric groups.</summary>
    Default = Frame | Layout | Render | Controls | Invalidation | Memory | Backend,
    /// <summary>Selects every supported metric group.</summary>
    All = Frame | Layout | Render | InputAndAnimation | Controls | Invalidation | Memory | Backend | Shaders
}

/// <summary>Configures an optional input-transparent display of one performance profiler's recorded data.</summary>
/// <remarks>
/// Values are immutable. Replace the owning profiler's overlay options on its UI thread to change
/// the display. The profiler validates the complete value before applying it and requests one
/// repaint; the display adds no controls, layout, input handlers, clock or history of its own.
/// Expensive collection options belong to the profiler and are independent of these visual options.
/// The display uses the previous completed frame. Viewport clipping can hide rows on very small surfaces.
/// </remarks>
/// <example>
/// <code>
/// profiler.OverlayOptions = profiler.OverlayOptions with
/// {
///     Visible = true,
///     Corner = PerformanceOverlayCorner.BottomRight,
///     ShowFrameGraph = true
/// };
/// </code>
/// </example>
public sealed record PerformanceOverlayOptions
{
    /// <summary>Gets whether diagnostics are drawn after application content. The default is false.</summary>
    public bool Visible { get; init; }

    /// <summary>Gets the display's detail level. The default is compact.</summary>
    public PerformanceOverlayMode Mode { get; init; }

    /// <summary>Gets the selected metric groups. Unavailable data is never presented as measured zero.</summary>
    public PerformanceOverlayMetrics Metrics { get; init; } = PerformanceOverlayMetrics.Default;

    /// <summary>Gets the viewport corner used for alignment. The default is top right.</summary>
    public PerformanceOverlayCorner Corner { get; init; } = PerformanceOverlayCorner.TopRight;

    /// <summary>Gets the finite, nonnegative margin in logical client pixels. The default is eight.</summary>
    /// <remarks>The effective margin is reduced if necessary to fit a small viewport.</remarks>
    public double Margin { get; init; } = 8;

    /// <summary>Gets whether bounded recorded frame durations for the displayed source are graphed.</summary>
    /// <remarks>The graph uses existing frame history. It does not measure presentation pacing or request new frames.</remarks>
    public bool ShowFrameGraph { get; init; }

    /// <summary>Gets whether recorded repaint rectangles are highlighted.</summary>
    /// <remarks>Highlights show recorded backbuffer repaint work, not native partial presentation or a timed animation.</remarks>
    public bool ShowRepaintRegions { get; init; }

    /// <summary>Gets whether recorded control bounds are outlined.</summary>
    /// <remarks>These bounded rectangular approximations require the recorder to have collected geometry.</remarks>
    public bool ShowControlBounds { get; init; }

    /// <summary>Gets whether recorded clipping bounds are outlined.</summary>
    /// <remarks>A rectangular approximation does not describe the exact clip path of a rotated or rounded shape.</remarks>
    public bool ShowClipBounds { get; init; }

    internal void Validate()
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
        if (!Enum.IsDefined(Corner)) throw new ArgumentOutOfRangeException(nameof(Corner));
        if ((Metrics & ~PerformanceOverlayMetrics.All) != 0) throw new ArgumentOutOfRangeException(nameof(Metrics));
        if (!double.IsFinite(Margin) || Margin < 0) throw new ArgumentOutOfRangeException(nameof(Margin));
    }
}
