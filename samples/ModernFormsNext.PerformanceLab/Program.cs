using System.Diagnostics;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Backend;
using IOPath = System.IO.Path;

namespace ModernFormsNext.PerformanceLab;

internal static class Program
{
    private const string Usage = "Usage: ModernFormsNext.PerformanceLab --scenario startup|virtualized-scroll|pacing-idle|pacing-tree|pacing-animation|pacing-resize|pacing-input --output <new directory> [--backend software|auto] [--pacing reference|refresh] [--warmup-ms 0..8000]";

    [STAThread]
    private static int Main(string[] args)
    {
        long enteredMain = Stopwatch.GetTimestamp();
        if (args is ["--help"])
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            var options = ParseOptions(args);
            using var process = Process.GetCurrentProcess();
            var initial = ProcessSample.Capture(process);
            Application.ConfigureRendering(new RenderingOptions { Backend = options.Backend });
            FrameworkBootstrap.EnsureInitialized();

            var profilerOptions = new PerformanceProfilerOptions
            {
                FrameCapacity = options.WarmupMs > 0 ? 2048 : 512,
                TrackAllocations = true,
                TrackGarbageCollections = true
            };
            // The recorder owns its epoch. Bracket its public Start call to bound the
            // conversion of a retained frame timestamp into time since Main.
            long recorderStartLower = Stopwatch.GetTimestamp();
            using var profiler = PerformanceProfiler.Start(profilerOptions);
            long recorderStartUpper = Stopwatch.GetTimestamp();
            var run = new BenchmarkRun(options.Scenario, profiler, process, initial,
                enteredMain, recorderStartLower, recorderStartUpper, options.Pacing, options.WarmupMs);
            var result = run.Execute();
            BenchmarkReport.Save(options.Output, result);
            Console.WriteLine($"Recorded {result.Scenario} in {options.Output}");
            return 0;
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static LabOptions ParseOptions(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            string key = args[index];
            if (key is not ("--scenario" or "--output" or "--backend" or "--pacing" or "--warmup-ms") || index + 1 >= args.Length ||
                string.IsNullOrWhiteSpace(args[index + 1]) || !arguments.TryAdd(key, args[index + 1]))
                throw new ArgumentException($"Invalid or repeated option: {key}");
        }

        BenchmarkScenario scenario = arguments.GetValueOrDefault("--scenario") switch
        {
            "startup" => new StartupScenario(),
            "virtualized-scroll" => new VirtualizedScrollScenario(),
            "pacing-idle" => new PacingScenario(PacingWorkload.Idle),
            "pacing-tree" => new PacingScenario(PacingWorkload.Tree),
            "pacing-animation" => new PacingScenario(PacingWorkload.Animation),
            "pacing-resize" => new PacingScenario(PacingWorkload.Resize),
            "pacing-input" => new PacingScenario(PacingWorkload.Input),
            _ => throw new ArgumentException("Choose one supported scenario.")
        };
        var backend = arguments.GetValueOrDefault("--backend", "software") switch
        {
            "software" => RenderingBackend.Software,
            "auto" => RenderingBackend.Auto,
            _ => throw new ArgumentException("Choose software or auto.")
        };
        if (!arguments.TryGetValue("--output", out string? requestedPath))
            throw new ArgumentException("Specify a new results directory.");

        string output = IOPath.GetFullPath(requestedPath);
        if (Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("The results path already exists.");

        string? pacing = arguments.GetValueOrDefault("--pacing");
        if (pacing is not (null or "reference" or "refresh"))
            throw new ArgumentException("Choose reference or refresh pacing.");
        if (scenario is PacingScenario && pacing is null) pacing = "reference";
        string? warmup = arguments.GetValueOrDefault("--warmup-ms");
        int warmupMs = 0;
        if (warmup is not null &&
            (!int.TryParse(warmup, System.Globalization.CultureInfo.InvariantCulture, out warmupMs) ||
             warmupMs is < 0 or > 8000 || pacing is null || scenario is StartupScenario || scenario.AllowsEmptyMeasurement))
            throw new ArgumentException("A warmup of up to 8000 ms requires a working pacing scenario.");
        return new(scenario, backend, output, pacing, warmupMs);
    }

    private sealed record LabOptions(BenchmarkScenario Scenario, RenderingBackend Backend, string Output, string? Pacing, int WarmupMs);
}
