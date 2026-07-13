[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ApkPath,
    [string]$DeviceId,
    [string]$SdkRoot,
    [switch]$AllowDowngrade,
    [switch]$UninstallFirst
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
$device = Select-AndroidDevice -Device @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable) -Serial $DeviceId
$resolvedApk = (Resolve-Path -LiteralPath $ApkPath -ErrorAction Stop).Path

if ($UninstallFirst) {
    Write-Warning 'Removing the existing package and its application data because -UninstallFirst was explicitly supplied.'
    & $adb -s $device.Serial uninstall 'com.programajster.modernformsnext.sample' | Out-Host
}

$arguments = Get-AdbInstallArguments -Serial $device.Serial -ApkPath $resolvedApk -AllowDowngrade:$AllowDowngrade
Write-Host "Installing '$resolvedApk' on $($device.Serial)..."
Invoke-CheckedNativeCommand -FilePath $adb -ArgumentList $arguments -Operation 'adb install'
[pscustomobject]@{ DeviceId = $device.Serial; ApkPath = $resolvedApk; PackageName = 'com.programajster.modernformsnext.sample' }
