using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

internal sealed class WindowsNativeWindowOperations : IVisualStudioNativeWindowOperations
{
    private const int GwlStyle = -16;
    private const int SwShowNoActivate = 4;

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

    private IntPtr attachedHandle;
    private IntPtr originalParent;
    private long originalStyle;
    private bool styleChanged;
    private bool parentChanged;

    public void Attach(IntPtr childHandle, IntPtr parentHandle)
    {
        if (attachedHandle != IntPtr.Zero)
            Detach(attachedHandle);

        originalStyle = GetWindowLongPtr(childHandle, GwlStyle).ToInt64();
        originalParent = GetParent(childHandle);

        try
        {
            var hostedStyle = originalStyle;
            hostedStyle &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
            hostedStyle |= WsChild | WsVisible;

            SetWindowLongPtrChecked(childHandle, GwlStyle, new IntPtr(hostedStyle));
            styleChanged = true;
            SetParentChecked(childHandle, parentHandle);
            parentChanged = true;
            ShowWindow(childHandle, SwShowNoActivate);
            SetWindowPositionChecked(
                childHandle,
                1,
                1,
                SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
            attachedHandle = childHandle;
        }
        catch
        {
            RestoreOriginalWindowState(childHandle);
            throw;
        }
    }

    public void Resize(IntPtr childHandle, int width, int height)
        => SetWindowPositionChecked(
            childHandle,
            Math.Max(1, width),
            Math.Max(1, height),
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);

    public void Focus(IntPtr childHandle)
    {
        SetLastError(0);
        _ = SetFocus(childHandle);
        var error = Marshal.GetLastWin32Error();
        if (error != 0)
            throw new Win32Exception(error, "Windows could not focus the hosted Designer window.");
    }

    public void Detach(IntPtr childHandle)
    {
        RestoreOriginalWindowState(childHandle);
        attachedHandle = IntPtr.Zero;
    }

    private void RestoreOriginalWindowState(IntPtr childHandle)
    {
        Exception? failure = null;

        if (parentChanged)
        {
            try
            {
                SetParentChecked(childHandle, originalParent);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                parentChanged = false;
            }
        }

        if (styleChanged)
        {
            try
            {
                SetWindowLongPtrChecked(childHandle, GwlStyle, new IntPtr(originalStyle));
                SetWindowPositionChecked(
                    childHandle,
                    1,
                    1,
                    SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
            finally
            {
                styleChanged = false;
            }
        }

        if (failure is not null)
            throw failure;
    }

    private static void SetWindowPositionChecked(IntPtr handle, int width, int height, uint flags)
    {
        if (!SetWindowPos(handle, IntPtr.Zero, 0, 0, width, height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not resize the hosted Designer window.");
    }

    private static void SetParentChecked(IntPtr childHandle, IntPtr parentHandle)
    {
        SetLastError(0);
        _ = SetParent(childHandle, parentHandle);
        var error = Marshal.GetLastWin32Error();
        if (error != 0)
            throw new Win32Exception(error, "Windows could not parent the Designer window into Visual Studio.");
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
            throw new Win32Exception(error, "Windows could not update the hosted Designer window style.");
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
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
