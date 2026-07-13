[CmdletBinding()]
param(
    [string]$Name,
    [string]$SdkRoot,
    [ValidateRange(1, 3600)][int]$BootTimeoutSeconds = 300,
    [ValidateSet('auto', 'host', 'swiftshader_indirect', 'swiftshader', 'angle_indirect')]
    [string]$GpuMode = 'auto',
    [switch]$ColdBoot,
    [switch]$WipeData,
    [switch]$NoWindow,
    [switch]$DisableAcceleration,
    [switch]$RestartAdb,
    [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$available = @(Get-AndroidAvd -SdkRoot $SdkRoot)
if (-not $available.Count) { throw 'No Android Virtual Device is installed in the resolved SDK.' }
if (-not $Name) {
    if ($available.Count -ne 1) {
        throw "Multiple AVDs are installed. Pass -Name. Available: $($available.Name -join ', ')"
    }
    $Name = $available[0].Name
}

$avd = @($available | Where-Object Name -eq $Name)
if ($avd.Count -ne 1) { throw "Android Virtual Device '$Name' was not found. Available: $($available.Name -join ', ')" }
if ($avd[0].IsRunning) {
    Write-Host "AVD '$Name' is already running as $($avd[0].Serial)."
    & (Join-Path $PSScriptRoot 'Wait-AndroidDevice.ps1') -DeviceId $avd[0].Serial -SdkRoot $SdkRoot -TimeoutSeconds $BootTimeoutSeconds -CancellationToken $CancellationToken | Out-Null
    return $avd[0].Serial
}

$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
if ($RestartAdb) {
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('kill-server') -Operation 'Stopping adb server'
    Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList @('start-server') -Operation 'Starting adb server'
}

$before = @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable | Where-Object Serial -like 'emulator-*').Serial
$arguments = @('-avd', $Name, '-gpu', $GpuMode)
if ($ColdBoot) { $arguments += '-no-snapshot-load' }
if ($WipeData) {
    Write-Warning "Wiping all user data for AVD '$Name' because -WipeData was explicitly supplied."
    $arguments += '-wipe-data'
}
if ($NoWindow) { $arguments += @('-no-window', '-no-audio') }
if ($DisableAcceleration) {
    Write-Warning 'Starting with CPU acceleration disabled. Boot can be extremely slow and is suitable only for diagnostics.'
    $arguments += @('-accel', 'off')
}

Write-Host "Starting AVD '$Name' with GPU mode '$GpuMode'."
$startParameters = @{
    FilePath = $avd[0].EmulatorPath
    ArgumentList = $arguments
    PassThru = $true
}
if ($NoWindow) { $startParameters.WindowStyle = 'Hidden' }
$process = Start-Process @startParameters

$deadline = [DateTime]::UtcNow.AddSeconds($BootTimeoutSeconds)
do {
    $CancellationToken.ThrowIfCancellationRequested()
    if ($process.HasExited) {
        throw "AVD '$Name' exited before connecting (exit code $($process.ExitCode)). Check emulator diagnostics and hardware acceleration."
    }

    foreach ($instance in @(Get-RunningAndroidAvd -SdkRoot $SdkRoot)) {
        if (($instance.Name -eq $Name -or $instance.Serial -notin $before) -and $instance.Serial) {
            Wait-AndroidDeviceReady -AdbPath $adb -Serial $instance.Serial -TimeoutSeconds ([Math]::Max(1, [int]($deadline - [DateTime]::UtcNow).TotalSeconds)) -CancellationToken $CancellationToken | Out-Null
            Write-Host "AVD '$Name' booted as $($instance.Serial)."
            return $instance.Serial
        }
    }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)

throw "AVD '$Name' did not connect and finish booting within $BootTimeoutSeconds seconds. Run Collect-AndroidDiagnostics.ps1 and inspect %LOCALAPPDATA%\Temp\AndroidEmulator."
