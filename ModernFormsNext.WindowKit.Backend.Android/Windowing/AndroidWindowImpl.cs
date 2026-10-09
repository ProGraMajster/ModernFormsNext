using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

internal class AndroidWindowImpl(AndroidWindowingPlatform platform) : IWindowImpl, IWindowInsetsProvider, IWindowHostPolicy
{
    internal readonly AndroidWindowingPlatform Platform = platform;
    internal readonly AndroidNativeViewHostProvider NativeViewHosts = new();
    internal readonly AndroidWindowTextInput TextInput = new(platform.VerifyAccess);
    internal IInputRoot? InputRoot { get; private set; }
    internal AndroidWindowImpl? Owner { get; private set; }
    internal virtual bool IsPopup => false;
    internal bool IsDialog { get; set; }
    internal bool Visible { get; set; }
    internal bool Enabled { get; private set; } = true;
    internal bool IsClosed { get; private set; }
    internal bool Active { get; private set; }
    internal bool Attached { get; private set; }
    internal Size RequestedSize { get; private set; } = new(400, 300);
    internal PixelPoint RequestedPosition { get; set; }
    internal string? Title { get; private set; }
    internal object[] NativeSurfaces { get; set; } = [];
    internal double ScaledDensity { get; set; } = 1;
    internal long PaintCount { get; set; }
    internal int ActivePointers { get; set; }
    internal long PresentationEpoch { get; private set; }
    internal event Action? PresentationRetired;
    internal void BeginPresentation() => PresentationEpoch++;
    private bool closing;
    private readonly AndroidScreenImpl screen = new();

    public Size ClientSize { get; private set; } = new(400, 300);
    public Size? FrameSize => ClientSize;
    public double RenderScaling { get; private set; } = 1;
    public double DesktopScaling => RenderScaling;
    public PixelPoint Position { get; private set; }
    public Size MaxAutoSizeHint => screen.AllScreens[0].WorkingArea.Size.ToSize(RenderScaling);
    public IPlatformHandle Handle { get; private set; } = new PlatformHandle(IntPtr.Zero, "AndroidView");
    public IScreenImpl Screen => screen;
    public IEnumerable<object> Surfaces => NativeSurfaces;
    public bool IsHostManaged => true;
    public WindowInsets CurrentInsets { get; private set; }
    public event EventHandler<WindowInsetsChangedEventArgs>? InsetsChanged;
    public Action<RawInputEventArgs>? Input { get; set; }
    public Action<Rect>? Paint { get; set; }
    public Action<Size, WindowResizeReason>? Resized { get; set; }
    public Action<double>? ScalingChanged { get; set; }
    public Action<PixelPoint>? PositionChanged { get; set; }
    public Action? Closed { get; set; }
    public Action? LostFocus { get; set; }
    public Action? Activated { get; set; }
    public Action? Deactivated { get; set; }
    public Action? GotInputWhenDisabled { get; set; }
    public Func<WindowCloseReason, bool>? Closing { get; set; }
    public Action<WindowState>? WindowStateChanged { get; set; }
    public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }
    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }
    public bool IsClientAreaExtendedToDecorations => false;
    public bool NeedsManagedDecorations => false;
    public Thickness ExtendedMargins => default;
    public Thickness OffScreenMargin => default;
    public WindowTransparencyLevel TransparencyLevel => WindowTransparencyLevel.None;
    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels => default;
    public WindowState WindowState
    {
        get => WindowState.Normal;
        set { Verify(); if (value != WindowState.Normal) Unsupported("desktop window state"); }
    }

    internal void Verify() { Platform.VerifyAccess(); ObjectDisposedException.ThrowIf(IsClosed, this); }
    internal static void Unsupported(string operation) => throw new PlatformNotSupportedException(
        $"Android Activity windows do not support {operation}. See the Android window capability matrix.");
    public void SetInputRoot(IInputRoot inputRoot) { Verify(); InputRoot = inputRoot; }
    public object? TryGetFeature(Type featureType)
        => featureType == typeof(INativeViewHostProvider) ? NativeViewHosts :
            featureType == typeof(ModernFormsNext.WindowKit.Platform.Storage.IStorageProvider) ? (IsClosed ? null : Platform.StorageProvider) :
            featureType == typeof(ITextInputMethod) ? TextInput :
            featureType == typeof(IWindowInsetsProvider) || featureType == typeof(IWindowHostPolicy) ? this : null;
    public virtual void Show(bool activate, bool isDialog)
    {
        Verify();
        Platform.Show(this, isDialog);
        if (activate) Platform.Host?.Activate(this);
    }
    public void Hide() { Verify(); Platform.Hide(this); }
    public void Activate() { Verify(); if (Visible && Enabled) Platform.Host?.Activate(this); }
    public void Invalidate(Rect rect) { Verify(); if (Visible) Platform.Host?.Invalidate(this); }
    public void SetTitle(string? title) { Verify(); Title = title; Platform.Host?.Update(this); }
    public void SetParent(IWindowImpl parent)
    {
        Verify();
        if (parent is not AndroidWindowImpl owner || !ReferenceEquals(owner.Platform, Platform))
            throw new ArgumentException("The owner must belong to the same Android host.", nameof(parent));
        owner.Verify();
        for (var ancestor = owner; ancestor is not null; ancestor = ancestor.Owner)
            if (ReferenceEquals(ancestor, this)) throw new ArgumentException("Window ownership cannot contain a cycle.", nameof(parent));
        Owner = owner;
    }
    public void SetEnabled(bool enable) { Verify(); Enabled = enable; Platform.Host?.Update(this); NativeViewHosts.NotifyState(); }
    public void Resize(Size size, WindowResizeReason reason = WindowResizeReason.Application)
    {
        Verify();
        if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width < 0 || size.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        // A size setter is a presentation hint. Only native layout confirms ClientSize.
        RequestedSize = size;
        Platform.Host?.Update(this);
    }
    public virtual void Move(PixelPoint point) { Verify(); Unsupported("arbitrary top-level positioning"); }
    public Point PointToClient(PixelPoint point) => new((point.X - Position.X) / RenderScaling, (point.Y - Position.Y) / RenderScaling);
    public PixelPoint PointToScreen(Point point) => new(Position.X + (int)Math.Round(point.X * RenderScaling), Position.Y + (int)Math.Round(point.Y * RenderScaling));
    public IPopupImpl CreatePopup() { Verify(); return Platform.CreatePopup(this); }
    public void SetTopmost(bool value) { Verify(); if (value) Unsupported("topmost windows"); }
    public void SetIcon(SKBitmap? icon) { Verify(); if (icon is not null) Unsupported("per-Form launcher icons"); }
    public void ShowTaskbarIcon(bool value) { Verify(); Unsupported("per-Form taskbar visibility"); }
    public void CanResize(bool value) { Verify(); Unsupported("application-controlled Activity resizability"); }
    public void SetMinMaxSize(Size minSize, Size maxSize)
    {
        Verify();
        if (minSize != default || maxSize != default) Unsupported("per-Form min/max Activity constraints");
    }
    public void BeginMoveDrag(PointerPressedEventArgs e) { Verify(); Unsupported("desktop move dragging"); }
    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e) { Verify(); Unsupported("desktop resize dragging"); }
    // These existing hint contracts allow the native host to choose its own chrome. Android
    // owns bars/decor fitting; no managed desktop title bar or fictitious extension is reported.
    public void SetSystemDecorations(SystemDecorations enabled) { Verify(); }
    public void SetExtendClientAreaToDecorationsHint(bool value) { Verify(); }
    public void SetExtendClientAreaChromeHints(ExtendClientAreaChromeHints hints) { Verify(); }
    public void SetExtendClientAreaTitleBarHeightHint(double height) { Verify(); }
    public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> levels) { Verify(); }
    public void SetFrameThemeVariant(PlatformThemeVariant variant) { Verify(); }
    public void SetCursor(ICursorImpl? cursor) { Verify(); if (cursor is not null) Unsupported("custom window cursors"); }

    internal void ConfirmGeometry(Size size, double density, PixelPoint origin, Screen display, IPlatformHandle handle)
    {
        Verify();
        bool wasAttached = Attached;
        bool resized = ClientSize != size, scaled = RenderScaling != density, moved = Position != origin;
        ClientSize = size; RenderScaling = density; Position = origin; Handle = handle; Attached = true;
        screen.Set(display);
        AndroidWindowingPlatform.Complete([
            () => { if (scaled) ScalingChanged?.Invoke(density); },
            () => { if (resized) Resized?.Invoke(size, WindowResizeReason.Unspecified); },
            () => { if (moved) PositionChanged?.Invoke(origin); },
            () => { if (!wasAttached) NativeViewHosts.ConfirmGeometry(); }
        ]);
    }
    internal void ConfirmInsets(WindowInsets value)
    {
        Verify();
        if (CurrentInsets == value) return;
        CurrentInsets = value;
        InsetsChanged?.Invoke(this, new(value));
    }
    internal void ConfirmFocus(bool focused)
    {
        if (IsClosed) return;
        focused &= Visible && Enabled && !IsPopup;
        if (Active == focused) return;
        Active = focused;
        if (focused) Activated?.Invoke();
        else AndroidWindowingPlatform.Complete([() => (InputRoot as IWindowSurfaceInput)?.CancelInput(),
            () => Deactivated?.Invoke(), () => LostFocus?.Invoke()]);
    }
    internal void RetirePresentation() => RetirePresentation(PresentationEpoch);
    internal void RetirePresentation(long expectedEpoch)
    {
        if (expectedEpoch != PresentationEpoch) return;
        long operation = ++PresentationEpoch;
        // Revoke the epoch before dismissing any request owned by this presentation.
        PresentationRetired?.Invoke();
        Attached = false;
        ActivePointers = 0;
        Handle = new PlatformHandle(IntPtr.Zero, "AndroidView");
        NativeSurfaces = [];
        AndroidWindowingPlatform.Complete([
            () => ConfirmFocus(false),
            () => { if (operation == PresentationEpoch) (InputRoot as IWindowSurfaceInput)?.CancelInput(); },
            () => { if (operation == PresentationEpoch) TextInput.Attach(null); }
        ]);
    }
    internal void DismissPopup() => (InputRoot as IWindowSurfaceInput)?.DismissPopup();
    internal bool RequestClose()
    {
        Verify();
        if (closing) return false;
        closing = true;
        try
        {
            if (Closing?.Invoke(WindowCloseReason.WindowClosing) == true) return false;
            Dispose();
            return true;
        }
        finally { closing = false; }
    }
    public void Dispose()
    {
        Platform.VerifyAccess();
        if (IsClosed) return;
        IsClosed = true;
        Visible = false;
        Platform.Remove(this);
        AndroidWindowingPlatform.Complete([
            () => Platform.CloseDescendants(this),
            RetirePresentation,
            () => Platform.Host?.Hide(this),
            () => TextInput.SetClient(null),
            () => Closed?.Invoke(),
            ClearCallbacks
        ]);
    }
    private void ClearCallbacks()
    {
        InputRoot = null; Input = null; Paint = null; Resized = null; ScalingChanged = null;
        PositionChanged = null; Closed = null; LostFocus = null; Activated = null; Deactivated = null;
        Closing = null; InsetsChanged = null; WindowStateChanged = null; GotInputWhenDisabled = null;
        ExtendClientAreaToDecorationsChanged = null; TransparencyLevelChanged = null;
    }
}
