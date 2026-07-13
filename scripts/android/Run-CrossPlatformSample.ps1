[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$DeviceId,
    [string]$AvdName,
    [string]$SdkRoot,
    [switch]$NoBuild,
    [switch]$NoRestore,
    [switch]$ClearLogcat,
    [switch]$FollowLogcat,
    [switch]$ColdBoot,
    [switch]$WipeAvdData,
    [switch]$NoEmulatorWindow,
    [switch]$DisableEmulatorAcceleration,
    [ValidateSet('auto', 'host', 'swiftshader_indirect', 'swiftshader', 'angle_indirect')]
    [string]$GpuMode = 'auto',
    [ValidateRange(1, 3600)][int]$BootTimeoutSeconds = 300
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
if ($AvdName) {
    $DeviceId = & (Join-Path $PSScriptRoot 'Start-AndroidEmulator.ps1') `
        -Name $AvdName `
        -SdkRoot $SdkRoot `
        -BootTimeoutSeconds $BootTimeoutSeconds `
        -GpuMode $GpuMode `
        -ColdBoot:$ColdBoot `
        -WipeData:$WipeAvdData `
        -NoWindow:$NoEmulatorWindow `
        -DisableAcceleration:$DisableEmulatorAcceleration
}

$devices = @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable)
$device = Select-AndroidDevice -Device $devices -Serial $DeviceId
$DeviceId = $device.Serial
& (Join-Path $PSScriptRoot 'Wait-AndroidDevice.ps1') -DeviceId $DeviceId -SdkRoot $SdkRoot -TimeoutSeconds $BootTimeoutSeconds | Out-Null

if ($NoBuild) {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $apk = Resolve-CrossPlatformSampleApk -RepositoryRoot $repositoryRoot -Configuration $Configuration
}
else {
    $apk = & (Join-Path $PSScriptRoot 'Build-CrossPlatformSample.ps1') -Configuration $Configuration -NoRestore:$NoRestore
}

$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
if ($ClearLogcat) {
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('-s', $DeviceId, 'logcat', '-c') -Operation 'Clearing logcat'
}

& (Join-Path $PSScriptRoot 'Install-CrossPlatformSample.ps1') -ApkPath $apk.FullName -DeviceId $DeviceId -SdkRoot $SdkRoot | Out-Null
$launch = & (Join-Path $PSScriptRoot 'Launch-CrossPlatformSample.ps1') -DeviceId $DeviceId -SdkRoot $SdkRoot
$launch

if ($FollowLogcat) {
    & (Join-Path $PSScriptRoot 'Watch-ModernFormsNextLogcat.ps1') -DeviceId $DeviceId -SdkRoot $SdkRoot
}
