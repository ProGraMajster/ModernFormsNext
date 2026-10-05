# Issue #72 — Android application/windowing implementation and validation

Date: 2026-10-04. Baseline: `bcf2bcb1726cbb697d2e56600188c4ef2c91ebe1`.
Working branch: `codex/issue-72-android-windowing`, isolated from the original checkout.
This report preserves the implementation-stage snapshot before final review, commit and PR.
Its test counts, source manifest and 54-path inventory describe that earlier tree, not subsequent
review corrections. See [the final review](issue-72-final-review.md) for the reviewed source,
new regressions, updated validation and acceptance decision. Logs, TRX files, APKs and device
captures remain in ignored output directories, principally `artifacts/issue-72/`.

The implemented policy is one main Form in one Activity, with owner-bound modal Forms and
reusable in-Activity popups. The public startup is Application.Run from AndroidWindowActivity.
See [the architecture, startup example and capability matrix](../android-windowing.md).

## A. Audit before editing (1–4)

1. At the requested baseline Android had lifecycle/activation/state/inset infrastructure from
   #63, main-thread platform dispatch, software Skia hosting, Choreographer, IME, hardware keys,
   multitouch and a canonical accessibility provider. It had no registered IWindowingPlatform,
   IWindowImpl or WindowKit external-loop dispatcher. Application.Run expected an owned loop.
2. Reused AndroidWindowKit initialization, ActivityTracker/reducer, weak-host helpers, lifecycle
   publisher, state codec, AndroidSkiaHostView, density/inset mapping, native keyboard/source
   classification, IME sessions, accessibility mapper/session/provider and animation frame source.
   Shared Form/WindowBase, ControlAdapter, ControlFocusScope (#160), validation (#161),
   ControlTextInputHost, InputBindingResolver and SkiaControlSurface routing remain canonical.
3. Added the Android implementations of existing window/popup/positioner/framebuffer contracts.
   The minimal neutral additions identify external loops, host-owned chrome, and native surface
   input. No shared API exposes Android Activity/View or Windows HWND types.
4. The manual application path was MainActivity -> AndroidAppHost -> AndroidSkiaHostView and
   SkiaControlSurface. AndroidAppHost manually forwarded rendering, input, keyboard ownership,
   diagnostics and lifecycle. That class was removed. MainActivity now delegates host lifecycle
   to AndroidWindowActivity and starts a real shared MainForm. The standalone Android.SmokeTest
   intentionally remains a technical platform-services host, not an alternate framework window
   implementation. AndroidKeyboardInput remains a small compatibility helper for tests of the
   existing public windowless surface; production MainActivity no longer invokes it.

## B. Architecture (5–12)

5. The [architecture diagram](../android-windowing.md#ownership-and-routing) identifies all owners.
6. AndroidWindowingPlatform registers existing IWindowingPlatform, validates one-main policy,
   ownership, Back ordering, host generations and terminal cleanup.
7. AndroidWindowImpl supplies confirmed native geometry, feature queries, text proxy, software
   surfaces, visibility/focus and close callbacks. AndroidPopupImpl also implements IPopupImpl
   and IPopupPositioner. Unsupported desktop operations throw explicitly.
8. The framework registry owns window contracts; application code owns Forms/control disposal.
   The process backend retains only a weak current Activity host.
9. Each Activity host owns its FrameLayout and native presentations. Each visible window owns
   one Skia View and bitmap through that presentation. Hidden/retired presentations release them.
10. The internal borrowed SkiaControlSurface mode uses the Form's exact adapter, text host and
    resolver. It neither reparents nor owns that control tree. The public windowless mode retains
    its prior ownership and behavior.
11. AndroidMainThreadDispatcher implements IExternallyOwnedDispatcherImpl over the existing
    main Handler. WindowKit posts/timers/InvokeAsync use that actual thread. No nested Looper
    or worker UI dispatcher is created.
12. Application.Run initializes lifetime, subscribes closure, shows the Form and returns on
    Android. Subscriptions persist to Exit. Startup failure releases them and cleans native
    windows. Exit finishes the host Activity but does not terminate the process main Looper.

## C. Window lifecycle (13–20)

13. Show follows shared Load/layout/VisibleChanged/native presentation/Shown behavior. Native
    activity and focus are confirmed separately; Shown does not claim first-paint completion.
14. Hide cancels input and releases the View while retaining the Form and control tree. Load
    and Shown are not repeated. Owner Hide dismisses reusable popups and closes modal descendants.
15. Close is terminal and idempotent, respects cancellable Closing for user closure, removes
    registry entries and completes modal results. Forced owner/app cleanup remains exception-safe.
16. Activation requires resumed state, actual native window focus and focused View. Popup-native
    focus maps to its logical owner for the shared popup text lease. Deactivation cancels gestures
    and held keys and retires text callbacks without inventing a new focus owner.
17. Activity replacement preserves the same Form/control identities and edited content, revokes
    old epochs/handles/IME sessions, dismisses transient popups and presents retained windows again.
    Native focus is reacquired; a retained native Window need not repeat Activity's focus callback.
18. Pause/Stop suspends surfaces/input/animation through existing lifecycle infrastructure.
    Foreground resumes it. Destroy without replacement produces NoHost, not fabricated user Close.
    Process death still requires the application's bounded, versioned saved-state payload.
19. OpenForms continues to track Forms through the shared lifecycle. Hide and detach do not
    manufacture terminal closure or duplicate membership; popups are not Forms.
20. MainWindowClosed, LastWindowClosed and Explicit retain their established meanings. The
    single-main Android policy limits desktop-style combinations, without creating new modes.

## D. Platform differences (21–29)

21. Size/density/origin/handle are committed from native layout/configuration facts before shared
    callbacks. Initial 400×300 is only the pre-layout default and is documented as such.
22. Arbitrary top-level Move rejects. Popup placement is bounded inside the current Activity.
23. Main resize is host-controlled. Modal/popup requested sizes are hints, clamped by native layout.
    Min/max desktop constraints reject without changing corresponding managed values.
24. Only Normal WindowState is supported; minimize/maximize reject.
25. Host-owned chrome disables the managed desktop title bar, border, centering and move/resize
    gesture route. Resizeable defaults false and cannot be enabled on host-managed windows.
26. Per-Form taskbar visibility, topmost and non-null native icons reject. Decoration/transparency
    hints retain explicit host policy rather than advertising fictitious desktop effects.
27. Screen reports the current Activity display snapshot; this does not implement multi-display
    enumeration or independent desktop monitor placement.
28. Handle is a borrowed JNI View handle, descriptor AndroidView, zero after detach. Never cache
    it across a presentation or treat it as HWND.
29. GetWindowingDiagnostics reports live state, geometry, insets, counters and capability policies,
    with no activation or text payload and no strong native references.

## E. Relationships (30–34)

30. Popups reuse the shared PopupWindow contract and text-ownership lease. Back, owner taps,
    pause/deactivation and detach dismiss them; subsequent Show remains valid. Positioner support
    matches the shared top-left/bottom-right anchor/gravity route.
31. ShowDialog(owner) presents a real native Skia surface in the same Activity, disables its owner,
    and returns the shared Task<DialogResult>. No nested event loop is used.
32. Parent validation rejects foreign/dead owners and cycles. Owner Close force-cleans descendants;
    modal completion restores owner enablement even after native Back.
33. One main per Activity and one concurrent Activity host are explicit constraints. A second
    independent main fails with no false visibility or OpenForms entry.
34. API 33+ uses OnBackInvokedDispatcher, earlier versions use OnBackPressed. Android may consume
    Back to dismiss an active IME first. Framework order is popup -> modal -> cancellable main.

## F. Input and lifecycle integration (35–42)

35. Native touches and accessibility focus request the existing canonical ControlFocusScope.
    No last-focused-control field or second focus tree was added.
36. Validation can veto a native touch focus transfer and its click while retaining the old
    editor session. Successful transfer retires that session. Form preview and command routing
    use the resulting canonical owner.
37. A stable weak native text proxy connects the existing revocable session to each new View.
    Composition, selection and text remain in shared editors; stale clients reject callbacks.
38. Pointer IDs, independent capture, cancellation and scroll gestures reuse shared routing.
    Native logical coordinates convert once into WindowBase's device-scaled route.
39. Android virtual nodes wrap the same canonical semantic objects. Real window screen bounds
    are already physical; the adapter does not apply density/origin twice. Canonical Form children
    intentionally omit the layout-only client-area peer. Android now validates and projects that
    existing parent chain, matching Automation without changing the shared accessibility API.
40. Remaining safe-area overlap reduces DisplayRectangle once. IME overlap remains informational;
    the sample's existing caret scrolling consumes it without double keyboard compensation.
41. Density updates shared window scaling and render geometry. Font scale remains a separate
    diagnostic/application preference; no automatic multiplication of authored font sizes.
42. WindowBase renders into the presentation's software framebuffer. AndroidSkiaHostView presents
    it through existing invalidation and Choreographer integration. No permanent render timer,
    GPU subsystem, second renderer or per-paint native geometry query was introduced.

## G. API and internal contracts (43–45)

43. New public framework/backend API:
    - WindowKit: IExternallyOwnedDispatcherImpl; Dispatcher.HasExternalEventLoop;
      IWindowHostPolicy.IsHostManaged; IWindowSurfaceInput.Pointer/Key/CancelInput/DismissPopup;
      WindowSurfacePointerAction (Down, Move, Up, Cancel). Backend-facing interfaces use PrivateApi.
    - Android: AndroidWindowActivity and protected OnStartApplication plus documented Activity
      forwarding overrides; AndroidActivityHost constructor, HasMainWindow, Start, Resume, Pause,
      Stop, ConfigurationChanged, WindowFocusChanged, HandleBack and Dispose.
    - Diagnostics: AndroidWindowKitBackend.GetWindowingDiagnostics, AndroidWindowingDiagnostics
      and AndroidWindowDiagnostics, their documented positional snapshot properties, and policy
      properties WindowPolicy, SupportedCapabilities and UnsupportedOperations.
    - AndroidMainThreadDispatcher additionally exposes the IDispatcherImpl members
      CurrentThreadIsLoopThread, Now, Signal, UpdateTimer, Signaled and Timer.
    - Sample-only types MainForm and WindowingValidationInstrumentation are not framework APIs.
44. New internal contracts/types: AndroidWindowingPlatform, AndroidWindowImpl, AndroidPopupImpl,
    AndroidWindowTextInput, AndroidScreenImpl, IAndroidWindowHost, native Presentation/Framebuffer,
    ControlAdapter surface transport/accessibility notification hook, borrowed surface constructor,
    WindowBase preview helpers, native window-coordinate flag and projected-parent lookup.
45. The external-loop marker is necessary to distinguish a native-owned loop from a missing loop.
    Host policy keeps desktop chrome decisions neutral. Surface input lets a backend route touch
    identities and native keys through the existing canonical tree without referencing the core
    framework or reproducing its control logic. No framework public member was removed.

## H. Changed files and removed paths (46–47)

The complete file inventory is recorded below after final validation. The removed transitional
production class is `samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AndroidAppHost.cs`.
MainActivity no longer manually forwards platform host lifecycle or constructs rendering/input
adapters. Existing native instrumentation lookups now traverse the Activity's container to find
the same real Skia View.

## I–J. Validation (48–60)

Final build/test and native evidence is recorded in the validation tables below. All builds are
sequential (`-m:1 /p:UseSharedCompilation=false`). Existing assertions, allocation thresholds,
test enablement and required checks were preserved. The static sample test was updated to inspect
the migrated backend-owned IME route rather than the deliberately deleted AndroidAppHost file.

The new native runner exercises Run return, actual focus/density, posts, timer delivery/cancellation,
InvokeAsync, IME composition, validation/click suppression, synthetic native hardware-source keys,
popup reuse, modal Back/result, Hide/Show, five Activity replacements, old-session retirement,
cancellable main closure, OpenForms cleanup and the surviving process Looper.
Native hardware-source injection is not physical-keyboard evidence. Window Back checks first
observe the native IME-hidden state; keyboard dismissal is not mislabeled as failed window closure.

The existing release matrix also covers four editor types, native multitouch/cancel, real animation
frames and idle, portrait/landscape, 20 seconds in background, 12 recreation/GC cycles, protocol
activation and reduced motion. Accessibility Phase 3/4 cover canonical actions, text privacy,
selection, viewport, calendar popup and stale native IDs after recreation.

The initially unavailable physical device became authorized during validation: nubia NX769J,
Android 16/API 36, arm64-v8a, 1116×2480, 480 dpi (density 3), active display mode approximately
60 Hz with 90/120 Hz modes advertised. It is tested separately from the Pixel_8 API 34 x86_64
emulator. Native instrumentation on a physical phone is physical-runtime evidence; it is not a
claim of manual finger gestures, vendor-IME typing, physical-keyboard or TalkBack speech validation.
Those manual checks, minimum API 23 runtime and broad OEM coverage remain
**NOT EXECUTED — environment unavailable**. Visual Studio F5/Designer-host interaction is also
separate from automated Designer and Windows native tests.

## K–M. Documentation, boundaries and readiness (61–72)

61–63. Added the startup/ownership/geometry/lifetime guide, complete capability matrix and this
report. Updated README, platform/backend status, lifecycle, cross-platform sample, accessibility,
animation architecture, platform-specific registry documentation and known limitations.

64. #79 still owns complete Android platform services; permissions do not imply those features.
65. #60 still owns arbitrary native-view embedding and composition.
66. #46 still owns a GPU renderer/swapchain; software Skia remains the rendering path here.
67. Linux/macOS still need their native window/input/render/lifecycle implementations. The neutral
external-loop capability is reusable but does not make those skeletons functional.
68–70. The implemented mobile policy must be assessed against the final evidence below. Broader
device qualification, distribution signing/store submission and desktop parity remain limitations.
Native lifecycle/IME/accessibility remain the highest regression-risk boundaries.
71. No framework signature removal, package/dependency/version change or canonical focus/semantic
tree replacement. Application.Run now has explicitly documented nonblocking semantics only on
externally owned loops. Unsupported Android desktop setters fail explicitly. The removed public
sample helper was transitional application code, not a released framework contract.
72. No PR is created by this task. Readiness is based on the final validation table, with emulator
and physical-runtime evidence explicitly separate; the working diff is left for owner review.

## Final build and automated contract evidence

All logs below are relative to ignored `artifacts/issue-72/`. The unchanged Git base is
`bcf2bcb1726cbb697d2e56600188c4ef2c91ebe1`; results qualify the uncommitted implementation,
not an existing upstream commit or released package.

| Check | Result | Evidence |
| --- | --- | --- |
| Full solution restore | PASS | `restore.log` |
| Full Debug build, including Windows/Android/Designer/VSIX | PASS, 0 warnings, 0 errors | `solution-Debug-verified.log` |
| Full Release build, including Windows/Android/Designer/VSIX | PASS, 0 warnings, 0 errors | `solution-Release-verified.log` |
| Full Debug tests | 4094 passed, 0 failed, 0 skipped | `tests-Debug-verified.log`, `test-Debug-verified/*.trx` |
| Full Release tests | 4094 passed, 0 failed, 0 skipped | `tests-Release-verified.log`, `test-Release-verified/*.trx` |
| Safe-area regression before correction | Expected failure: screen origin omitted insets | `safearea-before.log` |
| All new windowing contract cases after correction | 21 passed | `windowing-safearea-after.log` |
| Windows ControlGallery startup/normal close | PASS, real HWND, process exit 0 | `windows-startup-smoke.json` |
| Windows default DemoApp startup/normal close | PASS, real HWND, process exit 0 | `windows-startup-smoke.json` |
| Manual Windows visual interaction / Visual Studio F5 | NOT EXECUTED | Automated/native checks do not establish these |

Both full suites contain the same counts:

| Project | Passed per configuration |
| --- | ---: |
| Automation | 166 |
| Automation.Windows | 66 |
| CrossPlatform.Sample | 29 |
| Designer | 691 |
| Testing | 927 |
| Framework core | 1482 |
| VisualStudioExtension.Vsix | 26 |
| Android backend | 379 |
| Windows backend | 328 |

The last change after those full suites is confined to the native accessibility instrumentation:
it waits for its actual service event before reading focus. Debug and Release APK/AAB builds and
native runs below compile and exercise that final runner; framework implementation and deterministic
test sources are unchanged from the full green suites. No allocation-test failure occurred in
these final suites, and no assertion, threshold, skip or retry policy was weakened.

### Commands used

Run from the isolated worktree. Debug and Release builds/tests were executed sequentially.

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet build .\ModernFormsNext.slnx -c Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet build .\ModernFormsNext.slnx -c Release --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet test .\ModernFormsNext.slnx -c Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false --logger trx --results-directory artifacts/issue-72/test-Debug-verified
dotnet test .\ModernFormsNext.slnx -c Release --no-restore --no-build -m:1 /p:UseSharedCompilation=false --logger trx --results-directory artifacts/issue-72/test-Release-verified
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Debug -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=apk /p:EmbedAssembliesIntoApk=true
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Release -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=apk
dotnet publish samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj -f net10.0-android -c Release -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=aab
git -c core.safecrlf=false diff --check
```

The two Windows samples were launched from their built Debug `.exe` files with `Start-Process`,
confirmed to have native window handles, and closed with `CloseMainWindow`; both exited with 0.
No source or template content was added to DemoApp.

Native commands used explicit ADB serials and data-preserving replacement:

```powershell
adb -s <serial> install --no-streaming -r <signed-apk>
adb -s <serial> shell am force-stop com.programajster.modernformsnext.sample
adb -s <serial> shell am instrument -w com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.WindowingValidationInstrumentation
adb -s <serial> shell am instrument -w com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.AccessibilityInstrumentation
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId <serial> -ApkPath <signed-apk> -OutputDirectory <new-evidence-directory> -TimeoutSeconds 180
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId <serial> -ApkPath <signed-apk> -OutputDirectory <new-evidence-directory> -Scenario AccessibilityPhase4
```

The script records APK hash, source base/dirty state, model/API/ABI, display, original/final settings
and native artifacts. Instrumentation is opt-in; normal sample startup does not invoke fixtures.
APK replacement did not wipe data, uninstall the app, grant permissions or publish a package.

Release effective properties (`release-effective-properties.json`) are `RunAOTCompilation=true`,
`AndroidEnableProfiledAot=true`, `PublishTrimmed=true`, `TrimMode=partial`, `AndroidLinkMode=SdkOnly`,
`EmbedAssembliesIntoApk=true`, ABIs `android-arm64;android-x64`, min API 23 and target API 36.
ZIP inspection confirms framework/backend/sample AOT libraries in Release APK and AAB. This is
profiled Mono AOT with partial trimming, not NativeAOT or arbitrary full-trim safety. Local signing
is for validation; store/distribution signing and submission were not performed.

## Final native evidence (58–60)

| Scenario | Pixel_8 emulator, API 34, x86_64, density 2.625 | nubia NX769J phone, API 36, arm64-v8a, density 3 |
| --- | --- | --- |
| Windowing runner, Debug | 49/49 PASS | 49/49 PASS |
| Windowing runner, final Release | 49/49 PASS | 49/49 PASS |
| Release matrix on Debug APK | 95/95 PASS | 105/105 PASS |
| Release matrix on final Release APK | 95/95 PASS | 105/105 PASS |
| Accessibility Phase 4, Debug | 65/65 PASS | 65/65 PASS |
| Accessibility Phase 4, final Release | 65/65 PASS | 65/65 PASS |
| Accessibility Phase 3, final Debug runner | 49/49 PASS | 49/49 PASS |
| Accessibility Phase 3, final Release runner | 49/49 PASS | 10 consecutive runs, each 49/49 PASS |
| Normal sample launch/render capture | Observed and visually inspected | Observed and visually inspected |

Debug matrix/windowing/Phase 4 were executed after the last framework correction; the later
instrumentation-only Phase 3 synchronization is compiled and rerun in both configurations. Final
Release repeats all four runners from the final APK. Earlier failed development probes are retained
and explained below; they are not included in the PASS counts.

Evidence includes `native-windowing-<serial>-safearea-debug.log`, `<serial>-debug-matrix/`,
`<serial>-debug-accessibility/`, `<serial>-debug-phase3-final.log`,
`<serial>-release-windowing-final.log`, `<serial>-release-matrix-final/`,
`<serial>-release-accessibility-final/`, and `<serial>-release-phase3-final-*.log`.
The serials are emulator-5554 and FY24029104A4; device/provenance details are archived with each
matrix. Normal-launch images are `<serial>-preview.png` (Debug runtime after final framework fix).

The windowing runner confirms posts/timers/cancellation/InvokeAsync on the real main thread,
canonical validation and click suppression, native InputConnection composition, synthetic native
hardware-source command routing, popup reuse, modal completion, Hide/Show, five Activity replacements,
no retained old Views **or Activities** after GC, cancellable Back, terminal cleanup, and a live
process Looper after framework Exit. The matrix additionally executes four editor types, native
multitouch/cancel, rotation, 20-second Home/background/resume, protocol activation, reduced motion,
animation/idle and 12 recreation/GC cycles. All 12 cycles report `retired_views_alive=0` on each
device/configuration. These are bounded retention checks, not proof against every possible leak.

The phone advertises 60/90/120 Hz and accepts those requested modes, but every final measurement
reports approximately **60 Hz actual**. The emulator offers only 60 Hz. Neither result qualifies
actual 90/120 Hz rendering, GPU time or scanout. Matrices are correctness smoke, not a controlled
performance benchmark. Settings-before/settings-after comparisons pass for every archived matrix
and Phase 4 run.

### Final local packages

Build logs: `sample-debug-apk-focus-event.log`, `sample-release-apk-focus-event.log`,
`sample-release-aab-focus-event.log`; all commands exit 0 with no warnings/errors. Files are copied
to ignored `artifacts/issue-72/packages/` because changing AndroidPackageFormats replaces previous
format outputs in the default build directory. AAB was built and inspected; APK was installed/run.
No Play/bundletool split-install qualification is claimed.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| sample-debug-Signed.apk | 59674916 | `530D108145B4B16F67B0EE6196B5DDA89E07E2A25402DCC365468BE8F25FA2C0` |
| sample-release-Signed.apk | 27265705 | `544D0C185F0D7CE41F75CE9D6ACD9ABE7F99476E4100AED4CF0C27B5545DD010` |
| sample-release-Signed.aab | 27250392 | `83A9F3FD1A304809130149A842D3D0D0CAC185F15B50249C95388A00168C5BD5` |

## Readiness and remaining risks (68–72)

The bounded Android windowing foundation is implemented and ready for source review: real
Application.Run/Form startup, existing focus/validation/IME/accessibility integration, native
lifecycle replacement and software rendering have both emulator and physical-runtime evidence.
The sample's application-level rendering/input host has been removed.

This is still an experimental source-tree backend, not a production-readiness declaration or an
issue-closure recommendation. Remaining qualification includes API 23–32 legacy Back, vendor IME
typing and composition, physical keyboard layouts, manual TalkBack speech/gestures, broader OEMs,
actual high-refresh-rate rendering and distribution artifacts. Multiple independent top-level
Forms/concurrent Activities, desktop window manipulation, GPU rendering and complete platform
services remain outside the documented policy. Framework Exit is terminal in the current process;
another Application.Run requires a fresh process. Activity recreation during a live application
is supported and does not restart Run.

The highest compatibility risks are external-loop startup semantics, safe-area coordinate mapping,
native focus/IME transitions, accessibility projection and resource retirement. Existing Windows,
Automation, Designer and VSIX tests remain green; manual Windows visual/F5 checks are not inferred
from them. #79, #60, #46 and Linux/macOS backend work remain separate.

Final Git hygiene: the worktree remains on `codex/issue-72-android-windowing` at the requested base;
`git diff --check` passes. The original `docs/refresh-screenshots` checkout remains at
`0fbb20a674effa41d463f18216311d9ce8b0b3c1` with only its preexisting untracked `.codex/config.toml`.
No staging, commit, push, PR, merge, issue update/closure, version change or deployment was performed.

## Problems found during validation

- First native Show requested an invalidation before OnStart; presentation now defers it until
  the existing surface lifecycle allows rendering.
- Native recreation can preserve a focused Window without replaying Activity's focus callback.
  Focus confirmation now reads the actual attached native hierarchy at the existing lifecycle/
  focus transitions, instead of treating a missing callback as loss of focus.
- The original Android accessibility membership check assumed reciprocal direct Parent/child
  links. Real Form semantics deliberately omit the client-area implementation peer. A first
  attempted shared-parent correction failed Automation's existing contract test and was reverted.
  The final Android adapter projects the validated canonical ancestry; shared semantics stay intact.
- The migrated sample had a static source assertion referring to the removed AndroidAppHost file.
  It now checks the backend-owned shared IME route and absence of native editor widgets.
- Instrumentation's generic key injection was an IME/virtual source and correctly bypassed
  shortcuts. The hardware test now sends explicit native keyboard-source View events; it makes no
  physical-device claim. Window Back tests observe native IME dismissal before asserting closure.
- Review added guards against continuing a native presentation after a synchronous text/geometry
  callback retires it, and against an older IME/presentation epoch clearing newer ownership.
- Review aligned Form preview with the existing raw-key order: a preview focus change affects
  subsequent binding resolution, and Form.KeyUp still observes a shortcut-consumed release.
- The API 36 phone exposed a missing safe-area offset in Control.PointToScreen. Painting and
  touch transport already used DisplayRectangle, while screen conversion still used only the
  border. The canonical conversion now uses that same display origin. A regression failed before
  the fix (15,21 instead of 45,81 at density 3) and passes afterward, including native touch.
- An emulator Release probe delivered Back after IME insets became invisible but before Android
  removed the IME Back callback. The log explicitly reported a callback on a hidden IME. The
  runner now waits for native window/accessibility transition idle after verifying hidden insets;
  it never retries Back or increases the assertion timeout.
- The older Phase 3 runner read service-side accessibility focus immediately after PerformAction.
  On the physical AOT runtime two of four baseline runs failed that lookup, while the action
  returned success. It now uses UiAutomation.ExecuteAndWaitForEvent for the exact native focus
  event before querying the service cache. The action still executes once; all 49 assertions remain.

These were corrected at their contract/lifecycle boundary. No timeout increase, disabled test,
allocation-threshold adjustment or unconditional retry was used to manufacture success.

## Complete changed-file inventory (46–47)

54 intended paths; AndroidAppHost.cs is deleted. All others are modified or newly added source,
tests or documentation. No binaries, logs or machine configuration are included.

```text
docs/android-accessibility.md
docs/android-backend.md
docs/android-windowing.md
docs/application-lifecycle.md
docs/architecture/android-animation-runtime.md
docs/cross-platform-sample.md
docs/development/issue-72-android-windowing-report.md
docs/known-limitations.md
docs/platform-specific-code.md
docs/platforms/android.md
ModernFormsNext.CrossPlatform.Sample.Tests/AndroidToolingContractTests.cs
ModernFormsNext.WindowKit.Backend.Android.Tests/AndroidWindowingContractTests.cs
ModernFormsNext.WindowKit.Backend.Android/Accessibility/AndroidAccessibilitySession.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/Accessibility/AndroidAccessibilityNodeProvider.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/AndroidWindowKitBackend.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/Dispatching/AndroidMainThreadDispatcher.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/Rendering/AndroidSkiaHostView.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/Windowing/AndroidActivityHost.cs
ModernFormsNext.WindowKit.Backend.Android/Platform/Windowing/AndroidWindowActivity.cs
ModernFormsNext.WindowKit.Backend.Android/Windowing/AndroidWindowImpl.cs
ModernFormsNext.WindowKit.Backend.Android/Windowing/AndroidWindowingDiagnostics.cs
ModernFormsNext.WindowKit.Backend.Android/Windowing/AndroidWindowingPlatform.cs
ModernFormsNext.WindowKit.Backend.Android/Windowing/AndroidWindowSupport.cs
ModernFormsNext.WindowKit.Backend/Properties/AssemblyInfo.cs
ModernFormsNext.WindowKit/Dispatcher.cs
ModernFormsNext.WindowKit/ExternallyOwnedDispatcherImpl.cs
ModernFormsNext.WindowKit/Platform/IWindowHostPolicy.cs
ModernFormsNext/Application.cs
ModernFormsNext/Application.Lifecycle.cs
ModernFormsNext/Application.Testing.cs
ModernFormsNext/Control.Accessibility.cs
ModernFormsNext/Control.cs
ModernFormsNext/ControlAdapter.cs
ModernFormsNext/ControlAdapter.SurfaceInput.cs
ModernFormsNext/Form.cs
ModernFormsNext/SkiaControlSurface.cs
ModernFormsNext/SkiaControlSurface.Keyboard.cs
ModernFormsNext/WindowBase.cs
ModernFormsNext/WindowBase.Lifecycle.cs
ModernFormsNext/WindowBase.SurfaceInput.cs
README.md
samples/ModernFormsNext.CrossPlatform.Sample/App.cs
samples/ModernFormsNext.CrossPlatform.Sample/MainForm.cs
samples/ModernFormsNext.CrossPlatform.Sample/MainPage.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AccessibilityInstrumentation.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AccessibilityPhase4Instrumentation.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AndroidAppHost.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AndroidManifest.xml
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/MainActivity.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/NativeValidationViews.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/ReleaseValidationInstrumentation.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/SampleApplication.cs
samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/WindowingValidationInstrumentation.cs
samples/ModernFormsNext.CrossPlatform.Sample/README.md
```
