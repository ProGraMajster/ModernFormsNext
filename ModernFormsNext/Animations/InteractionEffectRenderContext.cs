using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Animations;

/// <summary>Provides shared render data to a custom interaction effect.</summary>
public sealed class InteractionEffectRenderContext
{
    internal InteractionEffectRenderContext(
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
    public Control Target { get; }

    /// <summary>Gets the target-local Skia canvas.</summary>
    public SKCanvas Canvas { get; }

    /// <summary>Gets target-local device-pixel bounds.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Gets the current logical-to-device scale.</summary>
    public double Scaling { get; }
}
