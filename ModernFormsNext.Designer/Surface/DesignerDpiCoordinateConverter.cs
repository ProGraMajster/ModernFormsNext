using System.Drawing;

namespace ModernFormsNext.Designer.Surface;

/// <summary>
/// Converts designer surface geometry at the boundary between logical units and device pixels.
/// </summary>
/// <remarks>
/// The design document, layout engine, shell panel metrics, surface viewport, and interaction
/// logic use logical pixels. The Windows input pipeline routes device-pixel mouse coordinates to
/// controls, while the Skia canvas also renders in device pixels. Those two boundaries are the
/// only places where the designer applies the monitor DPI scale. The independent designer preview
/// scale is composed with DPI only for content rendered inside the previewed form.
/// </remarks>
internal static class DesignerDpiCoordinateConverter
{
    /// <summary>
    /// Converts a device-pixel pointer position to logical designer surface coordinates.
    /// </summary>
    /// <param name="deviceX">The horizontal device-pixel coordinate.</param>
    /// <param name="deviceY">The vertical device-pixel coordinate.</param>
    /// <param name="dpiScale">The monitor DPI scale, where 1 represents 96 DPI.</param>
    /// <returns>The corresponding logical surface point.</returns>
    internal static PointF DeviceToLogical(float deviceX, float deviceY, double dpiScale)
    {
        ValidateScale(dpiScale, nameof(dpiScale));

        return new PointF((float)(deviceX / dpiScale), (float)(deviceY / dpiScale));
    }

    /// <summary>
    /// Converts an integer device-pixel pointer position to a logical panel pixel.
    /// </summary>
    /// <param name="deviceX">The horizontal device-pixel coordinate.</param>
    /// <param name="deviceY">The vertical device-pixel coordinate.</param>
    /// <param name="dpiScale">The monitor DPI scale, where 1 represents 96 DPI.</param>
    /// <returns>The containing logical pixel used by panel hit testing.</returns>
    internal static Point DeviceToLogicalPoint(int deviceX, int deviceY, double dpiScale)
    {
        var logical = DeviceToLogical(deviceX, deviceY, dpiScale);
        return new Point((int)Math.Floor(logical.X), (int)Math.Floor(logical.Y));
    }

    /// <summary>
    /// Converts a logical surface rectangle to device pixels by scaling its edges.
    /// </summary>
    /// <param name="logicalBounds">The rectangle in logical designer surface coordinates.</param>
    /// <param name="dpiScale">The monitor DPI scale, where 1 represents 96 DPI.</param>
    /// <returns>The corresponding device-pixel rectangle.</returns>
    /// <remarks>
    /// Scaling the right and bottom edges instead of scaling width and height independently keeps
    /// adjacent rectangles and selection adorners on the same rounded device-pixel boundary.
    /// </remarks>
    internal static Rectangle LogicalToDevice(Rectangle logicalBounds, double dpiScale)
    {
        ValidateScale(dpiScale, nameof(dpiScale));

        var left = LogicalToDevice(logicalBounds.Left, dpiScale);
        var top = LogicalToDevice(logicalBounds.Top, dpiScale);
        var right = LogicalToDevice(logicalBounds.Right, dpiScale);
        var bottom = LogicalToDevice(logicalBounds.Bottom, dpiScale);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Converts one logical coordinate or length to device pixels.
    /// </summary>
    /// <param name="logicalValue">The value in logical pixels.</param>
    /// <param name="dpiScale">The monitor DPI scale, where 1 represents 96 DPI.</param>
    /// <returns>The rounded device-pixel value.</returns>
    internal static int LogicalToDevice(int logicalValue, double dpiScale)
    {
        ValidateScale(dpiScale, nameof(dpiScale));
        return (int)Math.Round(logicalValue * dpiScale);
    }

    /// <summary>
    /// Composes monitor DPI with the form-preview scale for design-content rendering.
    /// </summary>
    /// <param name="dpiScale">The monitor DPI scale, where 1 represents 96 DPI.</param>
    /// <param name="previewScale">The logical zoom chosen to fit the designed form in the surface.</param>
    /// <returns>The combined logical-design-unit to device-pixel scale.</returns>
    internal static double CombineWithPreviewScale(double dpiScale, float previewScale)
    {
        ValidateScale(dpiScale, nameof(dpiScale));
        ValidateScale(previewScale, nameof(previewScale));
        return dpiScale * previewScale;
    }

    private static void ValidateScale(double scale, string parameterName)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(parameterName, scale, "Scale must be a finite value greater than zero.");
    }
}
