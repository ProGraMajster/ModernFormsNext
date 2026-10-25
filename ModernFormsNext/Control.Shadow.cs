using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext;

public partial class Control
{
    private Shadow? shadow;
    private SKPaint? shadowPaint;
    private SKMaskFilter? shadowMaskFilter;
    private SKPath? shadowCutout;
    private Size shadowCutoutSize;
    private float shadowCutoutRadius = -1;
    private float shadowFilterSigma = -1;

    /// <summary>Gets or sets an optional outer shadow composed behind this control by its parent.</summary>
    /// <remarks>
    /// Null disables the shared shadow. Changing the value repaints the parent to erase the old
    /// footprint, without changing layout or hit testing. The border radius comes from the current
    /// style. The same presentation transform and opacity apply to content and shadow, including
    /// disabled-state opacity. Ancestors clip the shadow; a root surface cannot display pixels
    /// outside its bounds. Set on the UI thread. The immutable value is borrowed; the control owns
    /// and releases its reusable native paint/filter cache. GroupBox's legacy shadow remains a
    /// separate opt-in appearance; avoid enabling both for the same effect.
    /// </remarks>
    [DefaultValue(null), Category("Appearance")]
    public Shadow? Shadow
    {
        get => shadow;
        set
        {
            if (Equals(shadow, value)) return;
            shadow = value;
            DisposeShadowRendering();
            // The old shadow can extend outside the child's bitmap. The composition owner must
            // repaint its background and overlapping siblings, even when this child is offscreen.
            Parent?.Invalidate();
            Invalidate();
        }
    }

    internal RectangleF ShadowLocalArea
    {
        get
        {
            if (shadow is null || shadow.Color.A == 0) return RectangleF.Empty;
            float scale = (float)Scaling;
            float spread = shadow.Spread * scale;
            if (ScaledWidth + 2 * spread <= 0 || ScaledHeight + 2 * spread <= 0) return RectangleF.Empty;
            // Three Gaussian deviations plus an antialias fringe conservatively bound the mask.
            float fringe = shadow.BlurRadius * scale * 3 + 1;
            return RectangleF.FromLTRB(shadow.OffsetX * scale - spread - fringe,
                shadow.OffsetY * scale - spread - fringe,
                ScaledWidth + shadow.OffsetX * scale + spread + fringe,
                ScaledHeight + shadow.OffsetY * scale + spread + fringe);
        }
    }

    internal void DrawShadow(SKCanvas canvas, float x, float y)
    {
        if (shadow is null || shadow.Color.A == 0 || EffectiveOpacity <= 0) return;
        var description = shadow;
        var parent = Parent;
        // CurrentStyle is extensible. Resolve it before borrowing native cache objects, then
        // retire the frame if an override disposed, reparented or replaced this presentation.
        int logicalRadius = CurrentStyle.Border.GetRadius();
        if (IsDisposed || Disposing || !ReferenceEquals(parent, Parent) || !ReferenceEquals(description, shadow)) return;
        float scale = (float)Scaling;
        float spread = shadow.Spread * scale;
        var box = new SKRect(shadow.OffsetX * scale - spread, shadow.OffsetY * scale - spread,
            ScaledWidth + shadow.OffsetX * scale + spread, ScaledHeight + shadow.OffsetY * scale + spread);
        if (box.Width <= 0 || box.Height <= 0) return;
        shadowPaint ??= new SKPaint { IsAntialias = true };
        float sigma = shadow.BlurRadius * scale;
        if (shadowFilterSigma != sigma)
        {
            // A control keeps at most one filter. Geometry/opacity changes reuse it; DPI or
            // blur changes replace and explicitly release the previous native wrapper.
            var previous = shadowMaskFilter;
            shadowMaskFilter = sigma == 0 ? null : SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            shadowPaint.MaskFilter = shadowMaskFilter;
            previous?.Dispose();
            shadowFilterSigma = sigma;
        }
        shadowPaint.Color = new SKColor(shadow.Color.R, shadow.Color.G, shadow.Color.B,
            (byte)Math.Clamp((int)MathF.Round(shadow.Color.A * EffectiveOpacity), 0, 255));
        float cutoutRadius = Math.Clamp(logicalRadius * scale, 0, Math.Min(ScaledWidth, ScaledHeight) / 2f);
        var cutoutSize = new Size(ScaledWidth, ScaledHeight);
        if (shadowCutout is null || shadowCutoutSize != cutoutSize || shadowCutoutRadius != cutoutRadius)
        {
            shadowCutout ??= new SKPath();
            shadowCutout.Reset();
            shadowCutout.AddRoundRect(new SKRect(0, 0, ScaledWidth, ScaledHeight), cutoutRadius, cutoutRadius);
            shadowCutoutSize = cutoutSize;
            shadowCutoutRadius = cutoutRadius;
        }
        float radius = Math.Clamp(logicalRadius * scale + spread, 0, Math.Min(box.Width, box.Height) / 2);
        canvas.Save();
        try
        {
            canvas.Translate(x, y);
            // This is an outer shadow, including for transparent backgrounds. Do not rely on
            // opaque content to cover its interior. Reuse the local rounded cutout at every
            // parent position; only size/DPI/radius changes rebuild its finite native path.
            canvas.ClipPath(shadowCutout, SKClipOperation.Difference, true);
            canvas.DrawRoundRect(box, radius, radius, shadowPaint);
        }
        finally { canvas.Restore(); }
    }

    private void DisposeShadowRendering()
    {
        if (shadowPaint is null) return;
        shadowPaint.MaskFilter = null;
        shadowMaskFilter?.Dispose();
        shadowMaskFilter = null;
        shadowPaint.Dispose();
        shadowPaint = null;
        shadowCutout?.Dispose();
        shadowCutout = null;
        shadowCutoutRadius = -1;
        shadowFilterSigma = -1;
    }
}
