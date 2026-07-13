[CmdletBinding()]
param(
    [string]$SdkRoot,
    [switch]$PathOnly
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
if (-not $PathOnly) {
    Write-Host "adb: $adb"
    & $adb version | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "adb version failed with exit code $LASTEXITCODE." }
}
$adb
