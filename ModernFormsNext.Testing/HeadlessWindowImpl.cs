using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;

namespace ModernFormsNext.Testing;

/// <summary>
/// Implements the existing WindowKit top-level contract without creating a native window or surface.
/// </summary>
internal sealed class HeadlessWindowImpl : IWindowImpl
{
    private readonly HeadlessPlatformHandle handle = new();
    private readonly HeadlessScreenImpl screen;
    private Size clientSize;
    private PixelPoint position;
    private double renderScaling;
    private WindowState windowState;
    private WindowTransparencyLevel transparencyLevel = WindowTransparencyLevel.None;
    private int pendingInvalidationCount;
    private bool enabled = true;

    public HeadlessWindowImpl(TestViewport viewport)
    {
        clientSize = new Size(viewport.Width, viewport.Height);
        renderScaling = viewport.RenderScale;
        screen = new HeadlessScreenImpl(() => clientSize, () => renderScaling);
    }

    public Size ClientSize => clientSize;

    public Size? FrameSize => clientSize;

    public double RenderScaling => renderScaling;

    public IEnumerable<object> Surfaces => Array.Empty<object>();

    public Action<RawInputEventArgs>? Input { get; set; }

    public Action<Rect>? Paint { get; set; }

    public Action<Size, WindowResizeReason>? Resized { get; set; }

    public Action<double>? ScalingChanged { get; set; }

    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

    public Action? Closed { get; set; }

    public Action? LostFocus { get; set; }

    public double DesktopScaling => renderScaling;

    public PixelPoint Position => position;

    public Action<PixelPoint>? PositionChanged { get; set; }

    public Action? Deactivated { get; set; }

    public Action? Activated { get; set; }

    public IPlatformHandle Handle => handle;

    public Size MaxAutoSizeHint => new(32768, 32768);

    public IScreenImpl Screen => screen;

    public WindowState WindowState
    {
        get => windowState;
        set
        {
            if (windowState == value)
                return;
            windowState = value;
            WindowStateChanged?.Invoke(value);
        }
    }

    public Action<WindowState>? WindowStateChanged { get; set; }

    public Action? GotInputWhenDisabled { get; set; }

    public Func<WindowCloseReason, bool>? Closing { get; set; }

    public bool IsClientAreaExtendedToDecorations { get; private set; }

    public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }

    public bool NeedsManagedDecorations { get; private set; } = true;

    public Thickness ExtendedMargins => default;

    public Thickness OffScreenMargin => default;

    public WindowTransparencyLevel TransparencyLevel => transparencyLevel;

    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels => default;

    internal bool IsShown { get; private set; }

    internal bool IsDisposed { get; private set; }

    internal bool IsEnabled => enabled;

    internal int PendingInvalidationCount => pendingInvalidationCount;

    internal int TotalInvalidationCount { get; private set; }

    internal bool HasNativeWindow => false;

    public object? TryGetFeature(Type featureType)
    {
        ArgumentNullException.ThrowIfNull(featureType);
        return null;
    }

    public void SetInputRoot(IInputRoot inputRoot)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);
    }

    public Point PointToClient(PixelPoint point)
        => new(
            (point.X - position.X) / renderScaling,
            (point.Y - position.Y) / renderScaling);

    public PixelPoint PointToScreen(Point point)
        => new(
            position.X + (int)Math.Round(point.X * renderScaling),
            position.Y + (int)Math.Round(point.Y * renderScaling));

    public void SetCursor(ICursorImpl? cursor)
    {
    }

    public IPopupImpl? CreatePopup() => null;

    public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
    {
        ArgumentNullException.ThrowIfNull(transparencyLevels);
        WindowTransparencyLevel next = transparencyLevels.Count > 0
            ? transparencyLevels[0]
            : WindowTransparencyLevel.None;
        if (transparencyLevel == next)
            return;
        transparencyLevel = next;
        TransparencyLevelChanged?.Invoke(next);
    }

    public void SetFrameThemeVariant(PlatformThemeVariant themeVariant)
    {
    }

    public void Invalidate(Rect rect)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        pendingInvalidationCount++;
        TotalInvalidationCount++;
    }

    public void Show(bool activate, bool isDialog)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        IsShown = true;
        if (activate)
            Activated?.Invoke();
    }

    public void Hide()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        IsShown = false;
    }

    public void Activate()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Activated?.Invoke();
    }

    public void SetTopmost(bool value)
    {
    }

    public void SetTitle(string? title)
    {
    }

    public void SetParent(IWindowImpl parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    public void SetEnabled(bool enable) => enabled = enable;

    public void SetSystemDecorations(SystemDecorations enabled)
        => NeedsManagedDecorations = enabled == SystemDecorations.None;

    public void SetIcon(SKBitmap? icon)
    {
    }

    public void ShowTaskbarIcon(bool value)
    {
    }

    public void CanResize(bool value)
    {
    }

    public void BeginMoveDrag(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
    }

    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
    }

    public void Resize(Size newClientSize, WindowResizeReason reason = WindowResizeReason.Application)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!double.IsFinite(newClientSize.Width) || newClientSize.Width < 0)
            throw new ArgumentOutOfRangeException(nameof(newClientSize));
        if (!double.IsFinite(newClientSize.Height) || newClientSize.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(newClientSize));
        if (clientSize == newClientSize)
            return;

        clientSize = newClientSize;
        Resized?.Invoke(clientSize, reason);
    }

    public void Move(PixelPoint point)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (position == point)
            return;
        position = point;
        PositionChanged?.Invoke(point);
    }

    public void SetMinMaxSize(Size minSize, Size maxSize)
    {
    }

    public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint)
    {
        if (IsClientAreaExtendedToDecorations == extendIntoClientAreaHint)
            return;
        IsClientAreaExtendedToDecorations = extendIntoClientAreaHint;
        ExtendClientAreaToDecorationsChanged?.Invoke(extendIntoClientAreaHint);
    }

    public void SetExtendClientAreaChromeHints(ExtendClientAreaChromeHints hints)
    {
    }

    public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight)
    {
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        IsShown = false;
        pendingInvalidationCount = 0;
        Closed?.Invoke();
        Input = null;
        Paint = null;
        Resized = null;
        ScalingChanged = null;
        TransparencyLevelChanged = null;
        Closed = null;
        LostFocus = null;
        PositionChanged = null;
        Deactivated = null;
        Activated = null;
        WindowStateChanged = null;
        GotInputWhenDisabled = null;
        Closing = null;
        ExtendClientAreaToDecorationsChanged = null;
    }

    internal void SetRenderScale(double scale)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Render scale must be finite and greater than zero.");
        if (Math.Abs(renderScaling - scale) < 0.000001d)
            return;

        renderScaling = scale;
        ScalingChanged?.Invoke(scale);
    }

    internal int ConsumePendingInvalidations()
    {
        var count = pendingInvalidationCount;
        pendingInvalidationCount = 0;
        return count;
    }

    private sealed class HeadlessPlatformHandle : IPlatformHandle
    {
        public IntPtr Handle => IntPtr.Zero;

        public string HandleDescriptor => "HEADLESS";
    }

    private sealed class HeadlessScreenImpl(Func<Size> getSize, Func<double> getScaling) : IScreenImpl
    {
        public int ScreenCount => 1;

        public IReadOnlyList<Screen> AllScreens => [CreateScreen()];

        public Screen ScreenFromWindow(IWindowBaseImpl window) => CreateScreen();

        public Screen ScreenFromPoint(PixelPoint point) => CreateScreen();

        public Screen ScreenFromRect(PixelRect rect) => CreateScreen();

        private Screen CreateScreen()
        {
            Size size = getSize();
            double scaling = getScaling();
            var bounds = new PixelRect(
                0,
                0,
                Math.Max(1, (int)Math.Round(size.Width * scaling)),
                Math.Max(1, (int)Math.Round(size.Height * scaling)));
            return new Screen(scaling, bounds, bounds, isPrimary: true);
        }
    }
}
