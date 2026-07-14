[CmdletBinding()]
param(
    [string]$DeviceId,
    [string]$SdkRoot,
    [ValidateRange(1, 300)][int]$ProcessTimeoutSeconds = 30,
    [switch]$ForceStop
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
$device = Select-AndroidDevice -Device @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable) -Serial $DeviceId
$package = 'com.programajster.modernformsnext.sample'
$component = Resolve-AndroidLaunchActivity -AdbPath $adb -Serial $device.Serial -PackageName $package
$arguments = Get-AdbLaunchArguments -Serial $device.Serial -Component $component -ForceStop:$ForceStop
Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList $arguments -Operation 'Android activity launch'

$deadline = [DateTime]::UtcNow.AddSeconds($ProcessTimeoutSeconds)
do {
    $pidOutput = @(& $adb -s $device.Serial shell pidof $package 2>$null | Select-Object -First 1)
    if ($pidOutput.Count -and $pidOutput[0].Trim()) {
        $processId = ($pidOutput[0].Trim() -split '\s+')[0]
        Write-Host "Launched $component on $($device.Serial) as PID $processId."
        return [pscustomobject]@{ DeviceId = $device.Serial; PackageName = $package; ActivityName = $component; ProcessId = $processId }
    }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $deadline)

throw "The activity launch command completed, but package '$package' did not remain running for $ProcessTimeoutSeconds seconds. Collect logcat and crash diagnostics."
