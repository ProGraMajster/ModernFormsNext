using System;
using System.Threading;
using System.Diagnostics;
using ModernFormsNext.WindowKit.Diagnostics;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using ModernFormsNext.WindowKit.Backend.MicroCom;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32
{
    internal class FramebufferManager : IFramebufferPlatformSurface, IDisposable
    {
        private const int _bytesPerPixel = 4;
        private static readonly PixelFormat s_format = PixelFormat.Bgra8888;

        private readonly IntPtr _hwnd;
        private readonly object _lock;
        private readonly Action _onDisposeAction;
        private readonly Func<double> _getScaling;
        private IntPtr _paintDc;
        private UnmanagedMethods.RECT _paintRegion;

        internal bool IsFullPaint => _paintDc == IntPtr.Zero || AllocatedSize is not { } size ||
            (_paintRegion.left <= 0 && _paintRegion.top <= 0 &&
             _paintRegion.right >= size.Width && _paintRegion.bottom >= size.Height);

        private FramebufferData? _framebufferData;

        // These primitive facts describe real allocations, even when diagnostics starts after
        // the window was shown. They do not expose or retain native memory in a snapshot.
        internal PixelSize? AllocatedSize => _framebufferData?.Size;
        internal int? AllocatedRowBytes => _framebufferData?.RowBytes;
        internal long BackingGeneration { get; private set; }
        internal TimeSpan? PresentationCpuTime { get; private set; }

        public FramebufferManager(IntPtr hwnd, Func<double> getScaling)
        {
            _hwnd = hwnd;
            _lock = new object();
            _onDisposeAction = DrawAndUnlock;
            _getScaling = getScaling;
        }

        // Borrow BeginPaint's clipped DC only while the owning WM_PAINT is active. A stack
        // scope restores a prior context if application callbacks enter native painting again.
        internal PaintContext BeginPaint(IntPtr dc, UnmanagedMethods.RECT rectangle)
        {
            PresentationCpuTime = null;
            var scope = new PaintContext(this, _paintDc, _paintRegion);
            _paintDc = dc;
            _paintRegion = rectangle;
            return scope;
        }

        internal readonly struct PaintContext(FramebufferManager owner, IntPtr previousDc,
            UnmanagedMethods.RECT previousRegion) : IDisposable
        {
            public void Dispose() { owner._paintDc = previousDc; owner._paintRegion = previousRegion; }
        }

        public ILockedFramebuffer Lock()
        {
            Monitor.Enter(_lock);

            LockedFramebuffer? fb = null;

            try
            {
                UnmanagedMethods.GetClientRect(_hwnd, out var rc);

                var width = Math.Max(1, rc.right - rc.left);
                var height = Math.Max(1, rc.bottom - rc.top);

                if (_framebufferData is null || _framebufferData?.Size.Width != width || _framebufferData?.Size.Height != height)
                {
                    _framebufferData?.Dispose();

                    _framebufferData = AllocateFramebufferData(width, height);
                    BackingGeneration++;
                }

                var framebufferData = _framebufferData.Value;

                return fb = new LockedFramebuffer(
                    framebufferData.Data.Address, framebufferData.Size, framebufferData.RowBytes,
                    new Vector(96 * _getScaling(), 96 * _getScaling()), s_format, _onDisposeAction);
            }
            finally
            {
                // We free the lock when for whatever reason framebuffer was not created.
                // This allows for a potential retry later.
                if (fb is null)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _framebufferData?.Dispose();
                _framebufferData = null;
            }
        }

        private void DrawAndUnlock()
        {
            try
            {
                if (_framebufferData.HasValue) {
                    var data = _framebufferData.Value;
                    bool measure = PlatformPerformanceDiagnostics.IsEnabled;
                    long start = measure ? Stopwatch.GetTimestamp() : 0;
                    if (_paintDc == IntPtr.Zero) DrawToWindow(_hwnd, data);
                    else {
                        var header = data.Header;
                        DrawDamageToDevice(_paintDc, data.Data.Address, ref header, _paintRegion);
                    }
                    // CPU submission only: GDI returning does not imply DWM presentation.
                    if (measure) PresentationCpuTime = Stopwatch.GetElapsedTime(start);
                }
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        internal static void DrawDamageToDevice(IntPtr dc, IntPtr pixels,
            ref UnmanagedMethods.BITMAPINFOHEADER header, UnmanagedMethods.RECT damage)
        {
            int backingHeight = -header.biHeight;
            int left = Math.Max(0, damage.left), top = Math.Max(0, damage.top);
            int width = Math.Min(header.biWidth, damage.right) - left;
            int height = Math.Min(backingHeight, damage.bottom) - top;
            if (width > 0 && height > 0)
            {
                // Describe the damaged rows as a complete top-down DIB band. Cropping the
                // original negative-height DIB with a bottom-origin source y can select
                // different rows on DIB-section and device-dependent destinations (including
                // HWNDs), notably when the damage reaches the bottom edge with source (0,0).
                // Borrow the first damaged row, preserving the full framebuffer stride: no
                // pixel copy/allocation is needed, and only the damaged columns are presented.
                var bandHeader = header;
                bandHeader.biHeight = -height;
                var bandPixels = IntPtr.Add(pixels, checked(top * header.biWidth * _bytesPerPixel));
                UnmanagedMethods.StretchDIBits(dc, left, top, width, height,
                    left, 0, width, height, bandPixels, ref bandHeader, 0, 0x00CC0020);
            }
        }

        private static FramebufferData AllocateFramebufferData(int width, int height)
        {
            var service = AvaloniaGlobals.GetRequiredService<IRuntimePlatform>();
            var bitmapBlob = service.AllocBlob(checked(width * height * _bytesPerPixel));

            return new FramebufferData(bitmapBlob, width, height);
        }

        private static void DrawToDevice(FramebufferData framebufferData, IntPtr hDC, int destX = 0, int destY = 0, int srcX = 0,
            int srcY = 0, int width = -1,
            int height = -1)
        {
            if (width == -1)
                width = framebufferData.Size.Width;
            if (height == -1)
                height = framebufferData.Size.Height;

            var bmpInfo = framebufferData.Header;

            UnmanagedMethods.SetDIBitsToDevice(hDC, destX, destY, (uint)width, (uint)height, srcX, srcY,
                0, (uint)framebufferData.Size.Height, framebufferData.Data.Address, ref bmpInfo, 0);
        }

        private static bool DrawToWindow(IntPtr hWnd, FramebufferData framebufferData, int destX = 0, int destY = 0, int srcX = 0,
            int srcY = 0, int width = -1,
            int height = -1)
        {
            if (framebufferData.Data.IsDisposed)
                throw new ObjectDisposedException("Framebuffer");

            if (hWnd == IntPtr.Zero)
                return false;

            var hDC = UnmanagedMethods.GetDC(hWnd);

            if (hDC == IntPtr.Zero)
                return false;

            try
            {
                DrawToDevice(framebufferData, hDC, destX, destY, srcX, srcY, width, height);
            }
            finally
            {
                UnmanagedMethods.ReleaseDC(hWnd, hDC);
            }

            return true;
        }

        private readonly struct FramebufferData
        {
            public IUnmanagedBlob Data { get; }

            public PixelSize Size { get; }

            public int RowBytes => Size.Width * _bytesPerPixel;

            public UnmanagedMethods.BITMAPINFOHEADER Header { get; }

            public FramebufferData(IUnmanagedBlob data, int width, int height)
            {
                Data = data;
                Size = new PixelSize(width, height);

                var header = new UnmanagedMethods.BITMAPINFOHEADER();
                header.Init();

                header.biPlanes = 1;
                header.biBitCount = _bytesPerPixel * 8;
                header.Init();

                header.biWidth = width;
                header.biHeight = -height;

                Header = header;
            }

            public void Dispose()
            {
                Data.Dispose();
            }
        }
    }
}
