using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Animations;

/// <summary>
/// Applies a render clip for interaction effects.
/// </summary>
/// <remarks>
/// The abstraction intentionally accepts the shared Skia canvas so future shape or geometry
/// implementations can provide non-rectangular clips without changing effect APIs.
/// </remarks>
public interface IInteractionEffectClip
{
    /// <summary>Intersects the current canvas clip with the target's effect region.</summary>
    void Apply(SKCanvas canvas, Control target, Rectangle bounds);
}
