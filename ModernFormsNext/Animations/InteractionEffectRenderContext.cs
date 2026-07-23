using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Animations;

/// <summary>Provides shared render data to a custom interaction effect.</summary>
/// <remarks>
/// The attached effect reuses this context between frames to avoid render-loop allocations.
/// Read it only during <c>OnRender</c>; do not retain the context or its borrowed canvas.
/// </remarks>
public sealed class InteractionEffectRenderContext
{
    internal InteractionEffectRenderContext(
        Control target,
        SKCanvas canvas,
        Rectangle bounds,
        double scaling)
        => Reset(target, canvas, bounds, scaling);

    internal void Reset(
        Control target,
        SKCanvas canvas,
        Rectangle bounds,
        double scaling)
    {
        Target = target;
        Canvas = canvas;
        Bounds = bounds;
        Scaling = scaling;
    }

    /// <summary>Gets the attached target control.</summary>
    public Control Target { get; private set; } = null!;

    /// <summary>Gets the target-local Skia canvas.</summary>
    public SKCanvas Canvas { get; private set; } = null!;

    /// <summary>Gets target-local device-pixel bounds.</summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>Gets the current logical-to-device scale.</summary>
    public double Scaling { get; private set; }
}
