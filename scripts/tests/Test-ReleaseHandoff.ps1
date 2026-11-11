[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scripts = Split-Path -Parent $PSScriptRoot
$temp = [IO.Directory]::CreateTempSubdirectory('mfn-release-handoff-')
$root = $temp.FullName
$tag = 'v0.0.0-rc.1'
$repository = 'ProGraMajster/ModernFormsNext'

function Invoke-GitFixture {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw 'Git fixture setup failed.' }
}

function Assert-Rejected([string]$Reason, [scriptblock]$Action) {
    $errorText = $null
    try { & $Action | Out-Null } catch { $errorText = $_.Exception.Message }
    if (!$errorText -or $errorText -notlike "*$Reason*") {
        throw "Expected rejection containing '$Reason', got '$errorText'."
    }
}

# Simulate only the read-only API responses. Git resolves real tags in this fixture.
function gh([string]$Command, [string]$Route) {
    if ($Command -cne 'api') { throw 'Unexpected GitHub operation.' }
    $global:LASTEXITCODE = 0
    switch -CaseSensitive ($Route) {
        "repos/$repository/actions/runs/123" { $run | ConvertTo-Json -Depth 4 -Compress }
        "repos/$repository/issues/comments/456" { $comment | ConvertTo-Json -Depth 4 -Compress }
        default { throw 'Unexpected GitHub API route.' }
    }
}

Push-Location $root
try {
    Invoke-GitFixture init --quiet
    Invoke-GitFixture -c user.name=Fixture -c user.email=fixture@example.invalid -c commit.gpgsign=false commit --quiet --allow-empty -m 'Release fixture'
    $commit = Invoke-GitFixture rev-parse HEAD
    Invoke-GitFixture -c user.name=Fixture -c user.email=fixture@example.invalid -c tag.gpgsign=false tag -a $tag -m 'Local fixture'
    $tagObject = Invoke-GitFixture rev-parse $tag
    if ($tagObject -eq $commit) { throw 'Expected an annotated release tag in the fixture.' }
    $origin = Join-Path $root 'origin.git'
    Invoke-GitFixture clone --quiet --bare $root $origin
    Invoke-GitFixture remote add origin $origin
    $assertTag = Join-Path $scripts 'Assert-ReleaseTag.ps1'
    & $assertTag -Tag $tag -Commit $commit -CheckOrigin
    Assert-Rejected 'exact version tag' { & $assertTag -Tag '0.0.0-rc.1' -Commit $commit }
    Assert-Rejected 'Checkout SHA' { & $assertTag -Tag $tag -Commit ('0' * 40) }

    $run = [ordered]@{
        id = 123; path = '.github/workflows/release.yml'; event = 'push'
        head_branch = $tag; head_sha = $commit
        repository = @{ full_name = $repository }; head_repository = @{ full_name = $repository }
        status = 'completed'; conclusion = 'success'
    }
    $comment = [ordered]@{
        id = 456; author_association = 'OWNER'; user = @{ type = 'User'; login = 'ProGraMajster' }
        body = '{"fixture":true}'
    }
    $approval = Join-Path $root 'approval.json'
    $inputs = @{
        Repository = $repository; Tag = $tag; Commit = $commit; PrepareRunId = '123'
        ApprovalCommentId = '456'; ApprovalFile = $approval
    }
    $handoff = Join-Path $scripts 'Confirm-ReleaseHandoff.ps1'
    $run.event = 'workflow_dispatch'
    Assert-Rejected 'successful tag push' { & $handoff @inputs }
    $run.event = 'push'
    $run.conclusion = 'failure'
    Assert-Rejected 'successful tag push' { & $handoff @inputs }
    $run.conclusion = 'success'
    $comment.user.login = 'another-user'
    Assert-Rejected 'repository owner' { & $handoff @inputs }
    $comment.user.login = 'ProGraMajster'
    $comment.body = 'Approved by Fixture'
    Assert-Rejected 'bounded JSON object' { & $handoff @inputs }
    $comment.body = '{"fixture":true}'
    & $handoff @inputs
    if ((Get-Content -LiteralPath $approval -Raw) -cne $comment.body) {
        throw 'Approval JSON was not preserved.'
    }

    Invoke-GitFixture -c user.name=Fixture -c user.email=fixture@example.invalid -c commit.gpgsign=false commit --quiet --allow-empty -m 'Different commit'
    $different = Invoke-GitFixture rev-parse HEAD
    Invoke-GitFixture push --quiet origin ("{0}:refs/tags/{1}" -f "+$different", $tag)
    Invoke-GitFixture checkout --quiet --detach $commit
    Assert-Rejected 'on origin does not match' { & $assertTag -Tag $tag -Commit $commit -CheckOrigin }
    Write-Host 'Release handoff provenance, authorization and tag checks passed.'
}
finally {
    Pop-Location
    $resolved = Get-Item -LiteralPath $root
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($resolved.FullName -cne $root -or $resolved.Parent.FullName -ine $tempParent -or
        $resolved.Name -cnotlike 'mfn-release-handoff-*' -or ($resolved.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected temporary fixture path.'
    }
    Remove-Item -LiteralPath $resolved.FullName -Recurse -Force
}
