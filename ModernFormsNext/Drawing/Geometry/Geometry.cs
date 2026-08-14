using System.Numerics;

namespace ModernFormsNext.Drawing;

/// <summary>
/// Defines platform-neutral vector geometry that can be rendered, transformed, and shared.
/// </summary>
/// <remarks>
/// <para>
/// Geometry instances are mutable and may be shared by multiple <see cref="ModernFormsNext.Path"/>
/// controls. Every rendered mutation raises <see cref="Changed"/> synchronously so all consumers
/// can invalidate their native-path caches. Mutate geometry on the UI thread while it is in use.
/// </para>
/// <para>
/// This abstraction intentionally does not expose SkiaSharp types. Backends convert the geometry
/// to their native representation and cache that representation against <see cref="Version"/>.
/// </para>
/// </remarks>
public abstract class Geometry
{
    private Matrix3x2 transform = Matrix3x2.Identity;
    private int version;

    /// <summary>
    /// Occurs when a property, figure, segment, or point changes the rendered geometry.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the monotonically changing revision used by renderer caches.
    /// </summary>
    /// <remarks>
    /// The value is meaningful only for equality comparisons with a previously observed revision;
    /// it may wrap after a very large number of mutations.
    /// </remarks>
    public int Version => version;

    /// <summary>
    /// Gets or sets the finite platform-neutral transform applied to this geometry.
    /// </summary>
    /// <remarks>
    /// Translation uses logical pixels. The transform is applied before the owning control's
    /// presentation transform, and it affects rendering and geometry-aware hit testing equally.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when a matrix component is NaN or infinity.</exception>
    public Matrix3x2 Transform
    {
        get => transform;
        set
        {
            ValidateTransform(value, nameof(value));
            if (transform.Equals(value))
                return;

            transform = value;
            OnChanged();
        }
    }

    /// <summary>
    /// Raises <see cref="Changed"/> after a rendered value has changed.
    /// </summary>
    protected void OnChanged()
    {
        version = unchecked(version + 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal static void ValidatePoint(System.Drawing.PointF point, string parameterName)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new ArgumentException("Point coordinates must be finite.", parameterName);
    }

    internal static void ValidateSize(System.Drawing.SizeF size, string parameterName)
    {
        if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height) ||
            size.Width < 0f || size.Height < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                size,
                "Size components must be finite and non-negative.");
        }
    }

    internal static void ValidateRectangle(System.Drawing.RectangleF rectangle, string parameterName)
    {
        ValidatePoint(rectangle.Location, parameterName);
        ValidateSize(rectangle.Size, parameterName);
    }

    private static void ValidateTransform(Matrix3x2 value, string parameterName)
    {
        if (!float.IsFinite(value.M11) || !float.IsFinite(value.M12) ||
            !float.IsFinite(value.M21) || !float.IsFinite(value.M22) ||
            !float.IsFinite(value.M31) || !float.IsFinite(value.M32))
        {
            throw new ArgumentException("Transform components must be finite.", parameterName);
        }
    }
}
