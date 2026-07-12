using SkiaSharp;

namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Provides the canvas and logical surface metrics for an Android Skia render pass.
/// </summary>
public sealed class AndroidSkiaRenderEventArgs : EventArgs
{
    internal AndroidSkiaRenderEventArgs(SKCanvas canvas, float width, float height, float density, long renderCount)
    {
        Canvas = canvas;
        LogicalWidth = width;
        LogicalHeight = height;
        Density = density;
        RenderCount = renderCount;
    }

    /// <summary>Gets the borrowed canvas for the current render pass.</summary>
    /// <remarks>The receiver must not dispose or retain this canvas after the event returns.</remarks>
    public SKCanvas Canvas { get; }

    /// <summary>Gets the surface width in logical pixels.</summary>
    public float LogicalWidth { get; }

    /// <summary>Gets the surface height in logical pixels.</summary>
    public float LogicalHeight { get; }

    /// <summary>Gets the Android density scale applied to the canvas.</summary>
    public float Density { get; }

    /// <summary>Gets the one-based number of this render pass.</summary>
    public long RenderCount { get; }
}
