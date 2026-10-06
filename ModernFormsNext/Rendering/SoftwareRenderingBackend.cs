using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls.Platform.Surfaces;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Skia;
using SkiaSharp;

namespace ModernFormsNext.Rendering;

/// <summary>CPU raster adapter for existing platform framebuffer presentation.</summary>
internal sealed class SoftwareRenderingBackend(RenderingBackend requested) : IRenderingBackend
{
    public RenderingBackend RequestedBackend { get; } = requested;
    public RenderingBackend ActiveBackend => RenderingBackend.Software;
    public string Renderer => "Skia Raster";
    public IWindowRenderSurface CreateSurface(ITopLevelImpl window) => new SoftwareWindowSurface(this, window);

    private sealed class SoftwareWindowSurface(SoftwareRenderingBackend backend, ITopLevelImpl window) : IWindowRenderSurface
    {
        private ITopLevelImpl? target = window;
        private FrameResources? available;
        private readonly int ownerThread = Environment.CurrentManagedThreadId;

        internal void VerifyAccess()
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException("Render surfaces and frames belong to their creating UI thread.");
        }

        public IRenderFrame AcquireFrame(Rect damage)
        {
            VerifyAccess();
            var current = target;
            ObjectDisposedException.ThrowIf(current is null, this);
            // Never cache a platform surface: Android replaces it on presentation epochs, and
            // TestHost exposes a distinct, scoped framebuffer on every capture.
            IFramebufferPlatformSurface? platformSurface = null;
            foreach (object candidate in current.Surfaces)
                if (candidate is IFramebufferPlatformSurface framebuffer) { platformSurface = framebuffer; break; }
            if (platformSurface is null)
                throw new InvalidOperationException("The current window presentation does not expose a software framebuffer.");

            var frame = available ?? new FrameResources(this, backend);
            available = frame.Next;
            frame.Next = null;
            try {
                frame.Acquire(platformSurface, current.ClientSize, current.RenderScaling, damage);
                return new SoftwareFrame(frame);
            }
            catch {
                frame.Dispose();
                throw;
            }
        }

        internal void Return(FrameResources frame)
        {
            if (target is null) return;
            // A tiny free list permits nested native paints without allocating in steady state.
            // Only resource holders are reused. Leases are never reused: a retired reference
            // must not gain access to a later paint or release its lock through another Dispose.
            frame.Next = available;
            available = frame;
        }

        public void Dispose()
        {
            VerifyAccess();
            target = null;
            available = null;
        }
    }

    private sealed class SoftwareFrame(FrameResources resources) : IRenderFrame
    {
        private FrameResources? resources = resources;

        private FrameResources Active
        {
            get {
                var current = resources ?? throw new ObjectDisposedException(nameof(IRenderFrame));
                current.VerifyDrawing();
                return current;
            }
        }

        public SKCanvas Canvas => Active.Canvas;
        public SKImageInfo ImageInfo => Active.ImageInfo;
        public Size LogicalSize => Active.LogicalSize;
        public double Scale => Active.Scale;
        public Rect Damage => Active.Damage;
        public PerformanceRenderInfo RenderInfo => Active.RenderInfo;
        public void Complete() => Active.Complete();

        public void Dispose()
        {
            var current = resources;
            if (current is null) return;
            current.VerifyAccess();
            // Revoke before unlocking: native presentation may reenter painting or disposal.
            // A second Dispose on this lease must never reach a recycled resource holder.
            resources = null;
            current.Dispose();
        }
    }

    private sealed class FrameResources(SoftwareWindowSurface owner, SoftwareRenderingBackend backend) : IDisposable
    {
        private ILockedFramebuffer? framebuffer;
        private SKSurface? surface;
        private bool leased;
        private bool completed;
        internal FrameResources? Next;

        internal void VerifyAccess() => owner.VerifyAccess();

        internal void VerifyDrawing()
        {
            VerifyAccess();
            if (!leased || completed) throw new ObjectDisposedException(nameof(IRenderFrame));
        }

        public SKCanvas Canvas => !completed && surface is not null ? surface.Canvas :
            throw new ObjectDisposedException(nameof(IRenderFrame));
        public SKImageInfo ImageInfo { get; private set; }
        public Size LogicalSize { get; private set; }
        public double Scale { get; private set; }
        public Rect Damage { get; private set; }

        internal void Acquire(IFramebufferPlatformSurface platform, Size logicalSize, double scale, Rect damage)
        {
            leased = true;
            completed = false;
            framebuffer = platform.Lock();
            ImageInfo = new SKImageInfo(framebuffer.Size.Width, framebuffer.Size.Height,
                framebuffer.Format.ToSkColorType(), framebuffer.Format == PixelFormat.Rgb565 ? SKAlphaType.Opaque : SKAlphaType.Premul);
            LogicalSize = logicalSize;
            Scale = scale;
            Damage = damage;
            surface = SKSurface.Create(ImageInfo, framebuffer.Address, framebuffer.RowBytes)
                ?? throw new InvalidOperationException("Skia could not create the software rendering surface.");
        }

        public PerformanceRenderInfo RenderInfo {
            get {
                var backing = framebuffer ?? throw new ObjectDisposedException(nameof(IRenderFrame));
                return new PerformanceRenderInfo {
                    RequestedBackend = backend.RequestedBackend, ActiveBackend = backend.ActiveBackend,
                    Renderer = backend.Renderer, Acceleration = PerformanceAcceleration.Software,
                    Boundary = PerformanceFrameBoundary.SharedRender,
                    Scale = Scale, LogicalWidth = (int)LogicalSize.Width, LogicalHeight = (int)LogicalSize.Height,
                    PixelWidth = ImageInfo.Width, PixelHeight = ImageInfo.Height,
                    PixelFormat = backing.Format == PixelFormat.Bgra8888 ? "BGRA8888" :
                        backing.Format == PixelFormat.Rgba8888 ? "RGBA8888" :
                        backing.Format == PixelFormat.Rgb565 ? "RGB565" : null,
                    RowBytes = backing.RowBytes, BackingBytes = (long)backing.RowBytes * backing.Size.Height,
                    Redraw = Damage.Contains(new Rect(LogicalSize)) ? PerformanceRedraw.FullSurface : PerformanceRedraw.PartialSurface
                };
            }
        }

        public void Complete()
        {
            owner.VerifyAccess();
            ObjectDisposedException.ThrowIf(!leased, this);
            // Raster drawing already writes directly to backing. Seal the drawing lease now,
            // but unlock only when the using scope ends, after profiler disposal (legacy order).
            completed = true;
        }

        public void Dispose()
        {
            owner.VerifyAccess();
            if (!leased) return;
            leased = false;
            var previousSurface = surface;
            var previousFramebuffer = framebuffer;
            surface = null;
            framebuffer = null;
            try {
                // Retire Skia before unlocking memory. Always release the framebuffer even if
                // Skia disposal fails, and propagate the platform's unlock/presentation failure.
                try { previousSurface?.Dispose(); }
                finally { previousFramebuffer?.Dispose(); }
            }
            finally { owner.Return(this); }
        }
    }
}
