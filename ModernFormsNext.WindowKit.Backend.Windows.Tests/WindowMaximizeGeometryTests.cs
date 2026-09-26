using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Controls;
using Xunit;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

/// <summary>Checks the native maximize contract without moving the desktop pointer or changing display settings.</summary>
[Collection(WindowsUiCollection.Name)]
public sealed class WindowMaximizeGeometryTests
{
    /// <summary>Physical rectangles cover fractional DPI, negative origins and taskbars on all four edges.</summary>
    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    [InlineData(175)]
    [InlineData(200)]
    [InlineData(225)]
    public void MaximizedClientExcludesOnlyFrameOutsideWorkArea(int percent)
    {
        int width = 1920 * percent / 100, height = 1080 * percent / 100;
        int frame = 8 * percent / 100, taskbar = 48 * percent / 100;
        foreach (var (x, y) in new[] { (0, 0), (-width, -height), (1920, -240) })
        {
            var monitor = new RECT { left = x, top = y, right = x + width, bottom = y + height };
            foreach (int edge in Enumerable.Range(0, 5))
            {
                var work = monitor;
                if (edge == 1) work.left += taskbar;
                if (edge == 2) work.top += taskbar;
                if (edge == 3) work.right -= taskbar;
                if (edge == 4) work.bottom -= taskbar;
                var outer = new RECT {
                    left = work.left - frame, top = work.top - frame,
                    right = work.right + frame, bottom = work.bottom + frame
                };
                Assert.Equal(work, WindowImpl.GetMaximizedClientRect(outer, work));
                Assert.Equal(work, WindowImpl.GetMaximizedClientRect(work, work));

                // A maximum-size constraint can make the native rectangle smaller than
                // the monitor. Client calculation must not enlarge it to fill the screen.
                var constrained = new RECT {
                    left = work.left + 20, top = work.top + 20,
                    right = work.left + 400, bottom = work.top + 300
                };
                Assert.Equal(constrained, WindowImpl.GetMaximizedClientRect(constrained, work));
                outer.right = constrained.right;
                outer.bottom = constrained.bottom;
                constrained.left = work.left;
                constrained.top = work.top;
                Assert.Equal(constrained, WindowImpl.GetMaximizedClientRect(outer, work));
            }
        }
    }

    /// <summary>Transient disjoint rectangles never produce an inverted native client area.</summary>
    [Fact]
    public void OffscreenRectangleWithoutWorkAreaIntersectionIsPreserved()
    {
        var outer = new RECT { left = -32000, top = -32000, right = -31900, bottom = -31900 };
        Assert.Equal(outer, WindowImpl.GetMaximizedClientRect(outer, new RECT { right = 1920, bottom = 1080 }));
    }

    /// <summary>Both entry points must use the same work area and preserve the normal placement on every attached monitor.</summary>
    [Fact]
    public void ManagedAndNativeMaximizeHaveIdenticalClientAndWindowBounds()
    {
        using var form = new Form { ClientSize = new System.Drawing.Size(600, 360) };
        form.Show();
        try
        {
            nint hwnd = form.PlatformHandle.Handle;
            foreach (var monitor in GetMonitors())
            {
                Assert.True(SetWindowPos(hwnd, 0, monitor.rcWork.left + 40, monitor.rcWork.top + 40, 600, 360,
                    SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
                Assert.True(GetWindowRect(hwnd, out var normal));
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    // FormTitleBar assigns this property. The native system command bypasses
                    // that setter, as does Aero Snap; real drag acceptance is recorded separately.
                    form.WindowState = FormWindowState.Maximized;
                    AssertCustomMaximized(hwnd, monitor.rcWork);
                    Assert.True(GetWindowRect(hwnd, out var managedMaximized));
                    form.WindowState = FormWindowState.Normal;
                    AssertWindowRect(hwnd, normal);
                    NativeSendMessage(hwnd, (uint)WindowsMessage.WM_SYSCOMMAND, 0xF030, 0);
                    Assert.Equal(FormWindowState.Maximized, form.WindowState);
                    AssertCustomMaximized(hwnd, monitor.rcWork);
                    AssertWindowRect(hwnd, managedMaximized);
                    NativeSendMessage(hwnd, (uint)WindowsMessage.WM_SYSCOMMAND, 0xF120, 0);
                    Assert.Equal(FormWindowState.Normal, form.WindowState);
                    AssertWindowRect(hwnd, normal);
                }
            }
        }
        finally { form.Close(); }
    }

    /// <summary>Native decoration and resize styles retain Windows' outer bounds for both maximize paths.</summary>
    [Theory]
    [InlineData(SystemDecorations.None, true)]
    [InlineData(SystemDecorations.None, false)]
    [InlineData(SystemDecorations.BorderOnly, true)]
    [InlineData(SystemDecorations.BorderOnly, false)]
    [InlineData(SystemDecorations.Full, true)]
    [InlineData(SystemDecorations.Full, false)]
    public void DecorationAndResizeStylesPreserveNativeMaximizeAndRestore(SystemDecorations decorations, bool resizable)
    {
        using var window = new WindowImpl();
        window.SetSystemDecorations(decorations);
        window.CanResize(resizable);
        window.Show(false, false);
        nint hwnd = window.Handle.Handle;
        Assert.True(GetWindowRect(hwnd, out var normal));
        var monitor = MONITORINFO.Create();
        Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR.MONITOR_DEFAULTTONEAREST), ref monitor));
        window.WindowState = WindowState.Maximized;
        Assert.True(GetWindowRect(hwnd, out var managed));
        if (decorations != SystemDecorations.Full) AssertCustomMaximized(hwnd, monitor.rcWork);
        else
        {
            var origin = new POINT();
            Assert.True(ClientToScreen(hwnd, ref origin));
            Assert.True(GetClientRect(hwnd, out var client));
            Assert.True(origin.X >= monitor.rcWork.left && origin.Y > monitor.rcWork.top);
            Assert.True(origin.X + client.Width <= monitor.rcWork.right && origin.Y + client.Height <= monitor.rcWork.bottom);
        }
        window.WindowState = WindowState.Normal;
        AssertWindowRect(hwnd, normal);
        // ShowWindow can maximize even a nonresizable window whose SC_MAXIMIZE menu item is disabled.
        ShowWindow(hwnd, ShowWindowCommand.Maximize);
        AssertWindowRect(hwnd, managed);
        window.WindowState = WindowState.Normal;
        AssertWindowRect(hwnd, normal);
    }

    /// <summary>Fullscreen uses the entire monitor and normal move/resize is not clamped.</summary>
    [Fact]
    public void FullscreenAndNormalWindowsKeepTheirOwnGeometry()
    {
        using var window = new WindowImpl();
        window.SetSystemDecorations(SystemDecorations.None);
        window.Show(false, false);
        nint hwnd = window.Handle.Handle;
        foreach (var monitor in GetMonitors())
        {
            Assert.True(SetWindowPos(hwnd, 0, monitor.rcWork.left + 40, monitor.rcWork.top + 40, 600, 360,
                SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
            Assert.True(GetWindowRect(hwnd, out var normal));
            window.WindowState = WindowState.FullScreen;
            AssertWindowRect(hwnd, monitor.rcMonitor);
            AssertCustomMaximized(hwnd, monitor.rcMonitor);
            window.WindowState = WindowState.Normal;
            AssertWindowRect(hwnd, normal);
            AssertCustomMaximized(hwnd, normal);
        }
    }

    /// <summary>Injected DPI messages must not scale physical maximized work-area coordinates again.</summary>
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(168)]
    [InlineData(192)]
    public void MaximizedDpiChangeUsesSuggestedPhysicalRectangle(int dpi)
    {
        using var form = new Form { ClientSize = new System.Drawing.Size(600, 360) };
        form.Show();
        try
        {
            nint hwnd = form.PlatformHandle.Handle;
            form.WindowState = FormWindowState.Maximized;
            Assert.True(GetWindowRect(hwnd, out var outer));
            var monitor = MONITORINFO.Create();
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR.MONITOR_DEFAULTTONEAREST), ref monitor));
            nint memory = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
            try
            {
                Marshal.StructureToPtr(outer, memory, false);
                NativeSendMessage(hwnd, (uint)WindowsMessage.WM_DPICHANGED, (dpi << 16) | dpi, memory);
            }
            finally { Marshal.FreeHGlobal(memory); }
            Assert.Equal(dpi / 96d, form.Scaling);
            AssertWindowRect(hwnd, outer);
            AssertCustomMaximized(hwnd, monitor.rcWork);
        }
        finally { form.Close(); }
    }

    /// <summary>Both NCCALCSIZE layouts update only their first RECT, preserving adjacent native data.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NcCalcSizePreservesPayloadAfterProposedRectangle(bool fullParameters)
    {
        using var form = new Form { ClientSize = new System.Drawing.Size(600, 360) };
        form.Show();
        try
        {
            nint hwnd = form.PlatformHandle.Handle;
            form.WindowState = FormWindowState.Maximized;
            Assert.True(GetWindowRect(hwnd, out var outer));
            var monitor = MONITORINFO.Create();
            Assert.True(GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR.MONITOR_DEFAULTTONEAREST), ref monitor));
            byte[] sentinel = Enumerable.Repeat((byte)0x5A, 80).ToArray();
            nint memory = Marshal.AllocHGlobal(sentinel.Length);
            try
            {
                Marshal.Copy(sentinel, 0, memory, sentinel.Length);
                Marshal.StructureToPtr(outer, memory, false);
                NativeSendMessage(hwnd, (uint)WindowsMessage.WM_NCCALCSIZE, fullParameters ? 1 : 0, memory);
                Assert.Equal(monitor.rcWork, Marshal.PtrToStructure<RECT>(memory));
                var remaining = new byte[sentinel.Length - Marshal.SizeOf<RECT>()];
                Marshal.Copy(memory + Marshal.SizeOf<RECT>(), remaining, 0, remaining.Length);
                Assert.All(remaining, value => Assert.Equal((byte)0x5A, value));
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        finally { form.Close(); }
    }

    /// <summary>Maximizing must not demote a topmost HWND as the previous post-maximize SetWindowPos did.</summary>
    [Fact]
    public void MaximizingPreservesTopmostStyle()
    {
        using var window = new WindowImpl();
        window.SetSystemDecorations(SystemDecorations.None);
        window.Show(false, false);
        window.SetTopmost(true);
        window.WindowState = WindowState.Maximized;
        Assert.True((GetWindowLong(window.Handle.Handle, (int)WindowLongParam.GWL_EXSTYLE) & (uint)WindowStyles.WS_EX_TOPMOST) != 0);
    }

    private static void AssertCustomMaximized(nint hwnd, RECT expected)
    {
        Assert.True(GetClientRect(hwnd, out var client));
        var origin = new POINT();
        Assert.True(ClientToScreen(hwnd, ref origin));
        Assert.Equal((expected.left, expected.top, expected.Width, expected.Height),
            (origin.X, origin.Y, client.Width, client.Height));
    }

    private static void AssertWindowRect(nint hwnd, RECT expected)
    {
        Assert.True(GetWindowRect(hwnd, out var actual));
        Assert.Equal((expected.left, expected.top, expected.right, expected.bottom),
            (actual.left, actual.top, actual.right, actual.bottom));
    }

    private static List<MONITORINFO> GetMonitors()
    {
        var monitors = new List<MONITORINFO>();
        Assert.True(NativeEnumDisplayMonitors(0, 0, (nint handle, nint dc, ref RECT rect, nint data) => {
            var info = MONITORINFO.Create();
            if (GetMonitorInfo(handle, ref info)) monitors.Add(info);
            return true;
        }, 0));
        Assert.NotEmpty(monitors);
        return monitors;
    }

    private delegate bool MonitorCallback(nint monitor, nint dc, ref RECT rect, nint data);
    [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors")]
    private static extern bool NativeEnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint NativeSendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
