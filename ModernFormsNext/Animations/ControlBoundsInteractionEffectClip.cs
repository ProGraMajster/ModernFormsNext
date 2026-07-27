using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Animations;

/// <summary>Clips effects to control bounds and the current style corner radius.</summary>
public sealed class ControlBoundsInteractionEffectClip : IInteractionEffectClip
{
    /// <summary>Gets the reusable stateless clip instance.</summary>
    public static ControlBoundsInteractionEffectClip Instance { get; } = new();

    private ControlBoundsInteractionEffectClip()
    {
    }

    /// <inheritdoc/>
    public void Apply(SKCanvas canvas, Control target, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(target);
        BorderStyle border = target.CurrentStyle.Border;
        int radius = border.GetRadius();
        if (radius == 0 && border.Radius is null)
            radius = target.Style.Border.GetRadius();
        radius = target.LogicalToDeviceUnits(radius);
        if (radius <= 0)
        {
            canvas.ClipRect(new SKRect(0, 0, bounds.Width, bounds.Height));
            return;
        }

        using var roundRect = new SKRoundRect(
            new SKRect(0, 0, bounds.Width, bounds.Height),
            radius,
            radius);
        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
    }
}
