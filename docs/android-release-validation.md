# Android release validation matrix

Android remains an **experimental single-surface host**. Issue [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69)
collects repeatable evidence for the existing rendering, input, accessibility, lifecycle and
animation paths. It does not declare general Android production, store, GPU or trim safety.

The focused `LayoutPolicy` scenario certifies opt-in inherited layout configuration with the same
production fixture as the Windows HWND runner. It observes intermediate semantic geometry under
the native motion policy, retarget/cancel/local override, and zero-area snap/restore. API29+ native
frame-committed captures verify that collapsed content disappears and restored content returns.
Use a separate output directory per explicit emulator/device; this does not qualify API23–28
capture behavior or manual gestures. See [animated layout](architecture/animated-layout.md).

The focused `VisualStateMetrics` scenario uses the same production fixture as the Windows HWND
runner. It checks compatible intermediate font size, line height, letter spacing and corner radius,
then exact targets, cancellation and restoration. API29+ frame-committed screenshots independently
compare the rendered TextBox glyph width and rounded corner pixels at source, target and restored
states. Motion-disabled devices follow the immediate policy; screenshots qualify settled states.
This synthetic enabled-state scenario does not qualify manual hover, vendor IME behavior or general
control typography parity. See the [supported metrics matrix](architecture/layout-aware-visual-state-metrics.md).

## Evidence types and acceptance

The focused `VectorGeometryValidationInstrumentation` runner uses the same bounded fixture as the
Windows `--vector-geometry` HWND runner and the extended Shapes Gallery. It exercises endpoint arcs,
shared group operands, XOR/Union, Stretch and a mutable parent clip. Source/alternative/restored
frame-committed captures assert exact solid content pixels in both clip halves and the combined
area's center; synthetic native touch reaches the included child and is rejected in the excluded
half. A changed unrelated corner cannot satisfy the owned content probes. Run explicitly on each
emulator/device and preserve the fresh `vector-geometry-*` artifact directory. This adds no manual
gesture, GPU, broad SVG or general Android production claim.

```powershell
adb -s $serial shell am instrument -w com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.VectorGeometryValidationInstrumentation
```

Keep these labels separate in every report:

- **AUTOMATED**: shared/backend tests, builds and archive inspection on the development machine.
- **EMULATOR**: instrumentation executed in a booted Android image; record API, ABI and renderer.
- **PHYSICAL DEVICE**: instrumentation executed on the identified phone; record model, API and GPU.
- **MANUAL OBSERVATION**: a person operated or inspected the named scenario. Synthetic native
  events and agent-inspected screenshots do not establish finger ergonomics or screen-reader speech.
- **NOT EXECUTED**: missing environment, unavailable mode or an unperformed scenario. A device's
  advertised mode or a successful package build is not a runtime pass.

Functional gates are exact: all assertions pass, no recorder/scheduler failures, composition
commits once, pointer captures are independent, cancellation does not click, background callbacks
stop, foreground resumes without consuming background time, recreation retains shared state and
exactly one active surface, and reduced motion reaches its target and becomes idle. Settings before
and after a run must match. Failure of any executed assertion blocks accepting that run.

Timing/allocation/memory figures are **observations**, not a production performance guarantee.
Archive individual frames; report callback duration and interval percentiles against the *observed*
refresh interval (60 Hz: 16.67 ms; 90 Hz: 11.11 ms; 120 Hz: 8.33 ms). Android `NativePaint` measurements
from the existing `PerformanceProfiler` cover software callbacks, not GPU completion or scanout.
An unhonored preferred-mode request leaves that requested cadence **NOT EXECUTED**. Emulator
SwiftShader measurements cannot be compared directly with a phone's GPU or used as a hardware SLA.

The default matrix records managed heap, process PSS and weak references to retired native views
after forced collection on each of 12 Activity recreations. The optional `LifecycleStress` profile
uses the same lifecycle gates with a configurable 20–600-second background dwell and 12–100
recreations. The Activity requests an actual task transition before measuring background work.
The runner records a baseline and per-cycle values, elapsed duration and the OS thermal status
when API 29+ exposes it. Use the actual dwell and samples to review growth and retained objects.
A bounded run with zero live retired wrappers establishes that observation for its sample points.
PSS includes managed and native memory, including runtime and rendering caches.

## Repeatable commands

Use a clean reviewed checkout and the repository's .NET/Android SDK. Select the ADB target explicitly
with `scripts/android/Get-AndroidDevices.ps1`. Unlock a physical phone before testing. Preserve its
data and settings; do not wipe an AVD or uninstall the sample to make a test pass.

```powershell
# Standalone Debug APK (no fast-deployment dependency).
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj `
  -f net10.0-android -c Debug -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false `
  /p:EmbedAssembliesIntoApk=true /p:AndroidPackageFormats=apk

# Profiled Mono AOT and partial/SDK trimming use the existing Release defaults.
dotnet build samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj `
  -f net10.0-android -c Release -t:SignAndroidPackage -m:1 /p:UseSharedCompilation=false `
  /p:AndroidPackageFormats=apk

$apk = 'samples/ModernFormsNext.CrossPlatform.Sample/bin/Release/net10.0-android/com.programajster.modernformsnext.sample-Signed.apk'
$device = '<explicit adb serial>'
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -OutputDirectory artifacts/android-release/matrix
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -Scenario AccessibilityPhase4 -OutputDirectory artifacts/android-release/accessibility
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -Scenario SystemReducedMotion -OutputDirectory artifacts/android-release/system-motion
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -Scenario VisualStateMetrics -OutputDirectory artifacts/android-release/visual-state-metrics
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -Scenario NativeView -OutputDirectory artifacts/android-release/native-view
./scripts/android/Invoke-ReleaseValidation.ps1 -DeviceId $device -ApkPath $apk `
  -Scenario LifecycleStress -BackgroundSeconds 90 -RecreationCycles 36 -TimeoutSeconds 300 `
  -OutputDirectory artifacts/android-release/lifecycle-stress

# Packaging evidence only: installing the APK above is a separate gate.
dotnet publish samples/ModernFormsNext.CrossPlatform.Sample/ModernFormsNext.CrossPlatform.Sample.csproj `
  -f net10.0-android -c Release -m:1 /p:UseSharedCompilation=false /p:AndroidPackageFormats=aab
```

Use a fresh output directory for each device/configuration/scenario. The script records source base,
dirty paths, APK SHA-256, hardware/software identity, advertised display modes, GPU identity,
installation output, instrumentation results and original/final settings. Native matrix output has
a fresh per-run directory with screenshots and profiler CSVs. Retain artifacts locally; do not commit
device serials, local paths, user data or package binaries. A dirty source record must not be presented
as an exact clean-commit build.

Installation preserves sample data and avoids incremental deployment. The minimum API 23 uses
an explicit staged APK and `pm install -r`. Other images and devices use `adb install --no-streaming -r`.
UiAutomation temporarily owns the accessibility connection. The full matrix
requests modes and orientation only for its own window, keeps that window awake, and restores its
flags, orientation, animation preference and control visibility. The optional `SystemReducedMotion`
scenario temporarily writes only `global/animator_duration_scale=0`; PowerShell restores its original
value, including an absent setting, in `finally`. Keep the script running until restoration completes.
If interrupted externally, compare the archived settings and restore that one original value before
accepting the run. Other scenarios do not write global settings.
Instrumentation has a 180-second host deadline. Set `-TimeoutSeconds` to cover the entire run when
using a longer stress profile, including startup, background dwell, foreground resume and all
recreation cycles. A timeout fails the run and stops only the selected sample process before
checking/restoring settings. The NativeView and LifecycleStress runs each supply a unique
artifact directory with a current-run `result.txt` and a scenario-specific success marker.

The opt-in ImeCompatibility scenario uses the same Release APK and device selector. Pass
`-Scenario ImeCompatibility` with a fresh output directory. The scenario runs the four editor
fixtures through native InputConnection methods with Polish and CJK data, verifies surrounding
text and selection and checks stale-connection rejection on focus handoff, disposal and
Activity recreation. Its marker is `ANDROID_IME_COMPATIBILITY_PASS` and requires a successful
scenario-specific run. The default release matrix continues to report its own marker.

## Scenario boundaries

| Lane | What the runner exercises | What it does not establish |
|---|---|---|
| IME / #62 | Native `InputConnection` composition/commit, Polish Unicode, emoji deletion, four canonical editors, multiline input | Gboard interaction, CJK composition, vendor spans, handwriting, physical keyboard layouts |
| Multi-touch | Real synthetic `MotionEvent` pointer IDs through the native view; independent release and cancel, using native accessibility bounds including insets | Physical finger ergonomics, every drag/scroll gesture |
| Rendering | Ellipse and Bezier path beside text; parallel rotation/scale; native screenshots and production frame snapshots | Pixel-perfect raster quality, GPU timing, every theme/control, native scanout smoothness |
| Lifecycle / #63 | Home, 20 seconds background with no frame work, foreground time rebasing, landscape/portrait viewport update, 12 Activity recreations, protocol Intent. The opt-in stress runs the extended background and recreation profile. | OS process death, reboot persistence, screen-off matrix, multi-window |
| Accessibility / #59 | Existing Phase 4 native virtual-node/action/text/grid fixture | New manual TalkBack speech or gesture evidence |
| Reduced motion | Application preference and a separate actual Android zero-scale startup probe | Hot toggles/fractional scales through Settings UI or every vendor's accessibility controls |
| Reliability | Bounded frame counters, heap/PSS, retired-view and optional thermal observations | Overnight and battery-discharge stress, native cache attribution and proof of leak freedom |
| Packaging | Standalone APK runtime and AAB construction, declared AOT/trim properties | Play submission, distribution signing, full trimming, NativeAOT or arbitrary application trim safety |
| NativeViewHost / #60 | Native EditText and Android View through the existing fixture. Checks ownership, nested scroll, focus/IME handoff, popup/modal, background, Activity recreation and disposal. | WebView, media surfaces, GPU completion and scanout. GPU backend qualification belongs to #46. |

The fixed-position instrumentation fixture is portrait-sized. Rotation assertions check native/shared
viewport and retained state; they do not claim that the fixture becomes a responsive landscape UI.
Screenshots show the current windowless control-buffer scaling, including visibly coarse edges at
high density. Physical backing-buffer dimensions alone do not prove every control is rasterized at
native density. General Android DPI/font-scale integration remains experimental.

## Remaining environment matrix

Repeat on the minimum declared API 23, intermediate API/device classes, additional vendors and an
environment that actually grants 90/120 Hz. Exercise Gboard/CJK and vendor keyboards manually,
TalkBack speech/gestures, hardware keyboards, font scaling, screen off/on, process death and longer
thermal/cache/lifecycle stress. Preserve earlier historical evidence with its original date and scope.
For each attempt, record the target/API/ABI, package hash, source and dirty status, profile,
assertion totals and settings comparison. Keep fixture-based native host results distinct from
external WebView and media host cases. For a physical stress run, record the charging state
when observable because USB power can affect the results.

The [1.11.0 roadmap completion report](development/1.11.0-roadmap-completion.md#issue-69--android-release-validation-matrix)
retains the baseline evidence and its source provenance. The dated 1.12.0 observations continue
the same experimental qualification.

## 1.12.0 qualification

The accepted experimental Android scope includes the existing API 23, 34 and 36
emulator evidence and a physical NX769J (API 36). The physical `LifecycleStress`
run passed 147 assertions with 90 seconds in the background, 36 Activity
recreations, one active surface and zero retired native views after collection.
The recorded heap and PSS measurements apply to that bounded run.

On this device, two `ReleaseMatrix` runs each passed 107 assertions.
Requested 60, 90 and 120 Hz modes all reported an active 60 Hz during the
sample animation, including after 120 Hz was selected in Android settings.
The 60 Hz CPU paint and callback data, native injected multi-touch checks,
and their runtime provenance are recorded in the
[#133 qualification results](https://github.com/ProGraMajster/ModernFormsNext/issues/133#issuecomment-7066655798).

A separate observed Gboard session on NX769J entered Latin text and moved
focus with Next. Live CJK and other keyboard providers are covered by
[#537](https://github.com/ProGraMajster/ModernFormsNext/issues/537).
Human TalkBack and physical Android keyboard validation are tracked
in [#59](https://github.com/ProGraMajster/ModernFormsNext/issues/59)
and [#109](https://github.com/ProGraMajster/ModernFormsNext/issues/109).
Actual 90/120 Hz, physical finger multi-touch, more device profiles and
extended stability remain open in [#538](https://github.com/ProGraMajster/ModernFormsNext/issues/538).
GPU completion and scanout were unavailable in the 1.12.0 device measurements.
