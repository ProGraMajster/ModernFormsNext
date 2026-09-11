# Cross-platform sample

> [!WARNING]
> The Android target is **Experimental** in ModernFormsNext 1.10.0. This sample validates the
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

## Hardware command section

The shared page provides Ctrl+S, Ctrl+Shift+S and F1 command cases, with buttons and separate
editor/page/save-as/help counts so keyboard and visual actions can be compared. These actions
update sample state; they do not write files. Deterministic tests, offscreen sample renders and
scoped API 34 native shortcut observations are recorded in the
[Android hardware-input matrix](android-hardware-input.md#validation-matrix).

| Action | Expected command route |
|---|---|
| Focus the first single-line editor and press Ctrl+S | Its local editor Save command increments `editor`. |
| Clear **Enable editor Ctrl+S binding**, refocus that editor, and press Ctrl+S | The unavailable inner binding allows the page Save fallback; `page` increments. |
| Press Ctrl+Shift+S with focus in the page | The distinct page Save As command increments `save-as`. |
| Press F1 with a control selected in the attached page | The Application InputBinding selects Help; the page's RoutedCommand handler increments `help`. |
| Activate **Save (same editor command)** or **Save As (Ctrl+Shift+S)** | The same domain command is used; disabling editor Save also disables its Button. |

Use an eligible keyboard event source and keep focus inside the shared page. Check that Save
and Save As remain distinct, each key-down invokes at most one action, and release does not
invoke a second action. Toggle availability and confirm the documented outer fallback. Repeat
with another focused control, then recreate the Activity and verify registrations do not run
twice. A key repeat is another delivered KeyDown, so repeat-safe commands may run again.

Separately type ordinary and Shift-modified text, use AltGraph/dead-key input where available,
and exercise software IME composition. These must not execute the demo commands or duplicate
text. InputConnection events remain editing input even when they carry modifiers or a device ID.
Hardware text entry requires the IME/text service to commit text; falling back to the Skia native
View alone does not translate printable keys. Record this [text fallback boundary](android-hardware-input.md#handled-input-and-compatibility)
separately from successful shortcuts.
Do not count a virtual `adb input keyevent` as positive hardware-shortcut evidence. An eligible
emulated device still provides emulator evidence, not a physical-keyboard observation.

The Android native handler converts the backend's platform key once and calls the existing
surface resolver; it does not execute commands itself. F1 uses the current command route rather
than a predicate that searches for any page retaining Selected state. Application registrations
belong to the shared page lifetime: registration is the final construction step with rollback,
and disposal removes the exact entry from its captured collection before child cleanup, including
after Application exit or a failing child callback. Activity detach/recreation borrows the page,
preserving state without adding duplicate global bindings. The default input status omits printable
key identities as well as text payloads. Windows uses its normal Form route. There is no additional
Android WindowBase or application/window host.

API 34 emulator shortcuts have scoped native evidence; API 36 positive key delivery remains
unavailable through the tested console transport. Physical hardware keyboards and unavailable device/
layout combinations are **NOT EXECUTED — environment unavailable**. The broader device matrix
and general Android windowing host remain separate work.

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
[Android platform status](platforms/android.md) for the complete 1.10.0 support matrix.
