<# .SYNOPSIS
Classifies the checked-out PR merge revision and writes the required job's outputs.
.DESCRIPTION
CI passes immutable GitHub event SHAs through environment variables. A failed
classification selects full validation. A mismatched checkout fails the job.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseCommit,
    [Parameter(Mandatory)][string]$HeadCommit,
    [string]$OutputPath = 'artifacts/ci/changes.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Ci.Common.psm1') -Force
$root = Split-Path -Parent $PSScriptRoot
$actualHead = (Invoke-CiGit $root @('rev-parse', 'HEAD')).Trim()
if ($actualHead -cne $HeadCommit) { throw 'The checked-out commit does not match the PR merge revision.' }
$result = Get-CiChangeClassification -RepositoryRoot $root -BaseCommit $BaseCommit -HeadCommit $HeadCommit
$outputFile = Join-Path $root $OutputPath
[IO.Directory]::CreateDirectory((Split-Path -Parent $outputFile)) | Out-Null
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputFile -Encoding utf8
Write-Host "Full CI: $($result.Full). $($result.Reason)"
if ($env:GITHUB_OUTPUT) {
    "full=$($result.Full.ToString().ToLowerInvariant())" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
if ($env:GITHUB_STEP_SUMMARY) {
    "Validation: **$(if ($result.Full) { 'full Release build and all tests' } else { 'documentation' })**. Compared ``$BaseCommit`` to checked-out ``$HeadCommit``." |
        Out-File -LiteralPath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}
