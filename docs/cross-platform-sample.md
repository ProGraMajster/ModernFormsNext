# Cross-platform sample

`samples/ModernFormsNext.CrossPlatform.Sample` is intentionally one project, similar in source
organization to a .NET MAUI application but without MAUI, XAML, AndroidX, or native controls that
imitate framework widgets.

## Project shape

```text
ModernFormsNext.CrossPlatform.Sample/
|-- ModernFormsNext.CrossPlatform.Sample.csproj
|-- App.cs
|-- MainPage.cs
|-- Shared/
|-- Platforms/
|   |-- Windows/
|   `-- Android/
|-- Assets/
`-- README.md
```

The project targets `net10.0-windows` and `net10.0-android`. Conditional item groups exclude the
opposite platform host. `App`, state, the platform-service contract, page construction, controls,
layout, events, and rendering behavior are compiled from the same files for both targets.

## One framework root

`App.Root` is one `MainPage : Control`. It contains real ModernFormsNext `Label`, `TextBox`,
`CheckBox`, and `Button` instances. Windows adds that root directly to `WindowsAppHost : Form`.
Android's activity creates only `AndroidSkiaHostView`; `AndroidAppHost` connects the native surface
to `SkiaControlSurface`, which runs the framework control paint and input pipeline.

The Android `SampleApplication` owns `App`, while each `MainActivity` owns only its short-lived
surface adapter. Rotation therefore recreates the activity and view but reuses the same shared
root and state. No static activity reference is retained.

## Run

Windows:

```powershell
.\scripts\windows\Run-CrossPlatformSample.ps1
```

Android:

```powershell
.\scripts\android\Resolve-Adb.ps1
.\scripts\android\Get-AndroidDevices.ps1 -IncludeUnavailable
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial> -ClearLogcat
```

Add `-FollowLogcat` to keep the stable `ModernFormsNext` log tag visible after launch. See
[Android and adb](android-adb.md) for device selection and emulator diagnostics.

## Manual checks

1. Confirm platform, OS, backend, dispatcher, host lifecycle, logical size, and render count.
2. Activate the shared button and verify its counter.
3. Edit the `TextBox`, including non-ASCII text and emoji, and verify the bound label.
4. Toggle the `CheckBox` and verify it enables/disables the shared action.
5. Post through the dispatcher and verify the callback counter.
6. On Android, request camera permission and test grant, denial, and permanent denial.
7. Rotate Android while text and counters are changed; state should survive activity recreation.
8. Background/foreground the application and verify lifecycle plus redraw.

## Honest limitation

This proves one real control tree, not full Android window parity. Android still lacks general
`Application.Run(Form)`, multiple windows, complete focus/accessibility semantics, platform
dialogs, clipboard, file pickers, drag-and-drop, and several backend services. Windows remains the
primary and best-supported target.
