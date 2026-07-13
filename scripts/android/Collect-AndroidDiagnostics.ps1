[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$SdkRoot,
    [string]$OutputDirectory,
    [switch]$HostOnly,
    [switch]$IncludeBugReport
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sdk = Resolve-AndroidSdkRoot -SdkRoot $SdkRoot
$adb = Resolve-AndroidTool -Name adb -SdkRoot $sdk
if (-not $HostOnly) {
    $device = Select-AndroidDevice -Device @(Get-AndroidDevice -SdkRoot $sdk -IncludeUnavailable) -Serial $DeviceId
    $DeviceId = $device.Serial
}
elseif (-not $DeviceId) {
    $DeviceId = 'host-only'
}

if (-not $OutputDirectory) {
    $safeSerial = $DeviceId -replace '[^A-Za-z0-9_.-]', '_'
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\android\$((Get-Date).ToString('yyyyMMdd-HHmmss'))-$safeSerial"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Save-AdbOutput {
    param([string]$Name, [string[]]$Arguments)
    $path = Join-Path $OutputDirectory $Name
    $output = @(& $adb -s $DeviceId @Arguments 2>&1)
    [IO.File]::WriteAllLines($path, [string[]]$output)
}

$package = 'com.programajster.modernformsnext.sample'
$hostInformation = @(
    "CollectedUtc=$([DateTime]::UtcNow.ToString('O'))",
    "AndroidSdk=$sdk",
    "Adb=$adb",
    "OS=$([Environment]::OSVersion.VersionString)",
    "ProcessorCount=$([Environment]::ProcessorCount)"
)
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'host.txt'), [string[]]$hostInformation)
$emulator = Resolve-AndroidTool -Name emulator -SdkRoot $sdk
if (Test-Path -LiteralPath $emulator -PathType Leaf) {
    $emulatorVersion = @(& $emulator -version 2>&1)
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'emulator-version.txt'), [string[]]$emulatorVersion)
    $acceleration = @(& $emulator -accel-check 2>&1)
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'emulator-acceleration.txt'), [string[]]$acceleration)
}

if (-not $HostOnly) {
    $deviceList = @(& $adb devices -l 2>&1)
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'devices.txt'), [string[]]$deviceList)
    Save-AdbOutput 'getprop.txt' @('shell', 'getprop')
    Save-AdbOutput 'package.txt' @('shell', 'dumpsys', 'package', $package)
    Save-AdbOutput 'activity.txt' @('shell', 'dumpsys', 'activity', 'activities')
    Save-AdbOutput 'window.txt' @('shell', 'dumpsys', 'window', 'windows')
    Save-AdbOutput 'meminfo.txt' @('shell', 'dumpsys', 'meminfo', $package)
    Save-AdbOutput 'logcat.txt' @('logcat', '-d', '-v', 'threadtime')

    if ($DeviceId -like 'emulator-*') {
        Save-AdbOutput 'avd-name.txt' @('emu', 'avd', 'name')
        Save-AdbOutput 'emulator-status.txt' @('emu', 'avd', 'status')
    }
}

if ($IncludeBugReport -and -not $HostOnly) {
    $bugReportPath = Join-Path $OutputDirectory 'bugreport.zip'
    & $adb -s $DeviceId bugreport $bugReportPath | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Warning "adb bugreport failed with exit code $LASTEXITCODE." }
}
elseif ($IncludeBugReport) {
    Write-Warning '-IncludeBugReport is ignored with -HostOnly because no adb device is selected.'
}

$emulatorCrashRoot = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Temp\AndroidEmulator' } else { $null }
if ($emulatorCrashRoot -and (Test-Path -LiteralPath $emulatorCrashRoot -PathType Container)) {
    $crashFiles = @(Get-ChildItem -LiteralPath $emulatorCrashRoot -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 20)
    if ($crashFiles.Count) {
        $crashOutput = Join-Path $OutputDirectory 'host-emulator-crashes'
        New-Item -ItemType Directory -Path $crashOutput -Force | Out-Null
        foreach ($file in $crashFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $crashOutput $file.Name) -Force
        }
    }
}

Write-Host "Android diagnostics: $OutputDirectory"
Get-Item -LiteralPath $OutputDirectory
