[CmdletBinding()]
param([string]$SdkRoot)

# Backward-compatible name retained for contributors who used the first Android scripts.
& (Join-Path $PSScriptRoot 'Get-AndroidAvds.ps1') -SdkRoot $SdkRoot
