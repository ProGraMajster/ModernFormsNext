using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Threading;
using SkiaSharp;

// Opt-in native fixture: the external UIA client drives ordinary Invoke/visibility/
// parenting APIs. Reflection is read-only diagnostics of renderer-owned bitmaps.
internal static class PresentationLifetimeScenario
{
    private static readonly FieldInfo Buffer = typeof(Control).GetField("back_buffer", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static int Run(int count)
    {
        using var form = new Form { Text = "Presentation ready", ClientSize = new Size(900, 600) };
        using var other = new Form { Text = "Other fragment", ClientSize = new Size(200, 100) };
        var left = new Panel { Bounds = new Rectangle(0, 70, 820, 480) };
        var right = new Panel { Bounds = left.Bounds };
        form.Controls.Add(left); form.Controls.Add(right);
        var page = new Panel { Bounds = new Rectangle(0, 0, 810, 450) };
        left.Controls.Add(page);
        for (int i = 0; i < count; i++)
            page.Controls.Add(new Button { Text = i.ToString(), AccessibleAutomationId = "item." + i,
                Bounds = new Rectangle(i % 40 * 20, i / 40 * 20, 20, 20) });
        var action = new Button { Text = "Advance", AccessibleAutomationId = "presentation.advance", Bounds = new Rectangle(10, 10, 160, 40) };
        form.Controls.Add(action);
        int step = 0, pendingHeartbeat = 0;
        long lastBeat = Stopwatch.GetTimestamp();
        double maxHeartbeatMs = 0;
        Exception? failure = null;
        using var heartbeat = new System.Threading.Timer(_ => {
            if (Interlocked.Exchange(ref pendingHeartbeat, 1) != 0) return;
            Dispatcher.UIThread.Post(() => {
                maxHeartbeatMs = Math.Max(maxHeartbeatMs, Stopwatch.GetElapsedTime(lastBeat).TotalMilliseconds);
                lastBeat = Stopwatch.GetTimestamp(); Interlocked.Exchange(ref pendingHeartbeat, 0);
            });
        }, null, 100, 100);
        action.Click += (_, _) => {
            try {
                long start = Stopwatch.GetTimestamp();
                if (step % 2 == 0) {
                    page.Visible = false;
                    Require(Measure(page).Bytes == 0, "Hidden subtree retained renderer buffers.");
                    var destination = ReferenceEquals(page.Parent, left) ? right : left;
                    page.Parent!.Controls.Remove(page); destination.Controls.Add(page);
                    Require(Measure(page).Bytes == 0, "Hidden reparent allocated renderer buffers.");
                } else {
                    page.Visible = true;
                    // Retire one real control each cycle without touching the preserved item.
                    var transient = new Button { Text = "Transient", Bounds = new Rectangle(0, 420, 80, 20) };
                    page.Controls.Add(transient); page.Controls.Remove(transient); transient.Dispose();
                }
                _ = UpdateWindow(form.PlatformHandle.Handle);
                var pixels = Measure(page);
                if (page.Visible) Require(pixels.Bytes > 0, "Visible subtree was not repainted.");
                step++;
                other.Text = "Other " + step;
                form.Text = "Presentation " + step;
                using var process = Process.GetCurrentProcess();
                Console.WriteLine("SAMPLE:" + JsonSerializer.Serialize(new { step, count, visible = page.Visible,
                    bitmapCount = pixels.Count, bitmapBytes = pixels.Bytes, privateBytes = process.PrivateMemorySize64,
                    managedBytes = GC.GetTotalMemory(false), maxHeartbeatMs,
                    actionMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds }));
            } catch (Exception error) { failure = error; Console.Error.WriteLine(error); Application.Exit(); }
        };
        form.Shown += (_, _) => {
            other.Show();
            Dispatcher.UIThread.Post(() => {
                _ = UpdateWindow(form.PlatformHandle.Handle);
                Console.WriteLine("HWND:" + form.PlatformHandle.Handle.ToInt64());
            });
        };
        Application.Run(form);
        return failure is null && step == 200 ? 0 : 1;
    }

    private static (int Count, long Bytes) Measure(Control root)
    {
        int count = 0; long bytes = 0;
        var stack = new Stack<Control>(); stack.Push(root);
        while (stack.TryPop(out var control)) {
            if (Buffer.GetValue(control) is SKBitmap bitmap && bitmap.Handle != IntPtr.Zero) { count++; bytes += (long)bitmap.RowBytes * bitmap.Height; }
            foreach (Control child in control.Controls) stack.Push(child);
        }
        return (count, bytes);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(nint hwnd);
}
