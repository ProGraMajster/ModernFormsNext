# Performance diagnostics

## Current checkpoint

Issue #58 remains **OPEN/PARTIAL**. This checkpoint provides the instrumentation,
bounded snapshots and optional HUD described below so critical DPI/performance
investigation in #120 can use real observations. Explicit managed-heap/working-set
snapshots, specialized virtualization producers, calibrated workload allocation
budgets and broader Android/device performance qualification are deferred. The five
Gallery workload families are available; their existence is not a benchmark result.

`ModernFormsNext.Diagnostics.PerformanceProfiler` records framework work through the
existing layout, input, animation and rendering paths. Its optional display draws after
application content; it is not a control, does not participate in layout or hit testing,
and does not create another rendering loop.

This implementation is under validation for issue #58. Build, package, native,
visual and allocation-budget results are pending until recorded in the
[development report](development/codex-autonomous-issue-run.md). The API and measurement
definitions below describe the implementation, not a completed platform-performance claim.

## Start and stop recording

Start on the initialized application's UI thread, for example from an explicit
diagnostics command. Keep the returned object for the desired recording lifetime and
dispose it on that same thread. Only one profiler may own a UI thread. A page should
not replace a session owned by its application.

```csharp
using ModernFormsNext.Diagnostics;

// Run in an application UI callback. Store the session for later commands.
var profiler = PerformanceProfiler.Start(new PerformanceProfilerOptions
{
    FrameCapacity = 256,
    SlowFrameCapacity = 32,
    SlowFrameThreshold = TimeSpan.FromMilliseconds(33.3),
    DetailedControls = false,
    TrackAllocations = true,
    TrackGarbageCollections = true
});

// Later, on the same UI thread:
PerformanceSnapshot snapshot = profiler.Capture();
if (snapshot.LatestFrame is { } frame)
{
    Console.WriteLine($"{frame.RenderInfo.Backend}: {frame.Duration.TotalMilliseconds:0.##} ms");
}

profiler.Dispose();
```

The default display is hidden. Recording and programmatic snapshots work without it.
Capture makes read-only copies; the detached snapshot can be read on another thread
and retained after disposal. Capture and export allocate explicitly. Do not call them
from every paint solely to refresh a label: the built-in display borrows bounded data
from the same recorder without cloning the public snapshot.

Recording capacity is finite. `DroppedFrames`, `DroppedDetails`, `DroppedSources`,
`DroppedRegions` and other availability counters distinguish omitted observations from
zero work. Enable detailed control tracking only when needed; it records bounded numeric
identities and control type metadata, not control names or entered text.

## Read the measurements correctly

| Field or group | Meaning |
| --- | --- |
| `Duration` | Monotonic elapsed time around the observed callback, including waits and preemption. It is not CPU execution time, GPU time or presentation latency. |
| `Interval` / `FramesPerSecond` | Start-to-start interval for a comparable source and backing generation. A first frame has no interval. Demand-driven idle time lowers the observed frequency; it is not evidence of dropped display refreshes. |
| `Work` | Framework work inside this frame's active boundary. Timing categories overlap and must not be summed to invent total CPU time. |
| `ThreadWorkSincePreviousFrame` | Work outside frames on the UI thread since the preceding outer frame start. It is context from all sources, not work assigned exclusively to the displayed window. Layout and input often occur here before paint. |
| `UnframedWork` | Cumulative work outside frames since recording started, not another per-frame duration. |
| Layout / preferred size | Executed layout and the current preferred-size query/cache/core boundaries. There is no fabricated universal measure/arrange split. |
| Repainted / composited / cache reuse | An attempted dirty-buffer paint, a successful buffer composition into its parent, or reuse of a clean buffer. These are distinct operations. A cached parent need not visit its descendants; composition culls children outside the active clip before allocating their backing. Invisible and zero-size skips are separate. |
| Invalidation / redraw | Requests to invalidate are separate from actual root redraw policy. The Windows backend reports FullSurface or PartialSurface from native paint damage. Recording alone preserves local damage; the diagnostic HUD requests full presentation. |
| Allocated bytes | Optional managed allocation on the UI thread during the frame, including application callbacks there. Native Skia, Java, GPU and other-thread allocations are excluded. |
| GC deltas | Optional process-wide collection counts. They do not establish that this control or frame caused collection. |
| Shader creation / disposal | Framework-owned shader wrappers and transformed replacements in production gradient/glass/Hue/ColorBox paths. Counts cover explicit UI disposal, not native finalization, arbitrary consumer Skia objects or native-memory usage. |
| Surface information | Actual backend, acceleration availability, scale, logical/device dimensions, format, stride and generation where the host supplies them. Unsupported data stays unavailable. |
| Slow frame | A callback duration exceeding the configured threshold, with bounded retained context. An idle gap by itself does not make the following render slow. |

Compare frames from the same source and generation. The display uses the previous
completed frame for its current root; its graph reads the existing bounded history.
It never combines unrelated windows into a single FPS series. Failed callbacks retain
their completion status. HUD elapsed work is separately identified as `OverlayTime`;
enabling visualization still adds real work and can affect a measured frame.

Windows observes its native paint callback and the shared software framebuffer path.
Android observes its existing native drawing callback plus the nested shared control
render. A directly borrowed `SkiaControlSurface` has a shared-render boundary; a TestHost
capture is offscreen. None of these establishes compositor completion, GPU execution or
physical display pacing. GPU-specific facts require an actual supplying backend.

## Configure the display

Replace the immutable options on the owner thread. A valid change requests one repaint
of known live roots. No timer or automatic repaint follows each metric sample.

```csharp
profiler.OverlayOptions = new PerformanceOverlayOptions
{
    Visible = true,
    Mode = PerformanceOverlayMode.Compact,
    Metrics = PerformanceOverlayMetrics.Default | PerformanceOverlayMetrics.Shaders,
    Corner = PerformanceOverlayCorner.BottomRight,
    Margin = 8, // logical client pixels
    ShowFrameGraph = true
};

// Collection continues with the display hidden.
profiler.OverlayOptions = profiler.OverlayOptions with { Visible = false };
```

Compact mode shows one row per selected group; expanded mode adds available source,
counter and interval context. Metric selection changes presentation, not collection
policy: selecting Memory does not silently enable allocation tracking. Unavailable
values are shown as `n/a`, not a measured zero. The display follows theme text/colors
and clips to the client viewport. Very small viewports can hide rows; select fewer
groups or compact mode instead of treating the HUD as normal scrollable application UI.

`ShowRepaintRegions`, `ShowControlBounds` and `ShowClipBounds` enable bounded rectangular
visual diagnostics. Repaint highlighting represents observed buffer work, with no separate
flash animation. Bounds and clipping are approximations in root logical coordinates,
not exact paths for rotated or rounded content. They do not inspect another semantic tree
or provide GPU overdraw measurement. The recorder collects these regions when requested;
enabling an option does not retroactively reconstruct older frames.

Recorded diagnostic invalidation rectangles conservatively describe the whole dirty backbuffer.
The rendering pipeline separately propagates the device-local `Invalidate(Rectangle)` damage. Window request counts
observe actual `InvalidateCore` calls; coalesced counts describe duplicate entries in a
framework invalidation batch. Clip diagnostics use the axis-aligned intersection of cached
ancestor bounds and preserve the existing presentation coordinate transformations.

No default shortcut is reserved. Use the existing command/input binding collection:

```csharp
using ModernFormsNext.WindowKit.Input;

var toggle = new DelegateCommand(() =>
    profiler.OverlayOptions = profiler.OverlayOptions with
    {
        Visible = !profiler.OverlayOptions.Visible
    });
var binding = new KeyBinding(toggle,
    new KeyGesture(Keys.F12, KeyModifiers.Control));
var bindings = root.InputBindings; // use the application's actual root or window scope
bindings.Add(binding);

// When the owning page/feature is removed, on its UI thread:
if (bindings.Contains(binding)) bindings.Remove(binding);
```

The example's gesture is an application choice; omit it or wrap registration in a
development-build condition. Existing focused-control routing, `CanExecute`, handled
keys and IME rules remain authoritative. Store the collection used at registration:
window closure may release it before a page is disposed. Remove only the owned binding,
then dispose the owned profiler. Do not clear unrelated application registrations.

## Export and application scopes

Export is explicit; there is no automatic file, network endpoint or live inspection server.

```csharp
// Call on the owner UI thread at an explicit capture boundary.
using var stream = File.Create("performance.json");
profiler.WriteJson(stream);
```

The recorder does not harvest `Control.Text`, `Name`, accessibility values, input payloads,
native handles or live control references. Explicit application-defined metric labels
are authored data: avoid putting secrets into them. Snapshot/export metadata still
describes the application's environment and implementation, so choose where to share it.

Applications can add a bounded scope to the same recorder:

```csharp
using (var work = profiler.Measure(PerformanceActivityKind.Custom))
{
    UpdateApplicationModel();
    work.Complete();
}
```

Scope disposal closes measurement on every exit; `Complete` marks the successful path.
Detailed diagnostics, custom counters and future virtualization/backend producers use
the recorder's contracts, not a parallel set of counters owned by the HUD. Actual recycled
container or GPU execution data must come from those implementations when available.

## ControlGallery and repeatable checks

Open **Performance diagnostics** in ControlGallery and choose **Start / stop**. The
session is explicitly owned by that page. It records detail/allocation/GC data for the
demonstration and stops when the page unloads. Its local Ctrl+F12 toggle applies while
focus is inside the page. **Read snapshot** proves programmatic use with the HUD hidden.
Workload buttons operate without profiling as well.

| Workload | What to compare |
| --- | --- |
| Gradients | Shared bounds-dependent brushes, stop mutation and repainted buffers; actual shader create/dispose counts. |
| Clipping / scroll | Visible pixels versus controls still visited or painted; do not equate clipping with CPU culling. |
| Animate card | Existing layout transition, real scheduler work, cache composition and return to idle. |
| Nested layout | Changed padding in nested containers, executed layout and preferred-size work. |
| Local invalidation | One small child request, repainted buffers versus reused siblings, and the actual root redraw policy. |

These five families belong to #58's validation in the existing test suites and Gallery.
The separate future PerformanceLab application (#114) is not needed to exercise them.
Record warmup, measured iterations, configuration, source, runtime, viewport and scale.
Compare disabled, recording-only, HUD and detailed modes separately. Image copying/PNG
encoding, explicit snapshots and export belong outside the measured rendering workload.
Allocation/count budgets require measured baselines and documented tolerances; timing
on shared machines needs variance reporting rather than brittle single-sample limits.
The numbers in the issue's example display are not established budgets or achieved results.

## Designer and platform boundaries

The existing Designer layout and surface renderer add optional `DesignerLayout` and
`DesignerRender` scopes to the same active recorder. They profile real safe preview work;
rendering includes the Designer's existing offscreen preview and composition costs.
Without a recorder the scopes are inert. There is no new preview pipeline, inspection
mode, property picker or Designer-owned profiler. Runtime Developer Tools (#61) can later
consume these diagnostics.

TestHost uses the real framework paint callback and deterministic input/lifecycle. It
can validate counts, raster placement, input transparency, cleanup and bounded behavior.
Its animation clock does not replace the profiler's monotonic cost clock. Offscreen
capture is not evidence of native presentation or physical GPU performance.

Physical Android reliability and display pacing remain a separate #69 matrix. General
Android windows (#72), GPU acceleration (#46), shared virtualization (#55), and the full
inspector (#61) are distinct future capabilities. Existing borrowed Android control
surfaces and Windows windows can use the current profiler without pretending those
future implementations are complete.

For coordinate units, monitor transitions and partial painting, see [High DPI](high-dpi.md).
