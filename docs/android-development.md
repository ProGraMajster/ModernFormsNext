# Android development

> [!WARNING]
> Android support in ModernFormsNext 1.9.0 is **Experimental**. APIs, project structure, and
> runtime behavior may change, and production use is not yet recommended. Start with the
> [Android platform status](platforms/android.md).

Android support is an experimental vertical slice. Windows remains the primary and most complete
runtime. The cross-platform sample is intentionally a normal .NET multi-target project; it does
not use MAUI, XAML, AndroidX, or native Android controls as framework widgets.

## Requirements

- the .NET SDK selected by `global.json` (`10.0.201`, with .NET 10 feature-band roll-forward);
- the .NET Android workload (`dotnet workload install android` when it is not installed);
- an Android SDK with platform tools, build tools, and a platform matching the installed workload;
- the JDK selected by .NET for Android/Visual Studio;
- either an authorized USB-debugging device or an existing Android Virtual Device (AVD);
- Windows PowerShell 5.1 or newer for the repository scripts.

For a predictable first emulator run, use an x86_64 API 34 phone AVD with at least 4 GB RAM and
hardware acceleration enabled. API 34 is not a target-SDK requirement; it is a conservative test
device for this experimental backend. The project currently uses API 23 as `minSdkVersion`, while
the installed .NET Android reference pack selects `targetSdkVersion`.

The scripts do not install workloads, accept licenses, create AVDs, edit PATH, or change global
Visual Studio settings. `Resolve-AndroidSdk.ps1` checks an explicit `-SdkRoot`, Android environment
variables, PATH, the standard per-user and Visual Studio SDK locations, and Android Studio registry
keys. Paths containing spaces are passed as single process arguments.

```powershell
dotnet workload list
.\scripts\android\Resolve-AndroidSdk.ps1 -RequireEmulator
.\scripts\android\Test-AndroidTooling.ps1
```

If Android licenses or components are missing, use the Visual Studio Installer/Android SDK Manager
that owns the selected SDK. Do not mix build-tools directories from unrelated SDK installations.

## Build modes

Visual Studio Debug uses .NET for Android fast deployment:

- portable PDBs and managed debugging are enabled;
- AOT is disabled;
- `EmbedAssembliesIntoApk=false` allows the IDE to deploy changed assemblies efficiently.

A raw `adb install` cannot use those external fast-deployment assemblies. The repository build
script therefore invokes `SignAndroidPackage` and overrides `EmbedAssembliesIntoApk=true` to create
a standalone signed APK:

```powershell
.\scripts\android\Build-CrossPlatformSample.ps1 -Configuration Debug
```

Release embeds assemblies and enables Android AOT:

```powershell
.\scripts\android\Build-CrossPlatformSample.ps1 -Configuration Release
```

Use `-NoAot` only as an explicit diagnostic comparison. A successful non-AOT Release build is not
a substitute for the required Release/AOT validation.

The sample references `System.Formats.Nrbf 10.0.5` explicitly. `System.Drawing.Common` brings
`System.Private.Windows.Core` into the Android asset graph, but that compatibility assembly's AOT
dependency is not currently selected transitively. Removing the explicit reference makes Mono AOT
fail while loading that graph.

Android 16 build tools may also emit `XA0141` for the current HarfBuzzSharp 7.3.0.1 native assets,
which do not advertise 16 KB page-size compatibility. This is an upstream package warning and must
remain visible until that native dependency is upgraded.

## Visual Studio F5 and Ctrl+F5

Set `ModernFormsNext.CrossPlatform.Sample` as the startup project, select `net10.0-android`, and
then select a concrete authorized device/AVD in Visual Studio's standard debug target selector. F5
starts the managed debugger; Ctrl+F5 builds, deploys, and launches without attaching it. The
project is a normal .NET for Android application (`OutputType=Exe`, `AndroidApplication=true`) and
uses the SDK's `Mobile`, `Android`, and `AndroidApplication` project capabilities; it does not need
a desktop launch profile or a custom device selector.

Visual Studio persists the last selected target framework in the ignored per-user
`ModernFormsNext.CrossPlatform.Sample.csproj.user` file. If the toolbar shows only the Windows
executable or no Android device list:

1. confirm the Android workload is installed in the same Visual Studio instance;
2. make the cross-platform sample the startup project;
3. select `net10.0-android` in the framework selector;
4. close/reload the project if the per-user state still contains
   `<ActiveDebugFramework>net10.0-windows</ActiveDebugFramework>`;
5. choose an `adb devices` entry in state `device` (not `offline` or `unauthorized`).

The repository's ADB launch path does not hard-code a managed activity class. After installation it
asks Android to resolve the package's launcher intent from the merged manifest, then passes that
installed component to `am start -W`. The project uses the current plural
`AndroidPackageFormats=apk` property; Microsoft documents the singular property as deprecated in
the [.NET for Android build-property reference](https://learn.microsoft.com/dotnet/android/building-apps/build-properties).

The Android target deliberately sets `SupportsHotReload=false`. This is narrowly scoped and does
not disable debugging. The reason is a project-system capability mismatch observed with Visual
Studio 18.7:

1. the generic .NET SDK advertises `SupportsHotReload`;
2. the .NET Android SDK removes the `LaunchProfiles` capability;
3. Visual Studio attempts to import the launch-profile-based
   `Microsoft.VisualStudio.ProjectSystem.HotReload.IProjectHotReloadLaunchProvider`;
4. no provider satisfies the Android capability constraints, so the import finds zero exports.

Adding a desktop `launchSettings.json` profile with `commandName: Project` is not a valid Android
fix and bypasses the platform deployment contract. Keeping the Android workload and disabling only
the unsupported Hot Reload capability preserves normal F5/Ctrl+F5 deployment and managed debug.

## Runtime architecture

`AndroidWindowKit.Initialize` owns application context, weak activity tracking, the main-thread
dispatcher, permission coordination, and diagnostics. `AndroidSkiaHostView` owns one native Skia
view, physical/logical density conversion, attachment, resize, touch, hardware keys, IME, and
coalesced invalidation. Core `SkiaControlSurface` renders and routes those events through the real
ModernFormsNext `Control` tree.

Activity lifecycle and native surface attachment are separate. A render requested while paused or
detached remains coalesced until the surface is both attached and resumed. Pause, stop, detach, and
Android pointer cancellation release framework pointer capture. Activity recreation disposes only
the short-lived host; `SampleApplication` retains the shared `App`, state, and `MainPage` tree.

The host forwards every stable Android pointer ID. Core hit testing captures the deepest control,
generates one click only for a valid tap, cancels pressed state when a drag wins, and hands content
drags to the nearest `AutoScroll` ancestor. All gesture distances are logical pixels after density
conversion. Multiple pointers have independent routing state; touch moves deliberately do not
manufacture hover.

The IME bridge exposes surrounding text, UTF-16 selection, active composition, commit, finish,
deletion, and cursor movement. Code-point deletion is converted without splitting surrogate pairs;
the framework editor additionally deletes complete text elements for emoji/combining safety. There
is no hidden Android `EditText` and no second Android-specific application model.

## Diagnostics

The stable backend logcat tag is `ModernFormsNext`. Lifecycle, initialization, resize, failure, and
disposal messages are logged; per-frame logging stays opt-in to avoid flooding logcat.
`SkiaControlSurface` also accepts an optional pointer diagnostic sink. It is `null` by default, so
normal rendering and input do not allocate diagnostic strings. When enabled, each transition
reports pointer ID, logical coordinates, hit target, capture, gesture owner, click, and cancellation.

```powershell
.\scripts\android\Watch-ModernFormsNextLogcat.ps1 -DeviceId <serial> -Clear
.\scripts\android\Collect-AndroidDiagnostics.ps1 -DeviceId <serial>
```

See [Android and adb](android-adb.md), [Android backend](android-backend.md),
[Android permissions](android-permissions.md), and [the cross-platform sample](cross-platform-sample.md).
