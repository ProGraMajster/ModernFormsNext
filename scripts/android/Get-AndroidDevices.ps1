[CmdletBinding()]
param(
    [string]$SdkRoot,
    [switch]$IncludeUnavailable,
    [switch]$RestartServer
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$devices = @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable:$IncludeUnavailable -RestartServer:$RestartServer)
if (-not $devices.Count) {
    Write-Warning 'No matching Android device was detected. Start an AVD or enable and authorize USB debugging.'
}
$devices
