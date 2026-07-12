# Android development

## Requirements

- the repository's .NET 10 SDK and Android workload;
- an Android SDK containing `platform-tools/adb`;
- either a USB-debugging device or an installed Android Virtual Device;
- Windows PowerShell 5.1 or newer.

The scripts do not install an SDK, change machine PATH, create an AVD, or accept device licenses.
They inspect the current shell, common Visual Studio/Android Studio SDK locations, and Android
Studio registry entries, then report actionable failures.

## Build

```powershell
dotnet build .\samples\ModernFormsNext.CrossPlatform.Sample\ModernFormsNext.CrossPlatform.Sample.csproj `
  -f net10.0-android -c Debug
```

For Release/AOT, the sample explicitly references `System.Formats.Nrbf 10.0.5`. The current
`System.Drawing.Common` dependency brings `System.Private.Windows.Core` into the Android asset
graph, but does not select `System.Formats.Nrbf` transitively for AOT. Removing that explicit
reference currently causes Mono AOT to fail while loading the Windows compatibility assembly.

Android 16's build tools may warn that the existing `HarfBuzzSharp.NativeAssets.Android 7.3.0.1`
binary does not advertise 16 KB page-size compatibility. This is an upstream native-asset warning,
not proof that a device run was performed; keep it visible until the dependency is updated.

## Backend boundary

`AndroidWindowKit.Initialize` owns application context, activity tracking, the main-thread
dispatcher, permission coordination, and diagnostics. `AndroidSkiaHostView` owns one native view,
density, resize, touch, IME, and surface lifecycle. Core `SkiaControlSurface` owns the adaptation to
the actual ModernFormsNext `Control` tree.

Keep Android APIs under platform host/backend directories. Shared `App` and `MainPage` must not
reference `Activity`, `View`, `MotionEvent`, or `InputMethodManager`.

## Diagnostics

The stable logcat tag is `ModernFormsNext`. Lifecycle, initialization, resize, failures, and
disposal are logged. Per-render logging is opt-in through
`AndroidWindowKitOptions.EnableDetailedDiagnostics` to avoid flooding logcat.

See [Android and adb](android-adb.md) and [Android permissions](android-permissions.md).
