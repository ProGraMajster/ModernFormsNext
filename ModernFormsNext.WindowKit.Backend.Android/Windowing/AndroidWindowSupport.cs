using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Controls.Primitives.PopupPositioning;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

internal sealed class AndroidWindowTextInput(Action verifyAccess) : ITextInputMethod
{
    private readonly WeakHostReference<ITextInputMethod> native = new();
    private ITextInputClient? client;
    private long generation;
    internal void Attach(ITextInputMethod? method)
    {
        verifyAccess();
        var old = native.Target;
        long operation = ++generation;
        if (ReferenceEquals(old, method)) return;
        if (old is not null) native.ClearIfCurrent(old);
        try { old?.SetClient(null); }
        finally
        {
            // Detaching an IME can invoke application composition observers. A newer
            // attachment made by such an observer must survive this older transaction.
            if (generation == operation && method is not null) { native.Set(method); method.SetClient(client); }
        }
    }
    public void SetClient(ITextInputClient? value) { verifyAccess(); client = value; native.Target?.SetClient(value); }
    internal void Detach(ITextInputMethod expected)
    {
        verifyAccess();
        if (ReferenceEquals(native.Target, expected)) Attach(null);
    }
    public bool SetKeyboardVisible(bool visible) { verifyAccess(); return native.Target?.SetKeyboardVisible(visible) ?? false; }
}

internal sealed class AndroidScreenImpl : IScreenImpl
{
    private Screen[] screens = [new(1, new PixelRect(0, 0, 400, 300), new PixelRect(0, 0, 400, 300), true)];
    internal void Set(Screen display) => screens = [display];
    public int ScreenCount => 1;
    public IReadOnlyList<Screen> AllScreens => screens;
    public Screen ScreenFromWindow(IWindowBaseImpl window) => screens[0];
    public Screen ScreenFromPoint(PixelPoint point) => screens[0];
    public Screen ScreenFromRect(PixelRect rect) => screens[0];
}

internal sealed class AndroidPopupImpl : AndroidWindowImpl, IPopupImpl, IPopupPositioner
{
    internal AndroidPopupImpl(AndroidWindowingPlatform platform, AndroidWindowImpl owner) : base(platform)
        => SetParent(owner);
    internal override bool IsPopup => true;
    public IPopupPositioner PopupPositioner => this;
    public void SetWindowManagerAddShadowHint(bool enabled) { Verify(); }
    public override void Move(PixelPoint point) { Verify(); RequestedPosition = point; Platform.Host?.Update(this); }
    public void Update(PopupPositionerParameters parameters)
    {
        Verify();
        // Shared popup callers provide a top-left anchor and bottom-right gravity. Clamp the
        // resulting presentation to the Activity area; there is no independent desktop screen.
        if (parameters.Anchor != PopupAnchor.TopLeft || parameters.Gravity != PopupGravity.BottomRight)
            Unsupported("this popup anchor/gravity combination");
        RequestedPosition = Owner!.PointToScreen(parameters.AnchorRectangle.TopLeft + parameters.Offset);
        Resize(parameters.Size);
    }
    public override void Show(bool activate, bool isDialog) => base.Show(false, false);
}
