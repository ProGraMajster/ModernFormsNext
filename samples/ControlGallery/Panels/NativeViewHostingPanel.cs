using System;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Backend.Windows;
using ModernFormsNext.WindowKit.Platform;

namespace ControlGallery.Panels;

/// <summary>Demonstrates the native airspace baseline using two small Windows EDIT peers.</summary>
public sealed class NativeViewHostingPanel : BasePanel
{
    /// <summary>Creates a bounded hosting, scrolling and composition demonstration.</summary>
    public NativeViewHostingPanel()
    {
        var actions = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 140 });
        actions.Controls.Add(new Label { Bounds = new(15, 10, 660, 50), Multiline = true,
            Text = "Native HWND peers live above same-window Skia content. Scroll, resize and Tab between editors. Opacity suspends the native band." });
        var viewport = Controls.Add(new Panel { Dock = DockStyle.Fill, AutoScroll = true });
        var first = viewport.Controls.Add(new NativeViewHost { Bounds = new(30, 30, 300, 70), TabIndex = 0,
            Text = "First native peer", PeerFactory = new EditFactory("Native editor A") });
        viewport.Controls.Add(new NativeViewHost { Bounds = new(90, 75, 300, 70), TabIndex = 1,
            Text = "Second native peer", PeerFactory = new EditFactory("Native editor B") });
        viewport.Controls.Add(new TextBox { Bounds = new(30, 170, 300, 40), TabIndex = 2, Text = "Framework editor" });
        viewport.Controls.Add(new Label { Bounds = new(30, 600, 500, 50), Text = "End of scrolling content" });
        var opacity = actions.Controls.Add(new Button { Bounds = new(15, 70, 190, 40), Text = "Toggle opacity" });
        opacity.Click += (_, _) => viewport.Opacity = viewport.Opacity < 1 ? 1 : .5f;
        var visible = actions.Controls.Add(new Button { Bounds = new(215, 70, 190, 40), Text = "Hide/show peer A" });
        visible.Click += (_, _) => first.Visible = !first.Visible;
        var order = actions.Controls.Add(new Button { Bounds = new(415, 70, 190, 40), Text = "Bring A to front" });
        order.Click += (_, _) => first.BringToFront();
    }

    // Validation/demo peer only. A feature adapter uses the same typed site and owned lease,
    // without handlers for bounds, scroll, DPI, visibility, focus or Form discovery.
    private sealed class EditFactory(string text) : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site)
        {
            var windows = (WindowsNativeViewSite)site;
            var handle = CreateWindowEx(0, "EDIT", text, 0x50010000, 0, 0, 1, 1,
                windows.ParentWindow, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            return new WindowsHwndViewPeer(windows, handle, ownsWindow: true);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int exStyle, string type, string text, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr data);
    }
}
