# Issue #120: high-DPI audit, fix and validation

## Checkpoint and scope

Audit baseline: `b2c1985723a25db20a4e4a185608395b083c312b`, master after
PR #121 merged the current #58 instrumentation checkpoint. That PR passed CI
35448715433 and was made ready before merging. #58 remains open for later phases.
Branch `codex/issue-120-high-dpi` starts at that freshly fetched master.
The audit and plan below were committed **before implementation** (`6f57b83`). Only #120 follows #58;
the earlier issue queue is suspended. No package version or release changes.

## Audited pipeline at baseline

| Boundary | Actual units and ownership | Finding |
| --- | --- | --- |
| Windows process bootstrap | Win32Platform requests PMv2 before creating its message HWND, then older fallbacks | A host can already have established awareness; no thread-awareness override should be introduced. Initial window DPI currently uses GetDpiForMonitor. |
| HWND/client/frame rectangles | Win32 physical pixels; WindowImpl divides client/frame sizes by RenderScaling | No second scaling of the native client size found. |
| WM_DPICHANGED | wParam DPI / 96 becomes RenderScaling; suggested RECT goes directly to SetWindowPos | Suggested RECT is already physical; current code correctly avoids multiplying it. ScalingChanged fires before resize, and the framework does not subscribe to it. |
| Layout | Bounds, Size, Location, Dock, Anchor, margins, padding, min/max and DisplayRectangle are logical | Default docking/flow/table are already logical. Control.ClientSize incorrectly exposes the scaled painting size to custom layout. |
| Control painting | Per-control SKBitmap uses ScaledSize in device pixels; renderers use device-space ClientRectangle/PaddedClientRectangle and fonts scaled once | Padding and borders are inconsistently converted; image/text layout combines device font/glyph dimensions with logical padding and unscaled image size. |
| Custom child layout | Consumer uses ClientSize to set child Bounds; MarkdownEditor also uses device PaddedClientRectangle; SplitContainer limit combines device extent with logical sizes | Values enter layout already scaled and are scaled again when the child is rasterized. |
| Windows backing | CPU Skia raster into raw GetClientRect-sized BGRA buffer, GDI SetDIBitsToDevice presentation | Correct physical backing dimensions; allocation occurs on size changes. Paint and presentation nevertheless redraw the full surface. |
| Dirty/cache state | Control.Invalidate(Rectangle) marks its entire bitmap; WindowBase discards the rectangle; batching tracks windows only | Full-window invalidation. NeedsPaint recursively includes invisible implicit scrollbars that are never painted/cleared, keeping ancestor buffers dirty indefinitely. |
| Clipping | WindowBase ignores Paint damage rectangle; parent compositing visits every visible child before raster clipping | Offscreen controls can allocate oversized buffers; cached siblings are composited outside useful damage. |
| Input | Win32 client pixels divide once to WindowKit logical; WindowBase bridges back to device-space control input | Existing hit tests, transformed presentation, text geometry and control events use device coordinates. Preserve this established contract rather than add a second input model. |
| Popup/screen | WindowKit PointToScreen uses logical client to physical screen; popup positioner scales anchor/size once and constrains against physical work area | Native popup transport is already coherent. Control.PointToScreen assumes the top-level origin rather than asking for the actual native client origin; test this boundary explicitly. |
| DPI transition | Scale getters change, backbuffers recreate if dimensions differ | No framework subscription to scale changes; cached text/layout/presentation must refresh even when logical dimensions remain unchanged. |

The framework's device-coordinate painting API is intentional and used by external
custom renderers. A wholesale change to ClientRectangle, paint or mouse coordinates
would introduce unnecessary compatibility risk. The fix will keep those units
explicit while correcting logical layout inputs, including ClientSize.

Relevant Windows contracts: [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged),
[GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow),
[process awareness setup](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process).

## Reproduction before implementation

An ignored audit console uses the existing TestHost and the baseline Release
assemblies. It hosts nested panels, a docked 236-unit sidebar, a 192x46 button with
48-unit left padding and a child icon at x=15 with width=24, and a setup card centered
using the content's ClientSize. No application or production source is modified.
The same source is retained for the after run.

| Scale | Setup card inside content | Text left / icon right (device px) | Mean paint (ms) | Maximum paint (ms) |
| --- | --- | --- | --- | --- |
| 100% | yes | 50 / 39 | 3.65 | 4.18 |
| 125% | yes | 50 / 48 | 6.80 | 7.52 |
| 150% | no | 50 / 58 | 10.99 | 13.08 |
| 175% | no | 50 / 68 | 14.73 | 17.03 |
| 200% | no | 50 / 78 | 18.86 | 19.76 |
| 225% | no | 50 / 87 | 24.26 | 25.07 |
| 250% | no | 50 / 97 | 30.90 | 32.90 |
| 5120x2880 physical / 225% | no | 50 / 87 | 139.49 | 151.10 |

Regular rows use 1200x700 logical viewports. The last row sets the existing headless
backend's double-precision logical client size to 5120/2.25 by 2880/2.25. The observed
frame is exactly 5120x2880, row bytes 20,480, backing bytes 58,982,400. This disproves
an oversized *native* framebuffer in this reproduction. Child backbuffers total
132,946,596 bytes and include dimensions inflated by the layout bug.

Each row warms once, routes twelve alternating hover moves through production
input and paints through the existing backend callback; the first two measured
frames are excluded from timing aggregates. Layout is stabilized before measurement,
not explicitly forced before each frame. At 5K, the final frame repaints 8 controls,
visits 31, composites 9, allocates 17,528 managed bytes, and reallocates zero control
surfaces. Input work is below 0.1 ms in that frame; layout passes are zero. Invisible
implicit scrollbars leave 7 explicit tree nodes reporting dirty after painting.
Repeated large compositing/repainting is a separate problem from layout inflation.

These are local elapsed offscreen paint measurements, not calibrated benchmarks
or display-refresh timings. TestHost creates/copies its own temporary framebuffer
outside the profiler's paint boundary. Its increasing backing generation does not
mean the Windows backend reallocates on every hover. A native persistent-framebuffer
measurement will separately cover damage/presentation. The reported physical-machine
two-second hover delay has not been reproduced on this host.

Local evidence: `artifacts/autonomous-audit/phase120-before/` (matrix, eight profiler
JSON exports and three screenshots), baseline build/restore logs, and the retained
`phase120-probe` source. Screenshot inspection confirms overlapping button text and
a setup card almost entirely outside the lower-right viewport at 225%.

## Implementation plan

1. Add explicit logical client/padded rectangles and correct Control.ClientSize to
   logical units. Preserve documented device-space ClientRectangle and painting/input
   compatibility. Correct internal child layout, scaled padding/border/image geometry,
   and screen conversion; do not alter the consumer's layout formulas.
2. Subscribe once to backend DPI transitions, refresh DPI-dependent layout/text and
   cached paint resources; prefer effective HWND DPI at creation with older fallback.
   Preserve suggested-rectangle physical handling and host-established awareness.
3. Carry bounded union damage through window batching to backend invalidation. Keep
   complete control bitmap redraw where required, but restrict ancestor composition
   and window raster/presentation to damage. Exclude nonpaintable children from dirty
   propagation; cull outside clipping before allocating child surfaces. Be conservative
   for bounds changes, transforms, overlapping/transparent controls and diagnostic HUDs.
4. Add focused regressions in existing TestHost/core/Windows projects and an existing
   Gallery scenario: seven scales, nested Dock/Anchor, auto-size, text/icon spacing,
   overlay/popup, scrolling, pointer press/drag/hover, resize/maximize and DPI changes.
   Verify partial vs full raster equality with overlapping controls and transforms.
5. Repeat identical before/after profiling, add native persistent-backing evidence and
   exact 5K-equivalent regression. Retest ModernTubeDownloader from an isolated copy
   because its available checkout has substantial pre-existing uncommitted work.
6. Document the final coordinate contract and measured limits; Debug/Release builds,
   focused and complete suites, ApiCompat, docs/package validation and diff review.
   Push dedicated PR, verify CI, ready/merge when safe, fetch master and stop.

## Implemented correction

The [coordinate contract](../high-dpi.md) documents the final API, transition,
painting and input ownership. ClientSize is logical; the explicit logical client
rectangles support layout while existing ClientRectangle/custom paint remains device
space. Shared padding/image/border calculations, MarkdownEditor and SplitContainer
are corrected. HWND DPI changes refresh preferred-size, text and bitmap caches.
Control screen conversion includes the native client origin and managed border;
popup placement retains fractional coordinates instead of truncating twice.

Damage now reaches window batching, ancestor composition, raster clipping and GDI
presentation. Hidden implicit scrollbars no longer keep buffers dirty. Moving,
hiding and transformed children erase old areas conservatively. Pixel-equivalence
regressions compare partial paints with full paints for overlapping transparency,
rotation, move and visibility changes at all seven scales.

A separate performance cause was verified in the pinned
[SkiaSharp 3.119.2 canvas source](https://github.com/mono/SkiaSharp/blob/v3.119.2/binding/SkiaSharp/SKCanvas.cs):
DrawBitmap converts a mutable bitmap to an immutable image before clipping its
destination. Merely clipping the destination still copied each full 5K ancestor.
The ordinary composition path now extracts a bounded source subset before that safe
copy. It does not alias borrowed mutable pixels into a potentially retained image.

Physical screenshot inspection also caught an error in the first partial GDI
implementation: top-down bitmap storage does not make the StretchDIBits source
rectangle's Y origin top-down. The corrected transfer is covered by native memory-DC
pixel tests in both halves, at nonzero offsets and outside clipped boundaries.
Screenshots and framebuffer pixels now agree for the sampled button region.

## Executed matrix and physical monitor acceptance

The connected monitors were measured, not inferred: primary bounds 0,0–1920,1080
with GetDpiForWindow=96; second bounds 1920,0–7040,2880 with GetDpiForWindow=216.
The physical fixture moves its owned HWND and receives actual OS WM_DPICHANGED
notifications. It does not change display settings or inject DPI messages.

Seven-scale deterministic tests cover 100%, 125%, 150%, 175%, 200%, 225% and 250%:
logical client/layout sizes, Dock, right/bottom Anchor, AutoSize/FlowLayout, image and
text spacing, wheel scrolling, press/capture/drag release, popup placement, resize,
scale changes and return to 100%, framebuffer size, and partial/full pixel equality.
A native owned-HWND matrix covers all seven scales plus exact 5120x2880 backing,
maximization/restoration and local hover damage. Its injected DPI values are
**synthetic**, separately labelled from physical monitor acceptance. A dedicated
suggested-rectangle case changes origin and size without pre-sizing the HWND.

Physical before/after uses the same fixture against baseline b2c1985 and the fix.
Each case warms with a full frame, routes sixteen alternating pointer moves through
native input and paints through WM_PAINT. The first two frames are excluded. These
are local elapsed paint/presentation measurements, not DWM display latency or FPS.

| Actual display / window | Baseline layout | Fixed layout | Before mean / max ms | After mean / max ms | Before / after dirty pixels |
| --- | --- | --- | --- | --- | --- |
| primary-100-normal | inside | inside | 3.84 / 5.11 | 0.32 / 0.41 | 840,000 / 9,312 |
| second-225-normal | outside | inside | 22.94 / 24.04 | 1.14 / 1.92 | 4,252,500 / 46,004 |
| second-225-maximized | outside | inside | 71.96 / 73.92 | 1.18 / 1.29 | 14,745,600 / 46,004 |
| second-225-full-5k | outside | inside | 71.40 / 73.68 | 1.04 / 1.13 | 14,745,600 / 46,004 |
| return-primary-100 | inside | inside | 3.87 / 4.78 | 0.13 / 0.17 | 840,000 / 9,312 |

At physical 5K/225%, backing remains 5120x2880, stride 20,480 and 58,982,400 bytes.
Hover causes zero control-surface allocations and zero layout passes; native backing
generation remains stable. Every physical case verifies hover state, one click per
press/release, and displayed button pixels against the framebuffer. Actual screenshots
were inspected for the normal/maximized second-monitor and return paths.

The identical forced-full offscreen snapshot benchmark improved from 139.49 ms to
60.59 ms at 5K. It intentionally paints a full temporary TestHost surface and is not
comparable to native partial hover. The reported two-second delay was not reproduced
on this host; the measured full-window work and layout inflation were reproduced
and removed. No constant 60 FPS claim is made. Recording counts explicit framework
surface allocations; transient Skia copies are not counted as such.

Local raw evidence: `artifacts/autonomous-audit/phase120-{before,after}/`,
`phase120-native-{before,after}.log`, and `phase120-physical-{before,after}/` contain
matrix results, profiler JSON, framebuffer captures and actual display screenshots.
The tracked UiAutomationHost fixture and tests reproduce the acceptance paths.

## Consumer retest

ModernTubeDownloader was available with pre-existing local changes. A byte-preserved
isolated source copy was built against this framework, with a separate data root
and an offline HTTP fixture. No application DPI changes or workarounds were made.
Actual MainForm sidebar, first-run setup card, downloads content, resize/maximize,
hover/press and return to the primary monitor were exercised. The fixture exposes
the existing downloads view after the setup captures; it does not claim successful
tool provisioning. Seven physical window cases passed, with mean hover frame times
0.18–0.99 ms and maxima below 2.1 ms. Screen/framebuffer button pixels matched.
Maximized first-run and downloads screenshots were visually inspected. Source hashes
are retained in `phase120-consumer/source-hashes.json`; source checkout is preserved.

**NOT EXECUTED:** live yt-dlp/FFmpeg/Deno provisioning/downloads, manual hardware
pointer latency measurement, and physical DPI settings other than these two displays.
Those are distinct from the automated native-input and seven-scale regression tests.

## Validation status

Initial full Release restore/build passed (four pre-existing NU1902 SourceLink
transitive dependency warnings, no errors); all 3381 tests in nine projects passed.
The final committed-source Debug/Release, ApiCompat, docs/packages, Gallery, CI and
merge results are recorded in the final validation section after execution.
