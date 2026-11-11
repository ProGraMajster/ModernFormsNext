<#
.SYNOPSIS
Authenticates the GitHub preparation run and the release owner's approval comment.
.DESCRIPTION
This script makes read-only GitHub requests before candidate retrieval. The local
checkout must be the exact tagged commit. The approved JSON is written to a new,
private runner file and validated against the artifact bytes by ReleaseEvidence.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$')][string]$Repository,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$Commit,
    [Parameter(Mandatory)][ValidatePattern('^[1-9][0-9]{0,19}$')][string]$PrepareRunId,
    [Parameter(Mandatory)][ValidatePattern('^[1-9][0-9]{0,19}$')][string]$ApprovalCommentId,
    [Parameter(Mandatory)][string]$ApprovalFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& (Join-Path $PSScriptRoot 'Assert-ReleaseTag.ps1') -Tag $Tag -Commit $Commit -CheckOrigin

$rawRun = & gh api "repos/$Repository/actions/runs/$PrepareRunId"
if ($LASTEXITCODE -ne 0) { throw 'The specified GitHub preparation run could not be read.' }
$run = $rawRun | ConvertFrom-Json -AsHashtable
if ([string]$run.id -cne $PrepareRunId -or $run.path -cne '.github/workflows/release.yml' -or
    $run.event -cne 'push' -or $run.head_branch -cne $Tag -or $run.head_sha -cne $Commit -or
    $run.repository.full_name -cne $Repository -or $run.head_repository.full_name -cne $Repository -or
    $run.status -cne 'completed' -or $run.conclusion -cne 'success') {
    throw 'The preparation run must be a successful tag push from this repository and SHA.'
}

$rawComment = & gh api "repos/$Repository/issues/comments/$ApprovalCommentId"
if ($LASTEXITCODE -ne 0) { throw 'The specified owner approval comment could not be read.' }
$comment = $rawComment | ConvertFrom-Json -AsHashtable
$owner = $Repository.Split('/')[0]
if ([string]$comment.id -cne $ApprovalCommentId -or $comment.author_association -cne 'OWNER' -or
    $comment.user.type -cne 'User' -or $comment.user.login -ine $owner -or $comment.body -isnot [string]) {
    throw 'Publication approval must come from the repository owner.'
}

$body = $comment.body.Trim()
if ($body -cmatch '(?s)\A\x60\x60\x60json\r?\n(.*)\r?\n\x60\x60\x60\z') { $body = $Matches[1] }
if ([Text.Encoding]::UTF8.GetByteCount($body) -gt 16KB -or !$body.StartsWith('{') -or !$body.EndsWith('}')) {
    throw 'Use a bounded JSON object for the owner approval.'
}
$path = [IO.Path]::GetFullPath($ApprovalFile)
if (Test-Path -LiteralPath $path) { throw 'Use a fresh private approval path.' }
[IO.File]::WriteAllText($path, $body)
Write-Host 'Exact successful preparation run and repository owner approval source authenticated.'
