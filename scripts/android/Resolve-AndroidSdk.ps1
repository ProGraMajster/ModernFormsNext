[CmdletBinding()]
param(
    [string]$SdkRoot,
    [switch]$RequireEmulator,
    [switch]$PathOnly
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$resolved = Resolve-AndroidSdkRoot -SdkRoot $SdkRoot -RequireEmulator:$RequireEmulator
if (-not $PathOnly) {
    Write-Host "Android SDK: $resolved"
    Write-Host "adb: $(Join-Path $resolved 'platform-tools\adb.exe')"
    $emulator = Join-Path $resolved 'emulator\emulator.exe'
    if (Test-Path -LiteralPath $emulator -PathType Leaf) {
        Write-Host "emulator: $emulator"
    }
}
$resolved
