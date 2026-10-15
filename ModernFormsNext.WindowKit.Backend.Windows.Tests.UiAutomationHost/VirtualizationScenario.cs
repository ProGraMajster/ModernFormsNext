using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Threading;

// This fixture uses the actual Windows Form/HWND and native key/wheel messages. Pure geometry
// and windowless tests cannot qualify adapter focus, scrollbar displacement or visibility sequencing.
internal static class VirtualizationScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Shared virtualization native validation", ClientSize = new Size(720, 460) };
            var view = form.Controls.Add(new ListView { Dock = DockStyle.Fill, ItemSize = new Size(160, 28), ItemsLayout = ItemsLayout.Stack });
            view.SuspendLayout();
            for (int index = 0; index < 100_000; index++) view.Items.Add($"Entry {index}");
            view.VirtualizationEnabled = true;
            view.ResumeLayout();
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(hwnd != 0 && IsWindow(hwnd), "A real native HWND is required.");
                    view.Select();
                    SendMessage(hwnd, 0x0100, 0x23, 0); // WM_KEYDOWN, VK_END through the real backend.
                    Require(view.SelectedIndex == 99_999 && view.GetItemContainer(99_999)?.Focused == true, "Native End did not realize/select/focus the last occurrence.");
                    var selected = view.SelectedItem;
                    var peer = view.AccessibilityObject.GetSelected()!;
                    long identity = peer.RuntimeId;
                    view.ScrollToItem(0);
                    Require(view.Focused && view.SelectedItem == selected, "Retirement lost logical selection or canonical host focus.");
                    Require((peer.State & AccessibleStates.Offscreen) != 0, "Unrealized item must be offscreen.");
                    Require(peer.PerformAction(AccessibleActions.Focus), "Logical item peer did not realize/focus its row.");
                    Require(peer.RuntimeId == identity && view.GetItemContainer(99_999)?.Focused == true, "Recycling changed logical peer identity or focus.");
                    Point screen = view.PointToScreen(new(view.LogicalToDeviceUnits(40), view.LogicalToDeviceUnits(40)));
                    int oldScroll = view.VerticalScrollProperties.Value;
                    SendMessage(hwnd, 0x020A, (nint)(120 << 16), (nint)((screen.Y << 16) | (screen.X & 0xffff)));
                    Require(view.VerticalScrollProperties.Value < oldScroll, "Native wheel did not use existing scrollbars.");
                    for (int pass = 0; pass < 100; pass++)
                    {
                        int index = pass * 7919 % 100_000;
                        view.ScrollToItem(index);
                        Require(view.GetItemContainer(index) is not null, "Jump did not realize its target.");
                        var snapshot = view.GetVirtualizationSnapshot();
                        Require(snapshot.RealizedContainerCount <= 24 && snapshot.CreatedContainerCount <= 24, "Native live/control creation count exceeded viewport/cache bounds.");
                        Require(snapshot.RealizedContainerCount == view.Controls.Count, "Native container mapping differs from actual control tree.");
                    }
                    view.Visible = false;
                    view.ScrollToItem(50_000);
                    view.Visible = true;
                    view.PerformLayout();
                    Require(view.GetItemContainer(50_000) is not null, "Hidden-ancestor scroll/layout did not preserve its target.");
                    view.Items.Insert(0, new ListViewItem { Text = "Inserted" });
                    Require(view.SelectedIndex == 100_000 && view.SelectedItem == selected, "Native insert lost logical selection.");
                    view.Items.Move(100_000, 25_000);
                    Require(view.SelectedIndex == 25_000 && view.SelectedItem == selected, "Native move lost logical selection.");
                    form.ClientSize = new Size(760, 520);
                    view.ScrollToItem(25_000);
                    Require(view.GetItemContainer(25_000) is not null, "Native resize did not realize target.");
                    Console.WriteLine($"VIRTUALIZATION:NATIVE:{hwnd}:logical={view.ItemCount}:live={view.Controls.Count}:created={view.GetVirtualizationSnapshot().CreatedContainerCount}");
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("VIRTUALIZATION:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
