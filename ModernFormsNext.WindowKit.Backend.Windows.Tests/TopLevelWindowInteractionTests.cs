using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

[Collection(WindowsUiCollection.Name)]
public sealed class TopLevelWindowInteractionTests
{
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;

    [Fact]
    public void NormalFormCreatesAResizableTopLevelWindow()
    {
        using var form = new Form
        {
            ClientSize = new System.Drawing.Size(800, 600)
        };

        form.Show();
        try
        {
            var hwnd = form.PlatformHandle.Handle;
            var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();

            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.Equal(IntPtr.Zero, GetParent(hwnd));
            Assert.Equal(0, style & WsChild);
            Assert.NotEqual(0, style & WsThickFrame);
            Assert.NotEqual(0, style & WsMinimizeBox);
            Assert.NotEqual(0, style & WsMaximizeBox);
        }
        finally
        {
            form.Close();
        }
    }

    [Fact]
    public void SystemWindowDragPreparationReleasesBackendOwnedMouseCapture()
    {
        using var window = new WindowImpl();
        var hwnd = window.Handle.Handle;

        window.TakeNativeMouseCapture();
        Assert.Equal(hwnd, GetCapture());

        window.PrepareForSystemWindowDrag();

        Assert.Equal(IntPtr.Zero, GetCapture());
    }

    [Fact]
    public void CancelModeRetiresCapturedPressBeforeLateRelease()
    {
        using var form = new Form { UseSystemDecorations = true, ClientSize = new(300, 200) };
        var button = form.Controls.Add(new Button { Dock = DockStyle.Fill });
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        form.Show();
        try
        {
            nint hwnd = form.PlatformHandle.Handle;
            nint point = (64 << 16) | 64;
            // Deliver window messages directly. This exercises the HWND backend without
            // moving the hardware cursor or injecting system-wide mouse events.
            SendMessage(hwnd, (uint)WindowsMessage.WM_LBUTTONDOWN, 1, point);
            Assert.Equal(hwnd, GetCapture());
            Assert.True(button.Capture);
            SendMessage(hwnd, (uint)WindowsMessage.WM_CANCELMODE, 0, 0);
            Assert.Equal(nint.Zero, GetCapture());
            Assert.False(button.Capture);

            SendMessage(hwnd, (uint)WindowsMessage.WM_LBUTTONUP, 0, point);
            Assert.Equal(0, clicks);
            SendMessage(hwnd, (uint)WindowsMessage.WM_LBUTTONDOWN, 1, point);
            SendMessage(hwnd, (uint)WindowsMessage.WM_LBUTTONUP, 0, point);
            Assert.Equal(1, clicks);
            Assert.Equal(nint.Zero, GetCapture());
        }
        finally { form.Close(); }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetCapture();

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int index)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, index)
            : new IntPtr(GetWindowLong32(hWnd, index));
}
