# Cross-platform sample

> [!WARNING]
> The Android target is **Experimental** in ModernFormsNext 1.9.0. This sample validates the
> current shared-control vertical slice; it does not represent complete Android platform parity.

`samples/ModernFormsNext.CrossPlatform.Sample` is intentionally one application project, organized
like a .NET MAUI project but without MAUI, XAML, AndroidX, or a second shared library.

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

The project targets `net10.0-windows` and `net10.0-android`. Conditional item groups exclude only
the opposite platform directory. `App`, persistent state, the platform-service contract,
`MainPage`, control construction, layout, event behavior, and the core Skia control adapter all
remain in this project and are compiled for both targets. Platform code is confined to
`Platforms/Windows` and `Platforms/Android`.

## One application and one framework root

`App.Root` is one `MainPage : Control`. It contains actual ModernFormsNext controls, including a
scrollable area, labels, single-line and multiline text boxes, Unicode and emoji content,
checkboxes, buttons, and a flow layout. It exercises focus, resizing, scrolling, dispatching,
lifecycle diagnostics, IME composition, and camera-permission states.

Windows attaches that root to `WindowsAppHost : Form`. Android creates one native
`AndroidSkiaHostView`; `AndroidAppHost` connects it to `SkiaControlSurface`, which executes the same
framework layout, rendering, hit testing, selection, keyboard, IME, and pointer pipeline. Android
does not construct an alternate page, native `EditText`, or demonstration renderer that bypasses
`Control`.

Android forwards every stable pointer ID. The shared surface captures the deepest enabled control,
keeps small movement tap-eligible, raises exactly one click for a valid release, and cancels the
child press when movement becomes a drag. A drag inside scrollable content updates the real
`ScrollableControl` scrollbars and clamps at their limits; it is not a second Android-only scroll
model. Density conversion happens before routing, so hit tests and the drag threshold use logical
pixels. Touch moves do not synthesize hover.

`SampleApplication` owns the shared `App`. Each `MainActivity` owns only its current view/adapter,
and the backend retains activities weakly. Configuration changes refresh density and surface size.
If Android recreates the activity for another reason, disposal detaches the old surface while the
process-owned root, edited text, counters, and state are reused by the new host.

## Run Windows

```powershell
.\scripts\windows\Run-CrossPlatformSample.ps1
```

Resize the window to exercise the same scrollable layout used by Android.

## Run Android from Visual Studio

1. Open `ModernFormsNext.slnx`.
2. Select the cross-platform sample as startup project.
3. Select `net10.0-android`; Visual Studio then exposes the normal .NET for Android device/AVD
   selector.
4. Use F5 for managed debugging or Ctrl+F5 for deploy/run.

The last framework is persisted in the ignored `.csproj.user` file. If only the Windows launch
target is visible, replace a stale `ActiveDebugFramework=net10.0-windows` selection by choosing
`net10.0-android` (or close the project, update that per-user value, and reload). Do not add a
desktop `launchSettings.json`: deployment and device discovery come from the installed .NET for
Android workload.

Android-only Hot Reload is disabled because Visual Studio 18.7 has no applicable
`IProjectHotReloadLaunchProvider` after the Android SDK removes launch profiles. This does not
disable F5 debugging and does not affect the Windows target.

## Run Android from PowerShell

```powershell
.\scripts\android\Resolve-AndroidSdk.ps1
.\scripts\android\Get-AndroidDevices.ps1 -IncludeUnavailable
.\scripts\android\Run-CrossPlatformSample.ps1 -DeviceId <serial> -ClearLogcat
```

Or start an existing AVD as part of the sequence:

```powershell
.\scripts\android\Run-CrossPlatformSample.ps1 `
  -AvdName <avd-name> -ColdBoot -FollowLogcat
```

See [Android and adb](android-adb.md) for separate build/install/launch commands, timeouts,
software-rendering diagnostics, and artifact collection.

## Manual validation checklist

1. Confirm platform, OS, backend, activity/window lifecycle, logical size, density, attachment,
   active pointer, focus, and render counters in the optional diagnostics area.
2. Activate the shared action and dispatcher buttons. Verify the separate **Action**, **Service
   invocation**, and **Service result** labels distinguish click receipt, service dispatch, and
   completion or controlled failure.
3. Edit both text boxes using Polish text (`zażółć gęślą jaźń`), emoji, combining input, and an IME
   with active composition where available. Verify no composition text is duplicated.
4. Move the caret, select text, use Backspace/Delete/Enter/arrows, and confirm emoji are not split.
5. Toggle the action and diagnostics checkboxes.
6. Resize Windows or rotate Android, then scroll to the permission and long-content sections.
7. Background and foreground Android; confirm redraw and that pointer capture is not left active.
8. Use **Check camera** first and confirm it does not show a dialog.
9. Use **Request camera** and test grant, denial, and permanent denial. The sample declares only
   camera; no broad storage or phone-state permission should appear in the merged manifest.
10. Use **Open app settings** only after the explicit button action, return to the app, and recheck.
11. Recreate the activity while the process remains alive and verify shared text/counters persist.
12. Press a button and drag beyond the tap threshold before releasing; verify the content scrolls
    and the button does not click. Test a small-jitter tap, direct scrollbar dragging, limits in both
    directions, and two simultaneous touches on distinct controls.

## Scope

This sample proves a real shared-control vertical slice, not complete Android parity. Android still
lacks general `Application.Run(Form)`, multiple framework windows, full accessibility semantics,
native dialogs, clipboard, file pickers, drag-and-drop, and several backend services. Windows
remains the primary and best-supported target. See the canonical
[Android platform status](platforms/android.md) for the complete 1.9.0 support matrix.
