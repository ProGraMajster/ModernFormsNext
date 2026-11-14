# Managed title-bar icon DPI (#138)

`FormTitleBar.Image` fits the supplied bitmap into a centered **16 × 16 logical pixel**
target, preserving aspect ratio. The ordinary title-bar slot remains 34 logical pixels
wide/high, leaving nine logical pixels of margin for a square icon. An authored smaller
title bar constrains the destination to its available client area. Rendering converts the
target to device pixels once, using the current scale; decoded bitmap dimensions describe
the source, not the destination.

| Monitor scale | Square destination in device pixels |
| --- | ---: |
| 100% | 16 |
| 125% | 20 |
| 150% | 24 |
| 175% | 28 |
| 200% | 32 |

```csharp
using var icon = SKBitmap.Decode("application-icon-64.png");
using var form = new Form { Image = icon, Text = "My application" };
Application.Run(form);
```

Set images on the UI thread. The managed title bar **borrows** the bitmap: keep it alive
while assigned and dispose it after replacement or form disposal. Assignment invalidates
painting; null/ShowImage updates visibility and slot layout. Existing DPI notifications
invalidate the control buffers and relayout the slot, so runtime scale changes use the new
destination without an icon-specific cache or another DPI event. The child remains
nonselectable and transparent to pointer routing; title-bar dragging and caption buttons
keep their existing paths. Its accessible role/type/view retain the former PictureBox's
Graphic/Image/Content semantics, with Hidden view while the icon is not visible.

The private icon child now owns this destination policy. General
`PictureBoxSizeMode.CenterImage` keeps its existing source-device-pixel semantics. This
change introduces no public member, external dependency, image loader or native interop.
The existing filtered bitmap draw helper performs sampling; no image copy or new image
cache is allocated on paint.

## Source detail and platform boundary

`Form.Image` passes an already decoded `SKBitmap` to the backend and the managed title bar.
There is no ICO stream/frame collection here from which to choose another resolution.
Supply enough source detail for the largest intended destination: 32 source pixels cover
a square icon through 200%, and 64 provide additional headroom. A 16-pixel raster enlarges
correctly but cannot regain missing detail. Transparent or rectangular artwork keeps its
proportions; it is not stretched to the entire title-bar slot.

Windows native/system chrome remains the backend's separate icon path, including its
existing native handle handling. Managed bitmap ownership tests do not qualify that native
handle lifetime. Android rejects a non-null per-Form launcher icon; the shared drawing fix
does not add that platform capability. A standalone `FormTitleBar.Image` only configures
its managed image, not the platform icon. Set `UseSystemDecorations` before showing a Form;
runtime changes are not supported.

The ControlGallery TitleBar page uses a cached 32-pixel source and a ShowImage toggle.
Its description explains source detail and monitor changes. DemoApp/template/startup are
unchanged.

## Regression evidence

On master `661c7a864ea3a1597029fafa6d5984fa54f36689`, **24 of 35** new raster/contract cases
failed. The old 16-pixel source stayed 16 device pixels at higher DPI, while large sources
could fill or clip to the slot. After the fix, all 35 pass: five scales crossed with square
and rectangular sources, runtime scale changes up/down, constrained slots, null/visibility,
replacement/disposal, both themes/chrome modes, unchanged ordinary CenterImage, and retained
quadrant detail from a 64-pixel source. The first corrected-product run passed 33/35; its two
remaining fixture errors attempted an unsupported post-show decoration change. The tests
now construct separate Forms with their intended decoration mode. Earlier logs are retained.

The existing owned-HWND DPI scenario now includes 175%, return transitions and icon raster
checks after real `WM_PAINT`, for both managed and system decorations. The focused native
test passes. These injected `WM_DPICHANGED` messages exercise the production Windows path;
inspection of its managed title-bar buffer is not physical monitor movement or GPU scanout.
Headless PNGs at 100% and 200% were inspected: centered margins, scaled icon and distinct
colored quadrants. Full solution and owned Gallery qualification are recorded in the PR.

The first full Debug suite passed **6059/6059**. Subsequent source review found that replacing
the implicit PictureBox would change its default accessibility metadata. An additional test
failed with Graphic expected / Client actual on the intermediate implementation. The private
child now retains the original image metadata; the final set has 36 cases and also checks
that the icon cannot take keyboard focus and becomes hidden in the accessibility view.
The earlier green suite does not substitute for qualification after this correction.
One follow-up fixture call used TestInput.Focus on implicit Form chrome, outside that
helper's admitted public tree. The test uses the same canonical Control.Select operation
directly, then verifies that neither the child nor the window's focus owner changed to it.

## Qualification observations — 2026-11-14

The final focused set passes **36/36**, and CI run
[43585706233](https://github.com/ProGraMajster/ModernFormsNext/actions/runs/43585706233)
on `aabbc61e583c53035d6519f38d87dbd3e5dc1bff` passes **6060/6060** plus the
DynamicResource JIT/trimmed deployment checks (60 retained diagnostics).
The local full Debug build succeeds, but its suite remains **6059/6060**:
`BrushInterpolationCompatibilityTests.PreparedPlanDoesNotAllocatePerIntermediateFrame`
measured **496 B** against its existing **0–256 B** limit. Archived runs from November 4
(Release, issue-70 qualification) and November 8 (Debug, issue-99 qualification) contain
the same test failure and the same 496 B result. The test and measured BrushAnimationPlan
code have no changes since `30e2570ce0b125a5ae012faf61693d922534b9f1`; that measured path
does not render title bars. Allocation type/stack and root cause remain unknown, and an
indirect timing effect is not excluded. No retry, tolerance, warmup or skip change was used
to erase this result. The later environment pause occurred after the failed test.

The owned software-rendered Gallery window was moved programmatically between the actual
1920×1080 and 5120×2880 monitors; `GetDpiForWindow` changed **96 → 216 → 96**.
Additional injected DPI messages covered 100/125/150/175/200% and return to 100%.
The 100%, 200%, physical 216-DPI and hidden-image captures were inspected: the icon keeps
its margins/proportions and responds to ShowImage. The Search example leaves the icon slot
free. This is automated native interaction and image inspection, not a human sharpness
assessment, GPU scanout measurement, or new display configuration. The 200% synthetic
window extends below the desktop; its off-screen lower area is black in PrintWindow, so
only the visible title/header region is evidence for that capture.

Two earlier Gallery attempts are retained but excluded as icon evidence: the driver first
used virtualized monitor coordinates, and both attempts launched from the repository root.
The existing sample ImageLoader resolves `Images` relative to the working directory, so
those launches loaded no sample icons. The corrected driver uses thread-scoped per-monitor
DPI awareness (restored on exit) and launches from `samples/ControlGallery`. No ImageLoader
behavior or machine display setting was changed. Raw logs, PNGs, TRX hashes and observations
are retained under `artifacts/scope-reaudit-2026-11-12/issue-138/`.
