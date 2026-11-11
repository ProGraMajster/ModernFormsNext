[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scripts = Split-Path -Parent $PSScriptRoot
$script = Join-Path $scripts 'ReleaseEvidence.ps1'
Import-Module (Join-Path $scripts 'ReleaseDocumentation.Common.psm1') -Force
$temp = [IO.Directory]::CreateTempSubdirectory('mfn-release-evidence-')
$root = Join-Path $temp.FullName 'candidate'
$tag = 'v1.12.0-rc.1'
$commit = '0123456789abcdef0123456789abcdef01234567'
$repository = 'ProGraMajster/ModernFormsNext'
$runId = '123456'
$argsForCandidate = @{
    CandidateDirectory = $root; Tag = $tag; Commit = $commit; Repository = $repository; PrepareRunId = $runId
}
$approvalFile = Join-Path $temp.FullName 'approval.json'
$vsixVersion = '1.12.0'

function Write-Record([string]$File, $Value) {
    $Value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $File -Encoding utf8NoBOM
}

function Get-Sha([string]$Path) { return (Get-FileHash -LiteralPath (Join-Path $root $Path)).Hash.ToLowerInvariant() }

function Assert-Rejected([string]$Reason, [scriptblock]$Action) {
    $errorText = $null
    try { & $Action | Out-Null } catch { $errorText = $_.Exception.Message }
    if (!$errorText -or $errorText -notlike "*$Reason*") {
        throw "Expected rejection containing '$Reason', got '$errorText'."
    }
}

function Invoke-RestMethod($Method, $ContentType, $TimeoutSec, $MaximumRedirection, $Body, $Uri) {
    $query = $Body | ConvertFrom-Json
    if ($Method -cne 'Post' -or $Uri -cne 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery?api-version=7.1-preview.1' -or
        $query.filters[0].criteria[0].value -cne 'ProGraMajster.ModernFormsNextDesigner') {
        throw 'Unexpected Marketplace request.'
    }
    @{ results = @(@{ extensions = @([pscustomobject]@{
        publisher = [pscustomobject]@{ publisherName = 'ProGraMajster' }
        extensionName = 'ModernFormsNextDesigner'
        versions = @([pscustomobject]@{ version = '1.11.0' })
    }) }) }
}

try {
    [IO.Directory]::CreateDirectory($root) | Out-Null
    foreach ($name in 'packages','release-docs') { New-Item -ItemType Directory -Path (Join-Path $root $name) | Out-Null }
    Set-Content -LiteralPath (Join-Path $root 'packages/ModernFormsNext.1.12.0-rc.1.nupkg') -Value 'package fixture'
    Set-Content -LiteralPath (Join-Path $root 'packages/ModernFormsNext.1.12.0-rc.1.snupkg') -Value 'symbols fixture'
    $archives = Get-ReleaseAssetNames -Version $tag
    foreach ($name in @($archives.Docs, $archives.Html, $archives.Samples, $archives.Sdk)) {
        Set-Content -LiteralPath (Join-Path $root "release-docs/$name") -Value $name
    }
    $zip = [IO.Compression.ZipFile]::Open((Join-Path $root 'ModernFormsNextDesigner.vsix'), [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $zip.CreateEntry('extension.vsixmanifest')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try {
            $writer.Write(@"
<PackageManifest xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata><Identity Id="ModernFormsNext.Designer" Version="$vsixVersion" /></Metadata>
</PackageManifest>
"@)
        }
        finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }
    Set-Content -LiteralPath (Join-Path $root 'ModernFormsNextSample.apk') -Value 'APK fixture'
    $browser = @{
        schema = 1; status = 'passed'; tag = $tag; commit = $commit; browser = 'Chromium fixture'
        archiveSha256 = '0' * 64; pages = @('one','two','three','four','five')
    }
    $browserFile = Join-Path $root 'browser.json'
    Write-Record $browserFile $browser
    Assert-Rejected 'different documentation archive' { & $script @argsForCandidate -Prepare }
    $browser.archiveSha256 = Get-Sha "release-docs/$($archives.Html)"
    Write-Record $browserFile $browser
    & $script @argsForCandidate -Prepare
    Assert-Rejected 'fresh candidate directory' { & $script @argsForCandidate -Prepare }
    Assert-Rejected 'approval.json' { & $script @argsForCandidate -ApprovalFile $approvalFile }

    $evidenceLink = "https://github.com/$repository/actions/runs/445566/artifacts/4"
    $approval = @{
        schema = 1; tag = $tag; commit = $commit; repository = $repository; prepareRunId = $runId
        manifestSha256 = Get-Sha 'release-evidence.json'; approved = $true
        visualStudio = @{ status = 'passed'; vsixSha256 = Get-Sha 'ModernFormsNextDesigner.vsix'; evidenceUrl = $evidenceLink }
        android = @{ status = 'physical-passed'; apkSha256 = Get-Sha 'ModernFormsNextSample.apk'; matrixUrl = $evidenceLink }
        marketplace = @{ status = 'approved-pending-upload'; vsixSha256 = Get-Sha 'ModernFormsNextDesigner.vsix'; evidenceUrl = $evidenceLink }
    }
    Write-Record $approvalFile $approval
    $verifiedPath = Join-Path $temp.FullName 'verified.json'
    & $script @argsForCandidate -ApprovalFile $approvalFile -ApprovalCommentId 456 -VerifiedEvidenceFile $verifiedPath
    $verified = Get-Content -LiteralPath $verifiedPath -Raw | ConvertFrom-Json
    if ($verified.vsixVersion -cne $vsixVersion -or $verified.marketplaceObservation.version -cne $vsixVersion -or
        $verified.marketplaceObservation.status -cne 'not-listed' -or $verified.approvalCommentId -cne '456') {
        throw 'Verified evidence did not retain the VSIX identity and Marketplace observation.'
    }
    $approval.marketplace.status = 'published'
    Write-Record $approvalFile $approval
    Assert-Rejected 'public listing lacks this version' {
        & $script @argsForCandidate -ApprovalFile $approvalFile -VerifiedEvidenceFile (Join-Path $temp.FullName 'inconsistent.json')
    }
    $approval.marketplace.status = 'approved-pending-upload'

    $approval.approved = 'true'
    Write-Record $approvalFile $approval
    Assert-Rejected 'exact prepared evidence manifest' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    $approval.approved = $true
    $approval.prepareRunId = '987654'
    Write-Record $approvalFile $approval
    Assert-Rejected 'different tag, commit, repository or preparation run' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    $approval.prepareRunId = $runId
    $approval.android.status = 'emulator-passed'
    Write-Record $approvalFile $approval
    Assert-Rejected 'physical Android matrix' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    $approval.android.status = 'physical-passed'
    $approval.marketplace.evidenceUrl = 'https://example.com/evidence'
    Write-Record $approvalFile $approval
    Assert-Rejected 'evidence link within this GitHub repository' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    $approval.marketplace.evidenceUrl = $evidenceLink
    $approval.marketplace.status = 'awaiting-approval'
    Write-Record $approvalFile $approval
    Assert-Rejected 'Marketplace handoff approval' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    $approval.marketplace.status = 'approved-pending-upload'
    Write-Record $approvalFile $approval
    $unexpected = Join-Path $root 'unexpected.txt'
    Set-Content -LiteralPath $unexpected -Value 'unexpected candidate fixture'
    Assert-Rejected 'Unexpected file or directory' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    Remove-Item -LiteralPath $unexpected
    Add-Content -LiteralPath (Join-Path $root 'ModernFormsNextDesigner.vsix') -Value 'mutated fixture'
    Assert-Rejected 'artifact bytes or names changed' { & $script @argsForCandidate -ApprovalFile $approvalFile }
    Write-Host 'Release evidence positive and fail-closed cases passed.'
}
finally {
    $resolved = Get-Item -LiteralPath $temp.FullName
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($resolved.FullName -cne $temp.FullName -or $resolved.Parent.FullName -ine $tempParent -or
        $resolved.Name -cnotlike 'mfn-release-evidence-*' -or ($resolved.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected temporary fixture path.'
    }
    Remove-Item -LiteralPath $resolved.FullName -Recurse -Force
}
