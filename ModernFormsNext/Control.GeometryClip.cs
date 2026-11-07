using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Drawing;
using ModernFormsNext.Layout;
using ModernFormsNext.Rendering.Skia;
using SkiaSharp;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_geometryClipProperty = PropertyStore.CreateKey();

    /// <summary>Gets or sets a reusable geometry that clips this control and its composed descendants.</summary>
    /// <remarks>
    /// Null preserves rectangular clipping. The borrowed geometry uses local logical coordinates;
    /// its transform, density and this control's presentation transform apply to both pixels and
    /// pointer hit testing. It clips the complete composition, including effects and shadow, without
    /// changing layout, focus or rectangular accessibility bounds. Mutations repaint the composition
    /// owner. Set and mutate on the UI thread. Each control owns only its cached native path and weak
    /// subscription; disposing the control never disposes the shared geometry. NativeViewHost islands
    /// suspend through the existing unsupported-composition mechanism while an ancestor has a clip.
    /// </remarks>
    /// <example><code>panel.ClipGeometry = new EllipseGeometry(new RectangleF(0, 0, 200, 120));</code></example>
    [Category("Appearance"), DefaultValue(null)]
    public Geometry? ClipGeometry
    {
        get => GeometryClipState?.Geometry;
        set
        {
            var previous = GeometryClipState;
            if (ReferenceEquals(previous?.Geometry, value)) return;
            previous?.Dispose();
            if (value is null) Properties.RemoveObject(s_geometryClipProperty);
            else Properties.SetObject(s_geometryClipProperty, new GeometryClipCache(this, value));
            OnGeometryClipChanged();
        }
    }

    private GeometryClipCache? GeometryClipState => Properties.GetObject(s_geometryClipProperty) as GeometryClipCache;
    private SKPath? GetGeometryClipPath() => GeometryClipState?.GetPath(ScaleFactor);
    // Detached preview surfaces use the same local path without introducing a second clip model.
    internal void ApplyGeometryClip(SKCanvas canvas)
    {
        if (GetGeometryClipPath() is { } path) canvas.ClipPath(path, SKClipOperation.Intersect, true);
    }
    private bool GeometryClipContains(PointF point) => GetGeometryClipPath() is not { } path || path.Contains(point.X, point.Y);
    private void OnGeometryClipChanged()
    {
        GeometryClipState?.Invalidate();
        Parent?.Invalidate();
        Invalidate();
        NotifyNativePresentationChanged();
    }
    private void DisposeGeometryClip()
    {
        GeometryClipState?.Dispose();
        Properties.RemoveObject(s_geometryClipProperty);
    }

    private sealed class GeometryClipCache : IDisposable
    {
        private readonly WeakGeometrySubscription<Control> subscription;
        private SKPath? path;
        private SizeF density;
        private int version;
        internal GeometryClipCache(Control control, Geometry geometry)
        {
            Geometry = geometry;
            subscription = new(control, geometry, static owner => owner.OnGeometryClipChanged());
        }
        internal Geometry Geometry { get; }
        internal SKPath GetPath(SizeF scale)
        {
            if (path is null || density != scale || version != Geometry.Version)
            {
                Invalidate();
                path = SkiaGeometryConverter.CreatePath(Geometry, scale);
                density = scale;
                version = Geometry.Version;
            }
            return path;
        }
        internal void Invalidate() { path?.Dispose(); path = null; }
        public void Dispose() { subscription.Dispose(); Invalidate(); }
    }
}
