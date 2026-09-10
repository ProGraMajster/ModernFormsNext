using System.Runtime.InteropServices;
using SkiaSharp;

namespace ModernFormsNext.Testing;

/// <summary>Stores immutable, detached device pixels captured from a headless window.</summary>
/// <remarks>
/// The caller owns this snapshot. It retains no controls, host, framebuffer or native resources.
/// Pixel reads and encoding may run on any thread after capture. Dispose releases the snapshot's
/// reference to its pixel data; a read that started before concurrent disposal may finish normally.
/// Copies already returned to the caller remain valid. Dimensions and scale remain readable after
/// disposal. Snapshots do not normalize platform fonts or provide golden-image comparison.
/// </remarks>
public sealed class RenderedSnapshot : IDisposable
{
    private byte[]? pixels;

    internal RenderedSnapshot(int pixelWidth, int pixelHeight, double renderScale, byte[] pixels)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        RenderScale = renderScale;
        this.pixels = pixels;
    }

    /// <summary>Gets the width in device pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>Gets the height in device pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>Gets the logical-to-device scale applied when this frame was captured.</summary>
    public double RenderScale { get; }

    /// <summary>Gets the tightly packed row length in bytes, equal to four times <see cref="PixelWidth"/>.</summary>
    public int RowBytes => PixelWidth * 4;

    /// <summary>Gets whether the snapshot's pixel data has been released.</summary>
    public bool IsDisposed => Volatile.Read(ref pixels) is null;

    /// <summary>Copies the complete image into a new caller-owned byte array.</summary>
    /// <returns>
    /// Top-to-bottom, tightly packed BGRA8888 pixels with premultiplied alpha. Mutating this array
    /// cannot change the snapshot. Each row contains <see cref="RowBytes"/> bytes.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The snapshot has been disposed.</exception>
    public byte[] CopyPixels() => (byte[])GetPixels().Clone();

    /// <summary>Reads a pixel as a straight-alpha Skia color.</summary>
    /// <param name="x">The zero-based device-pixel column.</param>
    /// <param name="y">The zero-based device-pixel row.</param>
    /// <returns>The pixel color. Premultiplied channels are expanded with integer rounding.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is outside the captured image.</exception>
    /// <exception cref="ObjectDisposedException">The snapshot has been disposed.</exception>
    public SKColor GetPixel(int x, int y)
    {
        byte[] source = GetPixels();
        if (x < 0 || x >= PixelWidth)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= PixelHeight)
            throw new ArgumentOutOfRangeException(nameof(y));
        int offset = y * RowBytes + x * 4;
        byte alpha = source[offset + 3];
        return new SKColor(
            Unpremultiply(source[offset + 2], alpha),
            Unpremultiply(source[offset + 1], alpha),
            Unpremultiply(source[offset], alpha), alpha);
    }

    /// <summary>Encodes this image as a lossless PNG in a new caller-owned array.</summary>
    /// <returns>PNG bytes that remain valid after the snapshot is disposed.</returns>
    /// <remarks>No file or stream is written. Temporary native encoding resources are disposed before returning.</remarks>
    /// <exception cref="ObjectDisposedException">The snapshot has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Skia cannot allocate or encode the image.</exception>
    public byte[] EncodePng()
    {
        byte[] source = GetPixels();
        using var bitmap = new SKBitmap(new SKImageInfo(PixelWidth, PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (bitmap.GetPixels() == IntPtr.Zero)
            throw new InvalidOperationException("Skia could not allocate the snapshot PNG buffer.");
        for (int row = 0; row < PixelHeight; row++)
            Marshal.Copy(source, row * RowBytes, IntPtr.Add(bitmap.GetPixels(), row * bitmap.RowBytes), RowBytes);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Skia could not encode the snapshot as PNG.");
        return encoded.ToArray();
    }

    /// <summary>Releases the snapshot's pixel storage. Repeated calls have no effect.</summary>
    public void Dispose() => Interlocked.Exchange(ref pixels, null);

    private byte[] GetPixels()
        => Volatile.Read(ref pixels) ?? throw new ObjectDisposedException(nameof(RenderedSnapshot));

    private static byte Unpremultiply(byte channel, byte alpha)
        => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (channel * 255 + alpha / 2) / alpha);
}
