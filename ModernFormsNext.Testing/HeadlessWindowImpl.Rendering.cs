using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;

namespace ModernFormsNext.Testing;

internal sealed partial class HeadlessWindowImpl
{
    internal const int MaximumSnapshotPixels = 16_777_216;
    private object[] renderingSurfaces = [];

    internal void ValidateSnapshotSize(int maximumPixels)
        => GetSnapshotSize(maximumPixels);

    internal RenderedSnapshot CaptureRenderedSnapshot(int maximumPixels)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!IsShown || Paint is null)
            throw new InvalidOperationException("A rendered snapshot requires a shown headless window.");
        if (renderingSurfaces.Length != 0)
            throw new InvalidOperationException("A rendered snapshot cannot recursively paint the same window.");

        PixelSize size = GetSnapshotSize(maximumPixels);
        double capturedScale = renderScaling;
        using var framebuffer = new SnapshotFramebuffer(size, capturedScale);
        renderingSurfaces = [framebuffer];
        try
        {
            // This is the production backend callback registered by WindowBase. It discovers
            // the scoped framebuffer and paints normal Form chrome, the adapter and cached
            // control buffers. A separate surface/tree would hide runtime rendering regressions.
            Paint(new Rect(clientSize));
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return framebuffer.CopySnapshot(capturedScale);
        }
        finally
        {
            // Keep the pixels alive until the entire Paint callback unwinds, including when
            // application painting closes the window or throws. Ordinary headless operations
            // continue to expose no surface after this scope ends.
            renderingSurfaces = [];
        }
    }

    private PixelSize GetSnapshotSize(int maximumPixels)
    {
        if (maximumPixels <= 0 || maximumPixels > MaximumSnapshotPixels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPixels), maximumPixels,
                $"The framebuffer budget must be between 1 and {MaximumSnapshotPixels} device pixels.");
        }

        // Match WindowBase.ScaledClientSize, which truncates positive logical dimensions
        // after applying RenderScaling. Do not render an extra fractional row or column.
        double width = Math.Truncate(clientSize.Width * renderScaling);
        double height = Math.Truncate(clientSize.Height * renderScaling);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1 ||
            width > maximumPixels || height > maximumPixels || width * height > maximumPixels)
        {
            throw new InvalidOperationException(
                $"The scaled framebuffer ({width} by {height} device pixels) must have positive dimensions " +
                $"and contain at most {maximumPixels} pixels.");
        }

        return new PixelSize((int)width, (int)height);
    }

    private sealed class SnapshotFramebuffer : IFramebufferPlatformSurface, IDisposable
    {
        private readonly SKBitmap bitmap;
        private readonly Vector dpi;
        private bool locked;
        private bool disposed;

        internal SnapshotFramebuffer(PixelSize size, double scale)
        {
            bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (bitmap.GetPixels() == IntPtr.Zero)
            {
                bitmap.Dispose();
                throw new InvalidOperationException("Skia could not allocate the bounded snapshot framebuffer.");
            }
            bitmap.Erase(SKColors.Transparent);
            dpi = new Vector(96 * scale, 96 * scale);
        }

        public ILockedFramebuffer Lock()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (locked)
                throw new InvalidOperationException("The snapshot framebuffer is already locked for painting.");

            locked = true;
            return new LockedFramebuffer(
                bitmap.GetPixels(), new PixelSize(bitmap.Width, bitmap.Height), bitmap.RowBytes,
                dpi, PixelFormat.Bgra8888, () => locked = false);
        }

        internal RenderedSnapshot CopySnapshot(double renderScale)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (locked)
                throw new InvalidOperationException("The production renderer did not release the snapshot framebuffer.");

            int rowBytes = checked(bitmap.Width * 4);
            byte[] pixels = new byte[checked(rowBytes * bitmap.Height)];
            // Copy each row explicitly so the detached format never exposes native row padding.
            for (int row = 0; row < bitmap.Height; row++)
                Marshal.Copy(IntPtr.Add(bitmap.GetPixels(), row * bitmap.RowBytes), pixels, row * rowBytes, rowBytes);
            return new RenderedSnapshot(bitmap.Width, bitmap.Height, renderScale, pixels);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            bitmap.Dispose();
        }
    }
}
