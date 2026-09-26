<# .SYNOPSIS
Runs the light documentation gate using the classifier's exact changed-file list.
#>
[CmdletBinding()]
param([string]$ChangesPath = 'artifacts/ci/changes.json')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Ci.Common.psm1') -Force
$root = Split-Path -Parent $PSScriptRoot
$changes = Get-Content -LiteralPath (Join-Path $root $ChangesPath) -Raw | ConvertFrom-Json
if ($changes.Full -or @($changes.Paths).Count -eq 0) { throw 'The documentation gate requires a nonempty documentation-only diff.' }
Test-CiDocumentation -RepositoryRoot $root -Paths $changes.Paths
