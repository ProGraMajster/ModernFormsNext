[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedCommit,

    [string]$ExpectedTag = 'local'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem

$normalizedVersion = ConvertTo-ReleaseVersion -Version $ExpectedVersion
if ($ExpectedCommit -cnotmatch '^[0-9a-fA-F]{40}$') {
    throw "ExpectedCommit '$ExpectedCommit' must be a full 40-character SHA."
}
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()
$assetNames = Get-ReleaseAssetNames -Version $normalizedVersion
$artifactRoot = (Resolve-Path -LiteralPath $ArtifactDirectory).Path

function Assert-ArchiveEntry {
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    if ($null -eq $Archive.GetEntry($EntryName)) {
        throw "Archive is missing required entry '$EntryName'."
    }
}

function Assert-CommonBundle {
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$RootDirectoryName,

        [Parameter(Mandatory = $true)]
        [string]$BundleName,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseNotesRelativePath
    )

    Assert-SafeArchiveEntries -Archive $Archive -ArchivePath $ArchivePath
    $rootPrefix = "$RootDirectoryName/"
    foreach ($entry in $Archive.Entries) {
        $name = $entry.FullName.Replace([char]92, '/')
        if (-not $name.StartsWith($rootPrefix, [StringComparison]::Ordinal)) {
            throw "Archive '$ArchivePath' contains entry '$name' outside expected root '$RootDirectoryName'."
        }
    }

    foreach ($required in @(
        "$rootPrefix$ReleaseNotesRelativePath",
        "${rootPrefix}LICENSE.txt",
        "${rootPrefix}metadata/version.txt",
        "${rootPrefix}metadata/commit.txt",
        "${rootPrefix}metadata/release.json"
    )) {
        Assert-ArchiveEntry -Archive $Archive -EntryName $required
    }

    $versionText = (Get-ArchiveEntryText -Archive $Archive -EntryName "${rootPrefix}metadata/version.txt").Trim()
    $commitText = (Get-ArchiveEntryText -Archive $Archive -EntryName "${rootPrefix}metadata/commit.txt").Trim()
    if ($versionText -cne $normalizedVersion -or $commitText -cne $ExpectedCommit) {
        throw "Archive '$ArchivePath' has mismatched text metadata version '$versionText' or commit '$commitText'."
    }

    $release = Get-ArchiveEntryText -Archive $Archive -EntryName "${rootPrefix}metadata/release.json" | ConvertFrom-Json
    if ($release.schemaVersion -ne 1 `
        -or $release.product -cne 'ModernFormsNext' `
        -or $release.bundle -cne $BundleName `
        -or $release.version -cne $normalizedVersion `
        -or $release.tag -cne $ExpectedTag `
        -or $release.commit -cne $ExpectedCommit `
        -or [string]::IsNullOrWhiteSpace($release.generatedAtUtc) `
        -or [string]::IsNullOrWhiteSpace($release.dotnetSdk)) {
        throw "Archive '$ArchivePath' has invalid release.json metadata."
    }

    $parsedTimestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse(
        $release.generatedAtUtc,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal,
        [ref]$parsedTimestamp)) {
        throw "Archive '$ArchivePath' has invalid generatedAtUtc '$($release.generatedAtUtc)'."
    }
}

function Resolve-ArchiveTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceEntry,

        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    $candidate = [Net.WebUtility]::HtmlDecode($Target).Trim()
    if ([string]::IsNullOrWhiteSpace($candidate) -or $candidate.StartsWith('#')) {
        return $null
    }
    if ($candidate.StartsWith('//') -or $candidate -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
        return $null
    }
    if ($candidate.StartsWith('/') -or $candidate.StartsWith('~/')) {
        throw "Offline documentation uses root-relative target '$candidate' in '$SourceEntry'."
    }

    $candidate = ($candidate -split '[?#]', 2)[0]
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return $null
    }
    $candidate = [Uri]::UnescapeDataString($candidate).Replace([char]92, '/')
    if ($candidate.EndsWith('/')) {
        $candidate += 'index.html'
    }

    $baseDirectory = $SourceEntry.Replace([char]92, '/')
    $lastSlash = $baseDirectory.LastIndexOf('/')
    $baseDirectory = if ($lastSlash -ge 0) { $baseDirectory.Substring(0, $lastSlash) } else { '' }
    $segments = [Collections.Generic.List[string]]::new()
    foreach ($segment in @($baseDirectory.Split('/', [StringSplitOptions]::RemoveEmptyEntries))) {
        $segments.Add($segment)
    }
    foreach ($segment in @($candidate.Split('/', [StringSplitOptions]::RemoveEmptyEntries))) {
        if ($segment -eq '.') {
            continue
        }
        if ($segment -eq '..') {
            if ($segments.Count -eq 0) {
                throw "Offline documentation target '$Target' escapes the archive in '$SourceEntry'."
            }
            $segments.RemoveAt($segments.Count - 1)
            continue
        }
        $segments.Add($segment)
    }

    return $segments -join '/'
}

function Assert-OfflineLinks {
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    $entryNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Archive.Entries) {
        $null = $entryNames.Add($entry.FullName.Replace([char]92, '/'))
    }

    foreach ($entry in $Archive.Entries | Where-Object { $_.FullName -match '(?i:\.html)$' }) {
        $text = Get-ArchiveEntryText -Archive $Archive -EntryName $entry.FullName
        $allLinks = [regex]::Matches($text, '(?is)\b(?:href|src)\s*=\s*["''](?<url>[^"'']+)["'']')
        foreach ($match in $allLinks) {
            $target = Resolve-ArchiveTarget -SourceEntry $entry.FullName -Target $match.Groups['url'].Value
            if ($null -ne $target -and -not $entryNames.Contains($target)) {
                throw "Offline documentation link '$($match.Groups['url'].Value)' in '$($entry.FullName)' resolves to missing '$target'."
            }
        }

        $resourceLinks = [regex]::Matches(
            $text,
            '(?is)<(?:script|img)\b[^>]*?\bsrc\s*=\s*["''](?<url>[^"'']+)["'']|<link\b[^>]*?\bhref\s*=\s*["''](?<url>[^"'']+)["'']')
        foreach ($match in $resourceLinks) {
            $url = $match.Groups['url'].Value
            if ($url.StartsWith('//') -or $url -match '^(?i:https?):') {
                throw "Offline documentation loads external resource '$url' in '$($entry.FullName)'."
            }
        }
    }

    foreach ($entry in $Archive.Entries | Where-Object { $_.FullName -match '(?i:\.css)$' }) {
        $text = Get-ArchiveEntryText -Archive $Archive -EntryName $entry.FullName
        foreach ($match in [regex]::Matches($text, '(?is)url\(\s*["'']?(?<url>[^)"'']+)["'']?\s*\)')) {
            $url = $match.Groups['url'].Value
            if ($url.StartsWith('//') -or $url -match '^(?i:https?):') {
                throw "Offline stylesheet loads external resource '$url' in '$($entry.FullName)'."
            }
            $target = Resolve-ArchiveTarget -SourceEntry $entry.FullName -Target $url
            if ($null -ne $target -and -not $entryNames.Contains($target)) {
                throw "Offline stylesheet resource '$url' in '$($entry.FullName)' resolves to missing '$target'."
            }
        }
    }

    Write-Verbose "Validated offline links in '$ArchivePath'."
}

function Assert-SampleProjects {
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$RootDirectoryName
    )

    $expectedProjects = @(
        'examples/ControlGallery/ControlGallery.csproj',
        'examples/Explorer/Explore.csproj',
        'examples/Outlaw/Outlaw.csproj',
        'reference/ModernFormsNext.DemoApp/ModernFormsNext.DemoApp.csproj'
    )
    $actualProjects = @(
        $Archive.Entries |
            Where-Object { $_.FullName -match '(?i:\.csproj)$' } |
            ForEach-Object { $_.FullName.Substring($RootDirectoryName.Length + 1) } |
            Sort-Object
    )
    if ([string]::Join('|', $actualProjects) -cne [string]::Join('|', ($expectedProjects | Sort-Object))) {
        throw "Samples archive contains an unexpected project selection: $($actualProjects -join ', ')."
    }

    foreach ($relativeProject in $expectedProjects) {
        $entryName = "$RootDirectoryName/$relativeProject"
        [xml]$project = Get-ArchiveEntryText -Archive $Archive -EntryName $entryName
        if ($project.SelectNodes("//*[local-name()='ProjectReference']").Count -ne 0) {
            throw "Sample '$entryName' still contains a repository ProjectReference."
        }
        $packageReferences = @($project.SelectNodes("//*[local-name()='PackageReference']"))
        if ($packageReferences.Count -eq 0) {
            throw "Sample '$entryName' contains no PackageReference."
        }
        foreach ($reference in $packageReferences) {
            if (-not $reference.GetAttribute('Include').StartsWith('ModernFormsNext', [StringComparison]::Ordinal) `
                -or $reference.GetAttribute('Version') -cne $normalizedVersion) {
                throw "Sample '$entryName' contains an unexpected package reference."
            }
        }
    }
}

$specs = @(
    [pscustomobject]@{ File = $assetNames.Docs; Root = "ModernFormsNext-$normalizedVersion-docs"; Bundle = 'docs'; Notes = 'RELEASE_NOTES.md' },
    [pscustomobject]@{ File = $assetNames.Html; Root = "ModernFormsNext-$normalizedVersion-docs-html"; Bundle = 'docs-html'; Notes = 'RELEASE_NOTES.md' },
    [pscustomobject]@{ File = $assetNames.Samples; Root = "ModernFormsNext-$normalizedVersion-samples"; Bundle = 'samples'; Notes = 'RELEASE_NOTES.md' },
    [pscustomobject]@{ File = $assetNames.Sdk; Root = "ModernFormsNext-$normalizedVersion-sdk"; Bundle = 'sdk'; Notes = 'release-notes/RELEASE_NOTES.md' }
)

foreach ($spec in $specs) {
    $archivePath = Join-Path $artifactRoot $spec.File
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Expected release archive '$archivePath' does not exist."
    }
    if ((Get-Item -LiteralPath $archivePath).Length -eq 0) {
        throw "Release archive '$archivePath' is empty."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        Assert-CommonBundle `
            -Archive $archive `
            -ArchivePath $archivePath `
            -RootDirectoryName $spec.Root `
            -BundleName $spec.Bundle `
            -ReleaseNotesRelativePath $spec.Notes

        switch ($spec.Bundle) {
            'docs' {
                foreach ($required in @('README.md', 'CHANGELOG.md', 'docs/getting-started.md', 'reference/xml/ModernFormsNext.xml', 'reference/public-api.txt', 'reference/public-api.json')) {
                    Assert-ArchiveEntry -Archive $archive -EntryName "$($spec.Root)/$required"
                }
            }
            'docs-html' {
                Assert-HtmlArchiveLayout -Archive $archive -RootDirectoryName $spec.Root
                if (-not ($archive.Entries | Where-Object { $_.FullName -match '(?i:\.css)$' }) `
                    -or -not ($archive.Entries | Where-Object { $_.FullName -match '(?i:\.js)$' })) {
                    throw "HTML archive '$archivePath' is missing local CSS or JavaScript assets."
                }
                Assert-OfflineLinks -Archive $archive -ArchivePath $archivePath
            }
            'samples' {
                Assert-ArchiveEntry -Archive $archive -EntryName "$($spec.Root)/README.md"
                Assert-SampleProjects -Archive $archive -RootDirectoryName $spec.Root
            }
            'sdk' {
                foreach ($required in @(
                    'README.md',
                    'documentation/README.md',
                    'docs-html/index.html',
                    'samples/README.md',
                    'samples/examples/ControlGallery/ControlGallery.csproj',
                    'samples/reference/ModernFormsNext.DemoApp/ModernFormsNext.DemoApp.csproj',
                    'reference/public-api.txt',
                    'reference/public-api.json'
                )) {
                    Assert-ArchiveEntry -Archive $archive -EntryName "$($spec.Root)/$required"
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "Validated 4 versioned documentation archives for ModernFormsNext $normalizedVersion at commit $ExpectedCommit."
