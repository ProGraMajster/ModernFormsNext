using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Windows;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

internal static class NativeViewScenario
{
    private static int assertions;
    internal static int Run()
    {
        try
        {
            using var main = new Form();
            Exception? failure = null;
            main.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try { Check(); } catch (Exception error) { failure = error; }
                finally { main.Close(); }
            });
            Application.Run(main);
            if (failure is not null) throw failure;
            Console.WriteLine($"NATIVE_VIEW:PASS:{assertions}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Check()
    {
        foreach (bool systemChrome in new[] { false, true })
        {
            using var form = new Form { UseSystemDecorations = systemChrome, ClientSize = new(500, 350) };
            using var second = new Form { UseSystemDecorations = systemChrome };
            var panel = form.Controls.Add(new Panel { Bounds = new(10, 10, 230, 140), AutoScroll = true });
            var editor = form.Controls.Add(new TextBox { Bounds = new(260, 10, 150, 35), TabIndex = 0 });
            var next = form.Controls.Add(new TextBox { Bounds = new(260, 60, 150, 35), TabIndex = 3 });
            var firstFactory = new Factory();
            var secondFactory = new Factory();
            var a = panel.Controls.Add(new NativeViewHost { Bounds = new(20, 20, 120, 50), TabIndex = 1, PeerFactory = firstFactory });
            var b = panel.Controls.Add(new NativeViewHost { Bounds = new(50, 40, 120, 50), TabIndex = 2, PeerFactory = secondFactory });
            panel.Controls.Add(new Control { Bounds = new(400, 400, 5, 5) });
            form.Show(); second.Show();
            form.Activate();
            var site = firstFactory.Sites.Last();
            var peer = firstFactory.Peers.Last();
            Require(IsWindow(peer) && IsWindow(site.ParentWindow), "real peers and containers");
            Require(GetParent(peer) == site.ParentWindow && GetParent(site.ParentWindow) == form.PlatformHandle.Handle, "HWND parenting");
            Require(a.HostingDiagnostics.State == NativeViewHostState.Attached, "attached diagnostics");
            Geometry(a, site);
            a.Width += 20; a.Left += 5; Geometry(a, site);
            panel.VerticalScrollProperties.Value = 35; Geometry(a, site);
            Require(a.HostingDiagnostics.Placement.PixelClip.Height < a.HostingDiagnostics.Placement.PixelBounds.Height, "partial scrolling clip");
            panel.Visible = false; Require(!IsWindowVisible(site.ParentWindow), "ancestor hide");
            panel.Visible = true; Require(IsWindowVisible(site.ParentWindow), "ancestor show");
            panel.Enabled = false; Require(!IsWindowEnabled(site.ParentWindow), "effective enabled");
            panel.Enabled = true;
            panel.Opacity = .5f; Require(!IsWindowVisible(site.ParentWindow), "unsupported opacity hidden");
            panel.Opacity = 1; Require(IsWindowVisible(site.ParentWindow), "supported composition restored");
            Require(firstFactory.Created == 1, "scroll and visibility retain peer");
            editor.Select();
            bool cancel = true;
            editor.Validating += (_, e) => e.Cancel = cancel;
            SetFocus(peer);
            Require(editor.Focused && !a.Focused && GetFocus() != peer, "native validation veto before focus");
            cancel = false;
            SetFocus(peer);
            Require(a.Focused && GetFocus() == peer, "native focus commits canonical host");
            next.Select();
            Require(next.Focused && GetFocus() == form.PlatformHandle.Handle, "native to shared focus return");
            a.Select();
            Require(a.Focused && GetFocus() == peer, "shared to native focus");
            int duplicateClicks = 0;
            a.Click += (_, _) => duplicateClicks++;
            SendMessage(peer, 0x201, new IntPtr(1), new IntPtr(10 | (10 << 16)));
            SendMessage(peer, 0x202, IntPtr.Zero, new IntPtr(10 | (10 << 16)));
            SendMessage(peer, 0x102, new IntPtr('x'), IntPtr.Zero);
            Require(duplicateClicks == 0 && GetWindowTextLength(peer) > 0 && next.Text.Length == 0, "native pointer and text stay outside Skia input");
            next.Select();
            // A genuine native message exercises the session's queue hook and existing Tab order.
            a.Select();
            PostMessage(peer, 0x100, new IntPtr(9), IntPtr.Zero);
            Pump();
            Require(b.Focused, "native Tab leaves through existing order");
            Require(GetWindow(firstFactory.Sites.Last().ParentWindow, 3) == secondFactory.Sites.Last().ParentWindow, "native host order");
            a.BringToFront();
            Require(GetWindow(secondFactory.Sites.Last().ParentWindow, 3) == firstFactory.Sites.Last().ParentWindow, "BringToFront native order");
            var otherPanel = form.Controls.Add(new Panel { Bounds = new(20, 190, 250, 120) });
            otherPanel.Controls.Add(a);
            Require(firstFactory.Created == 1 && IsWindow(peer), "same window reparent");
            second.Controls.Add(a);
            Require(!IsWindow(peer) && !IsWindow(site.ParentWindow) && firstFactory.Created == 2, "cross window retirement");
            Require(!site.IsCurrent && !site.TryFocus(), "stale site rejected");
            a.Dispose();
            Require(firstFactory.Peers.All(h => !IsWindow(h)), "control disposal destroys peers");
            var deferredFactory = new Factory(acceptsFocus: false);
            var deferredHost = form.Controls.Add(new NativeViewHost { Bounds = new(260, 220, 100, 35), PeerFactory = deferredFactory });
            deferredHost.Select();
            Require(deferredHost.Focused && GetFocus() == deferredFactory.Sites.Single().ParentWindow,
                "non-focusable peer retains native container keyboard boundary");
            deferredHost.Dispose();
            Require(deferredFactory.Peers.All(h => !IsWindow(h)), "deferred peer lease cleanup");
            var reentrantFactory = new Factory(acceptsFocus: false, focusAction: b.Select);
            var reentrantHost = form.Controls.Add(new NativeViewHost { Bounds = new(260, 220, 100, 35), PeerFactory = reentrantFactory });
            reentrantHost.Select();
            Require(b.Focused && GetFocus() == secondFactory.Peers.Last(),
                "reentrant native focus callback preserves newer canonical native owner");
            reentrantHost.Dispose();
            Require(reentrantFactory.Peers.All(h => !IsWindow(h)), "reentrant focus lease cleanup");
            var borrowed = CreateWindowEx(0, "EDIT", "", 0x50000000, 0, 0, 10, 10,
                form.PlatformHandle.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            try
            {
                var borrowedHost = form.Controls.Add(new NativeViewHost { Bounds = new(260, 180, 100, 35), PeerFactory = new BorrowFactory(borrowed) });
                borrowedHost.Dispose();
                Require(IsWindow(borrowed) && GetParent(borrowed) == form.PlatformHandle.Handle, "borrowed HWND detached without destruction");
            }
            finally { DestroyWindow(borrowed); }
            // Existing popup and modal top-levels remain outside the owner's child airspace.
            var combo = form.Controls.Add(new ComboBox { Bounds = new(260, 120, 180, 35) });
            combo.Items.Add("one"); combo.Items.Add("two"); combo.DroppedDown = true;
            Require(combo.DroppedDown && IsWindowVisible(secondFactory.Sites.Last().ParentWindow), "popup above native owner content");
            combo.DroppedDown = false;
            using var modal = new Form { ClientSize = new(200, 120) };
            var completion = modal.ShowDialog(form);
            Require(modal.Visible && !IsWindowEnabled(form.PlatformHandle.Handle), "modal disables native owner");
            modal.DialogResult = DialogResult.OK;
            Require(completion.IsCompleted && IsWindowEnabled(form.PlatformHandle.Handle), "modal close restores owner");
            form.Close(); second.Close();
            Require(firstFactory.Peers.Concat(secondFactory.Peers).All(h => !IsWindow(h)), "zero retained owned peer HWNDs");
            Require(firstFactory.Sites.Concat(secondFactory.Sites).All(s => !IsWindow(s.ParentWindow)), "zero retained container HWNDs");
        }
    }
    private static void Geometry(NativeViewHost host, WindowsNativeViewSite site)
    {
        GetWindowRect(site.ParentWindow, out var actual);
        var expected = host.HostingDiagnostics.Placement.PixelBounds;
        Require(actual.Right - actual.Left == expected.Width && actual.Bottom - actual.Top == expected.Height, "native bounds size");
        var origin = new NativePoint { X = actual.Left, Y = actual.Top };
        ScreenToClient(GetParent(site.ParentWindow), ref origin);
        Require(origin.X == expected.X && origin.Y == expected.Y, "client relative origin");
        var region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            Require(GetWindowRgn(site.ParentWindow, region) != 0, "native rectangular region");
            GetRgnBox(region, out var clip);
            var wanted = host.HostingDiagnostics.Placement.PixelClip;
            Require(clip.Left == wanted.X && clip.Top == wanted.Y && clip.Right == wanted.Right && clip.Bottom == wanted.Bottom, "native region extent");
        }
        finally { DeleteObject(region); }
    }
    private static void Pump()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
        { TranslateMessage(ref message); DispatchMessage(ref message); }
    }
    private static void Require(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); assertions++; }
    private sealed class Factory(bool acceptsFocus = true, Action? focusAction = null) : INativeViewFactory
    {
        internal int Created;
        internal readonly List<IntPtr> Peers = [];
        internal readonly List<WindowsNativeViewSite> Sites = [];
        public INativeViewPeer CreatePeer(INativeViewSite site)
        {
            var windows = (WindowsNativeViewSite)site;
            var handle = CreateWindowEx(0, "EDIT", "", 0x50010000, 0, 0, 1, 1, windows.ParentWindow, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            Created++; Peers.Add(handle); Sites.Add(windows);
            var peer = new WindowsHwndViewPeer(windows, handle, ownsWindow: true);
            return acceptsFocus ? peer : new DeferredFocusPeer(peer, focusAction);
        }
    }
    private sealed class DeferredFocusPeer(INativeViewPeer peer, Action? focusAction) : INativeViewPeer
    {
        public void Resize(ModernFormsNext.WindowKit.PixelSize size) => peer.Resize(size);
        public void RequestFocus() => focusAction?.Invoke();
        public bool TryMoveFocus(bool forward) => false;
        public void Dispose() => peer.Dispose();
    }
    private sealed class BorrowFactory(IntPtr window) : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site) => new WindowsHwndViewPeer((WindowsNativeViewSite)site, window, ownsWindow: false);
    }
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { internal IntPtr Window; internal uint Id; internal UIntPtr WParam; internal IntPtr LParam; internal uint Time; internal NativePoint Point; internal uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(int exStyle, string type, string text, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern int GetRgnBox(IntPtr region, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
}
