# Issue #58 — runtime performance diagnostics plan

Audit date: 2026-09-12. Baseline: merged `master`
`af6422a108de78dd68ea6aca2f7d92c44550d4d4`. Branch:
`codex/issue-58-performance-diagnostics`. This plan precedes implementation.

## Verified starting point

The complete live issue, its zero comments, dependency endpoint and related history
were refreshed after merging #59. No formal blocked-by dependency is recorded.
Related #46, #55, #61, #64, #69, #72 and the newly referenced #114 were read;
their proposal text is distinguished from current source. Older #64 comments about
missing deterministic time/input/rendering are superseded by the current TestHost.

The previous change is merged through [PR #119](https://github.com/ProGraMajster/ModernFormsNext/pull/119)
at this baseline. Its final documentation head was `75adf2f012197ae95d2ab21fd31f200cf2b9d1bd`;
[PR CI](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34708205488)
and [merged-master CI](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/34708696923)
passed. Local master and origin/master were equal before this branch was created.
An independent detached baseline restore/Release build passed with the same four
existing NU1902 build-dependency warnings. No dependency or version change is planned.

Current implementation and relevant source:

- `AnimationSchedulerDiagnostics`, Android animation diagnostics, font-resolution
  counters and TestHost diagnostics exist. They have different meanings and are not
  a common frame profiler. Reuse their production work boundaries and preserve APIs.
- `WindowBase.DoPaint` renders through a locked software framebuffer. The Windows
  WM_PAINT callback includes framebuffer disposal/GDI transfer, but cannot establish
  compositor completion. EndPaint currently lacks a finally around the paint callback.
- `SkiaControlSurface.Render` paints a borrowed real control tree. Android's pinned
  SKCanvasView 3.119.2 copies its bitmap after OnPaintSurface returns; a narrow wrapper
  around base.OnDraw can observe that complete host callback without copying it.
- `Control.PerformLayout` has an executed-layout boundary after suspended requests;
  `GetPreferredSize` has query/cache/core boundaries. There is no distinct universal
  measure/arrange pipeline, so no fabricated arrange duration will be exposed.
- Control and ControlAdapter paint loops repaint dirty backbuffers and composite
  cached ones. A canvas clip is not evidence that CPU rendering was culled.
- Core invalidation and the current native paint paths can repaint the whole client
  despite a smaller requested rectangle. Requested regions and actual redraw policy
  must be separate from cache reuse and controls visited/repainted.
- Bounds-dependent gradient shaders are owned at existing draw scopes. Transforming
  a shader creates another wrapper and disposes the original; glass and HueSlider
  paths also create shaders. REN-02 currently has no unified budget or profiler.
- There is no shared public adornment layer. A HUD can draw after root content into
  the existing canvas, without adding hit-testable controls or affecting app layout.
- TestHost already provides actual layout/input/rendering, controlled animation time,
  scoped services and cleanup. Designer already renders safe runtime previews; its
  existing renderer can be an optional consumer of the same profiling scopes.

## Ownership, data model and compatibility

Add an opt-in `PerformanceProfiler.Start(options)` entry point with exactly one
active profiler on its owning UI thread. The returned profiler is disposable.
Start, configuration, capture, scopes and disposal enforce thread affinity; detached
immutable snapshots can be read or exported elsewhere. Start on an initialized UI
dispatcher verifies access. No timer, dispatcher loop or new application model is added.

The disabled hot path checks a nullable thread-local recorder before reading time,
GC state, control IDs, metadata or ancestors. Do not add eager per-control profiler
fields, registrations, string formatting, source maps or snapshots while disabled.
Enabled recording uses preallocated bounded value storage. Options bound frame,
slow-frame, detail, source, region and extension-counter capacity; overflow is reported.
Snapshot/export allocation is explicit and separate from normal recording.

One internal WindowKit transport connects native frame sources to the Core recorder.
Existing InternalsVisibleTo relationships cover Core, backends and Testing. This
transport is a nullable thread-local sink with a revocable registration and concrete
struct scopes, not another collector or a public arbitrary native callback API.
It carries primitive renderer metadata and short-lived opaque source identity only.
Core maps weak identities to numeric IDs. Snapshots never retain Controls, windows,
SKCanvas, native handles, Activity, user text or control names. Optional control detail
uses bounded numeric identity/type metadata, not entered text or automation payloads.

The native outer frame and nested Core render share one recorder token. A directly
borrowed surface with no native frame gets an explicitly shared-only frame. Scope
nesting is bounded; copied, stale, out-of-order and revoked scopes cannot double-end
or contaminate a replacement session. Native cleanup runs even on render failure.
Recorder errors must not escape an OS callback or mask the original paint failure.
Profiler disposal revokes recording before releasing its bounded buffers and visual
resources; pending scopes cannot revive it. No automatic snapshot events call user
code inside rendering. Explicit capture/export occurs outside the hot path.

All public changes are additive, documented C# APIs in the shared framework. Preserve
existing WindowKit interfaces, render/layout signatures, scheduler APIs and packages.
Keep native code in the respective backend. No new package/dependency is required.

## Metric meanings

| Metric | Definition and qualification |
| --- | --- |
| Frame duration | Monotonic elapsed time around the actual host callback, including preemption/waits; shared-only fallback is labelled. This is not CPU execution time, GPU time or presentation latency. |
| Frame interval / observed FPS | Time between comparable frame starts for the same source/generation; the first interval is unavailable. Demand-driven idle time is included, not counted as dropped refresh frames. |
| Render | Existing shared content-paint scope within a frame. Native submission and optional HUD overhead are identified separately. Nested inclusive scopes are not summed as total frame duration. |
| Layout / preferred-size work | Executed layout passes and measured preferred-size queries/core calls/cache hits. Work between frames remains interval work, with unattributed UI-thread work distinguished from source-specific work. |
| Input / animation | Existing outer input routes and shared scheduler processing. Scheduler work spanning several windows is recorded once as thread work, not charged fully to every window. |
| Control work | Visits, dirty repaints, composites, cache reuse and actual skipped cases, with explicit counting boundaries. Tree totals/depth require opt-in bounded detail. |
| Invalidation | Accepted control requests, window requests/coalescing and bounded requested rectangles. Actual redraw policy remains separate; no claim to implement partial native redraw. |
| Memory | Optional UI-thread managed allocation deltas and process-wide GC deltas; explicit heap/working-set snapshots outside frames. Native Skia/Java/GPU memory is not included in managed bytes. |
| Renderer / surface | Verified backend/raster mode, scale, device/logical extent, format/stride/backing bytes when available, and genuine host/backing generation events. Unknown/custom hosts stay unknown. |
| Shader lifetime | Framework-owned create/dispose counts including transformed replacements, correlated with active frame/control scope; these are object counts, not total native-memory measurement. |
| Slow frame | Configurable measured-duration threshold with bounded context/history and availability labels. An idle start-to-start gap alone is not a slow render. |

Expose cumulative/interval snapshots and bounded frame history with useful rolling
statistics. Preserve frame/source identity when computing FPS or percentiles. Report
unavailable GPU/presentation/reset/virtualization metrics as unavailable, not zero.
Future subsystems can register bounded typed numeric counters and measured scopes;
provide documented virtualization reporting without implementing a fake recycler.
Default JSON export contains numeric metrics, definitions/capabilities, framework and
runtime environment metadata. Application-defined labels are explicitly authored,
length-bounded registration data; no UI content is harvested automatically.

## Implementation slices

1. **Recorder and contracts:** options, immutable metrics/history, ownership, clocks,
   allocation/GC opt-in, bounded scopes/counters, slow records and JSON export. Use
   Stopwatch for production cost; an internal controlled timestamp source supports
   deterministic tests without replacing the production renderer or scheduler.
2. **Native boundaries:** internal WindowKit transport, Windows WM_PAINT/finally and
   framebuffer metadata/lifetime, Android base.OnDraw/shared callback/recreation,
   and headless capture. Observe existing rendering; do not add another render loop.
3. **Core instrumentation:** layout/preferred size, paint/composition, invalidation,
   input and scheduler boundaries. Cover shader factories and actual owning disposal
   sites, including transformed/glass/HueSlider paths and exceptional unwinding.
4. **Optional HUD:** compact/expanded view, selected metrics, configurable corner,
   bounded frame graph, dirty/requested-region and repaint/bounds diagnostics where
   truthful. Draw after content with saved/restored canvas state. It is input
   transparent and creates no per-frame layout or idle repaint loop. Toggle through
   existing scoped input/command bindings; development shortcut is explicit opt-in.
   Account for HUD work separately and prevent its paints from recursively profiling
   themselves. Own and dispose any Skia resources with the session/overlay lifetime.
5. **Integration and stress:** a focused ControlGallery page and existing test suites
   exercise gradients, clipping, animations, nested layout and localized invalidation.
   Verify useful programmatic operation with the HUD off. Add a bounded optional
   Designer render/layout scope consumer if it fits existing safe previews; no picker,
   property inspector or arbitrary application-code execution is added here.
6. **Documentation and acceptance:** public XML/examples, profiling/troubleshooting
   guide, evidence/budgets, native differences and known-limitations updates. Update
   this session's durable report with each acceptance direction and remaining boundary.

## Validation and budgets

- Deterministic recorder tests cover time arithmetic, nested categories, two sources,
  idle gaps, thresholds, buffer overflow, explicit memory availability, counter
  registration, snapshot immutability/redaction, wrong-thread use and stale/disposed
  scopes. Native and managed failures retain original cleanup/exception semantics.
- Production TestHost tests cover real frame/layout/paint/invalidation/caching/input/
  scheduler attribution, disabled and enabled paths, observer lifetime, overlay input
  transparency and no added timer/dispatcher/render demand. Use real render pixels
  where visual behavior matters, not a duplicate test rendering implementation.
- Establish warmed Release baseline allocations and timing for the five requested
  workload families. Record configuration, source, runtime, iterations and variance.
  Define checked allocation/count budgets from those measurements before claiming
  regression thresholds. Require zero incremental allocations in the disabled
  recording hooks; compare real disabled workload costs against the exact baseline.
  Timing targets are observations with noise, not brittle shared-CI millisecond gates.
- Exercise native Windows frames, scale metadata, optional overlay/toggle/input and
  close; inspect actual captures. Android emulator checks cover actual frame metadata,
  pause/resume/recreation and disposal. Physical-device/perceived presentation checks
  remain NOT EXECUTED where the environment cannot supply them.
- Restore, serial Debug/Release full solution builds and all existing test projects;
  use `-m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true`. Full baseline
  was 3273 tests per configuration before this issue; final counts come from final TRX.
- Compare all public assembly/TFM APIs against exact baseline af6422a. Begin with
  the normal unfiltered rules; do not inherit issue #59's compiler-attribute exclusions.
  Investigate any discrepancy instead of suppressing an authored contract change.
- Validate unchanged-version packages, a fresh isolated package consumer, documentation
  scripts, DocFX and four archives; inspect final diff and required GitHub CI. Do not
  publish packages, tags, releases or extension artifacts. Preserve `.codex/config.toml`.

## Boundaries and completion policy

All seven live #58 acceptance directions and its four shader/allocation additions
have implementable scope now. GPU execution/presentation/context resets await #46;
real recycled-item producers await #55. General Android windows/popups await #72,
physical reliability matrix #69, and the full inspector/picker UI #61. None blocks
the current recorder, HUD, shader counters or representative stress validation.

The dedicated benchmark/stress application in #114 is separate and outside the
requested queue. Keep #58's required repeatable workloads in existing test/sample
projects; do not use #114 to defer those checks or create that application implicitly.

Push completed work to the dedicated PR, verify CI and final acceptance, mark Ready
and merge with the repository's normal merge-commit strategy once safe. Parent issue
OPEN/PARTIAL is not a reason to leave validated scope Draft. Fetch/pull the merged
master before auditing #61. Implementation findings may refine this plan through
documented compatible corrections; breaking API/package decisions require the owner.
