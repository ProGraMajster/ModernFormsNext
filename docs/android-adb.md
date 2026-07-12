# Android and adb

## Resolve the tool

```powershell
.\scripts\android\Resolve-Adb.ps1
```

Resolution order covers an existing `adb` command, `ANDROID_SDK_ROOT`, `ANDROID_HOME`, the normal
per-user Android SDK, the Visual Studio Android SDK under Program Files (x86), and Android Studio
registry keys. The script prints the selected absolute path and `adb version`. It does not modify
PATH.

## Devices

```powershell
.\scripts\android\Get-AndroidDevices.ps1 -IncludeUnavailable
```

`device` is ready. `unauthorized` requires accepting the debugging prompt on the device. `offline`
usually requires reconnecting/restarting the device or adb server. When multiple ready devices
exist, deployment requires an explicit `-DeviceId` so the script never chooses silently.

## Emulators

```powershell
.\scripts\android\Get-AndroidEmulators.ps1
.\scripts\android\Start-AndroidEmulator.ps1 -Name <avd-name>
```

The first command locates `emulator.exe` beside the resolved SDK and lists installed AVDs. The
second starts an existing AVD and waits for `sys.boot_completed`. It never creates or modifies an
AVD. An SDK may validly contain adb but no emulator package or AVD; the diagnostic distinguishes
those cases.

## Install and launch

```powershell
.\scripts\android\Run-CrossPlatformSample.ps1 `
  -Configuration Debug `
  -DeviceId <serial> `
  -ClearLogcat `
  -FollowLogcat
```

The script builds the Android target unless `-NoBuild` is supplied, finds the newest signed APK,
installs with `adb install -r`, and launches the stable component:

```text
com.programajster.modernformsnext.sample/
com.programajster.modernformsnext.sample.MainActivity
```

Use `-Reinstall` only when package data should be removed before installation.

## Logcat

```powershell
.\scripts\android\Watch-ModernFormsNextLogcat.ps1 -DeviceId <serial> -Clear
```

When the process is running, output is filtered by PID and the `ModernFormsNext` tag. Before launch,
the script falls back to tag-only filtering. Press Ctrl+C to stop.
