# ModernFormsNext PerformanceLab

This Windows-first benchmark application runs two versioned workloads through
the real window, layout and rendering pipeline. The existing
[PerformanceProfiler](../../docs/performance-diagnostics.md) records framework
work with the overlay disabled. Extra observations are limited to the process,
workload boundaries and Windows environment. The project is a non-packable sample.

## Run

From the repository root, build Release and run a fresh process per sample. Run
in the same Windows session and keep display, DPI, power policy, foreground
workload and rendering options consistent across comparisons. Use a new output
directory each time. The program exits after a valid run and only publishes
both result files together.

```powershell
$project = './samples/ModernFormsNext.PerformanceLab/ModernFormsNext.PerformanceLab.csproj'
dotnet build $project -c Release -m:1 /p:UseSharedCompilation=false
dotnet run --project $project -c Release --no-build -- --scenario startup --backend software --output artifacts/performancelab/startup-01
dotnet run --project $project -c Release --no-build -- --scenario virtualized-scroll --backend software --output artifacts/performancelab/scroll-01
```

The app accepts `software` and `auto`. Pin `software` when comparing CPU
baselines. `results.json` contains the versioned scenario, environment, raw
profiler capture, retained frame sequences and process counters. `report.md`
summarizes the same samples. Runtime and actual renderer come from the running
application. The managed Skia version and native Skia version are recorded
separately. Export occurs after measurement.

## Workloads

| Scenario | Boundary and fixed parameters |
| --- | --- |
| `startup` | Fresh process, from entry to `Main`, a 960 × 640 logical-pixel client area, and a flow layout with 72 labels and 24 buttons. Record Shown and the first completed full native paint with rendered control content. |
| `virtualized-scroll` | 100,000 indexed rows in the existing CollectionView, 28-logical-pixel rows and two extra realized cache items. Warm up 12 updates, reset the viewport, then perform 120 programmatic 56-logical-pixel updates at a requested 16 ms dispatcher interval. Record completed native paint callbacks and actual scroll/realization/recycling evidence. |

The benchmark scrolls through the public scrollbar position. Physical input,
wheel routing, gestures and fling are separate future workloads. Sources,
fixtures and branch versions should be recorded with the results. Extend the
harness by adding a benchmark scenario that supplies content and uses the
runner's timing and report boundaries. Give changed workloads a distinct
version before comparing them with a saved baseline.

## Interpret the data

- Startup ends at the recorded complete native paint. The epoch around
  profiler startup is bracketed with the same monotonic clock. The result
  excludes CLR process initialization before Main. Record the cache state
  separately when comparing multiple fresh processes.
- Framework layout, render, native paint and GDI submission are elapsed wall
  time. They can overlap and must not be summed into CPU time. The render
  category covers software drawing and composition. Isolated raster time is
  unavailable at this checkpoint.
- Process CPU is the delta of the process CPU clock at the reported interval
  boundaries. Startup uses the process sample taken early in Main and the
  readiness callback after paint. Scroll uses the post-warmup interval through
  the final callback. Percent CPU uses one logical core as the reference.
- Process working set and private bytes are point samples. Managed
  process allocations and profiler UI-thread frame allocations are different
  observations. Neither measures GPU memory.
- Distributions use nearest rank. The startup paint has one sample, and the
  first scroll interval is excluded if it could cross the warmup boundary.
  A dispatcher interval is a workload input, not a display cadence.
- The refresh value comes from the Windows device context of the window's
  monitor. It describes the reported mode. GDI submission, repaint intervals
  and a virtual display do not establish the timing of physical presentation.
  GPU execution and compositor presentation are reported as unavailable when
  the production renderer does not supply them.
- Missing frames, changed viewport/backend identities, recorder loss and
  unavailable observations are treated explicitly. Keep the raw data and the
  exact framework, runtime, display, worktree and scenario identities when
  revisiting this baseline for SkiaSharp 4.x or GPU.

[Windows display-device counters](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getdevicecaps)
