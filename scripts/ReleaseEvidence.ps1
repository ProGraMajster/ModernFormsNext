<#
.SYNOPSIS
Records a release candidate or verifies its approved publication handoff.
.DESCRIPTION
The manifest binds the automated results and selected artifacts to one tag, commit and
prepare run. Verification requires a separate owner approval with references for the
Visual Studio smoke, Android device matrix and Marketplace handoff. The workflow
authenticates the owner and the prepare run before calling the verification mode.
#>
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$Commit,
    [Parameter(Mandatory)][ValidatePattern('^[1-9][0-9]{0,19}$')][string]$PrepareRunId,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$')][string]$Repository,
    [Parameter(Mandatory, ParameterSetName = 'Prepare')][switch]$Prepare,
    [Parameter(Mandatory, ParameterSetName = 'Approve')][string]$ApprovalFile,
    [Parameter(ParameterSetName = 'Approve')][string]$VerifiedEvidenceFile,
    [Parameter(ParameterSetName = 'Approve')][ValidatePattern('\A[1-9][0-9]{0,19}\z')][string]$ApprovalCommentId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
$version = ConvertTo-ReleaseVersion -Version $Tag
if ($Tag -cne "v$version") { throw 'The candidate tag must be the exact v-prefixed release version.' }
$Commit = $Commit.ToLowerInvariant()
$root = (Get-Item -LiteralPath $CandidateDirectory)
if (!$root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'CandidateDirectory must be a real directory.'
}
$manifestPath = Join-Path $root.FullName 'release-evidence.json'
$rootNames = @('packages','release-docs','ModernFormsNextDesigner.vsix','ModernFormsNextSample.apk','browser.json','release-evidence.json')
if (@(Get-ChildItem -LiteralPath $root.FullName -Force | Where-Object { $rootNames -cnotcontains $_.Name }).Count) {
    throw 'Unexpected file or directory at the candidate root.'
}

function Read-Record([string]$Path, [int]$MaximumBytes) {
    $file = Get-Item -LiteralPath $Path
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $file.Length -le 0 -or $file.Length -gt $MaximumBytes) {
        throw 'Evidence must be a bounded, regular JSON file.'
    }
    try { return Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -AsHashtable -Depth 12 }
    catch { throw 'Evidence JSON is malformed.' }
}

function Assert-Fields($Record, [string[]]$Names, [string]$Kind) {
    if ($Record -isnot [System.Collections.IDictionary] -or $Record.Count -ne $Names.Count) {
        throw "$Kind has unexpected fields."
    }
    foreach ($name in $Names) {
        if (@($Record.Keys) -cnotcontains $name) { throw "$Kind is missing a required field." }
    }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-CandidateVsixVersion {
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $root.FullName 'ModernFormsNextDesigner.vsix'))
    try {
        $entry = $zip.GetEntry('extension.vsixmanifest')
        if (!$entry -or $entry.Length -gt 128KB) { throw 'The candidate must contain a bounded VSIX manifest.' }
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.CloseInput = $true
        $reader = [Xml.XmlReader]::Create($entry.Open(), $settings)
        try {
            $document = [Xml.XmlDocument]::new()
            $document.Load($reader)
        }
        finally { $reader.Dispose() }
        $namespaces = [Xml.XmlNamespaceManager]::new($document.NameTable)
        $namespaces.AddNamespace('v', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
        $identity = $document.SelectSingleNode('/v:PackageManifest/v:Metadata/v:Identity', $namespaces)
        if (!$identity -or $identity.GetAttribute('Id') -cne 'ModernFormsNext.Designer' -or
            $identity.GetAttribute('Version') -cnotmatch '\A[0-9]{1,9}(?:\.[0-9]{1,9}){2,3}\z') {
            throw 'The candidate must identify the ModernFormsNext Designer VSIX and its version.'
        }
        return $identity.GetAttribute('Version')
    }
    finally { $zip.Dispose() }
}

function Get-CandidateFiles {
    $packages = Get-Item -LiteralPath (Join-Path $root.FullName 'packages')
    $docs = Get-Item -LiteralPath (Join-Path $root.FullName 'release-docs')
    foreach ($directory in $packages, $docs) {
        if (!$directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Candidate artifacts must be in real directories.'
        }
    }
    $packageFiles = @(Get-ChildItem -LiteralPath $packages.FullName -Force | Sort-Object Name)
    if (!$packageFiles.Count) { throw 'The release package directory is empty.' }
    foreach ($package in $packageFiles) {
        if ($package.PSIsContainer -or $package.Name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9_.+-]*\.s?nupkg$') {
            throw 'Unexpected release package entry.'
        }
        "packages/$($package.Name)"
    }
    $names = Get-ReleaseAssetNames -Version $version
    $archiveNames = @($names.Docs, $names.Html, $names.Samples, $names.Sdk)
    $docEntries = @(Get-ChildItem -LiteralPath $docs.FullName -Force)
    if ($docEntries.Count -ne $archiveNames.Count -or
        @($docEntries | Where-Object { $_.PSIsContainer -or $archiveNames -cnotcontains $_.Name }).Count) {
        throw 'Unexpected documentation archive inventory.'
    }
    foreach ($archive in $archiveNames) { "release-docs/$archive" }
    'ModernFormsNextDesigner.vsix'
    'ModernFormsNextSample.apk'
    'browser.json'
}

function Get-ArtifactInventory {
    foreach ($relative in Get-CandidateFiles) {
        $file = Get-Item -LiteralPath (Join-Path $root.FullName $relative)
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Length -le 0) {
            throw 'Candidate artifacts must be nonempty, regular files.'
        }
        [ordered]@{ path = $relative; bytes = $file.Length; sha256 = Get-Sha256 $file.FullName }
    }
}

function Assert-CandidateIdentity($Record) {
    if ($Record.schema -ne 1 -or $Record.tag -cne $Tag -or $Record.commit -cne $Commit -or
        $Record.prepareRunId -cne $PrepareRunId -or $Record.repository -cne $Repository) {
        throw 'Evidence identifies a different tag, commit, repository or preparation run.'
    }
}

function Assert-RepositoryEvidenceUrl($Value) {
    $escapedRepo = [regex]::Escape($Repository)
    $pattern = "^https://github\.com/$escapedRepo/(?:actions/runs/[1-9][0-9]{0,19}(?:/artifacts/[1-9][0-9]{0,19})?|issues/[1-9][0-9]{0,9}#issuecomment-[1-9][0-9]{0,19})$"
    if ($Value -isnot [string] -or $Value -cnotmatch $pattern) {
        throw 'Use an evidence link within this GitHub repository.'
    }
}

$inventory = @(Get-ArtifactInventory)
$byPath = @{}
foreach ($row in $inventory) { $byPath[$row.path] = $row.sha256 }
$names = Get-ReleaseAssetNames -Version $version
$browser = Read-Record (Join-Path $root.FullName 'browser.json') 32KB
Assert-Fields $browser @('schema','status','tag','commit','archiveSha256','browser','pages') 'Browser evidence'
if ($browser.schema -ne 1 -or $browser.status -cne 'passed' -or $browser.tag -cne $Tag -or $browser.commit -cne $Commit -or
    $browser.archiveSha256 -cne $byPath["release-docs/$($names.Html)"] -or $browser.pages.Count -ne 5) {
    throw 'Browser evidence is missing or identifies a different documentation archive.'
}

if ($Prepare) {
    if (Test-Path -LiteralPath $manifestPath) { throw 'Use a fresh candidate directory for evidence.' }
    [ordered]@{
        schema = 1; tag = $Tag; commit = $Commit; repository = $Repository; prepareRunId = $PrepareRunId
        automated = @('release-build-and-tests','nuget-inventory','documentation-archives','offline-browser','vsix-payload')
        manual = @('visualStudio','android','marketplace'); vsixVersion = Get-CandidateVsixVersion
        artifacts = $inventory
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    Write-Host 'Release preparation recorded. Publication awaits owner approval.'
    return
}

$manifest = Read-Record $manifestPath 64KB
Assert-Fields $manifest @('schema','tag','commit','repository','prepareRunId','automated','manual','vsixVersion','artifacts') 'Release manifest'
Assert-CandidateIdentity $manifest
if (($manifest.automated -join ',') -cne 'release-build-and-tests,nuget-inventory,documentation-archives,offline-browser,vsix-payload' -or
    ($manifest.manual -join ',') -cne 'visualStudio,android,marketplace' -or $manifest.artifacts.Count -ne $inventory.Count) {
    throw 'The required release evidence or artifact inventory is incomplete.'
}
for ($i = 0; $i -lt $inventory.Count; $i++) {
    $actual = $inventory[$i]
    $recorded = $manifest.artifacts[$i]
    Assert-Fields $recorded @('path','bytes','sha256') 'Artifact record'
    if ($recorded.path -cne $actual.path -or $recorded.bytes -ne $actual.bytes -or $recorded.sha256 -cne $actual.sha256) {
        throw 'Prepared release artifact bytes or names changed.'
    }
}
if ($manifest.vsixVersion -cne (Get-CandidateVsixVersion)) { throw 'The VSIX version differs from the prepared manifest.' }

$approval = Read-Record $ApprovalFile 16KB
Assert-Fields $approval @('schema','tag','commit','repository','prepareRunId','manifestSha256','approved','visualStudio','android','marketplace') 'Owner approval'
Assert-CandidateIdentity $approval
if ($approval.approved -isnot [bool] -or !$approval.approved -or $approval.manifestSha256 -cne (Get-Sha256 $manifestPath)) {
    throw 'Owner approval must match the exact prepared evidence manifest.'
}

Assert-Fields $approval.visualStudio @('status','vsixSha256','evidenceUrl') 'Visual Studio approval'
if ($approval.visualStudio.status -cne 'passed' -or $approval.visualStudio.vsixSha256 -cne $byPath['ModernFormsNextDesigner.vsix']) {
    throw 'A passing Visual Studio smoke for the exact VSIX is required.'
}
Assert-RepositoryEvidenceUrl $approval.visualStudio.evidenceUrl

Assert-Fields $approval.android @('status','apkSha256','matrixUrl') 'Android approval'
if ($approval.android.status -cne 'physical-passed' -or $approval.android.apkSha256 -cne $byPath['ModernFormsNextSample.apk']) {
    throw 'A physical Android matrix for the exact APK is required.'
}
Assert-RepositoryEvidenceUrl $approval.android.matrixUrl

Assert-Fields $approval.marketplace @('status','vsixSha256','evidenceUrl') 'Marketplace approval'
if ($approval.marketplace.status -cnotin @('approved-pending-upload','submitted','published') -or
    $approval.marketplace.vsixSha256 -cne $byPath['ModernFormsNextDesigner.vsix']) {
    throw 'Marketplace handoff approval for the exact VSIX is required.'
}
Assert-RepositoryEvidenceUrl $approval.marketplace.evidenceUrl
if ($VerifiedEvidenceFile) {
    if (Test-Path -LiteralPath $VerifiedEvidenceFile) { throw 'Use a new path for verified release evidence.' }
    $observation = & (Join-Path $PSScriptRoot 'Get-ReleaseMarketplaceStatus.ps1') -VsixVersion $manifest.vsixVersion
    if ($approval.marketplace.status -ceq 'published' -and $observation.status -ceq 'not-listed') {
        throw 'Marketplace approval reports publication but the public listing lacks this version.'
    }
    $manifest.manual = [ordered]@{
        visualStudio = $approval.visualStudio
        android = $approval.android
        marketplace = $approval.marketplace
    }
    $manifest['manifestSha256'] = $approval.manifestSha256
    $manifest['approvalCommentId'] = $ApprovalCommentId
    $manifest['marketplaceObservation'] = $observation
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $VerifiedEvidenceFile -Encoding utf8NoBOM
}
Write-Host 'Exact candidate and owner-approved manual release gates verified.'
