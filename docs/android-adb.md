# Android and adb

Repository scripts under `scripts/android` provide a deterministic build/install/launch path that
is independent of Visual Studio. They never choose between multiple ready devices silently and do
not erase package or AVD data unless an explicit destructive switch is supplied.

## Resolve the SDK and tools

```powershell
.\scripts\android\Resolve-AndroidSdk.ps1
.\scripts\android\Resolve-Adb.ps1
```

Resolution checks, in order, an explicit `-SdkRoot`, `ANDROID_SDK_ROOT`, `ANDROID_HOME`, adb on
PATH, the normal per-user SDK, the Visual Studio SDK under Program Files (x86), and Android Studio
registry entries. The scripts print absolute paths and do not edit PATH.

On Windows, emulator 35.5.x can invoke its `emulator-check.exe` child without correctly quoting an
SDK path under Program Files (x86). The module uses the existing 8.3 executable path for the
emulator child-process boundary when available; adb and all user arguments still use normal
argument-array invocation. No SDK files are copied or relocated.

Run pure parser/argument tests without a device:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\android\Test-AndroidTooling.ps1
```

## Devices and boot readiness

```powershell
.\scripts\android\Get-AndroidDevices.ps1 -IncludeUnavailable
.\scripts\android\Wait-AndroidDevice.ps1 -DeviceId <serial> -TimeoutSeconds 300
```

`device` is usable. `unauthorized` requires accepting the USB-debugging prompt. `offline` commonly
requires reconnecting the device or explicitly passing `-RestartServer`. The wait command checks
both adb state and `sys.boot_completed`; it has a bounded timeout and accepts a .NET
`CancellationToken` when called from another PowerShell script.

When several devices are ready, every install, launch, log, and diagnostic command requires
`-DeviceId`. This prevents accidental deployment to the wrong physical phone.

## Existing AVDs and emulator startup

```powershell
.\scripts\android\Get-AndroidAvds.ps1
.\scripts\android\Start-AndroidEmulator.ps1 -Name pixel_7_api_34
```

The start command opens the existing AVD and waits for boot completion. It never creates an AVD.
Useful explicit options are:

- `-ColdBoot` disables snapshot loading for this launch;
- `-GpuMode swiftshader_indirect` is a diagnostic fallback when host GPU integration fails;
- `-DisableAcceleration` adds `-accel off` only for a very slow diagnostic boot;
- `-NoWindow` runs a headless emulator;
- `-RestartAdb` restarts the adb server before startup;
- `-WipeData` erases the AVD only when that switch is explicitly present.

Hardware acceleration is strongly recommended. If Visual Studio reports that acceleration is not
available, enable virtualization in firmware and the supported Windows hypervisor path, then cold
boot the AVD. Software rendering can validate packaging in a constrained environment, but it is
much slower and is not representative rendering-performance evidence. An x86_64 AVD may still be
unusable without a hypervisor; install or enable the emulator hypervisor for normal development.

If the emulator exits before connecting, inspect `%LOCALAPPDATA%\Temp\AndroidEmulator`, rerun with
`-ColdBoot -GpuMode swiftshader_indirect`, and collect repository diagnostics. Do not jump directly
to `-WipeData`; preserve developer state until corruption is actually established.

## Build, install, and launch separately

```powershell
$apk = .\scripts\android\Build-CrossPlatformSample.ps1 -Configuration Debug
.\scripts\android\Install-CrossPlatformSample.ps1 `
  -ApkPath $apk.FullName -DeviceId <serial>
.\scripts\android\Launch-CrossPlatformSample.ps1 -DeviceId <serial>
```

The build script creates a signed, standalone APK. Debug fast deployment is an IDE feature, so the
script overrides `EmbedAssembliesIntoApk=true`. Installation uses `adb install -r` and preserves
application data. `-UninstallFirst` removes the package and its data only when explicitly supplied;
`-AllowDowngrade` adds adb's downgrade flag. Runtime permissions are never silently granted.

Launch uses `am start -W` with the stable component and verifies that `pidof` reports a surviving
sample process. `-ForceStop` is optional and explicit.

## Complete run

For a connected device:

```powershell
.\scripts\android\Run-CrossPlatformSample.ps1 `
  -Configuration Debug -DeviceId <serial> -ClearLogcat
```

For an existing AVD:

```powershell
.\scripts\android\Run-CrossPlatformSample.ps1 `
  -AvdName pixel_7_api_34 -ColdBoot -GpuMode auto -FollowLogcat
```

The complete path waits for boot, builds (unless `-NoBuild`), installs, launches, verifies the PID,
and optionally follows logcat. `-WipeAvdData` is the complete-run equivalent of the deliberately
destructive emulator switch.

## Logcat and diagnostics

```powershell
.\scripts\android\Watch-ModernFormsNextLogcat.ps1 -DeviceId <serial> -Clear
.\scripts\android\Watch-ModernFormsNextLogcat.ps1 -DeviceId <serial> -AllProcessLogs
.\scripts\android\Collect-AndroidDiagnostics.ps1 -DeviceId <serial>
.\scripts\android\Collect-AndroidDiagnostics.ps1 -HostOnly
```

Normal log watching filters by both process ID and the stable `ModernFormsNext` tag. If the process
has already crashed, tag and `AndroidRuntime` fallback filters remain available. `-AllProcessLogs`
is intentionally noisier.

Diagnostics are written under ignored `artifacts/android/<timestamp>-<serial>` by default and
include device properties, package/activity/window state, memory information, full threadtime
logcat, AVD identity, and recent host emulator crash attachments. `-IncludeBugReport` additionally
requests Android's larger bugreport archive and may take several minutes.

Use `-HostOnly` when the emulator crashes before appearing in `adb devices`; it captures SDK,
emulator version/acceleration output, and recent host crash files without requiring a serial.
