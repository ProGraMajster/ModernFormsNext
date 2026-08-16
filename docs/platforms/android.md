# Android platform status

> [!WARNING]
> Android support in ModernFormsNext 1.10.0 is **Experimental**. APIs, project structure, and
> runtime behavior may still change. It is not yet recommended for production applications.

Windows remains the primary and most mature ModernFormsNext platform. Android 1.10.0 provides a
real shared-control vertical slice: a .NET Android host can attach one ModernFormsNext `Control`
tree to a SkiaSharp surface and exercise framework layout, rendering, input, and text editing.
It is not yet a general replacement for the Windows `Form` and WindowKit windowing path.

## Supported target

| Item | Current status |
| --- | --- |
| Target framework | `net10.0-android` |
| Minimum Android version | API 23, through `SupportedOSPlatformVersion=23.0` |
| UI host | One `AndroidSkiaHostView` / `SKCanvasView` |
| Framework root | One shared ModernFormsNext `Control` tree through `SkiaControlSurface` |
| Packaging exercised by the repository | APK |
| Production support | Not supported; experimental evaluation only |

The backend has no dependency on .NET MAUI, XAML, AndroidX, or native Android widgets as
framework controls. Android-specific types remain in `ModernFormsNext.WindowKit.Backend.Android`.

## What works today

- The Android backend and the Android targets of the repository samples compile with the .NET 10
  Android workload.
- `AndroidWindowKit.Initialize(...)` registers application context, lifecycle tracking, a
  main-thread dispatcher, permission services, platform information, and diagnostics.
- `AndroidSkiaHostView` renders a real ModernFormsNext control tree through SkiaSharp. Layout,
  invalidation, painting, selection, and hit testing use the same shared controls as Windows.
- Physical pixels are converted to logical pixels once at the Android boundary. Surface resize
  and density changes update the shared layout without applying density twice.
- Multi-touch pointer IDs, deepest-enabled-control hit testing, independent pointer capture,
  tap/click behavior, drag cancellation, and touch scrolling through `ScrollableControl` are
  implemented.
- Basic focus, hardware editing keys, and Android IME integration work for the shared text-box
  path. The input connection supports surrounding text, UTF-16 selection, composition, committed
  text, deletion by code point, arrows, Backspace, Delete, and Enter.
- Activity foreground/background transitions, pause/resume, configuration changes, and activity
  replacement are tracked. The cross-platform sample keeps its process-owned control tree and
  state when the host activity is recreated.
- The shared ThemeManager model, strict JSON stream loader, dynamic theme resources, built-in
  Light/Dark themes, and scheduler-based transitions compile for Android. Background/no-host time
  is excluded by the existing lifecycle integration.
- Manifest-aware runtime permission checks and serialized permission requests are available for
  the permission set documented in [Android manifests and permissions](../android-permissions.md).
- Deterministic tests cover permission mapping and queues, lifecycle planning, density conversion,
  surface state, text input state, shared pointer routing, scrolling, and sample configuration.
- Debug deployment through Visual Studio and repository `adb` scripts is supported. The
  cross-platform sample also has a Release configuration that embeds assemblies and enables AOT.

## Important limitations

- Android does not implement the general `Application.Run(Form)` startup path, `IWindowingPlatform`,
  or `IWindowImpl`. Applications must currently provide an Android activity and attach a control
  root explicitly.
- Android does not yet register a system light/dark or reduced-motion ThemeManager provider.
  `ThemeVariant.System` therefore uses the explicit Light/Dark apply fallback. This limitation does
  not imply runtime parity: startup, switching, storage streams, and visual transitions still need
  emulator/device validation for a release.
- Only one framework control surface is exercised. Multiple framework windows, popups, owned
  windows, and desktop-style window management are not available.
- Clipboard, native dialogs, file/folder pickers, drag and drop, notification delivery, camera and
  microphone capture, media, WebView, sharing, and cursor services are not implemented as complete
  Android WindowKit services. The permission service grants authorization only; it does not provide
  the corresponding device feature.
- Accessibility semantics and screen-reader integration are not complete.
- Focus, hardware keyboard behavior, and IME handling cover the current shared text-control path,
  but have not reached desktop parity across every control, keyboard, language, and vendor IME.
- Rotation and configuration changes are handled by the sample host, but general host-independent
  lifecycle, state restoration, safe-area/inset, and configuration policies are still evolving.
- Density conversion is implemented for the shared surface, but Android does not yet have complete
  platform-wide DPI, font-scaling, and system-UI integration.
- Runtime permission requests require the host activity to forward the platform callback. Android
  14 selected-photo access is not represented as a partial grant.
- The repository configures and validates APK output. Android App Bundle publishing and store
  submission are not documented as supported release paths.
- The cross-platform sample exercises Release AOT. General trimming compatibility is not declared,
  and arbitrary applications, controls, and reflection-based dynamic resource references have not
  been validated as trim-safe.
- Automated tests validate most backend rules without an emulator. A physical-device matrix,
  production performance targets, long-running stability tests, and broad device/IME/accessibility
  coverage are not yet part of the release gate.

## Requirements

- The .NET SDK selected by [`global.json`](../../global.json) (`10.0.201`, with .NET 10 feature-band
  roll-forward enabled).
- The .NET Android workload: `dotnet workload install android` when it is not installed.
- An Android SDK, matching build/platform tools, and a compatible JDK.
- An authorized USB-debugging device or an existing Android Virtual Device for deployment.
- PowerShell for the repository Android tooling scripts.

The current sample targets API 23 and later. The installed .NET Android reference pack selects the
target SDK used for compilation.

## Run the cross-platform sample

The recommended Android evaluation target is
[`samples/ModernFormsNext.CrossPlatform.Sample`](../../samples/ModernFormsNext.CrossPlatform.Sample/README.md).
It is one multi-target project whose `App`, `MainPage`, state, layout, and interaction logic compile
for both Windows and Android.

From Visual Studio, open `ModernFormsNext.slnx`, select the cross-platform sample, select
`net10.0-android` and a device or AVD, then use F5 or Ctrl+F5.

From PowerShell:

```powershell
.\scripts\android\Resolve-AndroidSdk.ps1
.\scripts\android\Get-AndroidDevices.ps1 -IncludeUnavailable
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial> -ClearLogcat
```

The smaller [`ModernFormsNext.Android.SmokeTest`](../../samples/ModernFormsNext.Android.SmokeTest/README.md)
is a native technical host for lifecycle, manifest, and permission checks. It is not the default
application template and does not demonstrate the shared framework control tree.

## Project shape

The cross-platform sample demonstrates the current arrangement:

```text
ModernFormsNext.CrossPlatform.Sample/
|-- App.cs                         # shared application state and root
|-- MainPage.cs                    # shared ModernFormsNext controls
|-- Shared/                        # shared service contracts and state
`-- Platforms/
    |-- Windows/                   # Form/Application.Run host
    `-- Android/                   # Activity, AndroidSkiaHostView adapter, manifest
```

Android startup initializes `AndroidWindowKit`, creates an `AndroidSkiaHostView`, and connects it to
the shared root with `SkiaControlSurface`. This explicit adapter is required until Android has a
complete WindowKit windowing implementation.

## Build and package

Build the Android sample directly:

```powershell
dotnet build .\samples\ModernFormsNext.CrossPlatform.Sample\ModernFormsNext.CrossPlatform.Sample.csproj `
  --framework net10.0-android --configuration Debug
```

Repository deployment scripts create a signed standalone APK by embedding assemblies. The sample's
Release configuration enables Android AOT and must remain part of release validation. `-NoAot` is a
diagnostic comparison, not equivalent release coverage.

## Troubleshooting

- If the Android target is unavailable, run `dotnet workload list` and install the workload into
  the SDK/Visual Studio instance used for the build.
- If no device appears, run `Get-AndroidDevices.ps1 -IncludeUnavailable`; authorize USB debugging,
  wait for boot completion, or specify the intended serial when several devices are connected.
- If only the Windows launch target is visible in Visual Studio, select `net10.0-android` explicitly
  and remove a stale per-user active-framework selection. Do not add a desktop
  `launchSettings.json` workaround.
- Android Hot Reload is intentionally disabled for this project-system combination; normal F5
  debugging remains available.
- Release builds can emit `XA0141` for the current HarfBuzzSharp native assets and 16 KB page-size
  compatibility. Treat it as a known dependency warning, not as proof of full device compatibility.
- Use `Watch-ModernFormsNextLogcat.ps1` and `Collect-AndroidDiagnostics.ps1` for tagged runtime logs,
  package/activity state, device properties, and host emulator failures.

See [Android development](../android-development.md), [Android and adb](../android-adb.md), and
[Android backend internals](../android-backend.md) for the detailed workflows.

## Next stages

Planned work is capability-based and has no promised completion date:

- implement the WindowKit windowing contracts and align Android startup with the framework app model;
- expand focus, keyboard, IME, accessibility, lifecycle, density, and configuration coverage;
- add capability-shaped Android services such as clipboard, pickers, dialogs, sharing, and drag/drop;
- validate trimming, AOT, App Bundle/store packaging, performance, and a broader device matrix;
- keep platform behavior behind shared contracts without moving Android APIs into framework code.

Track current work or report Android-specific issues in the
[ModernFormsNext issue tracker](https://github.com/ProGraMajster/ModernFormsNext/issues).
