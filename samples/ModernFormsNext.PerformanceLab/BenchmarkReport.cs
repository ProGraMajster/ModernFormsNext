using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IOPath = System.IO.Path;

namespace ModernFormsNext.PerformanceLab;

internal static class BenchmarkReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Publishes both detached files as one new result directory on the same volume.</summary>
    internal static void Save(string output, BenchmarkResult result)
    {
        string parent = IOPath.GetDirectoryName(output)
            ?? throw new ArgumentException("The results path must name a new directory.", nameof(output));
        Directory.CreateDirectory(parent);
        string staging = IOPath.Combine(parent, ".performancelab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string json = IOPath.Combine(staging, "results.json");
        string markdown = IOPath.Combine(staging, "report.md");
        try
        {
            using (var stream = new FileStream(json, FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, result, JsonOptions);
            File.WriteAllText(markdown, WriteMarkdown(result));
            Directory.Move(staging, output);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                File.Delete(json);
                File.Delete(markdown);
                Directory.Delete(staging);
            }
        }
    }

    private static string WriteMarkdown(BenchmarkResult result)
    {
        var report = new StringBuilder()
            .AppendLine("# ModernFormsNext PerformanceLab")
            .AppendLine()
            .AppendLine($"Scenario: {result.Scenario} ({result.WorkloadVersion})")
            .AppendLine(FormattableString.Invariant($"Recorded UTC: {result.RecordedAtUtc:O}"))
            .AppendLine($"Lab build: {result.Environment.LabBuild ?? "unavailable"}")
            .AppendLine($"Framework build: {result.Recorder.FrameworkVersion}")
            .AppendLine($"Runtime: {result.Environment.Runtime} {result.Environment.ProcessArchitecture}")
            .AppendLine($"SkiaSharp: managed {result.Environment.ManagedSkia}, native {result.Environment.NativeSkia}")
            .AppendLine($"OS: {result.Environment.WindowsVersion}")
            .AppendLine($"CPU: {result.Environment.ProcessorModel ?? "unavailable"}, {result.Environment.LogicalProcessors} logical processors")
            .AppendLine($"Physical RAM: {Mebibytes(result.Environment.PhysicalMemoryBytes)}")
            .AppendLine($"Display adapter: {result.Environment.DisplayAdapter ?? "unavailable"}")
            .AppendLine($"Window DPI: {result.Environment.WindowDpi?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}")
            .AppendLine($"Windows-reported display mode: {result.Environment.ReportedRefreshHz?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"} Hz")
            .AppendLine($"Backend: {result.Frames.RenderInfo.Backend}, requested {result.Frames.RenderInfo.RequestedBackend}, active {result.Frames.RenderInfo.ActiveBackend}")
            .AppendLine()
            .AppendLine($"Main to Shown: {Number(result.Startup.MainToShownMs)} ms")
            .AppendLine($"Main to first complete native paint: {Number(result.Startup.MainToFirstCompletePaintEstimateMs)} ms")
            .AppendLine($"Recorder-epoch bounds: {Number(result.Startup.MainToFirstCompletePaintLowerMs)}–{Number(result.Startup.MainToFirstCompletePaintUpperMs)} ms")
            .AppendLine($"Process window: {Number(result.Process.WallMs)} ms, CPU {Number(result.Process.TotalCpuMs)} ms ({Number(result.Process.CpuPercentOfOneLogicalCore)}% of one logical core)")
            .AppendLine($"Process working set: {Mebibytes((ulong)result.Process.Start.WorkingSetBytes)} → {Mebibytes((ulong)result.Process.End.WorkingSetBytes)}")
            .AppendLine($"Process private memory: {Mebibytes((ulong)result.Process.Start.PrivateBytes)} → {Mebibytes((ulong)result.Process.End.PrivateBytes)}")
            .AppendLine($"Managed process allocations: {result.Process.ManagedAllocatedBytes} B")
            .AppendLine()
            .AppendLine("| Retained observations | Samples | p50 | p95 | p99 |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: |");
        Line("Native paint wall time, ms", result.Frames.NativePaintWallMs);
        Line("Layout wall time, ms", result.Frames.LayoutWallMs);
        Line("Framework render wall time, ms", result.Frames.FrameworkRenderWallMs);
        Line("Callback interval, ms", result.Frames.CallbackIntervalMs);
        Line("Input dispatch wall time in observed interval, ms", result.Frames.InputDispatchWallMs);
        Line("Native framebuffer submission wall time, ms", result.Frames.SubmissionWallMs);
        Line("Managed UI-thread allocation, B", result.Frames.ManagedUiAllocationBytes);
        Line("GPU execution, ms", result.Frames.GpuExecutionMs);

        if (result.Pacing is { } pacing)
        {
            Line("Synthetic pointer posted to handler, ms", pacing.SyntheticQueueToHandlerMs);
            Line("Synthetic pointer posted to completed native paint, ms", pacing.InputToCompletedNativePaintMs);
            report.AppendLine()
                .AppendLine($"Pacing: {pacing.Mode}; updates {pacing.WorkloadUpdates}, native invalidation requests {pacing.Requests.Signals}, merged damage requests {pacing.Requests.Merged}.")
                .AppendLine($"Warmup requested: {pacing.MinimumWarmupMs} ms. Quiet JIT sample: {pacing.ObservedQuietJit}. JIT time during measurement: {Number(pacing.MeasuredJitMs)} ms.");
        }

        if (result.Scroll is { } scroll)
        {
            report.AppendLine()
                .AppendLine($"CollectionView: {scroll.LogicalItemCount} items, {scroll.Steps} measured offset updates of {scroll.StepLogicalPixels} logical px at a requested {scroll.RequestedIntervalMs} ms interval")
                .AppendLine($"Viewport offset: {scroll.StartOffset} → {scroll.EndOffset} logical px")
                .AppendLine($"Realized containers: {scroll.Before.RealizedContainerCount} → {scroll.After.RealizedContainerCount}")
                .AppendLine($"Container reuses in measured window: {scroll.After.ReusedContainerCount - scroll.Before.ReusedContainerCount}");
        }

        return report.AppendLine()
            .AppendLine("Profiler phase timings are elapsed wall time. Layout and rendering can overlap other observations.")
            .AppendLine("The framework render category includes software rendering and composition. Standalone raster timing is unavailable.")
            .AppendLine("GDI submission is a CPU-side wall-time observation. Display-mode Hz and callback intervals do not measure scanout.")
            .AppendLine(result.Frames.PresentationTimestampAvailable
                ? "The renderer supplied presentation timestamps in the raw capture. Physical scanout is unobserved."
                : "Actual compositor presentation and physical scanout are unavailable.")
            .AppendLine("Raw recorder data, process counters, frame identifiers and versions are in results.json.")
            .ToString();

        void Line(string name, Distribution? data)
        {
            report.AppendLine(data is null
                ? $"| {name} | 0 | unavailable | unavailable | unavailable |"
                : $"| {name} | {data.Samples} | {Number(data.P50)} | {Number(data.P95)} | {Number(data.P99)} |");
        }
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Mebibytes(ulong? bytes)
        => bytes is { } value ? Number(value / (1024d * 1024)) + " MiB" : "unavailable";
}
