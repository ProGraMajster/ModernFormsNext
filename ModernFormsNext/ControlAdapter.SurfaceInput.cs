using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext;

internal partial class ControlAdapter : IWindowSurfaceInput, WindowKit.Platform.Accessibility.IPlatformAccessibilitySurface
{
    private event Action<WindowKit.Platform.Accessibility.IPlatformAccessibleObject, int, int, int>? surfaceAccessibility;
    event Action<WindowKit.Platform.Accessibility.IPlatformAccessibleObject, int, int, int>?
        WindowKit.Platform.Accessibility.IPlatformAccessibilitySurface.AccessibilityNotification
    {
        add => surfaceAccessibility += value;
        remove => surfaceAccessibility -= value;
    }
    internal bool TryNotifySurfaceAccessibility(WindowKit.Platform.Accessibility.IPlatformAccessibleObject source,
        int eventId, int objectId, int childId)
    {
        if (surfaceAccessibility is null) return false;
        surfaceAccessibility(source, eventId, objectId, childId);
        return true;
    }
    private SkiaControlSurface? surfaceInput;
    internal Func<Control, bool>? PreserveSurfacePointer;
    internal Action<Control>? SurfaceFocusOwnerLost;
    private SkiaControlSurface SurfaceInput => surfaceInput ??= new SkiaControlSurface(ParentForm);
    internal override bool PreserveFocusPointerInteraction(Control previous) => PreserveSurfacePointer?.Invoke(previous) == true;
    internal override void OnFocusOwnerLost(Control previous) => SurfaceFocusOwnerLost?.Invoke(previous);

    void IWindowSurfaceInput.Pointer(int id, WindowSurfacePointerAction action, WindowKit.Point point)
    {
        WindowKit.Threading.Dispatcher.UIThread.VerifyAccess();
        if (ParentForm.IsBackendClosed || !ParentForm.Visible) return;
        // Native coordinates are logical. Existing control hit testing consumes device-space
        // coordinates, so apply density once here, just as WindowBase's raw pointer transport.
        var scale = ParentForm.Scaling;
        var display = ParentForm.DisplayRectangle;
        SurfaceInput.PointerDragThreshold = Math.Max(1, (int)Math.Round(8 * scale));
        SurfaceInput.ProcessPointer(id, (ControlSurfacePointerAction)action,
            (int)Math.Round((point.X - display.Left) * scale),
            (int)Math.Round((point.Y - display.Top) * scale));
        if (action == WindowSurfacePointerAction.Up && !ParentForm.IsBackendClosed)
            ParentForm.RequestSoftwareKeyboard(ParentForm.TextInputClient is not null);
    }

    bool IWindowSurfaceInput.Key(Key key, WindowKit.Input.KeyModifiers modifiers, bool down,
        bool textInput, bool deadKey, bool canceled)
    {
        if (ParentForm.IsBackendClosed || !ParentForm.Visible) return true;
        var args = KeyEventArgs.FromPlatformKey(key, modifiers);
        return down ? SurfaceInput.TryProcessKeyDown(args, textInput, deadKey)
            : SurfaceInput.TryProcessKeyUp(args, textInput, canceled);
    }

    void IWindowSurfaceInput.CancelInput()
    {
        if (surfaceInput is null) return;
        try { surfaceInput.ProcessPointer(ControlSurfacePointerAction.Cancel, 0, 0); }
        finally { surfaceInput.ResetKeyboardState(); }
    }

    void IWindowSurfaceInput.DismissPopup()
    {
        if (ParentForm is PopupWindow) ParentForm.Hide();
    }

    internal void DisposeSurfaceInput()
    {
        var previous = surfaceInput;
        surfaceInput = null;
        previous?.Dispose();
    }
}
