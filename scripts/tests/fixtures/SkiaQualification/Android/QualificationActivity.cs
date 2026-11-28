using Android.App;
using Android.Content.PM;
using Android.OS;
using ModernFormsNext;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Android.Windowing;
using SkiaSharp;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaQualification;
using Mfn = ModernFormsNext.Application;
using IOPath = System.IO.Path;

namespace SkiaQualification.Android;

/// <summary>Isolated Activity for software paint and typography qualification on Android.</summary>
[Activity(Name = "com.modernformsnext.skiaqualification.QualificationActivity",
    MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class QualificationActivity : AndroidWindowActivity
{
    private long activityStart;
    private PerformanceProfiler? profiler;
    private Handler? handler;

    /// <inheritdoc/>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        activityStart = Stopwatch.GetTimestamp();
        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc/>
    protected override void OnStartApplication()
    {
        profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
        {
            FrameCapacity = 512,
            TrackAllocations = true,
            TrackGarbageCollections = true
        });
        long measurementStart = Stopwatch.GetTimestamp();
        long cpuStart = global::Android.OS.Process.ElapsedCpuTime;
        long allocatedStart = GC.GetTotalAllocatedBytes(precise: true);
        int[] gcStart = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];

        var form = new Form();
        var view = form.Controls.Add(new CollectionView
        {
            Dock = DockStyle.Fill, ItemSize = new(300, 28), CacheItemCount = 2,
            ItemsSource = Enumerable.Range(0, 100_000).Select(i => $"Entry {i:D6}").ToArray()
        });
        var heading = form.Controls.Add(new Label
        {
            Dock = DockStyle.Top, Height = 245, Multiline = true,
            Font = new ModernFormsNext.Font("sans-serif", 14),
            Text = string.Join("\n", TextCorpus.Cases.Select(c => c.Text.Replace('\n', ' ')))
        });
        // Each timer step changes the viewport. Paint callbacks can coalesce, and the
        // profile explicitly keeps these two quantities independent.
        handler = new Handler(Looper.MainLooper!);
        int steps = 0;
        double? firstObservedPaintMs = null;
        var watch = Stopwatch.StartNew();
        int? initialOffset = null;
        int drain = 0;
        void Tick()
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(25))
            {
                global::Android.Util.Log.Error("MfnSkiaQualification", "Timed out before completing the software workload.");
                return;
            }
            if (firstObservedPaintMs is null)
            {
                if (profiler.Capture().Frames.All(f => !f.Completed || f.RenderInfo.Backend != "Android"))
                {
                    handler.PostDelayed(Tick, 16);
                    return;
                }
                firstObservedPaintMs = Stopwatch.GetElapsedTime(activityStart).TotalMilliseconds;
                initialOffset = view.VerticalScrollProperties.Value;
            }
            if (steps < 120)
            {
                if (steps % 8 == 0)
                    heading.Invalidate();
                view.VerticalScrollProperties.Value += 56;
                steps++;
                handler.PostDelayed(Tick, 16);
                return;
            }
            if (++drain < 5)
            {
                handler.PostDelayed(Tick, 16);
                return;
            }
            var recorded = profiler.Capture();
            var frames = recorded.Frames.Where(f => f.RenderInfo.Backend == "Android" && f.Completed).ToArray();
            var virtualized = view.GetVirtualizationSnapshot();
            if (frames.Length < 2 || recorded.RecorderFailures != 0 || recorded.DroppedFrames != 0 ||
                view.VerticalScrollProperties.Value - initialOffset != 120 * 56 ||
                virtualized.ReusedContainerCount < 1 ||
                frames.Any(f => f.RenderInfo.ActiveBackend != RenderingBackend.Software))
            {
                global::Android.Util.Log.Error("MfnSkiaQualification", "Software frame or virtualization evidence was incomplete.");
                return;
            }

            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var mode = Window?.DecorView?.Display?.GetMode();
            using var memory = new global::Android.OS.Debug.MemoryInfo();
            global::Android.OS.Debug.GetMemoryInfo(memory);
            var result = new
            {
                workload = "android-text-collection-v1", corpus = TextCorpus.Version, steps,
                firstObservedPaintMs,
                measurementMs = Stopwatch.GetElapsedTime(measurementStart).TotalMilliseconds,
                cpuMs = global::Android.OS.Process.ElapsedCpuTime - cpuStart,
                pssKiB = memory.TotalPss,
                workingSetBytes = process.WorkingSet64,
                managedAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedStart,
                processRuntime = RuntimeInformation.FrameworkDescription,
                managedSkia = typeof(SKCanvas).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                nativeSkia = SkiaSharpVersion.Native.ToString(), model = Build.Model,
                os = Build.VERSION.Release, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                dpi = Resources!.DisplayMetrics!.DensityDpi.ToString(),
                display = new { mode?.PhysicalWidth, mode?.PhysicalHeight, mode?.RefreshRate },
                source = GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                initialOffset, finalOffset = view.VerticalScrollProperties.Value,
                virtualized,
                frames = frames.Select(f => new
                {
                    f.Sequence, wallMs = f.Duration.TotalMilliseconds, intervalMs = f.Interval?.TotalMilliseconds,
                    renderMs = f.Work.RenderTime.TotalMilliseconds, layoutMs = f.Work.LayoutTime.TotalMilliseconds,
                    f.AllocatedBytes, active = f.RenderInfo.ActiveBackend.ToString(),
                    boundary = f.RenderInfo.Boundary.ToString(), f.RenderInfo.PixelWidth, f.RenderInfo.PixelHeight
                }).ToArray(),
                gc = gcStart.Select((before, generation) => GC.CollectionCount(generation) - before).ToArray()
            };
            string path = IOPath.Combine(GetExternalFilesDir(null)!.AbsolutePath, "results.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(result));
            File.Move(path + ".tmp", path, overwrite: true);
            global::Android.Util.Log.Info("MfnSkiaQualification", "Results written.");
        }

        handler.PostDelayed(Tick, 16);
        Mfn.Run(form);
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        handler?.RemoveCallbacksAndMessages(null);
        handler?.Dispose();
        profiler?.Dispose();
        base.OnDestroy();
    }
}

/// <summary>Initializes the software backend before the qualification Activity attaches.</summary>
[Application]
public sealed class QualificationApplication(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
    : global::Android.App.Application(handle, ownership)
{
    /// <inheritdoc/>
    public override void OnCreate()
    {
        base.OnCreate();
        Mfn.ConfigureRendering(new RenderingOptions { Backend = RenderingBackend.Software });
        AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this));
    }
}
