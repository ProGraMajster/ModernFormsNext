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
/// </remarks>
internal sealed class WindowsDesignerParentWindowHost
{
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private readonly IntPtr parentWindowHandle;
    private bool attached;

    public WindowsDesignerParentWindowHost(IntPtr parentWindowHandle)
    {
        if (parentWindowHandle == IntPtr.Zero)
            throw new ArgumentException("The Visual Studio parent HWND cannot be zero.", nameof(parentWindowHandle));

        this.parentWindowHandle = parentWindowHandle;
    }

    public int OwnerProcessId { get; private set; }

    public void Attach(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (attached)
            return;
        if (!IsWindow(parentWindowHandle))
            throw new InvalidOperationException("The Visual Studio parent HWND is no longer valid.");

        if (GetWindowThreadProcessId(parentWindowHandle, out var ownerProcessId) == 0
            || ownerProcessId == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not identify the process that owns the Visual Studio parent HWND.");
        }

        OwnerProcessId = checked((int)ownerProcessId);

        IPlatformHandle platformHandle = form.PlatformHandle;
        if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException(
                $"Visual Studio hosting requires an HWND, but the active backend reported '{platformHandle.HandleDescriptor ?? "<none>"}'.");
        }
        if (platformHandle.Handle == IntPtr.Zero)
            throw new InvalidOperationException("The Designer window returned a zero HWND.");

        var childHandle = platformHandle.Handle;
        var originalStyle = GetWindowLongPtr(childHandle, GwlStyle).ToInt64();
        var originalParent = GetParent(childHandle);
        var styleChanged = false;
        var parentChanged = false;

        try
        {
            var style = originalStyle;
            style &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
            style |= WsChild | WsVisible;
            SetWindowLongPtrChecked(childHandle, GwlStyle, new IntPtr(style));
            styleChanged = true;

            SetLastError(0);
            _ = SetParent(childHandle, parentWindowHandle);
            var parentError = Marshal.GetLastWin32Error();
            if (parentError != 0)
                throw new Win32Exception(parentError, "Windows could not attach the Designer to its Visual Studio pane.");
            parentChanged = true;

            if (!GetClientRect(parentWindowHandle, out var bounds))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the Visual Studio pane bounds.");

            if (!SetWindowPos(
                    childHandle,
                    IntPtr.Zero,
                    0,
                    0,
                    Math.Max(1, bounds.Right - bounds.Left),
                    Math.Max(1, bounds.Bottom - bounds.Top),
                    SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not size the embedded Designer window.");
            }

            attached = true;
        }
        catch
        {
            // Preserve the original failure while restoring every mutation that completed. The
            // process may otherwise survive as an invisible child of a pane that rejected it.
            if (parentChanged)
            {
                SetLastError(0);
                _ = SetParent(childHandle, originalParent);
            }

            if (styleChanged)
            {
                try
                {
                    SetWindowLongPtrChecked(childHandle, GwlStyle, new IntPtr(originalStyle));
                }
                catch
                {
                    // The initiating failure is more actionable than a secondary rollback error.
                }
            }

            throw;
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

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
