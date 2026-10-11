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

function Get-ReleaseApiAssemblySpecs {
    [CmdletBinding()]
    param()

    return @(
        [pscustomobject]@{ Id = 'ModernFormsNext'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Automation'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Automation.Windows'; Tfm = 'net10.0-windows' },
        [pscustomobject]@{ Id = 'ModernFormsNext.CodeGeneration'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Designer'; Tfm = 'net10.0-windows' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Designing'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.Testing'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit.Backend'; Tfm = 'net10.0' },
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit.Backend.Windows'; Tfm = 'net10.0' },
        # Select the platform surface once; combining both TFMs would duplicate DocFX UIDs.
        [pscustomobject]@{ Id = 'ModernFormsNext.WindowKit.Backend.Android'; Tfm = 'net10.0-android' }
    )
}

function ConvertTo-ReleaseSourceLinks {
    <# .SYNOPSIS
    Binds valid checkout-source links omitted from the offline site to the exact release commit.
    Missing files remain unchanged so DocFX still rejects broken documentation links.
    #>
    param([string]$Content, [string]$DocumentPath, [string]$ContentRoot, [string]$RepositoryRoot, [string]$Commit, [string[]]$RepositoryFiles)
    $root = [IO.Path]::GetFullPath($ContentRoot).TrimEnd([char[]]@('/', [char]92)) + [IO.Path]::DirectorySeparatorChar
    return [regex]::Replace($Content, '(?<prefix>(?<!!)!?\[[^\]\r\n]*\]\()(?<target>[^\s)]+)(?<suffix>\))', {
        param($match)
        $target = $match.Groups['target'].Value
        if ($match.Groups['prefix'].Value.StartsWith('!') -or $target -match '^(?:#|/|~|[A-Za-z][A-Za-z0-9+.-]*:)') { return $match.Value }
        $parts = $target -split '(?=[?#])', 2
        $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $DocumentPath) ([Uri]::UnescapeDataString($parts[0]))))
        if (-not $resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return $match.Value }
        $extension = [IO.Path]::GetExtension($resolved)
        if ($extension -match '^(?i:\.md|\.png|\.jpg|\.jpeg|\.gif|\.svg|\.json)$' -and [IO.File]::Exists($resolved)) { return $match.Value }
        $relative = [IO.Path]::GetRelativePath($root, $resolved).Replace([char]92, '/')
        if ($RepositoryFiles -cnotcontains $relative) { return $match.Value }
        if (-not [IO.File]::Exists((Join-Path $RepositoryRoot $relative))) { return $match.Value }
        $fragment = if ($parts.Count -gt 1) { $parts[1] } else { '' }
        $encodedPath = ($relative.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
        return "$($match.Groups['prefix'].Value)https://github.com/ProGraMajster/ModernFormsNext/blob/$Commit/$encodedPath$fragment$($match.Groups['suffix'].Value)"
    })
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
