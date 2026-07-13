[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$SdkRoot,
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 300,
    [ValidateRange(1, 30)][int]$PollSeconds = 2,
    [switch]$RestartServer,
    [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
if ($RestartServer) {
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('kill-server') -Operation 'Stopping adb server'
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('start-server') -Operation 'Starting adb server'
}

if (-not $DeviceId) {
    $device = Select-AndroidDevice -Device @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable)
    $DeviceId = $device.Serial
}

Write-Host "Waiting for Android device '$DeviceId' (timeout: $TimeoutSeconds seconds)..."
Wait-AndroidDeviceReady -AdbPath $adb -Serial $DeviceId -TimeoutSeconds $TimeoutSeconds -PollSeconds $PollSeconds -CancellationToken $CancellationToken
Write-Host "Android device '$DeviceId' is online and boot-complete."
