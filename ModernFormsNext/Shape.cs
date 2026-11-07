using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Provides the common control, rendering, stroke, cache, and hit-testing behavior for vector shapes.
/// </summary>
/// <remarks>
/// <para>
/// A Shape participates in normal ModernFormsNext layout, docking, anchoring, visibility,
/// presentation transforms, parent clipping, and designer selection. Its pixels are produced by
/// the shared Skia surface on Windows and Android; public geometry remains platform-neutral.
/// </para>
/// <para>
/// <see cref="Fill"/> and <see cref="Stroke"/> use the existing shareable Brush subsystem. A null
/// brush or <see cref="NoBrush"/> skips that paint operation. Property changes invalidate rendering
/// but do not request layout.
/// </para>
/// </remarks>
public abstract class Shape : Control
{
    private const float StrokeHitTolerance = 2f;
    private MfnBrush? fill;
    private MfnBrush? stroke;
    private float strokeThickness = 1f;
    private StrokeLineCap strokeLineCap;
    private StrokeLineJoin strokeLineJoin;
    private float miterLimit = 4f;
    private GeometryStretch stretch;
    private Geometry? definingGeometry;
    private WeakGeometrySubscription<Shape>? geometrySubscription;
    private Geometry? cachedGeometry;
    private int cachedGeometryVersion;
    private float cachedScaleX;
    private float cachedScaleY;
    private Rectangle cachedTarget;
    private float cachedStretchStroke;
    private SKPath? cachedPath;
    private SKPath? cachedStrokeHitPath;
    private float cachedHitStrokeWidth;
    private StrokeLineCap cachedHitLineCap;
    private StrokeLineJoin cachedHitLineJoin;
    private float cachedHitMiterLimit;

    /// <summary>Initializes a transparent vector control with a 100 by 100 logical-pixel default size.</summary>
    protected Shape()
    {
        SetControlBehavior(ControlBehaviors.Transparent);
    }

    /// <summary>Gets or sets inherited text that is not rendered by vector shapes.</summary>
    /// <remarks>
    /// The member remains available for source compatibility with <see cref="Control"/>, but it is
    /// hidden from designers because Shape rendering is defined only by geometry and brushes.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }

    /// <summary>Gets or sets the brush used to fill the defining geometry.</summary>
    /// <value>A shared brush, or <see langword="null"/> to skip filling. The default is null.</value>
    [Category("Appearance")]
    [Description("The shared brush used to fill the vector geometry; null performs no fill.")]
    [DefaultValue(null)]
    public virtual MfnBrush? Fill
    {
        get => fill;
        set => SetBrushField(ref fill, value);
    }

    /// <summary>Gets or sets the brush used to stroke the defining geometry.</summary>
    /// <value>A shared brush, or <see langword="null"/> to skip stroking. The default is null.</value>
    [Category("Appearance")]
    [Description("The shared brush used to stroke the vector geometry; null performs no stroke.")]
    [DefaultValue(null)]
    public MfnBrush? Stroke
    {
        get => stroke;
        set => SetBrushField(ref stroke, value);
    }

    /// <summary>Gets or sets how transformed geometry fits the padded client area.</summary>
    /// <remarks>
    /// None preserves existing raw logical coordinates. Other modes use tight path bounds after
    /// Geometry.Transform, center the result inside PaddedClientRectangle and reserve half the
    /// stroke thickness on every edge, even without a stroke brush. Stroke thickness itself is not
    /// stretched. Zero-width/height geometry is centered on that axis without inventing an area;
    /// the other axis determines uniform scale. UniformToFill and wide miters may be clipped by
    /// the control buffer. Density and presentation transforms are applied afterward. This changes
    /// rendering and hit testing without requesting layout; set on the UI thread.
    /// </remarks>
    [Category("Geometry"), DefaultValue(GeometryStretch.None)]
    public GeometryStretch Stretch
    {
        get => stretch;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (stretch == value) return;
            stretch = value;
            InvalidateGeometryCache();
        }
    }

    /// <summary>Gets or sets the stroke thickness in logical pixels.</summary>
    /// <value>A finite non-negative value. The default is 1.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative, NaN, or infinite value.</exception>
    [Category("Appearance")]
    [Description("The stroke thickness in logical pixels.")]
    [DefaultValue(1f)]
    public float StrokeThickness
    {
        get => strokeThickness;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Stroke thickness must be finite and non-negative.");
            if (strokeThickness.Equals(value))
                return;

            strokeThickness = value;
            OnStrokeThicknessChanged(EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets how open stroke endpoints are drawn.</summary>
    [Category("Appearance")]
    [Description("How open stroke endpoints are drawn.")]
    [DefaultValue(StrokeLineCap.Flat)]
    public StrokeLineCap StrokeLineCap
    {
        get => strokeLineCap;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The stroke line cap is not defined.");
            if (strokeLineCap == value)
                return;

            strokeLineCap = value;
            InvalidateStrokeCache();
        }
    }

    /// <summary>Gets or sets how consecutive stroke segments are joined.</summary>
    [Category("Appearance")]
    [Description("How consecutive stroke segments are joined.")]
    [DefaultValue(StrokeLineJoin.Miter)]
    public StrokeLineJoin StrokeLineJoin
    {
        get => strokeLineJoin;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The stroke line join is not defined.");
            if (strokeLineJoin == value)
                return;

            strokeLineJoin = value;
            InvalidateStrokeCache();
        }
    }

    /// <summary>Gets or sets the maximum miter length as a multiple of stroke thickness.</summary>
    /// <value>A finite value greater than or equal to 1. The default is 4.</value>
    [Category("Appearance")]
    [Description("The maximum miter length as a multiple of stroke thickness.")]
    [DefaultValue(4f)]
    public float MiterLimit
    {
        get => miterLimit;
        set
        {
            if (!float.IsFinite(value) || value < 1f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Miter limit must be finite and at least 1.");
            if (miterLimit.Equals(value))
                return;

            miterLimit = value;
            InvalidateStrokeCache();
        }
    }

    /// <summary>Responds when <see cref="StrokeThickness"/> changes.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>
    /// The default implementation invalidates rendering and the cached stroke hit path. Derived
    /// controls whose defining geometry depends on stroke thickness may rebuild that geometry
    /// before calling the base implementation.
    /// </remarks>
    protected virtual void OnStrokeThicknessChanged(EventArgs e)
        => InvalidateStrokeCache();

    /// <summary>Gets the geometry currently rendered by this shape.</summary>
    protected Geometry? DefiningGeometry => definingGeometry;

    /// <summary>Gets the default shape size in logical pixels.</summary>
    protected override Size DefaultSize => new(100, 100);

    /// <summary>
    /// Replaces the defining geometry and wires shared invalidation without retaining this control.
    /// </summary>
    /// <param name="geometry">The new geometry, or null for an empty shape.</param>
    protected void SetDefiningGeometry(Geometry? geometry)
    {
        if (ReferenceEquals(definingGeometry, geometry))
            return;

        geometrySubscription?.Dispose();
        geometrySubscription = null;
        definingGeometry = geometry;
        if (geometry is not null)
            geometrySubscription = new(this, geometry, static shape => shape.HandleGeometryChanged());
        InvalidateGeometryCache();
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        SKPath? path = GetCachedPath();
        if (path is not null && !path.IsEmpty)
        {
            SKRect paintBounds = path.Bounds;
            float scaledStrokeThickness = StrokeThickness * ScaleFactor.Width;
            SkiaShapeRenderer.Render(
                e.Canvas,
                path,
                paintBounds,
                Fill,
                Stroke,
                scaledStrokeThickness,
                StrokeLineCap,
                StrokeLineJoin,
                MiterLimit);
        }

        base.OnPaint(e);
    }

    internal override bool HitTestClient(PointF clientPoint)
    {
        if (!base.HitTestClient(clientPoint))
            return false;

        SKPath? path = GetCachedPath();
        if (path is null || path.IsEmpty)
            return false;

        if (SkiaBrushPaintFactory.CanRender(Fill) && path.Contains(clientPoint.X, clientPoint.Y))
            return true;

        if (!SkiaBrushPaintFactory.CanRender(Stroke) || StrokeThickness <= 0f)
            return false;

        SKPath? strokePath = GetStrokeHitPath();
        return strokePath is not null && strokePath.Contains(clientPoint.X, clientPoint.Y);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            geometrySubscription?.Dispose();
            geometrySubscription = null;
            // SKPath owns/finalizes its native handle. Do not dispose managed wrappers or
            // subscriptions from Control's finalizer, where their order is unspecified.
            DisposeNativeCaches();
        }
        base.Dispose(disposing);
    }

    private SKPath? GetCachedPath()
    {
        Geometry? geometry = definingGeometry;
        if (geometry is null || ScaledWidth <= 0 || ScaledHeight <= 0)
            return null;

        SizeF scale = ScaleFactor;
        Rectangle target = PaddedClientRectangle;
        if (cachedPath is not null && ReferenceEquals(cachedGeometry, geometry) &&
            cachedGeometryVersion == geometry.Version && cachedScaleX == scale.Width && cachedScaleY == scale.Height &&
            (Stretch == GeometryStretch.None || cachedTarget == target && cachedStretchStroke == StrokeThickness))
        {
            return cachedPath;
        }

        cachedPath?.Dispose();
        cachedStrokeHitPath?.Dispose();
        cachedStrokeHitPath = null;
        cachedGeometry = geometry;
        cachedGeometryVersion = geometry.Version;
        cachedScaleX = scale.Width;
        cachedScaleY = scale.Height;
        // Build before publishing the cache key: failed conversion must never reuse a partial path.
        cachedPath = null;
        using var converted = SkiaGeometryConverter.CreatePath(geometry, scale);
        if (Stretch != GeometryStretch.None) StretchPath(converted, target, scale);
        cachedPath = new SKPath(converted);
        cachedTarget = target;
        cachedStretchStroke = StrokeThickness;
        return cachedPath;
    }

    private void StretchPath(SKPath path, Rectangle target, SizeF density)
    {
        if (path.IsEmpty) return;
        double insetX = StrokeThickness * density.Width / 2d, insetY = StrokeThickness * density.Height / 2d;
        double width = target.Width - 2 * insetX, height = target.Height - 2 * insetY;
        if (width <= 0 || height <= 0) { path.Reset(); return; }
        SKRect bounds = path.ComputeTightBounds();
        double sx = bounds.Width > 0 ? width / bounds.Width : 1;
        double sy = bounds.Height > 0 ? height / bounds.Height : 1;
        if (Stretch != GeometryStretch.Fill)
        {
            double uniform = bounds.Width <= 0 ? sy : bounds.Height <= 0 ? sx :
                Stretch == GeometryStretch.Uniform ? Math.Min(sx, sy) : Math.Max(sx, sy);
            sx = sy = uniform;
        }
        double tx = target.Left + insetX + (width - bounds.Width * sx) / 2 - bounds.Left * sx;
        double ty = target.Top + insetY + (height - bounds.Height * sy) / 2 - bounds.Top * sy;
        if (!float.IsFinite((float)sx) || !float.IsFinite((float)sy) || !float.IsFinite((float)tx) || !float.IsFinite((float)ty))
            throw new ArgumentException("Geometry stretch exceeds the finite coordinate range.");
        path.Transform(new SKMatrix((float)sx, 0, (float)tx, 0, (float)sy, (float)ty, 0, 0, 1));
    }

    private SKPath? GetStrokeHitPath()
    {
        SKPath? path = GetCachedPath();
        if (path is null)
            return null;

        float scale = ScaleFactor.Width;
        float hitStrokeWidth = (StrokeThickness + (StrokeHitTolerance * 2f)) * scale;
        if (cachedStrokeHitPath is not null && cachedHitStrokeWidth == hitStrokeWidth &&
            cachedHitLineCap == StrokeLineCap && cachedHitLineJoin == StrokeLineJoin &&
            cachedHitMiterLimit == MiterLimit)
        {
            return cachedStrokeHitPath;
        }

        cachedStrokeHitPath?.Dispose();
        cachedStrokeHitPath = new SKPath();
        using var paint = new SKPaint { IsAntialias = true };
        SkiaShapeRenderer.ConfigureStroke(
            paint,
            hitStrokeWidth,
            StrokeLineCap,
            StrokeLineJoin,
            MiterLimit);
        paint.GetFillPath(path, cachedStrokeHitPath);
        cachedHitStrokeWidth = hitStrokeWidth;
        cachedHitLineCap = StrokeLineCap;
        cachedHitLineJoin = StrokeLineJoin;
        cachedHitMiterLimit = MiterLimit;
        return cachedStrokeHitPath;
    }

    private void HandleGeometryChanged()
        => InvalidateGeometryCache();

    /// <inheritdoc/>
    protected override void OnPaddingChanged(EventArgs e)
    {
        if (Stretch != GeometryStretch.None) InvalidateGeometryCache();
        base.OnPaddingChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(EventArgs e)
    {
        if (Stretch != GeometryStretch.None) InvalidateGeometryCache();
        base.OnSizeChanged(e);
    }

    private void InvalidateGeometryCache()
    {
        DisposeNativeCaches();
        Invalidate();
    }

    private void InvalidateStrokeCache()
    {
        if (Stretch != GeometryStretch.None) { InvalidateGeometryCache(); return; }
        cachedStrokeHitPath?.Dispose();
        cachedStrokeHitPath = null;
        Invalidate();
    }

    private void DisposeNativeCaches()
    {
        cachedPath?.Dispose();
        cachedPath = null;
        cachedStrokeHitPath?.Dispose();
        cachedStrokeHitPath = null;
        cachedGeometry = null;
    }

}
