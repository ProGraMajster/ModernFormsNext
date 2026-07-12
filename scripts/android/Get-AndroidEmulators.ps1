[CmdletBinding()]
param()

$adb = & (Join-Path $PSScriptRoot 'Resolve-Adb.ps1') -PathOnly
$sdkFromAdb = Split-Path (Split-Path $adb -Parent) -Parent
$roots = @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME, $sdkFromAdb) |
    Where-Object { $_ } |
    Select-Object -Unique
$emulator = $roots |
    ForEach-Object { Join-Path $_ 'emulator\emulator.exe' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1

if (-not $emulator) {
    Write-Warning "Android emulator.exe was not found next to the resolved SDK ($sdkFromAdb). adb can still deploy to a physical device."
    return
}

$runningNames = @{}
foreach ($device in (& $adb devices | Select-Object -Skip 1)) {
    $serial = ($device -split '\s+')[0]
    if ($serial -like 'emulator-*') {
        $name = (& $adb -s $serial emu avd name 2>$null | Select-Object -First 1)
        if ($name) { $runningNames[$name.Trim()] = $serial }
    }
}

foreach ($name in & $emulator -list-avds) {
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    [pscustomobject]@{
        Name = $name.Trim()
        IsRunning = $runningNames.ContainsKey($name.Trim())
        Serial = $runningNames[$name.Trim()]
        EmulatorPath = $emulator
    }
}
