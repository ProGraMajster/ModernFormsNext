using System.Diagnostics;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.Rendering;

namespace ModernFormsNext.PerformanceLab;

internal sealed record BenchmarkResult(
    int SchemaVersion,
    string Scenario,
    string WorkloadVersion,
    DateTimeOffset RecordedAtUtc,
    LabEnvironment Environment,
    StartupObservation Startup,
    ProcessObservation Process,
    FrameSummary Frames,
    ScrollObservation? Scroll,
    double? MeasuredStartOffsetMs,
    long[] MeasuredFrameSequences,
    PerformanceSnapshot Recorder)
{
    public PacingObservation? Pacing { get; init; }

    public IReadOnlyDictionary<string, string> UnavailableMeasurements
    {
        get
        {
            var observations = new Dictionary<string, string>
            {
                ["isolatedRasterTime"] = "The framework render category includes software drawing and composition.",
                ["physicalScanout"] = "Native paint and display-mode refresh do not observe physical scanout.",
                ["droppedDisplayedFrames"] = "Neither merged damage nor a native callback counts dropped display frames.",
                ["physicalInputLatency"] = "The lab can time a synthetic application message, not a physical input device."
            };
            if (Frames.GpuExecutionMs is null)
                observations["gpuExecution"] = "The selected renderer did not report GPU execution time.";
            if (!Frames.PresentationTimestampAvailable)
                observations["compositorPresentation"] = "The selected renderer did not report presentation timestamps.";
            if (Environment.ReportedRefreshHz is null)
                observations["displayModeRefresh"] = "Windows did not supply a usable mode for the window monitor.";
            return observations;
        }
    }
}

internal sealed record PacingObservation(
    string Mode, int? DisplayModeHz, FrameRequestCounts Requests, int WorkloadUpdates,
    int MinimumWarmupMs, bool ObservedQuietJit, double LastWarmupJitDeltaMs, double MeasuredJitMs,
    SyntheticInputObservation? SyntheticInput, Distribution? InputToCompletedNativePaintMs)
{
    public Distribution? SyntheticQueueToHandlerMs => SyntheticInput is { } input
        ? Distribution.From(input.Samples.Select(item => (item.HandledAt - item.PostedAt).TotalMilliseconds)) : null;
}

internal sealed record StartupObservation(
    double MainToShownMs,
    double MainToFirstCompletePaintLowerMs,
    double MainToFirstCompletePaintUpperMs,
    long FirstCompletePaintSequence)
{
    public double MainToFirstCompletePaintEstimateMs =>
        (MainToFirstCompletePaintLowerMs + MainToFirstCompletePaintUpperMs) / 2;
}

internal sealed record ScrollObservation(
    int LogicalItemCount, int RowHeight, int CacheItems, int WarmupSteps, int Steps,
    int StepLogicalPixels, int RequestedIntervalMs, int StartOffset, int EndOffset,
    ItemVirtualizationSnapshot Before, ItemVirtualizationSnapshot After);

internal readonly record struct ProcessSample(
    long Timestamp, double CpuMs, long WorkingSetBytes, long PrivateBytes, long ManagedAllocatedBytes)
{
    internal static ProcessSample Capture(Process process)
    {
        long timestamp = Stopwatch.GetTimestamp();
        process.Refresh();
        return new(timestamp, process.TotalProcessorTime.TotalMilliseconds, process.WorkingSet64,
            process.PrivateMemorySize64, GC.GetTotalAllocatedBytes(precise: true));
    }
}

internal sealed record ProcessObservation(
    double WallMs, double TotalCpuMs, double CpuPercentOfOneLogicalCore,
    ProcessSample Start, ProcessSample End)
{
    internal static ProcessObservation Compare(ProcessSample start, ProcessSample end)
    {
        double wall = Stopwatch.GetElapsedTime(start.Timestamp, end.Timestamp).TotalMilliseconds;
        double cpu = end.CpuMs - start.CpuMs;
        if (wall <= 0 || cpu < 0 || end.ManagedAllocatedBytes < start.ManagedAllocatedBytes)
            throw new InvalidOperationException("The process counters are inconsistent.");
        return new(wall, cpu, cpu / wall * 100, start, end);
    }

    public long ManagedAllocatedBytes => End.ManagedAllocatedBytes - Start.ManagedAllocatedBytes;
}

internal sealed record Distribution(int Samples, double P50, double P95, double P99)
{
    internal static Distribution? From(IEnumerable<double> source)
    {
        double[] sorted = source.Order().ToArray();
        if (sorted.Length == 0)
            return null;

        double Rank(double fraction) => sorted[(int)Math.Ceiling(fraction * sorted.Length) - 1];
        return new(sorted.Length, Rank(0.50), Rank(0.95), Rank(0.99));
    }
}

internal sealed record FrameSummary(
    int SampleCount,
    PerformanceRenderInfo RenderInfo,
    Distribution? NativePaintWallMs,
    Distribution? LayoutWallMs,
    Distribution? FrameworkRenderWallMs,
    Distribution? CallbackIntervalMs,
    Distribution? InputDispatchWallMs,
    Distribution? SubmissionWallMs,
    Distribution? ManagedUiAllocationBytes,
    Distribution? GpuExecutionMs,
    bool PresentationTimestampAvailable)
{
    internal static FrameSummary Create(PerformanceFrameMetrics[] frames, bool isMeasuredWindow, PerformanceRenderInfo firstPaintInfo)
        => new(frames.Length, frames.Length > 0 ? frames[0].RenderInfo : firstPaintInfo,
            Distribution.From(frames.Select(frame => frame.Duration.TotalMilliseconds)),
            Distribution.From(frames.Select(frame =>
                (frame.ThreadWorkSincePreviousFrame.LayoutTime + frame.Work.LayoutTime).TotalMilliseconds)),
            Distribution.From(frames.Select(frame => frame.Work.RenderTime.TotalMilliseconds)),
            // The first retained interval can cross the warmup/measurement boundary.
            Distribution.From(frames.Skip(isMeasuredWindow ? 1 : 0)
                .Where(frame => frame.Interval.HasValue)
                .Select(frame => frame.Interval!.Value.TotalMilliseconds)),
            Distribution.From(frames.Where(frame =>
                    frame.ThreadWorkSincePreviousFrame.InputEvents + frame.Work.InputEvents > 0)
                .Select(frame => (frame.ThreadWorkSincePreviousFrame.InputTime + frame.Work.InputTime).TotalMilliseconds)),
            Distribution.From(frames.Where(frame => frame.RenderInfo.PresentationCpuTime.HasValue)
                .Select(frame => frame.RenderInfo.PresentationCpuTime!.Value.TotalMilliseconds)),
            Distribution.From(frames.Where(frame => frame.AllocatedBytes.HasValue)
                .Select(frame => (double)frame.AllocatedBytes!.Value)),
            Distribution.From(frames.Where(frame => frame.RenderInfo.GpuDuration.HasValue)
                .Select(frame => frame.RenderInfo.GpuDuration!.Value.TotalMilliseconds)),
            frames.Any(frame => frame.RenderInfo.PresentationTimestamp.HasValue));
}
