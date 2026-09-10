using System.Runtime.ExceptionServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Controls.Primitives.PopupPositioning;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Testing;

// Compose the existing headless state/framebuffer implementation, but expose IPopupImpl only.
// PopupWindow therefore follows the same capability checks as a real backend popup.
internal sealed class HeadlessPopupImpl : IPopupImpl
{
    private TestPopupHost? testHost;
    private bool disposing;

    internal HeadlessPopupImpl(IWindowBaseImpl parent)
    {
        State = new HeadlessWindowImpl(new TestViewport(100, 100, parent.RenderScaling));
        PopupPositioner = new ManagedPopupPositioner(new ManagedPopupPositionerPopupImplHelper(parent,
            (position, size, scale) =>
            {
                State.SetRenderScale(scale);
                State.Move(position);
                State.Resize(size);
            }));
    }

    internal HeadlessWindowImpl State { get; }
    internal PopupWindow? HostedPopup { get; private set; }
    internal TestPopupHost GetTestHost(TestWindowHost parent)
        => testHost ??= new TestPopupHost(parent, this);

    public IPopupPositioner? PopupPositioner { get; private set; }
    public Size ClientSize => State.ClientSize;
    public Size? FrameSize => State.FrameSize;
    public double RenderScaling => State.RenderScaling;
    public IEnumerable<object> Surfaces => State.Surfaces;
    public Action<RawInputEventArgs>? Input { get => State.Input; set => State.Input = value; }
    public Action<Rect>? Paint { get => State.Paint; set => State.Paint = value; }
    public Action<Size, WindowResizeReason>? Resized { get => State.Resized; set => State.Resized = value; }
    public Action<double>? ScalingChanged { get => State.ScalingChanged; set => State.ScalingChanged = value; }
    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get => State.TransparencyLevelChanged; set => State.TransparencyLevelChanged = value; }
    public Action? Closed { get => State.Closed; set => State.Closed = value; }
    public Action? LostFocus { get => State.LostFocus; set => State.LostFocus = value; }
    public double DesktopScaling => State.DesktopScaling;
    public PixelPoint Position => State.Position;
    public Action<PixelPoint>? PositionChanged { get => State.PositionChanged; set => State.PositionChanged = value; }
    public Action? Deactivated { get => State.Deactivated; set => State.Deactivated = value; }
    public Action? Activated { get => State.Activated; set => State.Activated = value; }
    public IPlatformHandle Handle => State.Handle;
    public Size MaxAutoSizeHint => State.MaxAutoSizeHint;
    public IScreenImpl Screen => State.Screen;
    public WindowTransparencyLevel TransparencyLevel => State.TransparencyLevel;
    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels => State.AcrylicCompensationLevels;

    public void SetInputRoot(IInputRoot inputRoot)
    {
        ArgumentNullException.ThrowIfNull(inputRoot);
        HostedPopup = inputRoot is ControlAdapter { ParentForm: PopupWindow popup }
            ? popup : throw new ArgumentException("A headless popup requires the production PopupWindow root.", nameof(inputRoot));
    }

    public object? TryGetFeature(Type featureType) => State.TryGetFeature(featureType);
    public Point PointToClient(PixelPoint point) => State.PointToClient(point);
    public PixelPoint PointToScreen(Point point) => State.PointToScreen(point);
    public void SetCursor(ICursorImpl? cursor) => State.SetCursor(cursor);
    public IPopupImpl? CreatePopup() => State.CreatePopup();
    public void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> levels) => State.SetTransparencyLevelHint(levels);
    public void SetFrameThemeVariant(PlatformThemeVariant variant) => State.SetFrameThemeVariant(variant);
    public void Invalidate(Rect rect) => State.Invalidate(rect);
    public void Show(bool activate, bool isDialog) => State.Show(activate, isDialog);
    public void Hide() => State.Hide();
    public void Activate() => State.Activate();
    public void SetTopmost(bool value) => State.SetTopmost(value);
    public void SetWindowManagerAddShadowHint(bool enabled) { }

    public void Dispose()
    {
        if (disposing || State.IsDisposed) return;
        disposing = true;
        var failures = new List<Exception>();
        PopupWindow? popup = HostedPopup;
        try
        {
            if (popup is not null)
            {
                // Hide through the canonical route while its backend is alive. A hidden popup
                // can be reused; destruction additionally revokes the managed tree and clock.
                TryCleanup(popup.Hide, failures);
                popup.InputClock = null;
            }
            TryCleanup(State.Dispose, failures);
            if (popup is not null)
            {
                TryCleanup(popup.adapter.Dispose, failures);
                TryCleanup(popup.Dispose, failures);
            }
        }
        finally
        {
            if (ReferenceEquals(Application.ActivePopupWindow, popup)) Application.ActivePopupWindow = null;
            testHost?.Revoke();
            testHost = null;
            HostedPopup = null;
            // The managed positioner retains the parent backend through its placement helper.
            PopupPositioner = null;
            disposing = false;
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Headless popup cleanup failed.", failures);
    }

    private static void TryCleanup(Action action, List<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }
}
