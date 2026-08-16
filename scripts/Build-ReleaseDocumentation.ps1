[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$Commit,

    [string]$Tag = 'local',

    [string]$ReleaseNotesPath,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$RequireTag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force

$repositoryRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$normalizedVersion = ConvertTo-ReleaseVersion -Version $Version
$assetNames = Get-ReleaseAssetNames -Version $normalizedVersion
$releaseNotes = Resolve-ReleaseNotesFile -RepositoryRoot $repositoryRoot -Version $normalizedVersion -ReleaseNotesPath $ReleaseNotesPath

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$FilePath $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

function Get-GitOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    $output = & git -C $repositoryRoot @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Git command 'git $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
    }

    return ($output -join "`n").Trim()
}

function Copy-TrackedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathSpec,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot,

        [string]$DestinationPrefix = ''
    )

    $trackedFiles = @(& git -C $repositoryRoot ls-files -- $PathSpec)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not enumerate tracked files for '$PathSpec'."
    }
    if ($trackedFiles.Count -eq 0) {
        throw "No tracked files were found for '$PathSpec'."
    }

    foreach ($relativePath in $trackedFiles) {
        $normalized = $relativePath.Replace([char]92, '/')
        if (Test-IsForbiddenArchivePath -Path $normalized) {
            throw "Tracked path '$normalized' is not safe for a release bundle."
        }

        $source = Join-Path $repositoryRoot $relativePath
        $destinationRelative = if ([string]::IsNullOrWhiteSpace($DestinationPrefix)) {
            $relativePath
        }
        else {
            Join-Path $DestinationPrefix $relativePath
        }
        $destination = Join-Path $DestinationRoot $destinationRelative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        [IO.File]::Copy($source, $destination, $true)
    }
}

function Copy-TrackedDirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $trackedFiles = @(& git -C $repositoryRoot ls-files -- $SourceDirectory)
    if ($LASTEXITCODE -ne 0 -or $trackedFiles.Count -eq 0) {
        throw "No tracked files were found below '$SourceDirectory'."
    }

    $sourcePrefix = $SourceDirectory.TrimEnd([char[]]@('/', [char]92)) + '/'
    foreach ($relativePath in $trackedFiles) {
        $normalized = $relativePath.Replace([char]92, '/')
        if (-not $normalized.StartsWith($sourcePrefix, [StringComparison]::Ordinal)) {
            continue
        }
        if (Test-IsForbiddenArchivePath -Path $normalized) {
            throw "Tracked path '$normalized' is not safe for a release bundle."
        }

        $inside = $normalized.Substring($sourcePrefix.Length)
        $destination = Join-Path $DestinationDirectory $inside
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        [IO.File]::Copy((Join-Path $repositoryRoot $relativePath), $destination, $true)
    }
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory,

        [string[]]$ExcludedTopLevelNames = @()
    )

    $source = (Resolve-Path -LiteralPath $SourceDirectory).Path
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        $topLevel = @($relative.Replace([char]92, '/').Split('/', [StringSplitOptions]::RemoveEmptyEntries))[0]
        if ($ExcludedTopLevelNames -contains $topLevel) {
            continue
        }

        $destination = Join-Path $DestinationDirectory $relative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        [IO.File]::Copy($file.FullName, $destination, $true)
    }
}

function Convert-SampleProjectReferences {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SamplesRoot,

        [Parameter(Mandatory = $true)]
        [string]$PackageVersion
    )

    $allowedPackages = @(
        'ModernFormsNext',
        'ModernFormsNext.WindowKit.Backend.Windows'
    )

    foreach ($projectPath in Get-ChildItem -LiteralPath $SamplesRoot -Recurse -Filter '*.csproj' -File) {
        [xml]$project = Get-Content -LiteralPath $projectPath.FullName -Raw
        $references = @($project.SelectNodes("//*[local-name()='ProjectReference']"))
        if ($references.Count -eq 0) {
            throw "Sample project '$($projectPath.FullName)' has no repository ProjectReference to convert."
        }

        foreach ($reference in $references) {
            $include = $reference.GetAttribute('Include')
            $packageId = [IO.Path]::GetFileNameWithoutExtension($include)
            if ($allowedPackages -cnotcontains $packageId) {
                throw "Sample project '$($projectPath.FullName)' references unsupported project '$include'."
            }

            $packageReference = $project.CreateElement('PackageReference')
            $packageReference.SetAttribute('Include', $packageId)
            $packageReference.SetAttribute('Version', $PackageVersion)
            $null = $reference.ParentNode.ReplaceChild($packageReference, $reference)
        }

        $settings = [Xml.XmlWriterSettings]::new()
        $settings.Indent = $true
        $settings.IndentChars = '  '
        $settings.NewLineChars = "`r`n"
        $settings.NewLineHandling = [Xml.NewLineHandling]::Replace
        $settings.Encoding = [Text.UTF8Encoding]::new($false)
        $writer = [Xml.XmlWriter]::Create($projectPath.FullName, $settings)
        try {
            $project.Save($writer)
        }
        finally {
            $writer.Dispose()
        }
    }
}

function Remove-MissingDocfxNamespaceLinks {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SiteDirectory
    )

    $apiDirectory = Join-Path $SiteDirectory 'api'
    $counter = [pscustomobject]@{ Value = 0 }
    $namespacePattern = [regex]::new(
        '<dt>Namespace</dt><dd>.*?</dd>',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Singleline)
    $linkPattern = [regex]::new(
        '<a class="xref" href="(?<href>[^"?#]+\.html)">(?<label>.*?)</a>',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Singleline)

    foreach ($htmlFile in Get-ChildItem -LiteralPath $apiDirectory -Filter '*.html' -File) {
        $text = [IO.File]::ReadAllText($htmlFile.FullName)
        $updated = $namespacePattern.Replace($text, {
            param($namespaceMatch)

            return $linkPattern.Replace($namespaceMatch.Value, {
                param($linkMatch)

                $relativeTarget = $linkMatch.Groups['href'].Value.Replace('/', [IO.Path]::DirectorySeparatorChar)
                $targetPath = [IO.Path]::GetFullPath((Join-Path $htmlFile.DirectoryName $relativeTarget))
                if ($targetPath.StartsWith($apiDirectory, [StringComparison]::OrdinalIgnoreCase) `
                    -and -not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
                    $counter.Value++
                    return ('<span class="xref">{0}</span>' -f $linkMatch.Groups['label'].Value)
                }

                return $linkMatch.Value
            })
        })

        if ($updated -cne $text) {
            Write-Utf8File -Path $htmlFile.FullName -Content $updated
        }
    }

    Write-Host "Neutralized $($counter.Value) DocFX links to synthetic namespace pages that were not generated."
}

function Write-BundleMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleRoot,

        [Parameter(Mandatory = $true)]
        [string]$BundleName
    )

    $metadata = New-ReleaseMetadata `
        -Bundle $BundleName `
        -Version $normalizedVersion `
        -Tag $Tag `
        -Commit $Commit `
        -GeneratedAtUtc $generatedAtUtc `
        -DotNetSdk $dotnetSdk
    Write-ReleaseMetadata -BundleRoot $BundleRoot -Metadata $metadata
}

$actualCommit = Get-GitOutput -ArgumentList @('rev-parse', 'HEAD')
if ($actualCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw "Git returned an invalid HEAD commit '$actualCommit'."
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = $actualCommit
}
elseif ($Commit -cnotmatch '^[0-9a-fA-F]{40}$') {
    throw "Commit '$Commit' must be a full 40-character SHA."
}
elseif (-not $actualCommit.Equals($Commit, [StringComparison]::OrdinalIgnoreCase)) {
    throw "HEAD '$actualCommit' does not match expected commit '$Commit'."
}
$Commit = $Commit.ToLowerInvariant()

if ($RequireTag) {
    $expectedTag = "v$normalizedVersion"
    if ($Tag -cne $expectedTag) {
        throw "Release tag '$Tag' does not match expected tag '$expectedTag'."
    }

    $tagCommit = Get-GitOutput -ArgumentList @('rev-list', '-n', '1', $Tag)
    if (-not $tagCommit.Equals($Commit, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Tag '$Tag' points to '$tagCommit', not expected commit '$Commit'."
    }

}

$dotnetSdk = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdk)) {
    throw 'Could not determine the active .NET SDK version.'
}

$generatedAtUtc = [DateTimeOffset]::UtcNow
$outputRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
}
if ($outputRoot.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must not be the repository root.'
}
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null

$workingRoot = Join-Path $outputRoot ".release-documentation-work-$PID"
if (Test-Path -LiteralPath $workingRoot) {
    throw "Temporary working directory '$workingRoot' already exists."
}
[IO.Directory]::CreateDirectory($workingRoot) | Out-Null

try {
    $apiAssemblies = @(
        [pscustomobject]@{ Id = 'ModernFormsNext'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.CodeGeneration'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Designer'; Tfm = 'net10.0-windows' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Designing'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit.Backend'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit.Backend.Windows'; Tfm = 'net10.0' }
    )

    foreach ($assembly in $apiAssemblies) {
        $assemblyDirectory = Join-Path $repositoryRoot "$($assembly.Id)/bin/$Configuration/$($assembly.Tfm)"
        $assembly | Add-Member -NotePropertyName Directory -NotePropertyValue $assemblyDirectory
        $assembly | Add-Member -NotePropertyName Dll -NotePropertyValue (Join-Path $assemblyDirectory "$($assembly.Id).dll")
        $assembly | Add-Member -NotePropertyName Xml -NotePropertyValue (Join-Path $assemblyDirectory "$($assembly.Id).xml")
        foreach ($requiredPath in @($assembly.Dll, $assembly.Xml)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Release API input '$requiredPath' does not exist. Build the $Configuration configuration before creating documentation bundles."
            }
        }
    }

    $docfxSource = Join-Path $workingRoot 'docfx-source'
    [IO.Directory]::CreateDirectory((Join-Path $docfxSource 'content')) | Out-Null
    foreach ($rootDocument in @('README.md', 'CHANGELOG.md', 'RELEASING.md')) {
        [IO.File]::Copy((Join-Path $repositoryRoot $rootDocument), (Join-Path $docfxSource "content/$rootDocument"), $true)
    }
    foreach ($supportingFile in @('license.md', 'third-party-licenses.md', 'global.json')) {
        [IO.File]::Copy((Join-Path $repositoryRoot $supportingFile), (Join-Path $docfxSource "content/$supportingFile"), $true)
    }
    Copy-TrackedDirectoryContents -SourceDirectory 'docs' -DestinationDirectory (Join-Path $docfxSource 'content/docs')
    Copy-TrackedDirectoryContents -SourceDirectory 'samples/ModernFormsNext.CrossPlatform.Sample' -DestinationDirectory (Join-Path $docfxSource 'content/samples/ModernFormsNext.CrossPlatform.Sample')
    Copy-TrackedDirectoryContents -SourceDirectory 'samples/ModernFormsNext.Android.SmokeTest' -DestinationDirectory (Join-Path $docfxSource 'content/samples/ModernFormsNext.Android.SmokeTest')

    $sampleLandingPages = [ordered]@{
        'ControlGallery' = 'Primary Windows control, rendering, layout, input, animation, and theme gallery.'
        'Explorer' = 'Broader file-explorer-style Windows application example.'
        'Outlaw' = 'Broader mail-client-style Windows application example.'
        'ModernFormsNext.DemoApp' = 'Clean reference application aligned with the generated template.'
        'ModernFormsNext.DesignerPlayground' = 'Standalone internal host for designer development and manual validation.'
    }
    foreach ($sampleName in $sampleLandingPages.Keys) {
        $landingPage = @"
# $sampleName

$($sampleLandingPages[$sampleName])

See the [samples guide](../../docs/samples.md) for its role and repository run instructions.
"@
        Write-Utf8File -Path (Join-Path $docfxSource "content/samples/$sampleName/index.md") -Content "$landingPage`n"
    }

    $offlineReadmePath = Join-Path $docfxSource 'content/README.md'
    $offlineReadme = [IO.File]::ReadAllText($offlineReadmePath)
    $offlineSampleLinks = [ordered]@{
        '(samples/ControlGallery)' = '(samples/ControlGallery/index.md)'
        '(samples/Explorer)' = '(samples/Explorer/index.md)'
        '(samples/Outlaw)' = '(samples/Outlaw/index.md)'
        '(samples/ModernFormsNext.DemoApp)' = '(samples/ModernFormsNext.DemoApp/index.md)'
        '(samples/ModernFormsNext.DesignerPlayground)' = '(samples/ModernFormsNext.DesignerPlayground/index.md)'
        '(samples/ModernFormsNext.CrossPlatform.Sample)' = '(samples/ModernFormsNext.CrossPlatform.Sample/README.md)'
        '(samples/ModernFormsNext.Android.SmokeTest)' = '(samples/ModernFormsNext.Android.SmokeTest/README.md)'
    }
    foreach ($link in $offlineSampleLinks.GetEnumerator()) {
        $offlineReadme = $offlineReadme.Replace($link.Key, $link.Value, [StringComparison]::Ordinal)
    }
    $offlineReadme = [regex]::Replace(
        $offlineReadme,
        '(?m)^\[!\[[^\r\n]+\]\(https?://[^\r\n]+\)\]\([^\r\n]+\)\r?\n',
        '')
    Write-Utf8File -Path $offlineReadmePath -Content $offlineReadme
    [IO.File]::Copy((Join-Path $repositoryRoot 'docs-site/index.md'), (Join-Path $docfxSource 'index.md'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'docs-site/toc.yml'), (Join-Path $docfxSource 'toc.yml'), $true)

    $metadataSources = @($apiAssemblies | ForEach-Object {
        [ordered]@{
            src = $_.Directory
            files = @("$($_.Id).dll")
        }
    })
    $docfxConfig = [ordered]@{
        metadata = @(
            [ordered]@{
                src = $metadataSources
                dest = 'api'
                namespaceLayout = 'flattened'
                memberLayout = 'samePage'
            }
        )
        build = [ordered]@{
            template = @('default', 'modern')
            content = @(
                [ordered]@{ files = @('index.md', 'toc.yml', 'content/**.md', 'api/**.yml', 'api/index.md', 'api/toc.yml') }
            )
            resource = @(
                [ordered]@{ files = @('content/**.png', 'content/**.jpg', 'content/**.jpeg', 'content/**.gif', 'content/**.svg', 'content/**.json') }
            )
            globalMetadata = [ordered]@{
                _appName = 'ModernFormsNext'
                _appTitle = "ModernFormsNext $normalizedVersion Documentation"
                _enableSearch = $true
                _disableContribution = $true
                _disableBreadcrumb = $false
            }
            dest = (Join-Path $workingRoot 'site')
        }
    }
    $docfxConfigPath = Join-Path $docfxSource 'docfx.json'
    Write-Utf8File -Path $docfxConfigPath -Content "$(ConvertTo-Json $docfxConfig -Depth 10)`n"

    Push-Location $repositoryRoot
    try {
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('tool', 'run', 'docfx', 'metadata', $docfxConfigPath, '--warningsAsErrors', '--disableGitFeatures')
        [IO.File]::Copy((Join-Path $repositoryRoot 'docs-site/api-index.md'), (Join-Path $docfxSource 'api/index.md'), $true)
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('tool', 'run', 'docfx', 'build', $docfxConfigPath, '--warningsAsErrors', '--disableGitFeatures')
    }
    finally {
        Pop-Location
    }

    # DocFX's build manifest records its temporary source_base_path. It is an implementation
    # diagnostic rather than a site runtime asset, so exclude it from portable offline bundles.
    $docfxManifest = Join-Path $workingRoot 'site/manifest.json'
    if (Test-Path -LiteralPath $docfxManifest -PathType Leaf) {
        [IO.File]::Delete($docfxManifest)
    }
    Remove-MissingDocfxNamespaceLinks -SiteDirectory (Join-Path $workingRoot 'site')

    $apiDirectory = Join-Path $docfxSource 'api'
    $apiIdSet = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    Get-ChildItem -LiteralPath $apiDirectory -Filter '*.yml' -File |
        Where-Object Name -CNE 'toc.yml' |
        ForEach-Object { Select-String -LiteralPath $_.FullName -Pattern '^\s*commentId:\s*(?<id>.+?)\s*$' } |
        ForEach-Object { $null = $apiIdSet.Add($_.Matches[0].Groups['id'].Value.Trim('"', "'")) }
    $apiIds = @($apiIdSet)
    if ($apiIds.Count -eq 0) {
        throw 'DocFX did not produce any public API identifiers.'
    }

    $snapshot = [ordered]@{
        schemaVersion = 1
        product = 'ModernFormsNext'
        version = $normalizedVersion
        commit = $Commit
        assemblies = @($apiAssemblies.Id)
        symbols = $apiIds
    }
    $snapshotText = @(
        '# ModernFormsNext public API snapshot'
        "# Version: $normalizedVersion"
        "# Commit: $Commit"
        '# Generated from DocFX public/protected API metadata.'
        ''
        $apiIds
    ) -join "`n"

    $docsRootName = "ModernFormsNext-$normalizedVersion-docs"
    $docsRoot = Join-Path $workingRoot $docsRootName
    [IO.Directory]::CreateDirectory($docsRoot) | Out-Null
    foreach ($rootFile in @('README.md', 'CHANGELOG.md', 'RELEASING.md', 'LICENSE.txt', 'license.md', 'third-party-licenses.md')) {
        [IO.File]::Copy((Join-Path $repositoryRoot $rootFile), (Join-Path $docsRoot $rootFile), $true)
    }
    Copy-TrackedDirectoryContents -SourceDirectory 'docs' -DestinationDirectory (Join-Path $docsRoot 'docs')
    Copy-TrackedDirectoryContents -SourceDirectory 'docs-site' -DestinationDirectory (Join-Path $docsRoot 'docs-site')
    [IO.File]::Copy($releaseNotes, (Join-Path $docsRoot 'RELEASE_NOTES.md'), $true)
    $referenceRoot = Join-Path $docsRoot 'reference'
    [IO.Directory]::CreateDirectory((Join-Path $referenceRoot 'xml')) | Out-Null
    foreach ($assembly in $apiAssemblies) {
        [IO.File]::Copy($assembly.Xml, (Join-Path $referenceRoot "xml/$($assembly.Id).xml"), $true)
    }
    Write-Utf8File -Path (Join-Path $referenceRoot 'public-api.txt') -Content "$snapshotText`n"
    Write-Utf8File -Path (Join-Path $referenceRoot 'public-api.json') -Content "$(ConvertTo-Json $snapshot -Depth 6)`n"
    Write-BundleMetadata -BundleRoot $docsRoot -BundleName 'docs'

    $htmlRootName = "ModernFormsNext-$normalizedVersion-docs-html"
    $htmlRoot = Join-Path $workingRoot $htmlRootName
    Copy-DirectoryContents -SourceDirectory (Join-Path $workingRoot 'site') -DestinationDirectory $htmlRoot
    [IO.File]::Copy($releaseNotes, (Join-Path $htmlRoot 'RELEASE_NOTES.md'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'LICENSE.txt'), (Join-Path $htmlRoot 'LICENSE.txt'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'third-party-licenses.md'), (Join-Path $htmlRoot 'third-party-licenses.md'), $true)
    Write-BundleMetadata -BundleRoot $htmlRoot -BundleName 'docs-html'

    $samplesRootName = "ModernFormsNext-$normalizedVersion-samples"
    $samplesRoot = Join-Path $workingRoot $samplesRootName
    [IO.Directory]::CreateDirectory($samplesRoot) | Out-Null
    foreach ($sample in Get-ReleaseSampleSpecs) {
        Copy-TrackedDirectoryContents -SourceDirectory $sample.Source -DestinationDirectory (Join-Path $samplesRoot $sample.Destination)
    }
    Convert-SampleProjectReferences -SamplesRoot $samplesRoot -PackageVersion $normalizedVersion
    [IO.File]::Copy((Join-Path $repositoryRoot 'global.json'), (Join-Path $samplesRoot 'global.json'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'LICENSE.txt'), (Join-Path $samplesRoot 'LICENSE.txt'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'third-party-licenses.md'), (Join-Path $samplesRoot 'third-party-licenses.md'), $true)
    [IO.File]::Copy($releaseNotes, (Join-Path $samplesRoot 'RELEASE_NOTES.md'), $true)
    $samplesReadme = @"
# ModernFormsNext $normalizedVersion samples

These projects are copied from commit `$Commit` and reference the exact published NuGet package
version `$normalizedVersion`. Restore requires access to a feed containing that version.

## Examples

- `examples/ControlGallery`: the primary Windows control, rendering, layout, input, animation, and theme gallery.
- `examples/Explorer`: a broader file-explorer-style Windows application.
- `examples/Outlaw`: a broader mail-client-style Windows application.

## Template reference

- `reference/ModernFormsNext.DemoApp`: the clean reference application aligned with the generated template.

The experimental cross-platform and Android smoke-test projects are intentionally not included.
They depend on source-built, unpublished Android backend projects and repository-level tooling, so
copying them into a standalone release archive would produce a misleading or incomplete sample.
See the versioned documentation for their source-checkout workflow and current limitations.
"@
    Write-Utf8File -Path (Join-Path $samplesRoot 'README.md') -Content "$samplesReadme`n"
    Write-BundleMetadata -BundleRoot $samplesRoot -BundleName 'samples'

    $sdkRootName = "ModernFormsNext-$normalizedVersion-sdk"
    $sdkRoot = Join-Path $workingRoot $sdkRootName
    [IO.Directory]::CreateDirectory($sdkRoot) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $sdkRoot 'documentation')) | Out-Null
    foreach ($document in @('README.md', 'CHANGELOG.md', 'RELEASING.md', 'license.md', 'third-party-licenses.md')) {
        [IO.File]::Copy((Join-Path $docsRoot $document), (Join-Path $sdkRoot "documentation/$document"), $true)
    }
    Copy-DirectoryContents -SourceDirectory (Join-Path $docsRoot 'docs') -DestinationDirectory (Join-Path $sdkRoot 'documentation/docs')
    Copy-DirectoryContents -SourceDirectory $htmlRoot -DestinationDirectory (Join-Path $sdkRoot 'docs-html') -ExcludedTopLevelNames @('metadata', 'RELEASE_NOTES.md', 'LICENSE.txt', 'third-party-licenses.md')
    Copy-DirectoryContents -SourceDirectory $samplesRoot -DestinationDirectory (Join-Path $sdkRoot 'samples') -ExcludedTopLevelNames @('metadata', 'RELEASE_NOTES.md', 'LICENSE.txt', 'third-party-licenses.md')
    Copy-DirectoryContents -SourceDirectory $referenceRoot -DestinationDirectory (Join-Path $sdkRoot 'reference')
    [IO.Directory]::CreateDirectory((Join-Path $sdkRoot 'release-notes')) | Out-Null
    [IO.File]::Copy($releaseNotes, (Join-Path $sdkRoot 'release-notes/RELEASE_NOTES.md'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'LICENSE.txt'), (Join-Path $sdkRoot 'LICENSE.txt'), $true)
    [IO.File]::Copy((Join-Path $repositoryRoot 'third-party-licenses.md'), (Join-Path $sdkRoot 'third-party-licenses.md'), $true)
    $sdkReadme = @"
# ModernFormsNext $normalizedVersion offline reference bundle

This aggregate bundle is tied to tag `$Tag` and commit `$Commit`. It contains documentation source,
the ready-to-open offline HTML site, selected NuGet-based samples, XML documentation, and a stable
public API snapshot. It does not contain NuGet packages, symbols, a VSIX, build outputs, or a copy of
the full repository.

- Open `docs-html/index.html` to browse the offline site.
- Read `samples/README.md` before restoring sample projects.
- Use `reference/public-api.txt` or `reference/public-api.json` for compatibility comparisons.
- Inspect `metadata/release.json` for release identity and generation details.
"@
    Write-Utf8File -Path (Join-Path $sdkRoot 'README.md') -Content "$sdkReadme`n"
    Write-BundleMetadata -BundleRoot $sdkRoot -BundleName 'sdk'

    $archives = @(
        [pscustomobject]@{ Source = $docsRoot; Root = $docsRootName; Name = $assetNames.Docs },
        [pscustomobject]@{ Source = $htmlRoot; Root = $htmlRootName; Name = $assetNames.Html },
        [pscustomobject]@{ Source = $samplesRoot; Root = $samplesRootName; Name = $assetNames.Samples },
        [pscustomobject]@{ Source = $sdkRoot; Root = $sdkRootName; Name = $assetNames.Sdk }
    )
    foreach ($archive in $archives) {
        New-StableZip -SourceDirectory $archive.Source -DestinationPath (Join-Path $outputRoot $archive.Name) -RootDirectoryName $archive.Root -Timestamp $generatedAtUtc
    }

    Write-Host "Created $($archives.Count) versioned documentation archives for ModernFormsNext $normalizedVersion at commit $Commit."
    foreach ($archive in $archives) {
        $file = Get-Item -LiteralPath (Join-Path $outputRoot $archive.Name)
        Write-Host ("  {0} ({1:N0} bytes)" -f $file.Name, $file.Length)
    }
}
finally {
    if (Test-Path -LiteralPath $workingRoot) {
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
