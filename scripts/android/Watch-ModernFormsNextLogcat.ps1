[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$SdkRoot,
    [string]$Tag = 'ModernFormsNext',
    [switch]$Clear,
    [switch]$AllProcessLogs
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
$device = Select-AndroidDevice -Device @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable) -Serial $DeviceId
$DeviceId = $device.Serial
if ($Clear) {
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('-s', $DeviceId, 'logcat', '-c') -Operation 'Clearing logcat'
}

$package = 'com.programajster.modernformsnext.sample'
$pidOutput = @(& $adb -s $DeviceId shell pidof $package 2>$null | Select-Object -First 1)
$processId = if ($pidOutput.Count -and $pidOutput[0].Trim()) { ($pidOutput[0].Trim() -split '\s+')[0] } else { $null }
Write-Host "Watching logcat on $DeviceId. Press Ctrl+C to stop."

if ($AllProcessLogs -and $processId) {
    & $adb -s $DeviceId logcat "--pid=$processId"
}
elseif ($processId) {
    & $adb -s $DeviceId logcat "--pid=$processId" "$($Tag):V" '*:S'
}
else {
    Write-Warning "Package '$package' is not running; filtering by tag '$Tag' without a PID."
    & $adb -s $DeviceId logcat "$($Tag):V" 'AndroidRuntime:E' '*:S'
}
