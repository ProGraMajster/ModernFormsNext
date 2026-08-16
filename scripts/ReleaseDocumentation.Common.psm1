Set-StrictMode -Version Latest

function ConvertTo-ReleaseVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $normalized = $Version.Trim()
    if ($normalized.StartsWith('v', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(1)
    }

    $identifier = '(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
    $pattern = "^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-$identifier(?:\.$identifier)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$"
    if ($normalized -cnotmatch $pattern) {
        throw "Version '$Version' is not a valid SemVer 2.0 version."
    }

    return $normalized
}

function Get-ReleaseAssetNames {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $normalized = ConvertTo-ReleaseVersion -Version $Version
    return [pscustomobject]@{
        Docs = "ModernFormsNext-$normalized-docs.zip"
        Html = "ModernFormsNext-$normalized-docs-html.zip"
        Samples = "ModernFormsNext-$normalized-samples.zip"
        Sdk = "ModernFormsNext-$normalized-sdk.zip"
    }
}

function Get-ReleaseSampleSpecs {
    [CmdletBinding()]
    param()

    return @(
        [pscustomobject]@{ Source = 'samples/ControlGallery'; Destination = 'examples/ControlGallery'; Role = 'Windows control and rendering gallery' },
        [pscustomobject]@{ Source = 'samples/Explorer'; Destination = 'examples/Explorer'; Role = 'Broader Windows application example' },
        [pscustomobject]@{ Source = 'samples/Outlaw'; Destination = 'examples/Outlaw'; Role = 'Broader Windows application example' },
        [pscustomobject]@{ Source = 'samples/ModernFormsNext.DemoApp'; Destination = 'reference/ModernFormsNext.DemoApp'; Role = 'Generated-template reference application' }
    )
}

function Resolve-ReleaseNotesFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$Version,

        [string]$ReleaseNotesPath
    )

    $normalized = ConvertTo-ReleaseVersion -Version $Version
    $candidate = if ([string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
        Join-Path $RepositoryRoot "docs/$normalized-release-notes.md"
    }
    elseif ([IO.Path]::IsPathRooted($ReleaseNotesPath)) {
        $ReleaseNotesPath
    }
    else {
        Join-Path $RepositoryRoot $ReleaseNotesPath
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Release notes were not found at '$candidate'."
    }

    return (Resolve-Path -LiteralPath $candidate).Path
}

function Test-IsForbiddenArchivePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalized = $Path.Replace([char]92, '/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized.StartsWith('/') -or $normalized -match '^[A-Za-z]:/' ) {
        return $true
    }

    $segments = @($normalized.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
    if ($segments | Where-Object { $_ -eq '..' -or $_ -match '^(?i:\.git|artifacts|bin|obj|\.vs|TestResults?|tmp|temp)$' }) {
        return $true
    }

    $fileName = if ($segments.Count -gt 0) { $segments[-1] } else { $normalized }
    return $fileName -match '^(?i:\.env(?:\..*)?)$' `
        -or $fileName -match '(?i:\.(?:user|suo|pfx|snk|nupkg|snupkg|vsix|apk|aab|keystore|jks|tmp|cache))$'
}

function Write-Utf8File {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function New-ReleaseMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Bundle,

        [Parameter(Mandatory = $true)]
        [string]$Version,

        [Parameter(Mandatory = $true)]
        [string]$Tag,

        [Parameter(Mandatory = $true)]
        [string]$Commit,

        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$GeneratedAtUtc,

        [Parameter(Mandatory = $true)]
        [string]$DotNetSdk
    )

    return [ordered]@{
        schemaVersion = 1
        product = 'ModernFormsNext'
        bundle = $Bundle
        version = (ConvertTo-ReleaseVersion -Version $Version)
        tag = $Tag
        commit = $Commit.ToLowerInvariant()
        generatedAtUtc = $GeneratedAtUtc.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffZ', [Globalization.CultureInfo]::InvariantCulture)
        dotnetSdk = $DotNetSdk
    }
}

function Write-ReleaseMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleRoot,

        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Metadata
    )

    $metadataDirectory = Join-Path $BundleRoot 'metadata'
    [IO.Directory]::CreateDirectory($metadataDirectory) | Out-Null
    Write-Utf8File -Path (Join-Path $metadataDirectory 'version.txt') -Content "$($Metadata.version)`n"
    Write-Utf8File -Path (Join-Path $metadataDirectory 'commit.txt') -Content "$($Metadata.commit)`n"
    Write-Utf8File -Path (Join-Path $metadataDirectory 'release.json') -Content "$(ConvertTo-Json $Metadata -Depth 5)`n"
}

function New-StableZip {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string]$RootDirectoryName,

        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$Timestamp
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $source = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $fileEntries = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $fileEntries.Add([pscustomobject]@{
            File = $file
            Relative = [IO.Path]::GetRelativePath($source, $file.FullName).Replace([char]92, '/')
        })
    }
    $fileEntries.Sort([Comparison[object]]{
        param($left, $right)
        return [StringComparer]::Ordinal.Compare($left.Relative, $right.Relative)
    })
    if ($fileEntries.Count -eq 0) {
        throw "Cannot create an empty release archive from '$source'."
    }

    [IO.Directory]::CreateDirectory((Split-Path -Parent $DestinationPath)) | Out-Null
    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $stream = [IO.File]::Open($DestinationPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($fileEntry in $fileEntries) {
                $entryName = "$RootDirectoryName/$($fileEntry.Relative)"
                $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $Timestamp
                $input = $fileEntry.File.OpenRead()
                try {
                    $output = $entry.Open()
                    try {
                        $input.CopyTo($output)
                    }
                    finally {
                        $output.Dispose()
                    }
                }
                finally {
                    $input.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-ArchiveEntryText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "Archive does not contain '$EntryName'."
    }

    $stream = $entry.Open()
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-SafeArchiveEntries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    if ($Archive.Entries.Count -eq 0) {
        throw "Archive '$ArchivePath' is empty."
    }

    $entryNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Archive.Entries) {
        if ($entry.FullName.Contains([char]92)) {
            throw "Archive '$ArchivePath' contains a non-portable backslash entry '$($entry.FullName)'."
        }
        $name = $entry.FullName.Replace([char]92, '/')
        if (-not $entryNames.Add($name)) {
            throw "Archive '$ArchivePath' contains duplicate entry '$name'."
        }
        if (Test-IsForbiddenArchivePath -Path $name) {
            throw "Archive '$ArchivePath' contains forbidden entry '$name'."
        }

        if ($entry.Length -gt 0 -and $entry.Length -le 20MB -and $name -match '(?i:\.(?:cs|csproj|props|targets|json|xml|md|txt|html|css|js|yml|yaml|ps1|psm1))$') {
            $text = Get-ArchiveEntryText -Archive $Archive -EntryName $entry.FullName
            if ($text -match '(?i:[A-Za-z]:[\\/]Users[\\/][^\\/]+[\\/])' `
                -or $text -match '(?i:/Users/[^/]+/)' `
                -or $text -match '(?i:/home/[^/]+/)') {
                throw "Archive '$ArchivePath' contains an absolute local user path in '$name'."
            }
        }
    }
}

function Assert-HtmlArchiveLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$RootDirectoryName
    )

    foreach ($required in @('index.html', 'api/index.html', 'metadata/release.json')) {
        $entryName = "$RootDirectoryName/$required"
        if ($null -eq $Archive.GetEntry($entryName)) {
            throw "HTML archive is missing '$entryName'."
        }
    }
}

Export-ModuleMember -Function *
