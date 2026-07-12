[CmdletBinding()]
param(
    [string]$Name,
    [int]$BootTimeoutSeconds = 180
)

$available = @(& (Join-Path $PSScriptRoot 'Get-AndroidEmulators.ps1'))
if (-not $available) { throw 'No Android Virtual Device is installed in the resolved SDK.' }
if (-not $Name) {
    if ($available.Count -ne 1) {
        throw "Multiple AVDs are installed. Pass -Name. Available: $($available.Name -join ', ')"
    }
    $Name = $available[0].Name
}

$avd = $available | Where-Object Name -eq $Name | Select-Object -First 1
if (-not $avd) { throw "Android Virtual Device '$Name' was not found." }
if ($avd.IsRunning) {
    Write-Host "AVD '$Name' is already running as $($avd.Serial)."
    return $avd.Serial
}

$before = @(& (Join-Path $PSScriptRoot 'Get-AndroidDevices.ps1') -IncludeUnavailable).Serial
Start-Process -FilePath $avd.EmulatorPath -ArgumentList @('-avd', $Name) -WindowStyle Hidden
$adb = & (Join-Path $PSScriptRoot 'Resolve-Adb.ps1') -PathOnly
$deadline = (Get-Date).AddSeconds($BootTimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    $emulators = @(& $adb devices | Select-Object -Skip 1 | ForEach-Object { ($_ -split '\s+')[0] }) |
        Where-Object { $_ -like 'emulator-*' -and $_ -notin $before }
    foreach ($serial in $emulators) {
        $booted = (& $adb -s $serial shell getprop sys.boot_completed 2>$null).Trim()
        if ($booted -eq '1') {
            Write-Host "AVD '$Name' booted as $serial."
            return $serial
        }
    }
} while ((Get-Date) -lt $deadline)

throw "AVD '$Name' did not complete boot within $BootTimeoutSeconds seconds."
