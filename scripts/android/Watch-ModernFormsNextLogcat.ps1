[CmdletBinding()]
param(
    [string]$DeviceId,
    [switch]$Clear
)

$adb = & (Join-Path $PSScriptRoot 'Resolve-Adb.ps1') -PathOnly
$devices = @(& (Join-Path $PSScriptRoot 'Get-AndroidDevices.ps1'))
if ($DeviceId) {
    $device = $devices | Where-Object Serial -eq $DeviceId | Select-Object -First 1
    if (-not $device) { throw "Device '$DeviceId' is not connected and ready." }
}
elseif ($devices.Count -eq 1) {
    $DeviceId = $devices[0].Serial
}
elseif ($devices.Count -eq 0) {
    throw 'No usable Android device is connected.'
}
else {
    throw "Multiple devices are connected. Pass -DeviceId. Available: $($devices.Serial -join ', ')"
}

if ($Clear) { & $adb -s $DeviceId logcat -c }
$package = 'com.programajster.modernformsnext.sample'
$processId = (& $adb -s $DeviceId shell pidof $package 2>$null).Trim().Split(' ')[0]
Write-Host "Watching logcat on $DeviceId (tag ModernFormsNext, package $package). Press Ctrl+C to stop."
if ($processId) {
    & $adb -s $DeviceId logcat --pid=$processId 'ModernFormsNext:I' '*:S'
}
else {
    Write-Warning 'The sample process is not running; filtering by the stable ModernFormsNext tag instead.'
    & $adb -s $DeviceId logcat 'ModernFormsNext:I' '*:S'
}
