using System.Diagnostics;
using System.Drawing;
using ModernFormsNext.Diagnostics;
using UiTimer = ModernFormsNext.Timer;

namespace ModernFormsNext.PerformanceLab;

/// <summary>Runs one scenario on the owning STA and captures detached evidence outside paint.</summary>
internal sealed class BenchmarkRun(
    BenchmarkScenario scenario,
    PerformanceProfiler profiler,
    Process process,
    ProcessSample initial,
    long enteredMain,
    long recorderStartLower,
    long recorderStartUpper)
{
    private readonly UiTimer timer = new();
    private LabWindow window = null!;
    private Action tick = null!;
    private PerformanceFrameMetrics firstFrame;
    private BenchmarkResult? result;
    private Exception? failure;
    private long shownAt;
    private ProcessSample? measuredStart;
    private TimeSpan? measuredStartOffset;

    internal int PaintPasses => window.PaintPasses;

    internal BenchmarkResult Execute()
    {
        using (timer)
        using (window = new LabWindow
        {
            Text = $"ModernFormsNext PerformanceLab — {scenario.Id}",
            ClientSize = new Size(960, 640)
        })
        {
            window.Controls.Add(scenario.CreateContent());
            window.Shown += (_, _) =>
            {
                shownAt = Stopwatch.GetTimestamp();
                Schedule(25, WaitForFirstFrame);
            };
            window.Closed += (_, _) =>
            {
                timer.Stop();
                profiler.Dispose();
            };
            timer.Tick += OnTick;
            Application.Run(window);
        }
        if (failure is not null)
            throw new InvalidOperationException("PerformanceLab could not complete the scenario.", failure);
        return result ?? throw new InvalidOperationException("The window closed before data collection completed.");
    }

    internal void Schedule(int interval, Action action)
    {
        tick = action;
        timer.Interval = interval;
        timer.Start();
    }

    /// <summary>Starts the timed phase after setup and warmup have finished.</summary>
    internal void BeginMeasurement()
    {
        measuredStartOffset = profiler.Capture().Elapsed;
        measuredStart = ProcessSample.Capture(process);
    }

    internal void Complete(ScrollObservation? scroll = null)
    {
        timer.Stop();
        var measuredEnd = ProcessSample.Capture(process);
        var snapshot = profiler.Capture();
        if (snapshot.DroppedFrames != 0 || snapshot.DroppedScopes != 0 ||
            snapshot.DroppedSources != 0 || snapshot.RecorderFailures != 0)
            throw new InvalidOperationException("The capture is incomplete. Preserve the failure and inspect recorder limits.");

        var frames = measuredStartOffset is { } offset
            ? snapshot.Frames.Where(frame => frame.SourceId == firstFrame.SourceId && IsWindowFrame(frame)
                && frame.Started >= offset).ToArray()
            : [firstFrame];

        if (frames.Length == 0 || frames.Any(frame => !frame.Completed))
            throw new InvalidOperationException("The workload has missing or incomplete render callbacks.");
        if (scroll is not null && (frames.Length < 2 ||
            frames.Any(frame => frame.RenderInfo.Scale != firstFrame.RenderInfo.Scale ||
                frame.RenderInfo.PixelWidth != firstFrame.RenderInfo.PixelWidth ||
                frame.RenderInfo.PixelHeight != firstFrame.RenderInfo.PixelHeight ||
                frame.RenderInfo.ActiveBackend != firstFrame.RenderInfo.ActiveBackend ||
                frame.RenderInfo.HostGeneration != firstFrame.RenderInfo.HostGeneration ||
                frame.RenderInfo.BackingGeneration != firstFrame.RenderInfo.BackingGeneration)))
            throw new InvalidOperationException("The scroll capture requires multiple comparable frames from one viewport and backend.");

        double finishInProfiler = (firstFrame.Started + firstFrame.Duration).TotalMilliseconds;
        var startup = new StartupObservation(
            Stopwatch.GetElapsedTime(enteredMain, shownAt).TotalMilliseconds,
            Stopwatch.GetElapsedTime(enteredMain, recorderStartLower).TotalMilliseconds + finishInProfiler,
            Stopwatch.GetElapsedTime(enteredMain, recorderStartUpper).TotalMilliseconds + finishInProfiler,
            firstFrame.Sequence);
        var processWindow = ProcessObservation.Compare(measuredStart ?? initial, measuredEnd);
        var environment = WindowsEnvironment.Read(window.PlatformHandle.Handle);
        result = new(1, scenario.Id, scenario.WorkloadVersion, DateTimeOffset.UtcNow, environment,
            startup, processWindow, FrameSummary.Create(frames, measuredStartOffset is not null), scroll,
            measuredStartOffset?.TotalMilliseconds, frames.Select(frame => frame.Sequence).ToArray(), snapshot);

        window.Close();
    }

    private static bool IsWindowFrame(PerformanceFrameMetrics frame)
        => frame.RenderInfo.Boundary == PerformanceFrameBoundary.NativePaint && !frame.RenderInfo.IsOffscreen;

    private void WaitForFirstFrame()
    {
        // A full native callback with actual content rendering is the marker. Shown
        // and the timer only arrange a safe capture after the window message returns.
        firstFrame = profiler.Capture().Frames.FirstOrDefault(frame =>
            frame.Completed && IsWindowFrame(frame)
            && frame.RenderInfo.Redraw == PerformanceRedraw.FullSurface
            && frame.RenderInfo is { PixelWidth: > 0, PixelHeight: > 0, ActiveBackend: not null }
            && frame.Work.ControlsRepainted > 0 && frame.Work.RenderTime > TimeSpan.Zero);
        if (firstFrame.Completed)
            scenario.Begin(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            if (Stopwatch.GetElapsedTime(enteredMain) > TimeSpan.FromSeconds(30))
                throw new TimeoutException("Timed out waiting for the benchmark viewport and render callbacks.");
            tick();
        }
        catch (Exception error)
        {
            failure = error;
            timer.Stop();
            window.Close();
        }
    }

    private sealed class LabWindow : Form
    {
        internal int PaintPasses { get; private set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Read only from a later dispatcher tick. The profiler records actual
            // callback completion, including the child paint and native boundary.
            PaintPasses++;
        }
    }
}
