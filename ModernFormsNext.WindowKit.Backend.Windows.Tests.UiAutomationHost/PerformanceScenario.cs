using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Threading;

// Isolated native evidence: the ordinary Form paint pipeline is dispatched by user32 on the
// real application thread. This verifies callback/backing facts, not display/GPU presentation.
internal static class PerformanceScenario
{
    internal static int Run()
    {
        try
        {
            using var form = new Form { Text = "ModernFormsNext performance integration", ClientSize = new Size(320, 180) };
            var label = new Label { Text = "Native performance integration", Dock = DockStyle.Fill };
            form.Controls.Add(label);
            Exception? failure = null;
            PerformanceSnapshot? result = null;
            bool recordingStopped = false;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(GetWindowThreadProcessId(hwnd, out uint process) == GetCurrentThreadId() &&
                        process == Environment.ProcessId, "The scenario does not own its HWND/thread.");
                    using var profiler = PerformanceProfiler.Start();
                    Paint(hwnd);
                    var first = profiler.Capture().LatestFrame ?? throw new InvalidOperationException("No WM_PAINT frame recorded.");
                    VerifyNative(first);
                    int initialWidth = first.RenderInfo.PixelWidth!.Value;
                    long initialGeneration = first.RenderInfo.BackingGeneration!.Value;
                    form.ClientSize = new Size(400, 220);
                    Paint(hwnd);
                    var resized = profiler.Capture().LatestFrame!.Value;
                    VerifyNative(resized);
                    Require(resized.SourceId == first.SourceId, "Resize changed the native owner identity.");
                    Require(resized.RenderInfo.PixelWidth > initialWidth &&
                        resized.RenderInfo.BackingGeneration > initialGeneration,
                        "Resize did not report the newly allocated framebuffer.");
                    long inputBefore = profiler.Capture().UnframedWork.InputEvents;
                    SendMessage(hwnd, 0x0200, 0, (nint)((10 << 16) | 10)); // own WM_MOUSEMOVE
                    Require(profiler.Capture().UnframedWork.InputEvents == inputBefore + 1,
                        "The native pointer route was not measured exactly once.");
                    result = profiler.Capture();
                    profiler.Dispose();
                    Paint(hwnd);
                    recordingStopped = label.Visible && form.Visible;
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw new InvalidOperationException("Native performance scenario failed.", failure);
            Require(result is not null && result.Frames.Count >= 2, "The scenario did not complete both real paints.");
            Console.WriteLine("PERFORMANCE:" + JsonSerializer.Serialize(new
            {
                Frames = result!.Frames.Count,
                Completed = result.Frames.All(frame => frame.Completed),
                Backend = result.LatestFrame!.Value.RenderInfo.Backend,
                NativeBoundary = result.LatestFrame.Value.RenderInfo.Boundary.ToString(),
                RecordingStopped = recordingStopped,
                InputEvents = result.UnframedWork.InputEvents,
                RecorderFailures = result.RecorderFailures,
                Width = result.LatestFrame.Value.RenderInfo.PixelWidth,
                BackingGeneration = result.LatestFrame.Value.RenderInfo.BackingGeneration
            }));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifyNative(PerformanceFrameMetrics frame)
    {
        Require(frame.Completed && frame.RenderInfo.Backend == "Windows" &&
            frame.RenderInfo.Boundary == PerformanceFrameBoundary.NativePaint,
            "Shared rendering duplicated or replaced the native boundary.");
        Require(frame.RenderInfo.Acceleration == PerformanceAcceleration.Software &&
            frame.RenderInfo.PixelWidth > 0 && frame.RenderInfo.PixelHeight > 0 &&
            frame.RenderInfo.RowBytes == frame.RenderInfo.PixelWidth * 4 &&
            frame.RenderInfo.BackingBytes == (long)frame.RenderInfo.RowBytes * frame.RenderInfo.PixelHeight &&
            frame.RenderInfo.BackingGeneration > 0 && frame.RenderInfo.HostGeneration == 1,
            "The native callback did not report its actual locked BGRA framebuffer.");
        Require(frame.RenderInfo.GpuDuration is null && frame.RenderInfo.PresentationTimestamp is null &&
            frame.Duration >= frame.Work.RenderTime, "Native callback metadata overstates GPU or presentation evidence.");
        Require(frame.RenderInfo.PresentationCpuTime is { } transfer && transfer >= TimeSpan.Zero && transfer <= frame.Duration,
            "Native frame did not report its bounded GDI submission time.");
    }

    private static void Paint(nint hwnd)
    {
        Require(InvalidateRect(hwnd, 0, false), "Cannot invalidate the owned native window.");
        Require(UpdateWindow(hwnd), "Cannot synchronously paint the owned native window.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(nint hwnd, nint rectangle, [MarshalAs(UnmanagedType.Bool)] bool erase);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(nint hwnd);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
