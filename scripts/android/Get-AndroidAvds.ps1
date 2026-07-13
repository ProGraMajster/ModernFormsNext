[CmdletBinding()]
param([string]$SdkRoot)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$avds = @(Get-AndroidAvd -SdkRoot $SdkRoot)
if (-not $avds.Count) {
    Write-Warning 'No Android Virtual Device was found. Create an x86_64 AVD in Visual Studio or Android Studio Device Manager.'
}
$avds
