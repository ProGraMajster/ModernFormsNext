# Issue #46 Phase 0 — software rendering foundation

Initial evidence: 2026-10-05. Final source review: 2026-10-06.
Branch: `codex/issue-46-rendering-foundation`.
Exact base/master: `f521f9dfcfe601bf9b6199b88132cccb2380d1bf`.
Tracking: [child #170](https://github.com/ProGraMajster/ModernFormsNext/issues/170),
[parent #46](https://github.com/ProGraMajster/ModernFormsNext/issues/46).

The original implementation was validated locally before commit/publication. The final-review
section below records the corrections and renewed qualification prepared for the Phase 0 PR.
No merge, release, version bump or issue closure is part of this task. The original checkout and
its preexisting untracked configuration were preserved.
This report addresses the requested audit/report items 1–76; section ranges identify that mapping.

## A. Audit before implementation (1–6)

The base was fetched and verified against origin/master before creating separate feature and baseline
worktrees. Baseline production code remained at the exact SHA. Only the same additional test/measurement
fixture was placed in its existing Testing.Tests project; no production baseline file was edited.

| Path | Audited behavior before extraction |
| --- | --- |
| WindowBase.DoPaint | Finds the first framebuffer surface; locks; maps actual format/size to SKImageInfo; creates pointer-backed SKSurface; starts profiling; applies outward-rounded damage; paints chrome/content/HUD; disposes profiler, Skia and framebuffer in that order. |
| ControlAdapter / PaintEventArgs | Existing device-space painting and public SKCanvas/SKImageInfo/scaling contract. Cached control buffers and clipping belong to controls. |
| WindowBase invalidation | Coalesces pending logical regions, clips to current client bounds and sends native invalidation. ScaledClientSize truncates positive logical dimensions; damage edges use floor/ceil. |
| ITopLevelImpl.Surfaces | Existing heterogeneous presentation-capability enumeration. Framebuffer surface and lock are platform-owned. |
| Windows WindowImpl / FramebufferManager | WM_PAINT clips a BeginPaint DC; Lock owns the existing RAM allocation and resize; unlock transfers damaged top-down rows with StretchDIBits or the existing full SetDIBitsToDevice path. No alternate DIB. |
| AndroidWindowImpl / AndroidActivityHost | Native presentation publishes its bitmap surface, resizes before shared painting and draws the bitmap through AndroidSkiaHostView/SKCanvasView afterward. PresentationEpoch/Current gate retired Activity/window callbacks. |
| HeadlessWindowImpl | Capture exposes a temporary SnapshotFramebuffer, invokes the production WindowBase paint callback, removes the surface in finally and returns detached pixel bytes. Popups use the same backing implementation. |
| Diagnostics | PlatformRenderInfo supplies native host/generation/submission facts. PerformanceProfiler merges the shared render scope into its native frame and owns the optional HUD. |
| Designer / standalone offscreen | RuntimeControlPainter and export use SKBitmap/SKCanvas; SkiaControlSurface borrows a canvas. Neither requires a presented window or a GPU. |

Production pointer-backed SKSurface.Create was concentrated in WindowBase.DoPaint. Other searched
SKSurface.Create calls were test fixtures. Separate SKBitmap/SKCanvas paths were found in Control
back buffers, Control.RenderingDamage, PictureBox, PrintDocument, Designer RuntimeControlPainter,
DesignerCommandService export, SnapshotFramebuffer and RenderedSnapshot PNG encoding. Those have
explicit bitmap ownership and were not converted into synthetic window surfaces.

The only shared window framebuffer lock moved from WindowBase to SoftwareRenderingBackend. Windows
FramebufferManager and AndroidActivityHost.Framebuffer remain the presentation owners. Pixel-format
mapping remains the existing ToSkColorType mapping; RGB565 is opaque and RGBA/BGRA remain premultiplied.

## B–D. Architecture, public API and resolver (7–23)

See [rendering-backends.md](../architecture/rendering-backends.md) for the diagram, complete contracts,
startup examples and lifetime rules.

The public additions are RenderingBackend (Auto, Software only), sealed RenderingOptions with Backend,
Application.ConfigureRendering, Application.RequestedRenderingBackend and nullable
Application.ActiveRenderingBackend. PerformanceRenderInfo adds RequestedBackend, ActiveBackend and
FallbackReason. Existing PaintEventArgs, Control, Form and WindowKit public members remain compatible.

The internal boundary consists of IRenderingBackend, IWindowRenderSurface and IRenderFrame, implemented
by SoftwareRenderingBackend with private window-surface/frame classes. A backend is application-lived;
an adapter is window-lived; framebuffer/SKSurface ownership is frame-scoped. Native resources are not
owned by the abstract backend or the control tree.

Configuration copies the option. It is accepted before renderer/platform initialization and rejected
after either initialized state; mutation of the original option does not change any window. Form
resolves before native creation, WindowBase covers internal factories/popups, and Run covers custom
lifetime roots. Tests borrow/reset/restore the same state through Application.Testing. Undefined
values throw ArgumentOutOfRangeException, null options throw ArgumentNullException and late configuration
throws InvalidOperationException. Closing windows does not reset selection. The final-review guard
also checks the existing IWindowingPlatform service, covering direct public backend Initialize calls
that install platform services without populating WindowKitBackendRegistry.Current.

Auto -> Software and Software -> Software. Requested and active remain distinct. No fallback happens;
FallbackReason stays null. Active is null before resolution, then Software, never Auto. Existing
software acceleration diagnostics provide the honest capability fact; a separate public capability
framework was unnecessary. The internal resolver is the future capability-chain insertion point.

## E–G. Platform paths and shared paint (24–38)

Windows uses the same framebuffer manager, allocation, stride, clipping, DPI and GDI calls. No HWND
creation, native message handling or platform lifecycle source was modified. The existing real HWND
performance scenario now also verifies that native metadata updates preserve requested/active identity.

Android still presents through Activity-owned SKCanvasView. No Android windowing backend source changed.
The new adapter reacquires current Surfaces every frame, so activity/popup replacement cannot leave it
holding the old presentation. Insets/density, Choreographer, demand-driven invalidation, modal ownership,
input/IME and accessibility remain canonical. The native instrumentation adds two renderer-policy
assertions but does not change input timing, retries or timeout values.

WindowBase now acquires a frame, paints into its canvas and completes it. The paint ordering and
save/restore/content-clip/HUD boundaries are preserved. Floor/ceil device damage is tested directly at
1, 1.25, 1.5 and 2 scale. Existing native tests exercise partial redraw, multiple requests, resize,
popup painting and injected WM_DPICHANGED up to 2.5x, including a 5120x2880 backing.

Complete seals drawing; frame Dispose retires Skia and releases the lock after profiler scopes.
Acquisition, metadata, SKSurface creation, paint and unlock failure tests prove cleanup and reuse.
Nested paints get distinct resource holders and permanently revocable leases. Only the holders are
pooled; a disposed lease cannot access or release a later frame. Disposing an adapter blocks new
acquisition while an active frame unwinds independently. Cross-thread acquisition, live frame reads,
completion and disposal are rejected. The software
unlock policy after failure is deliberately unchanged: no abort or rollback capability is invented.

## H–I. Headless, Designer and diagnostics (39–48)

Headless Form/popup capture uses the new shared Software adapter. Designer, control caches, printing,
bitmap encoding and borrowed SkiaControlSurface remain the established software/borrowed-canvas paths.
No second control tree or rendering API was introduced.

PerformanceProfiler stays the sole recorder. Shared frames provide Skia Raster, Software, actual backing
dimensions/stride/bytes, logical size, scale, damage and selection. Native PlatformRenderInfo still
supplies host identity and generation/presentation facts. Merge preserves renderer identity when native
timing arrives later. No new snapshot is created in the profiling-disabled path. GPU duration,
presentation timestamp and context-reset values are not fabricated.

## J. Initial visual and performance comparison (49–52, 54)

These measurements describe the 2026-10-05 implementation before the final-review lifetime corrections.
The renewed comparison below supersedes them for the published source; the initial evidence is retained.

Environment: Windows 10.0.26300, .NET SDK 10.0.401, identical machine/font environment and source fixture.
Segoe UI font SHA-256:
`8134DBCD09E7B123C9A7F229D49CFFBCB01352CC72EA5E1076B65D0DCA9F73CD`.

The deterministic set covers Button, TextBox, Label, ellipse geometry with gradient/alpha, scroll clipping,
Form chrome, popup, resize, 1/1.25/1.5/2 scaling and a HUD with nondeterministic metrics disabled.
There are 21 BGRA comparisons (window, resized window and popup for seven scale/HUD combinations).
Exact raw-byte equality is required, with no tolerance increase.

An initial cold popup at 150% differed. Repeating the unchanged baseline reproduced the same difference.
The fixture was therefore updated identically on both checkouts to warm the popup and assert repeatability
before comparison. Original failure logs are retained. This demonstrates parity of the stabilized set;
it is not a portable font golden or a claim about arbitrary cold-start font-cache output.

| Measurement | Exact-base software | Refactored software |
| --- | ---: | ---: |
| Acquisition allocations / frame | 2688 B | 2640 B |
| Acquisition CPU / frame | 1.347 microseconds | 1.305 microseconds |
| Content paint CPU, mean of 30 profiled frames | 0.330 ms | 0.336 ms |
| Whole headless capture CPU, median of five batches | 0.705 ms | 0.692 ms |
| Whole headless capture allocations, median / capture | 1,095,113 B | 1,095,016 B |
| Resize + capture CPU, median | 1.517 ms | 1.511 ms |
| Pixel backing for the fixed viewport | 1,080,000 B | 1,080,000 B |
| Exact stabilized pixel comparisons | reference | 21/21 identical |

These comparison runs used DOTNET_TieredCompilation=0 for both test processes to keep JIT tier
transitions out of the short allocation comparison. This is a measurement environment setting only;
production defaults, repository configuration and the full test suites were unchanged. Content paint
was about 2% slower in this small sample while capture/acquisition were slightly faster: no material
regression or general speedup is established. Steady acquisition saves 48 managed bytes per frame.

The original default-JIT measurements are retained too: median capture 0.940/0.847 ms and
1,116,727/1,118,455 B for baseline/refactor. Within the refactored default-JIT run, allocations changed
from 1,118,566 to 1,096,974 B as execution warmed. That unstable measurement prompted the controlled
comparison, not a performance-threshold change. Both default-JIT and fixed-JIT runs retained 21/21
exact pixel parity. See comparison-summary.json, refactored-final and refactored-fixed-jit evidence.

The acquisition microbenchmark uses the old framebuffer/Skia sequence and the new adapter in the same
process, warmed for 256 iterations and measured over 2000 iterations. It includes surface creation,
canvas access and release, not application painting. The regression requires new allocations to be
no greater than the old path; existing performance thresholds were not changed.

The representative capture fixture uses the same 480x360 logical viewport at 1.25x, warm-up, five batches
of 100 captures and 20 alternating resizes. It resets/warm-renders the viewport between batches.
It includes TestHost layout, backing allocation and detached pixel copy, so it is not a native-display
benchmark. Separate profiler samples measure content paint CPU wall time, not GPU duration. Single-machine
timings are noisy and establish no general speedup. Backing storage remains 600x450x4 = 1,080,000 bytes.

## K. Initial validation on 2026-10-05 (53–61)

| Check | Result | Evidence |
| --- | --- | --- |
| Solution restore | PASS | restore.log |
| Full Debug solution build, including Designer/VSIX | PASS, 0 warnings/errors | build-Debug-reviewed.log |
| Full Release solution build, including Android AOT/Designer/VSIX | PASS, 0 warnings/errors | build-Release-reviewed.log |
| Full Debug tests | 4126 passed, 0 failed, 0 skipped | tests-Debug-reviewed/*.trx |
| Full Release tests | 4126 passed, 0 failed, 0 skipped | tests-Release-reviewed/*.trx |
| Backend + direct WindowBase damage tests | 17/17 | contracts-final.trx |
| Configuration/reset/diagnostic tests | 4/4 in each full suite | RenderingConfigurationTests |
| Exact-base parity + representative measurement fixture | 8/8; 21 raw images identical | baseline-final.trx, parity-final.trx, pixel-comparison.json |
| Designer | 691/691 in each full suite | Designer.Tests TRX |
| TestHost/headless | 939/939 in each full suite | Testing.Tests TRX |
| Windows backend/native host | 328/328 in each full suite | Windows backend TRX; real HWND/DPI/popup scenarios |
| Android backend contracts | 382/382 in each full suite | Android backend TRX |
| Android Debug signed APK | PASS, 0 warnings/errors | apk-Debug.log |
| Android Release signed APK/AOT | PASS, 0 warnings/errors | apk-Release.log |
| Android Release AAB publish/AOT/trimming | PASS | aab-Release.log; signed AAB |
| Emulator exact base | 61 native assertions passed | android-baseline-windowing.log |
| Emulator refactored Debug comparison | 63 native assertions passed; initial failure retained | android-Debug-comparison-windowing.log; android-Debug-windowing.log |
| Emulator refactored Release/AOT | 63 native assertions passed | android-Release-windowing.log |
| ControlGallery Auto/Software and template startup/close | PASS | windows-sample-smoke.json |
| git diff --check | PASS | final local diff check |

Full totals comprise Automation 166, Windows Automation 66, CrossPlatform sample 29, Designer 691,
Testing 939, core 1499, VSIX 26, Android backend 382 and Windows backend 328 tests per configuration.
No skip, timeout, retry policy or existing allocation threshold was changed. New source only adds
software contracts and tests; no production fake hardware backend is involved.

Windows native tests run on owned real HWNDs. DPI scenarios inject WM_DPICHANGED; they do not imply a
physical monitor changed DPI. Additional ControlGallery Auto/Software and default-template startup/
responsive-window/clean-close smokes passed. No full manual Gallery/Visual Studio F5 walkthrough was done.

Android runtime: AVD Codex_Issue62_API36, emulator-5554, sdk_gphone64_x86_64, Android 16/API 36,
emulator 36.6.11, Windows Hypervisor Platform. No physical Android device was attached:
**NOT EXECUTED — environment unavailable**. API 23 is manifest-verified, not device-tested.

The first refactored Debug instrumentation run failed at native-Back-dismisses-popup after 18 checks.
The exact-base APK subsequently passed 61 checks. A controlled reinstall/force-stop comparison of the
refactored Debug APK passed 63; refactored Release/AOT also passed 63. The extra checks verify Auto ->
Software and rejected late configuration. All original assertions remain. The cause of the initial
Back failure was not established; it remains a native IME/Back timing risk, not silently reclassified
as a known baseline defect. No renderer or windowing change, timeout adjustment or retry-policy change
was used to make later runs pass.

Successful native runs cover initial painting, confirmed density, popup/modal, repeated Show, reentrant
hide/show, background/resume, five Activity replacements, retired View/Activity collection, canonical
IME session replacement, close cancellation, final shutdown and no stale presentation reuse.

The first full Debug and Release suites each failed only the sample's old exact backend-label assertion.
It now asserts the complete new requested/active label under a scoped test runtime, including honest
uninitialized state. Both complete suites were rerun. No allocation failure or threshold weakening was
needed.

## L. Issue organization and future work (62–68)

Child #170, “Extract rendering backend abstraction with software renderer”, was created after searching
for existing rendering-backend/software-extraction issues. GitHub's actual parent/sub-issue relation
links it to #46. The [authoritative roadmap comment](https://github.com/ProGraMajster/ModernFormsNext/issues/46#issuecomment-6000074430)
lists Phases 0–7. Both issues remain open; GPU phases are unchecked.

The next separate task should choose and validate a bounded Windows GPU PoC, including acquire/present,
resize and lost/recreated resources. Vulkan versus ANGLE/Direct3D versus OpenGL is intentionally not
decided by this phase. No fake capability probing biases that decision.

The contracts contain no Ganesh/Graphite or native GPU types. Moving between Skia generations should
remain internal if the needed SKCanvas painting API remains available; Graphite is not validated.
#60 native composition and future Media ownership remain at the presentation boundary. Resource
sharing, synchronization and composition ordering still need their own real contracts and tests.

## M. File/API/package inventory (69–72)

The feature worktree contains 29 changed/new source, test, sample or documentation paths:

```text
docs/architecture/README.md
docs/architecture/rendering-backends.md
docs/development/issue-46-rendering-foundation-report.md
docs/development/README.md
ModernFormsNext.CrossPlatform.Sample.Tests/SampleApplicationTests.cs
ModernFormsNext.Testing.Tests/RenderingConfigurationTests.cs
ModernFormsNext.Testing.Tests/RenderingParityTests.cs
ModernFormsNext.Tests/RenderingBackendTests.cs
ModernFormsNext.Tests/WindowRenderingDamageTests.cs
ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/PerformanceScenario.cs
ModernFormsNext/Application.cs
ModernFormsNext/Application.Rendering.cs
ModernFormsNext/Application.Testing.cs
ModernFormsNext/Diagnostics/PerformanceProfiler.Recording.cs
ModernFormsNext/Diagnostics/PerformanceRenderInfo.cs
ModernFormsNext/Form.cs
ModernFormsNext/Rendering/IRenderFrame.cs
ModernFormsNext/Rendering/IRenderingBackend.cs
ModernFormsNext/Rendering/IWindowRenderSurface.cs
ModernFormsNext/Rendering/SoftwareRenderingBackend.cs
ModernFormsNext/RenderingBackend.cs
ModernFormsNext/RenderingOptions.cs
ModernFormsNext/WindowBase.cs
ModernFormsNext/WindowBase.InputBindings.cs
ModernFormsNext/WindowBase.Lifecycle.cs
ModernFormsNext/WindowBase.Performance.cs
samples/ControlGallery/Program.cs
samples/ModernFormsNext.CrossPlatform.Sample/MainPage.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/WindowingValidationInstrumentation.cs
```

No project/package reference, package ID, version, native SDK requirement, target framework, Android
minimum or solution structure changed. The framework NuGet package contains the same 11 paths as the exact-base package and no native
runtime/GPU payload. NuGet manifest differences are limited to the automatically generated Git branch attribute; dependencies and package identity are unchanged. Windows core output has the same 16 file paths. The exact-base and refactored Debug APKs expose identical sets of 438 lib entries.
Release APK uses the existing arm64-v8a/x86_64 runtime/AOT/Skia/HarfBuzz assets; the manifest remains
minSdk 23 / targetSdk 36. Release APK and AAB compilation exercise existing trimming/AOT, not a new
Windows NativeAOT guarantee. Native and NuGet inventories are preserved with the local evidence.

## N. Readiness, limitations and risks (73–76)

The software-only foundation is prepared for a focused Phase 0 PR. The final-review section below
supersedes the initial test and performance totals for the reviewed source. The initial
Android Back failure and unavailable physical/API-23 validation must remain visible in any PR description;
this is not an unconditional production Android qualification. No further architecture rewrite or GPU
implementation is required for this phase.

There are no intentional breaking public API changes. The highest review-sensitive details are pooled
resource holders with separate revoked leases, disposal/presentation after exceptions, detached
renderer/native metadata merging and startup-time freeze semantics. Internal contracts can evolve for
a real GPU implementation; external renderer plugin registration is not promised by this phase.

The visible diagnostics label now includes rendering selection. Other control painting and platform
presentation implementation is unchanged. Remaining qualification includes physical Android hardware,
API 23 runtime, broader IME/OEM behavior, manual VS Designer/F5 and the initial unexplained native Back
failure. GPU acceleration, driver fallback and GPU/media sharing remain out of scope.

## Reproduction and local evidence

All evidence is under `artifacts/rendering-foundation/` in the feature worktree, with baseline captures
and its APK build in the separate baseline worktree. Logs/TRX/PNG/BGRA/packages are ignored local artifacts,
not proposed source commits. Both worktrees use the same production base SHA. The feature commit adds
only the 29 intended source/test/sample/documentation paths listed above.

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet build .\ModernFormsNext.slnx -c Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet build .\ModernFormsNext.slnx -c Release --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet test .\ModernFormsNext.slnx -c Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false --logger trx
dotnet test .\ModernFormsNext.slnx -c Release --no-restore --no-build -m:1 /p:UseSharedCompilation=false --logger trx
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Debug --no-restore -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=apk /p:EmbedAssembliesIntoApk=true
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Release --no-restore -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=apk /p:EmbedAssembliesIntoApk=true
dotnet publish samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Release --no-restore -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=aab
git diff --check
```

For parity, compile/run the identical RenderingParityTests fixture on the exact base first with
MFN_RENDER_OUTPUT pointing to a new evidence directory. In a separate process against the refactor,
set MFN_RENDER_BASELINE to that directory and MFN_RENDER_OUTPUT to another directory, then use
`--filter FullyQualifiedName~RenderingParityTests` in Release. The comparison is raw BGRA, not PNG encoding.
For acquisition measurements use `--filter FullyQualifiedName~RenderingBackendTests` with MFN_RENDER_OUTPUT.

Native Android commands use explicit emulator serial, `adb install --no-streaming -r`, a force-stop of
the sample, and `adb shell am instrument -w` with
`com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.WindowingValidationInstrumentation`.
They preserve app data and do not clear or wipe the emulator.

## Final code/architecture review — 2026-10-06

All 29 paths in section M, including every new/untracked file, were reviewed against the exact base.
The review followed Form/Run/internal factories/PopupWindow, FrameworkBootstrap, registered and direct
Windows/Android initialization, TestHost creation/cleanup, native presentation and offscreen rendering.
Four concrete defects were corrected within those same paths:

1. Direct public backend Initialize installs IWindowingPlatform without necessarily registering
   WindowKitBackendRegistry.Current. ConfigureRendering now rejects that initialized service path too.
   The isolated real-Windows performance scenario reproduced the old acceptance before a Form existed;
   it now verifies rejection after both direct Initialize and FrameworkBootstrap.EnsureInitialized,
   with ActiveRenderingBackend remaining null until actual renderer resolution.
2. Pooling the lease object itself let a disposed reference regain access when the object was reused,
   and let a second Dispose release a different paint's lock. Only internal resource holders are now
   pooled. Each acquisition returns a small separate lease, permanently revoked before cleanup.
3. Frame canvas/metadata getters previously lacked thread verification. Live reads now verify the
   owner thread and reject access after Complete or Dispose. A foreign-thread regression verifies
   access, completion, disposal and surface acquisition without releasing the owner's live lock.
4. The diagnostic identity merge could replace an existing renderer with null when a nested caller
   supplied default metadata. The prior null-coalescing behavior is retained while still preserving
   Skia Raster against later native metadata. A regression reproduced the null before the correction.

The three new contract tests failed against the preceding implementation and passed after correction.
The Windows pre-window regression also failed before its correction. Failure logs are retained locally.
No timeout, retry, pixel tolerance, allocation threshold, package, public enum value or native
Windows/Android presentation code changed during review. The public API remains Auto/Software,
RenderingOptions, ConfigureRendering and the two read-only selection properties, with the documented
additive PerformanceRenderInfo fields. No remaining Phase 0 publication blocker was identified.

The lifetime audit confirms that nested paints have different active holders; a holder returns only
after Skia disposal and the framebuffer unlock have unwound. Adapter disposal prevents acquisition
without prematurely disposing an active frame. The nested finally always attempts framebuffer disposal
if Skia disposal throws. Existing tests inject lock, metadata, surface-creation, paint and unlock failures;
Skia's own disposal failure is covered by structural finally-path review, not an injected native failure.
The existing overlay recorder catches its own failures; escaping paint failures still unwind the frame.
No rollback policy or independent platform lifetime was added.

### Renewed validation for the reviewed source

| Check | Final result |
| --- | --- |
| Restore and full Debug/Release solution builds | PASS; both builds 0 warnings/errors |
| Full Debug suite | 4129 passed, 0 failed, 0 skipped |
| Full Release suite | 4129 passed, 0 failed, 0 skipped |
| Backend + direct WindowBase contracts | 20/20, including the three new regressions |
| Exact-base comparison fixture | 8/8; 21/21 exact raw BGRA images |
| Real Windows HWND/DPI/presentation and pre-window configuration | PASS in both full suites |
| ControlGallery Auto, ControlGallery --software, default template | Nonzero HWND, responsive, clean exit 0 |
| Android Debug APK, Release APK/AOT, Release AAB/AOT/trimming | PASS |
| Android API 36 emulator, reviewed Debug and Release | 63/63 native assertions in each, on the first renewed run |
| Physical Android | NOT EXECUTED — environment unavailable |
| API 23 runtime, full manual Gallery and Visual Studio F5 | NOT EXECUTED; prior limitations remain |
| Package inventory | Same 11 NuGet paths and 438 Debug APK lib paths; no new GPU payload |
| Source consistency and diff hygiene | 25/25 changed C# SHA-256 values match validation; git diff --check passes |

The full-suite increase is exactly three core tests (core 1502 instead of 1499); all other project counts
in section K are unchanged. The baseline and feature parity fixture SHA-256 remains identical:
`7AFF3465DAD460AF86AD6AAF420E94AE58E91E4DD6410EAD8363F87C4367CFB0`.
No first-render or portable-golden claim is added. The initial cold-popup observation and identical
warm-up policy described in section J are unchanged.

### Renewed bounded performance comparison

The fresh comparison again used the exact base, identical fixture and DOTNET_TieredCompilation=0
for measurement processes only. Full suites ran with the normal runtime defaults.

| Acquisition measurement | Exact base sequence | Reviewed implementation |
| --- | ---: | ---: |
| Managed allocation per frame | 2688 B | 2664 B |
| CPU per acquisition | 1.467 microseconds | 1.316 microseconds |
| Fixed viewport pixel backing | 1,080,000 B | 1,080,000 B |

Permanent lease revocation adds a 24-byte lease compared with the original refactor's 2640 B.
Total acquisition remains below the old raster path without changing the regression threshold.
This is a correctness tradeoff, not a performance-improvement claim.

The first renewed 30-frame paint sample was 0.366/0.578 ms (base/refactor), while full capture was
0.777/0.748 ms. Because these short CPU samples disagreed, three additional paired measurements were
run, reversing process order for the middle pair. All original samples are retained:

| Pair | Paint CPU base/refactor | Capture median base/refactor | Resize median base/refactor |
| --- | --- | --- | --- |
| 1 | 0.382 / 0.338 ms | 0.734 / 0.683 ms | 1.542 / 1.529 ms |
| 2, reversed process order | 0.363 / 0.357 ms | 0.759 / 0.719 ms | 1.623 / 1.566 ms |
| 3 | 0.381 / 0.357 ms | 0.729 / 0.725 ms | 1.636 / 1.622 ms |

Whole-capture allocations across those pairs were 1,095,026–1,095,108 B for the base and
1,095,095–1,095,234 B for the reviewed implementation. The isolated higher paint sample did not
reproduce in the paired runs. These bounded observations show no reproducible material regression;
they do not establish a general speedup or native display performance guarantee.

### Publication boundary and retained caveats

The requested publication is one logical commit, `Extract software rendering backend`, on
`codex/issue-46-rendering-foundation`, followed by a PR to master. The commit contains exactly the
29 paths in section M. Logs, source hashes, comparison images, measurement JSON, TRX, APK/AAB and
helper scripts stay in ignored `artifacts/rendering-foundation/final-review/`; the original checkout
and its `.codex/config.toml` remain untouched. Commit identity and PR/CI state belong to the GitHub
publication record rather than a self-referential commit hash in this document.

The PR must link `Fixes #170` and `Part of #46`. Both issues remain open until a separate merge task;
only #170 is an auto-close target. The parent roadmap retains all GPU phases unchecked. No GPU PoC,
backend selection decision, native composition (#60), Media sharing, merge or release is included.

The unexplained initial Android Back failure in section K is still unresolved. Renewed 63/63 passes
do not prove its cause or repair it. Physical Android and API 23 runtime remain unqualified, and
manual Gallery/Visual Studio F5 evidence is not claimed. These caveats must accompany the PR even
when required CI passes. The reviewed source is suitable for later final merge review after the
required `build` check succeeds; this task does not merge it.
