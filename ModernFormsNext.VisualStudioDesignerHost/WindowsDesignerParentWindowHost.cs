using System.ComponentModel;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.VisualStudioDesignerHost;

/// <summary>
/// Parents the modern .NET Designer window into the Visual Studio editor pane that launched it.
/// </summary>
/// <remarks>
/// This Windows-only adapter consumes the stable <see cref="WindowBase.PlatformHandle"/> contract.
/// It never reflects over framework internals and never owns or destroys the Visual Studio HWND.
/// Visual Studio owns the parent client bounds and chrome; the hosted form remains a borderless
/// child for its complete visible lifetime.
/// </remarks>
internal sealed class WindowsDesignerParentWindowHost
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const uint GwOwner = 4;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    private const long WsBorder = 0x00800000L;
    private const long WsDlgFrame = 0x00400000L;
    private const long WsCaption = WsBorder | WsDlgFrame;
    private const long WsChild = 0x40000000L;
    private const long WsClipSiblings = 0x04000000L;
    private const long WsMaximize = 0x01000000L;
    private const long WsMinimize = 0x20000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;

    private const long WsExDlgModalFrame = 0x00000001L;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExWindowEdge = 0x00000100L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExContextHelp = 0x00000400L;
    private const long WsExAppWindow = 0x00040000L;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    // A message-only parent keeps the cross-process HWND alive while WinForms recreates the
    // Visual Studio pane handle. It is invisible and outside the top-level window z-order.
    private static readonly IntPtr MessageOnlyWindow = new(-3);

    private IntPtr parentWindowHandle;
    private IntPtr childWindowHandle;
    private long originalStyle;
    private long originalExtendedStyle;
    private IntPtr originalParent;
    private bool originalStateCaptured;
    private bool parked;
    private IntPtr parkedParentWindowHandle;

    public WindowsDesignerParentWindowHost(IntPtr parentWindowHandle)
    {
        ValidateParent(parentWindowHandle);
        this.parentWindowHandle = parentWindowHandle;
    }

    public int OwnerProcessId { get; private set; }

    public void Attach(Form form)
        => Attach(form, parentWindowHandle);

    public void Attach(Form form, IntPtr replacementParentWindowHandle)
    {
        ArgumentNullException.ThrowIfNull(form);
        ValidateParent(replacementParentWindowHandle);

        if (!IsWindow(replacementParentWindowHandle))
            throw new InvalidOperationException("The Visual Studio parent HWND is no longer valid.");

        if (GetWindowThreadProcessId(replacementParentWindowHandle, out var ownerProcessId) == 0
            || ownerProcessId == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not identify the process that owns the Visual Studio parent HWND.");
        }

        IPlatformHandle platformHandle = form.PlatformHandle;
        if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException(
                $"Visual Studio hosting requires an HWND, but the active backend reported '{platformHandle.HandleDescriptor ?? "<none>"}'.");
        }
        if (platformHandle.Handle == IntPtr.Zero)
            throw new InvalidOperationException("The Designer window returned a zero HWND.");
        if (childWindowHandle != IntPtr.Zero && childWindowHandle != platformHandle.Handle)
            throw new InvalidOperationException("The Designer platform HWND changed during its hosted lifetime.");

        childWindowHandle = platformHandle.Handle;
        var wasParked = parked;
        var previousParent = GetParent(childWindowHandle);
        var previousStyle = GetWindowLongPtrChecked(childWindowHandle, GwlStyle).ToInt64();
        var previousExtendedStyle = GetWindowLongPtrChecked(childWindowHandle, GwlExStyle).ToInt64();

        if (!originalStateCaptured)
        {
            originalStyle = previousStyle;
            originalExtendedStyle = previousExtendedStyle;
            originalParent = previousParent;
            originalStateCaptured = true;
            DesignerHostDiagnosticLog.Write(
                $"HOST_WINDOW_CREATED Hwnd=0x{childWindowHandle.ToInt64():X} " +
                $"Style=0x{unchecked((ulong)originalStyle):X16} " +
                $"ExStyle=0x{unchecked((ulong)originalExtendedStyle):X16} " +
                $"Parent=0x{originalParent.ToInt64():X} Owner=0x{GetWindow(childWindowHandle, GwOwner).ToInt64():X} " +
                $"Dpi={GetDpiForWindow(childWindowHandle)}");
        }

        var hostedStyle = previousStyle;
        hostedStyle &= ~(
            WsPopup
            | WsCaption
            | WsThickFrame
            | WsSysMenu
            | WsMinimizeBox
            | WsMaximizeBox
            | WsMinimize
            | WsMaximize);
        hostedStyle |= WsChild | WsClipSiblings;

        var hostedExtendedStyle = previousExtendedStyle;
        hostedExtendedStyle &= ~(
            WsExDlgModalFrame
            | WsExTopmost
            | WsExWindowEdge
            | WsExClientEdge
            | WsExContextHelp
            | WsExAppWindow);

        DesignerHostDiagnosticLog.Write(
            $"PARENT_ATTACH_BEGIN Child=0x{childWindowHandle.ToInt64():X} " +
            $"Parent=0x{replacementParentWindowHandle.ToInt64():X} " +
            $"PreviousParent=0x{previousParent.ToInt64():X}");
        DesignerHostDiagnosticLog.Write(
            $"STYLE_BEFORE_ATTACH Style=0x{unchecked((ulong)previousStyle):X16} " +
            $"ExStyle=0x{unchecked((ulong)previousExtendedStyle):X16}");

        var parentChanged = false;
        try
        {
            // SetParent deliberately does not update WS_CHILD/WS_POPUP. Apply both style sets
            // first, then force non-client recalculation after the parent transition.
            if (hostedStyle != previousStyle)
                SetWindowLongPtrChecked(childWindowHandle, GwlStyle, new IntPtr(hostedStyle));
            if (hostedExtendedStyle != previousExtendedStyle)
                SetWindowLongPtrChecked(childWindowHandle, GwlExStyle, new IntPtr(hostedExtendedStyle));
            SetParentChecked(childWindowHandle, replacementParentWindowHandle);
            parentChanged = true;

            parentWindowHandle = replacementParentWindowHandle;
            OwnerProcessId = checked((int)ownerProcessId);
            parked = false;
            ResizeToParent(frameChanged: true);

            var actualStyle = GetWindowLongPtrChecked(childWindowHandle, GwlStyle).ToInt64();
            var actualExtendedStyle = GetWindowLongPtrChecked(childWindowHandle, GwlExStyle).ToInt64();
            DesignerHostDiagnosticLog.Write(
                $"STYLE_AFTER_ATTACH Style=0x{unchecked((ulong)actualStyle):X16} " +
                $"ExStyle=0x{unchecked((ulong)actualExtendedStyle):X16}");
            DesignerHostDiagnosticLog.Write(
                $"ATTACHED Child=0x{childWindowHandle.ToInt64():X} " +
                $"Parent=0x{parentWindowHandle.ToInt64():X} PreviousParent=0x{previousParent.ToInt64():X} " +
                $"StyleBefore=0x{unchecked((ulong)previousStyle):X16} " +
                $"StyleAfter=0x{unchecked((ulong)actualStyle):X16} " +
                $"ExStyleBefore=0x{unchecked((ulong)previousExtendedStyle):X16} " +
                $"ExStyleAfter=0x{unchecked((ulong)actualExtendedStyle):X16} " +
                $"Owner=0x{GetWindow(childWindowHandle, GwOwner).ToInt64():X} " +
                $"ParentDpi={GetDpiForWindow(parentWindowHandle)} ChildDpi={GetDpiForWindow(childWindowHandle)}");
            DesignerHostDiagnosticLog.Write(
                $"PARENT_ATTACH_OK Child=0x{childWindowHandle.ToInt64():X} " +
                $"Parent=0x{parentWindowHandle.ToInt64():X}");

            if (wasParked)
            {
                DesignerHostDiagnosticLog.Write(
                    $"PARENT_RECREATED OldParent=0x{parkedParentWindowHandle.ToInt64():X} " +
                    $"NewParent=0x{parentWindowHandle.ToInt64():X}");
                parkedParentWindowHandle = IntPtr.Zero;
            }
        }
        catch
        {
            // Preserve the original attachment failure while returning to the exact state seen
            // at method entry. This also keeps a parked child parked if a new pane HWND is bad.
            if (parentChanged)
            {
                try
                {
                    SetParentChecked(
                        childWindowHandle,
                        wasParked ? MessageOnlyWindow : previousParent);
                    parked = wasParked;
                }
                catch
                {
                }
            }

            try
            {
                SetWindowLongPtrChecked(childWindowHandle, GwlStyle, new IntPtr(previousStyle));
                SetWindowLongPtrChecked(childWindowHandle, GwlExStyle, new IntPtr(previousExtendedStyle));
            }
            catch
            {
            }

            throw;
        }
    }

    public void Park()
    {
        EnsureChildWindow();

        _ = ShowWindow(childWindowHandle, SwHide);
        parkedParentWindowHandle = parentWindowHandle;
        SetParentChecked(childWindowHandle, MessageOnlyWindow);
        parked = true;
        var actualParent = GetParent(childWindowHandle);
        DesignerHostDiagnosticLog.Write(
            $"PARENT_PARKED Child=0x{childWindowHandle.ToInt64():X} " +
            $"OldParent=0x{parentWindowHandle.ToInt64():X} Parent=HWND_MESSAGE " +
            $"ReportedParent=0x{actualParent.ToInt64():X} " +
            $"EffectiveVisible={IsWindowVisible(childWindowHandle)}");
    }

    public void ResizeToParent()
        => ResizeToParent(frameChanged: false);

    public void SetVisible(bool visible)
    {
        EnsureChildWindow();

        var currentParent = GetParent(childWindowHandle);
        if (parked)
        {
            DesignerHostDiagnosticLog.Write(
                $"VISIBILITY_CHANGED Requested={visible} Effective=false Parent=HWND_MESSAGE");
            return;
        }

        _ = ShowWindow(childWindowHandle, visible ? SwShowNoActivate : SwHide);
        DesignerHostDiagnosticLog.Write(
            $"VISIBILITY_CHANGED Requested={visible} Effective={IsWindowVisible(childWindowHandle)} " +
            $"Parent=0x{currentParent.ToInt64():X}");
    }

    public void RequestFocus()
    {
        EnsureChildWindow();

        SetLastError(0);
        var previousFocus = SetFocus(childWindowHandle);
        var error = Marshal.GetLastWin32Error();
        if (previousFocus == IntPtr.Zero && error != 0)
            throw new Win32Exception(error, "Windows could not focus the hosted Designer window.");

        DesignerHostDiagnosticLog.Write(
            $"FOCUS_REQUEST Child=0x{childWindowHandle.ToInt64():X} " +
            $"Previous=0x{previousFocus.ToInt64():X} Current=0x{GetFocus().ToInt64():X}");
    }

    private void ResizeToParent(bool frameChanged)
    {
        EnsureChildWindow();

        var currentParent = GetParent(childWindowHandle);
        if (parked)
        {
            DesignerHostDiagnosticLog.Write("BOUNDS_UPDATE_SKIPPED Parent=HWND_MESSAGE");
            return;
        }
        if (!IsWindow(parentWindowHandle))
            throw new InvalidOperationException("The Visual Studio parent HWND is no longer valid.");
        if (!GetClientRect(parentWindowHandle, out var bounds))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the Visual Studio pane bounds.");

        var width = Math.Max(1, bounds.Right - bounds.Left);
        var height = Math.Max(1, bounds.Bottom - bounds.Top);
        var flags = SwpNoZOrder | SwpNoActivate;
        if (frameChanged)
            flags |= SwpFrameChanged;

        if (!SetWindowPos(childWindowHandle, IntPtr.Zero, 0, 0, width, height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not size the embedded Designer window.");

        DesignerHostDiagnosticLog.Write(
            $"BOUNDS_UPDATED X=0 Y=0 Width={width} Height={height} " +
            $"ParentDpi={GetDpiForWindow(parentWindowHandle)} ChildDpi={GetDpiForWindow(childWindowHandle)}");
    }

    private void EnsureChildWindow()
    {
        if (childWindowHandle == IntPtr.Zero || !IsWindow(childWindowHandle))
            throw new InvalidOperationException("The Designer child HWND is no longer valid.");
    }

    private static void ValidateParent(IntPtr parent)
    {
        if (parent == IntPtr.Zero)
            throw new ArgumentException("The Visual Studio parent HWND cannot be zero.", nameof(parent));
    }

    private static IntPtr GetWindowLongPtrChecked(IntPtr hWnd, int nIndex)
    {
        SetLastError(0);
        var value = IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
        var error = Marshal.GetLastWin32Error();
        if (value == IntPtr.Zero && error != 0)
            throw new Win32Exception(error, "Windows could not read the Designer window style.");

        return value;
    }

    private static void SetWindowLongPtrChecked(IntPtr hWnd, int nIndex, IntPtr value)
    {
        SetLastError(0);
        var previous = IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, value)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, value.ToInt32()));
        var error = Marshal.GetLastWin32Error();
        if (previous == IntPtr.Zero && error != 0)
            throw new Win32Exception(error, "Windows could not configure the Designer child-window style.");
    }

    private static void SetParentChecked(IntPtr child, IntPtr parent)
    {
        SetLastError(0);
        var previousParent = SetParent(child, parent);
        var error = Marshal.GetLastWin32Error();
        if (previousParent == IntPtr.Zero && error != 0)
            throw new Win32Exception(error, "Windows could not attach the Designer to its Visual Studio pane.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint dwErrCode);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
