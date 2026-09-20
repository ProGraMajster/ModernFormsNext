# Android release validation matrix

Android remains an **experimental single-surface host**. Issue [#69](https://github.com/ProGraMajster/ModernFormsNext/issues/69)
collects repeatable evidence for the existing rendering, input, accessibility, lifecycle and
animation paths. It does not declare general Android production, store, GPU or trim safety.

## Evidence types and acceptance

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

The bounded stress records managed heap, process PSS and weak references to retired native views
after collection on each of 12 Activity recreations. Review growth and retained objects; a short
plateau or zero retired wrappers does not prove long-run leak freedom or native cache health.
No cache/GPU counters are invented when the production profiler does not expose them.

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

Installation uses `adb install --no-streaming -r`: sample data stays in place and vendor incremental
installation is avoided. UiAutomation temporarily owns the accessibility connection. The full matrix
requests modes and orientation only for its own window, keeps that window awake, and restores its
flags, orientation, animation preference and control visibility. The optional `SystemReducedMotion`
scenario temporarily writes only `global/animator_duration_scale=0`; PowerShell restores its original
value, including an absent setting, in `finally`. Keep the script running until restoration completes.
If interrupted externally, compare the archived settings and restore that one original value before
accepting the run. Other scenarios do not write global settings.
Instrumentation has a 180-second host deadline (`-TimeoutSeconds` can explicitly adjust it). A timeout
fails the run and stops only the selected sample process before checking/restoring settings.

## Scenario boundaries

| Lane | What the runner exercises | What it does not establish |
|---|---|---|
| IME / #62 | Native `InputConnection` composition/commit, Polish Unicode, emoji deletion, four canonical editors, multiline input | Gboard interaction, CJK composition, vendor spans, handwriting, physical keyboard layouts |
| Multi-touch | Real synthetic `MotionEvent` pointer IDs through the native view; independent release and cancel, using native accessibility bounds including insets | Physical finger ergonomics, every drag/scroll gesture |
| Rendering | Ellipse and Bezier path beside text; parallel rotation/scale; native screenshots and production frame snapshots | Pixel-perfect raster quality, GPU timing, every theme/control, native scanout smoothness |
| Lifecycle / #63 | Home, 20 seconds background with no frame work, foreground time rebasing, landscape/portrait viewport update, 12 Activity recreations, protocol Intent | OS process death, reboot persistence, screen-off matrix, multi-window/native host integration |
| Accessibility / #59 | Existing Phase 4 native virtual-node/action/text/grid fixture | New manual TalkBack speech or gesture evidence |
| Reduced motion | Application preference and a separate actual Android zero-scale startup probe | Hot toggles/fractional scales through Settings UI or every vendor's accessibility controls |
| Reliability | Bounded frame counters, heap/PSS and retired-view observations | Overnight/thermal/battery stress or proof of leak freedom |
| Packaging | Standalone APK runtime and AAB construction, declared AOT/trim properties | Play submission, distribution signing, full trimming, NativeAOT or arbitrary application trim safety |
| Future hosts | **BLOCKED** by #60 (native views/WebView/media); optional GPU backend belongs to #46 | These deferred lanes must not be implemented just to complete this matrix |

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
Unsupported future-host lanes remain blocked, rather than passing by omission.

Current dated results and their exact source/PR provenance are recorded in the
[1.11.0 roadmap completion report](development/1.11.0-roadmap-completion.md#issue-69--android-release-validation-matrix).
Missing broader lanes keep #69 **PARTIAL**. This matrix supplies a reviewed experimental Android
checkpoint; the final release audit must assess it against the existing release policy and must not
silently promote it to complete Android production support.
