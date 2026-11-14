using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;
using ModernFormsNext.WindowKit.Threading;

// Issue #130 must run with a real Form/HWND: a headless root does not reproduce
// the visibility and layout sequence of the native window adapter.
internal static class ScrollLayoutScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "Scroll layout regression", ClientSize = new Size(800, 500) };
            form.Style.Border.Width = 0;
            form.TitleBar.Visible = false;
            var firstView = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
            var secondView = form.Controls.Add(new Panel { Dock = DockStyle.Fill, Visible = false });
            var list = firstView.Controls.Add(new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false
            });
            for (int i = 0; i < 15; i++)
                list.Controls.Add(new Panel { Size = new Size(600, 150), Margin = new Padding(0, 0, 0, 14) });

            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Set the client after native creation and removal of the framework chrome.
                    form.ClientSize = new Size(800, 500);
                    Require(list.Size == new Size(800, 500), "The native viewport must match the issue reproduction.");
                    int origin = list.Controls[0].Top;
                    var scrollbar = (VerticalScrollBar)typeof(ScrollableControl).GetField("vscrollbar", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list)!;
                    var renderer = RenderManager.GetRenderer<ScrollBarRenderer>()!;
                    var errors = new List<string>();
                    var events = new List<ScrollEventArgs>();
                    int notifiedOffset = 0;
                    list.Scroll += (_, e) => {
                        Require(e.ScrollOrientation == ScrollOrientation.VerticalScroll, "Native Scroll reported the wrong axis.");
                        Require(e.OldValue == notifiedOffset && e.NewValue == scrollbar.Value - scrollbar.Minimum,
                            "Native Scroll did not report consecutive committed logical offsets.");
                        Require(e.OldValue != e.NewValue, "Native Scroll reported a no-op.");
                        notifiedOffset = e.NewValue;
                        events.Add(e);
                        // Observe the original #130 geometry inside the public callback, not only
                        // after the input transaction has returned to the native test driver.
                        Check("Scroll callback");
                        Console.WriteLine("SCROLL_EVENT:" + JsonSerializer.Serialize(e));
                    };
                    Check("initial");
                    list.VerticalScrollProperties.Value = 480;
                    Require(events.Count == 1 && events[^1].Type == ScrollEventType.ThumbPosition,
                        "Programmatic native scroll must publish exactly one neutral event.");
                    Check("scrolled");
                    secondView.Visible = true;
                    secondView.BringToFront();
                    firstView.Visible = false;
                    Check("hidden");
                    firstView.Visible = true;
                    firstView.BringToFront();
                    secondView.Visible = false;
                    Check("shown");
                    list.PerformLayout();
                    Check("PerformLayout");
                    Require(events.Count == 1, "Hide/show/layout without movement invented a Scroll event.");
                    Wheel(-120);
                    Require(events.Count == 2 && events[^1].Type == ScrollEventType.SmallIncrement,
                        "Native wheel must publish exactly one small-increment event.");
                    Require(scrollbar.Value == 480 + scrollbar.SmallChange, "Native wheel did not advance by SmallChange.");
                    Check("wheel after show");

                    Rectangle thumb = renderer.GetThumbDragBounds(scrollbar);
                    Point drag = Center(thumb);
                    Mouse(scrollbar, drag, 0x0200);
                    Mouse(scrollbar, drag, 0x0201, 1);
                    int previous = scrollbar.Value;
                    int beforeDragEvents = events.Count;
                    for (int step = 1; step <= 3; step++)
                    {
                        Mouse(scrollbar, new Point(drag.X, drag.Y + step * 12), 0x0200, 1);
                        Require(scrollbar.Value > previous, "Native thumb drag did not advance.");
                        previous = scrollbar.Value;
                        Require(events.Count == beforeDragEvents + step && events[^1].Type == ScrollEventType.ThumbTrack,
                            "Each native thumb movement must publish one tracking event.");
                        Check($"thumb move {step}");
                    }
                    Mouse(scrollbar, new Point(drag.X, drag.Y + 36), 0x0202);
                    int beforeTrack = scrollbar.Value;
                    int beforeTrackEvents = events.Count;
                    Point track = Center(renderer.GetIncrementTrackBounds(scrollbar));
                    Mouse(scrollbar, track, 0x0200);
                    Mouse(scrollbar, track, 0x0201, 1);
                    Mouse(scrollbar, track, 0x0202);
                    Require(scrollbar.Value == Math.Min(scrollbar.Maximum, beforeTrack + scrollbar.LargeChange),
                        "Native track click did not advance by LargeChange.");
                    Check("track page");
                    Require(events.Count == beforeTrackEvents + 1 && events[^1].Type == ScrollEventType.LargeIncrement,
                        "Native track click must publish one page event.");

                    int beforeAccessibleEvents = events.Count;
                    Require(list.AccessibilityObject.PerformAction(AccessibleActions.Scroll,
                        AccessibleScrollRequest.ToPercent(null, 50)), "Native-host accessible scroll failed.");
                    Require(scrollbar.Value == scrollbar.Maximum / 2, "Accessible scroll did not reach the requested offset.");
                    Check("accessible scroll");
                    Require(events.Count == beforeAccessibleEvents + 1 && events[^1].Type == ScrollEventType.ThumbPosition,
                        "Accessible percentage must publish one neutral event.");
                    var target = list.Controls[^1].AccessibilityObject;
                    long targetId = target.RuntimeId;
                    Require(target.PerformAction(AccessibleActions.ScrollIntoView), "Native-host ScrollIntoView failed.");
                    Check("accessible reveal");
                    int revealed = scrollbar.Value;
                    Rectangle targetBounds = target.Bounds;
                    list.PerformLayout();
                    Require(target.PerformAction(AccessibleActions.ScrollIntoView) && scrollbar.Value == revealed,
                        "Repeated reveal after layout moved already visible content.");
                    Require(target.RuntimeId == targetId && target.Bounds == targetBounds
                        && (target.State & AccessibleStates.Offscreen) == 0,
                        "Reveal/layout changed accessible identity, geometry or visibility.");
                    Check("accessible reveal relayout");

                    // Run the public primitive through the existing HWND under the same
                    // layout/visibility checks while keeping the semantic action above intact.
                    int beforePublicReveal = events.Count;
                    Require(list.ScrollControlIntoView(list.Controls[0]) && scrollbar.Value == 0,
                        "Public reveal did not return the first row to the native viewport.");
                    Require(events.Count == beforePublicReveal + 1 && events[^1].Type == ScrollEventType.ThumbPosition,
                        "Public reveal must publish a single neutral event for actual movement.");
                    Require(list.ScrollControlIntoView(list.Controls[0]) && events.Count == beforePublicReveal + 1,
                        "Repeating public reveal of a visible native row must stay quiet.");
                    Check("public reveal");

                    foreach (int value in new[] { 0, 480, scrollbar.Maximum - scrollbar.SmallChange, scrollbar.Maximum })
                    {
                        scrollbar.Value = value;
                        for (int cycle = 0; cycle < 2; cycle++)
                        {
                            secondView.Visible = true;
                            secondView.BringToFront();
                            firstView.Visible = false;
                            Check($"hidden {value}/{cycle}");
                            firstView.Visible = true;
                            firstView.BringToFront();
                            secondView.Visible = false;
                            Require(scrollbar.Value == value, "View navigation changed a valid scroll position.");
                            Check($"shown {value}/{cycle}");
                            for (int pass = 0; pass < 3; pass++)
                            {
                                list.PerformLayout();
                                Check($"layout {value}/{cycle}/{pass}");
                            }
                        }
                    }
                    int beforeClampedWheel = events.Count;
                    Wheel(-120);
                    Require(events.Count == beforeClampedWheel, "Clamped native wheel invented a Scroll event.");
                    Require(scrollbar.Value == scrollbar.Maximum, "Wheel at Maximum must stay clamped.");
                    Check("wheel at maximum");
                    Wheel(120);
                    Require(scrollbar.Value == scrollbar.Maximum - scrollbar.SmallChange, "Wheel back from Maximum failed.");
                    Check("wheel back from maximum");

                    scrollbar.Value = scrollbar.Maximum;
                    form.ClientSize = new Size(800, 900);
                    Require(scrollbar.Value == scrollbar.Maximum && scrollbar.Maximum == 15 * 164 - list.Height,
                        "Resize did not clamp to the new content/viewport range.");
                    Check("resize clamp");
                    form.ClientSize = new Size(800, 500);

                    // Exercise both visible and hidden range changes; effective Visible is false
                    // for the latter, but scrollbar Value must still move the retained bounds.
                    foreach (bool hidden in new[] { false, true })
                    {
                        scrollbar.Value = scrollbar.Maximum;
                        firstView.Visible = !hidden;
                        while (list.Controls.Count > 5)
                        {
                            var child = list.Controls[^1];
                            list.Controls.Remove(child);
                            child.Dispose();
                        }
                        Require(scrollbar.Value == scrollbar.Maximum && scrollbar.Maximum == 5 * 164 - list.Height,
                            "Removing content did not clamp to the remaining extent.");
                        Check($"content clamp hidden={hidden}");
                        while (list.Controls.Count > 1)
                        {
                            var child = list.Controls[^1];
                            list.Controls.Remove(child);
                            child.Dispose();
                        }
                        Require(scrollbar.Value == 0, "A fitting child must reset the offset.");
                        Require(!LocalVisibility(scrollbar), "The scrollbar must disappear when content fits.");
                        Check($"content fits hidden={hidden}");
                        var last = list.Controls[0];
                        list.Controls.Clear();
                        last.Dispose();
                        Require(!LocalVisibility(scrollbar), "The scrollbar must stay hidden for empty content.");
                        Check($"empty hidden={hidden}");
                        for (int i = 0; i < 15; i++)
                            list.Controls.Add(new Panel { Size = new Size(600, 150), Margin = new Padding(0, 0, 0, 14) });
                        firstView.Visible = true;
                        Require(scrollbar.Visible && scrollbar.Value == 0, "Scrollbar failed to reappear at the origin.");
                        Check($"repopulated hidden={hidden}");
                    }
                    if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));

                    void Check(string stage)
                    {
                        var position = (Point)typeof(ScrollableControl).GetField("scroll_position", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list)!;
                        var common = typeof(Control).Assembly.GetType("ModernFormsNext.Layout.CommonProperties")!;
                        var bounds = (Size)common.GetMethod("GetLayoutBounds", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [list])!;
                        var bar = list.VerticalScrollProperties;
                        Console.WriteLine("SCROLL_LAYOUT:" + JsonSerializer.Serialize(new {
                            stage, bar.Value, bar.Maximum, bar.LargeChange, position,
                            first = list.Controls.Count == 0 ? Rectangle.Empty : list.Controls[0].Bounds,
                            display = list.DisplayRectangle, layout = bounds
                        }));
                        if (position.Y != bar.Value || list.DisplayRectangle.Top != origin - bar.Value)
                            errors.Add($"{stage}: value={bar.Value}, position={position.Y}, display={list.DisplayRectangle}");
                        for (int i = 0; i < list.Controls.Count; i++)
                            if (list.Controls[i].Top != origin + i * 164 - bar.Value)
                                errors.Add($"{stage}: child {i} top={list.Controls[i].Top}, expected={origin + i * 164 - bar.Value}");
                        if (list.Controls.Count != 0 && !list.Controls.Any(c => c.Top < list.Height && c.Bottom > 0))
                            errors.Add($"{stage}: content leaves an empty viewport.");
                    }

                    void Wheel(int delta)
                    {
                        Point screen = list.PointToScreen(new Point(list.LogicalToDeviceUnits(40), list.LogicalToDeviceUnits(40)));
                        SendMessage(form.PlatformHandle.Handle, 0x020A, (nint)(delta << 16), Pack(screen));
                    }

                    void Mouse(Control target, Point localDevicePoint, uint message, int buttons = 0)
                    {
                        Point screen = target.PointToScreen(localDevicePoint);
                        var client = new NativePoint { X = screen.X, Y = screen.Y };
                        Require(ScreenToClient(form.PlatformHandle.Handle, ref client), "Cannot map native pointer coordinates.");
                        SendMessage(form.PlatformHandle.Handle, message, buttons, Pack(new Point(client.X, client.Y)));
                    }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("SCROLL_LAYOUT:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static nint Pack(Point point) => (nint)((point.Y << 16) | (point.X & 0xffff));
    private static Point Center(Rectangle rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    private static bool LocalVisibility(Control control)
        => (bool)typeof(Control).GetProperty("DesiredVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
