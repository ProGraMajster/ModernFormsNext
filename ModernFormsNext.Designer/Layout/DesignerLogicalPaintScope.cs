using ModernFormsNext;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Presents a device-pixel Skia canvas as a logical-pixel canvas for designer chrome.
/// </summary>
/// <remarks>
/// ModernFormsNext control sizes and designer panel metrics are logical pixels, while each
/// control's backing bitmap uses device pixels. The scope applies the monitor DPI transform once
/// and supplies paint arguments whose scale is 1, allowing panel renderers to use one consistent
/// logical coordinate system for backgrounds, clipping, text, rows, and adornments.
/// </remarks>
internal sealed class DesignerLogicalPaintScope : IDisposable
{
    private readonly SKCanvas canvas;
    private bool disposed;

    private DesignerLogicalPaintScope(PaintEventArgs devicePaintArgs)
    {
        ArgumentNullException.ThrowIfNull(devicePaintArgs);

        if (!double.IsFinite(devicePaintArgs.Scaling) || devicePaintArgs.Scaling <= 0)
            throw new ArgumentOutOfRangeException(nameof(devicePaintArgs), "Paint scaling must be finite and greater than zero.");

        canvas = devicePaintArgs.Canvas;
        canvas.Save();
        canvas.Scale((float)devicePaintArgs.Scaling);
        PaintArgs = new PaintEventArgs(devicePaintArgs.Info, canvas, scaling: 1d);
    }

    /// <summary>
    /// Gets paint arguments whose canvas and scale are expressed in logical designer pixels.
    /// </summary>
    /// <remarks>
    /// The backing <see cref="PaintEventArgs.Info"/> remains device-pixel metadata for the
    /// existing control bitmap; designer chrome must use its logical control dimensions for
    /// layout.
    /// </remarks>
    internal PaintEventArgs PaintArgs { get; }

    /// <summary>
    /// Enters the logical coordinate space for a designer chrome paint pass.
    /// </summary>
    /// <param name="devicePaintArgs">The original device-pixel paint arguments.</param>
    /// <returns>A scope that restores the canvas transform when disposed.</returns>
    internal static DesignerLogicalPaintScope Begin(PaintEventArgs devicePaintArgs)
        => new(devicePaintArgs);

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;

        canvas.Restore();
        disposed = true;
    }
}
