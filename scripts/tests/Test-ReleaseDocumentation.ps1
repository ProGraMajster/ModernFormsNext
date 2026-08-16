[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptsRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $scriptsRoot 'ReleaseDocumentation.Common.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:assertionCount = 0

function Assert-Equal {
    param(
        [AllowNull()]
        [object]$Expected,

        [AllowNull()]
        [object]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $script:assertionCount++
    if ($Expected -cne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $script:assertionCount++
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [Parameter(Mandatory = $true)]
        [string]$MessagePattern
    )

    $script:assertionCount++
    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "Expected error matching '$MessagePattern', got '$($_.Exception.Message)'."
        }
        return
    }

    throw "Expected an error matching '$MessagePattern', but no error was raised."
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "ModernFormsNext-release-doc-tests-$([Guid]::NewGuid().ToString('N'))"
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null

try {
    Assert-Equal '1.10.0' (ConvertTo-ReleaseVersion -Version 'v1.10.0') 'A tag version must lose exactly one v prefix.'
    Assert-Equal '1.10.0-preview.docs.1' (ConvertTo-ReleaseVersion -Version '1.10.0-preview.docs.1') 'Prerelease versions must be preserved.'
    Assert-Throws { ConvertTo-ReleaseVersion -Version '1.10' } 'valid SemVer'
    Assert-Throws { ConvertTo-ReleaseVersion -Version 'vv1.10.0' } 'valid SemVer'
    Assert-Throws { ConvertTo-ReleaseVersion -Version '1.10.0-01' } 'valid SemVer'

    $names = Get-ReleaseAssetNames -Version 'v1.10.0'
    Assert-Equal 'ModernFormsNext-1.10.0-docs.zip' $names.Docs 'Docs asset naming must be stable.'
    Assert-Equal 'ModernFormsNext-1.10.0-docs-html.zip' $names.Html 'HTML asset naming must be stable.'
    Assert-Equal 'ModernFormsNext-1.10.0-samples.zip' $names.Samples 'Samples asset naming must be stable.'
    Assert-Equal 'ModernFormsNext-1.10.0-sdk.zip' $names.Sdk 'SDK asset naming must be stable.'

    $sampleSpecs = @(Get-ReleaseSampleSpecs)
    Assert-Equal 4 $sampleSpecs.Count 'The public sample selection must remain intentional.'
    Assert-True ($sampleSpecs.Source -ccontains 'samples/ControlGallery') 'ControlGallery must be selected.'
    Assert-True ($sampleSpecs.Destination -ccontains 'reference/ModernFormsNext.DemoApp') 'The template app must remain clearly separated as a reference.'
    Assert-True (-not ($sampleSpecs.Source -ccontains 'samples/ModernFormsNext.DesignerPlayground')) 'DesignerPlayground must not leak into the public samples bundle.'
    Assert-True (-not ($sampleSpecs.Source -ccontains 'samples/ModernFormsNext.Android.SmokeTest')) 'The technical Android smoke host must not leak into the public samples bundle.'

    foreach ($forbidden in @(
        'ModernFormsNext-1.10.0-docs/.git/config',
        'ModernFormsNext-1.10.0-samples/examples/ControlGallery/bin/app.dll',
        'ModernFormsNext-1.10.0-samples/examples/ControlGallery/obj/project.assets.json',
        'ModernFormsNext-1.10.0-docs/artifacts/log.txt',
        'ModernFormsNext-1.10.0-docs/secrets/.env',
        'ModernFormsNext-1.10.0-docs/signing/key.pfx',
        'C:/Users/Developer/file.md',
        '../escape.txt'
    )) {
        Assert-True (Test-IsForbiddenArchivePath -Path $forbidden) "Path '$forbidden' must be rejected."
    }
    Assert-True (-not (Test-IsForbiddenArchivePath -Path 'ModernFormsNext-1.10.0-docs/docs/getting-started.md')) 'Normal documentation paths must remain allowed.'

    $notesRepository = Join-Path $temporaryRoot 'notes-repository'
    [IO.Directory]::CreateDirectory((Join-Path $notesRepository 'docs')) | Out-Null
    Assert-Throws { Resolve-ReleaseNotesFile -RepositoryRoot $notesRepository -Version '1.10.0' } 'Release notes were not found'
    Write-Utf8File -Path (Join-Path $notesRepository 'docs/1.10.0-release-notes.md') -Content '# Notes'
    Assert-True ((Resolve-ReleaseNotesFile -RepositoryRoot $notesRepository -Version '1.10.0').EndsWith('1.10.0-release-notes.md', [StringComparison]::Ordinal)) 'The default release-notes convention must resolve.'

    $metadataRoot = Join-Path $temporaryRoot 'metadata-bundle'
    $metadata = New-ReleaseMetadata `
        -Bundle 'docs' `
        -Version '1.10.0' `
        -Tag 'v1.10.0' `
        -Commit '0123456789abcdef0123456789abcdef01234567' `
        -GeneratedAtUtc ([DateTimeOffset]::Parse('2026-08-16T12:00:00Z')) `
        -DotNetSdk '10.0.201'
    Write-ReleaseMetadata -BundleRoot $metadataRoot -Metadata $metadata
    $release = Get-Content -LiteralPath (Join-Path $metadataRoot 'metadata/release.json') -Raw | ConvertFrom-Json
    Assert-Equal '1.10.0' $release.version 'release.json must contain the normalized version.'
    Assert-Equal '0123456789abcdef0123456789abcdef01234567' $release.commit 'release.json must contain the full commit.'
    Assert-Equal 'v1.10.0' $release.tag 'release.json must contain the tag.'

    $orderedSource = Join-Path $temporaryRoot 'ordered-source'
    [IO.Directory]::CreateDirectory((Join-Path $orderedSource 'nested')) | Out-Null
    Write-Utf8File -Path (Join-Path $orderedSource 'z.txt') -Content 'z'
    Write-Utf8File -Path (Join-Path $orderedSource 'a.txt') -Content 'a'
    Write-Utf8File -Path (Join-Path $orderedSource 'nested/m.txt') -Content 'm'
    $orderedZip = Join-Path $temporaryRoot 'ordered.zip'
    New-StableZip -SourceDirectory $orderedSource -DestinationPath $orderedZip -RootDirectoryName 'root' -Timestamp ([DateTimeOffset]::Parse('2026-08-16T12:00:00Z'))
    $archive = [IO.Compression.ZipFile]::OpenRead($orderedZip)
    try {
        $entryOrder = @($archive.Entries.FullName) -join '|'
        Assert-Equal 'root/a.txt|root/nested/m.txt|root/z.txt' $entryOrder 'ZIP entries must use stable ordinal path ordering.'
        Assert-SafeArchiveEntries -Archive $archive -ArchivePath $orderedZip
    }
    finally {
        $archive.Dispose()
    }

    $unsafeSource = Join-Path $temporaryRoot 'unsafe-source'
    [IO.Directory]::CreateDirectory((Join-Path $unsafeSource 'bin')) | Out-Null
    Write-Utf8File -Path (Join-Path $unsafeSource 'bin/leak.txt') -Content 'leak'
    $unsafeZip = Join-Path $temporaryRoot 'unsafe.zip'
    New-StableZip -SourceDirectory $unsafeSource -DestinationPath $unsafeZip -RootDirectoryName 'root' -Timestamp ([DateTimeOffset]::Parse('2026-08-16T12:00:00Z'))
    $archive = [IO.Compression.ZipFile]::OpenRead($unsafeZip)
    try {
        Assert-Throws { Assert-SafeArchiveEntries -Archive $archive -ArchivePath $unsafeZip } 'forbidden entry'
    }
    finally {
        $archive.Dispose()
    }

    $localPathSource = Join-Path $temporaryRoot 'local-path-source'
    [IO.Directory]::CreateDirectory($localPathSource) | Out-Null
    Write-Utf8File -Path (Join-Path $localPathSource 'manifest.json') -Content '{ "source": "C:/Users/Developer/repository" }'
    $localPathZip = Join-Path $temporaryRoot 'local-path.zip'
    New-StableZip -SourceDirectory $localPathSource -DestinationPath $localPathZip -RootDirectoryName 'root' -Timestamp ([DateTimeOffset]::Parse('2026-08-16T12:00:00Z'))
    $archive = [IO.Compression.ZipFile]::OpenRead($localPathZip)
    try {
        Assert-Throws { Assert-SafeArchiveEntries -Archive $archive -ArchivePath $localPathZip } 'absolute local user path'
    }
    finally {
        $archive.Dispose()
    }

    $htmlSource = Join-Path $temporaryRoot 'html-source'
    [IO.Directory]::CreateDirectory((Join-Path $htmlSource 'api')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $htmlSource 'metadata')) | Out-Null
    Write-Utf8File -Path (Join-Path $htmlSource 'api/index.html') -Content '<html></html>'
    Write-Utf8File -Path (Join-Path $htmlSource 'metadata/release.json') -Content '{}'
    $htmlZip = Join-Path $temporaryRoot 'html.zip'
    New-StableZip -SourceDirectory $htmlSource -DestinationPath $htmlZip -RootDirectoryName 'root' -Timestamp ([DateTimeOffset]::Parse('2026-08-16T12:00:00Z'))
    $archive = [IO.Compression.ZipFile]::OpenRead($htmlZip)
    try {
        Assert-Throws { Assert-HtmlArchiveLayout -Archive $archive -RootDirectoryName 'root' } 'index.html'
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryRoot)) {
        [IO.Directory]::Delete($temporaryRoot, $true)
    }
}

Write-Host "Release documentation script tests passed ($script:assertionCount assertions)."
