[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    [xml]$versionProperties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
    $versionNode = $versionProperties.SelectSingleNode("//*[local-name()='ModernFormsNextPackageVersion']")
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Directory.Build.props does not define ModernFormsNextPackageVersion."
    }
    $ExpectedVersion = $versionNode.InnerText.Trim()
}

$packageSpecs = @(
    [pscustomobject]@{ Id = "ModernFormsNext"; Frameworks = @("net10.0", "net10.0-windows"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.CodeGeneration"; Frameworks = @("net10.0"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.Designer"; Frameworks = @("net10.0-windows"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.Designing"; Frameworks = @("net10.0"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.Templates"; Frameworks = @(); Symbols = $false; Template = $true },
    [pscustomobject]@{ Id = "ModernFormsNext.WindowKit"; Frameworks = @("net10.0"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.WindowKit.Backend"; Frameworks = @("net10.0"); Symbols = $true; Template = $false },
    [pscustomobject]@{ Id = "ModernFormsNext.WindowKit.Backend.Windows"; Frameworks = @("net10.0"); Symbols = $true; Template = $false }
)

function Get-ArchiveEntryText {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "Archive '$($Archive.ToString())' does not contain '$EntryName'."
    }

    $stream = $entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream)
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

function Get-NuspecMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$PackageId
    )

    $nuspecEntry = @($Archive.Entries | Where-Object { $_.FullName -ieq "$PackageId.nuspec" })
    if ($nuspecEntry.Count -ne 1) {
        throw "Expected one '$PackageId.nuspec' entry, found $($nuspecEntry.Count)."
    }

    [xml]$nuspec = Get-ArchiveEntryText -Archive $Archive -EntryName $nuspecEntry[0].FullName
    $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($null -eq $metadata) {
        throw "Package '$PackageId' has no nuspec metadata node."
    }

    return [pscustomobject]@{ Document = $nuspec; Metadata = $metadata; Text = $nuspec.OuterXml }
}

function Assert-SafeArchiveEntries {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    foreach ($entry in $Archive.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if ($name -match '(^|/)(bin|obj|\.vs)(/|$)' `
            -or $name -match '\.(user|suo|apk|vsix|tmp|cache)$' `
            -or $name -match '(^|/)(tmp|temp)(/|$)' `
            -or $name -match '^[A-Za-z]:/' `
            -or $name.StartsWith('/')) {
            throw "Archive '$ArchivePath' contains forbidden entry '$name'."
        }

        if ($entry.Length -gt 0 -and $entry.Length -le 1MB -and $name -match '\.(nuspec|props|targets|json|xml|md|csproj)$') {
            $text = Get-ArchiveEntryText -Archive $Archive -EntryName $entry.FullName
            if ($text -match '[A-Za-z]:[\\/]Users[\\/]' -or $text -match '/Users/[^/]+/') {
                throw "Archive '$ArchivePath' contains an absolute local user path in '$name'."
            }
        }
    }
}

function Test-FrameworkEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EntryName,

        [Parameter(Mandatory = $true)]
        [string]$Framework,

        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $escapedFramework = [regex]::Escape($Framework)
    $frameworkSuffix = if ($Framework.EndsWith('-windows', [StringComparison]::Ordinal)) { '[^/]*' } else { '' }
    return $EntryName -match "^lib/$escapedFramework$frameworkSuffix/$([regex]::Escape($FileName))$"
}

function Assert-PackageArchive {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Spec,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        Assert-SafeArchiveEntries -Archive $archive -ArchivePath $Path
        $nuspec = Get-NuspecMetadata -Archive $archive -PackageId $Spec.Id
        $idNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='id']")
        $versionNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='version']")
        if ($idNode.InnerText -cne $Spec.Id -or $versionNode.InnerText -cne $ExpectedVersion) {
            throw "Package '$Path' identifies as '$($idNode.InnerText)' version '$($versionNode.InnerText)'."
        }

        if ($nuspec.Text.IndexOf('1.8.0', [StringComparison]::Ordinal) -ge 0) {
            throw "Package '$Path' contains stale 1.8.0 active nuspec metadata."
        }

        $readmeNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='readme']")
        $iconNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='icon']")
        if ($null -eq $readmeNode -or $readmeNode.InnerText -cne 'README.md' -or $null -eq $archive.GetEntry('README.md')) {
            throw "Package '$Path' does not declare and contain README.md."
        }
        if ($null -eq $iconNode -or $iconNode.InnerText -cne 'icon.png' -or $null -eq $archive.GetEntry('icon.png')) {
            throw "Package '$Path' does not declare and contain icon.png."
        }

        $repositoryNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='repository']")
        if ($null -eq $repositoryNode -or $repositoryNode.GetAttribute('url') -cne 'https://github.com/ProGraMajster/ModernFormsNext') {
            throw "Package '$Path' has missing or unexpected repository metadata."
        }

        foreach ($dependency in @($nuspec.Metadata.SelectNodes(".//*[local-name()='dependency']"))) {
            $dependencyId = $dependency.GetAttribute('id')
            $dependencyVersion = $dependency.GetAttribute('version')
            if ($dependencyId -ceq 'Microsoft.VisualStudio.SDK' -or ($dependencyId -ceq 'MessagePack' -and $dependencyVersion -match '2\.5\.192')) {
                throw "Package '$Path' contains forbidden dependency '$dependencyId' version '$dependencyVersion'."
            }
            if ($dependencyId.StartsWith('ModernFormsNext', [StringComparison]::Ordinal) -and $dependencyVersion.IndexOf($ExpectedVersion, [StringComparison]::Ordinal) -lt 0) {
                throw "Package '$Path' has mismatched internal dependency '$dependencyId' version '$dependencyVersion'."
            }
        }

        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        if ($Spec.Template) {
            if (-not ($entries | Where-Object { $_ -match '(^|/)\.template\.config/template\.json$' }) `
                -or -not ($entries | Where-Object { $_ -match '(^|/)MyApp\.csproj$' })) {
                throw "Template package '$Path' is missing template.json or MyApp.csproj."
            }
            if ($entries | Where-Object { $_ -match '^lib/' }) {
                throw "Template package '$Path' unexpectedly contains library output."
            }
        }
        else {
            foreach ($framework in $Spec.Frameworks) {
                if (-not ($entries | Where-Object { Test-FrameworkEntry -EntryName $_ -Framework $framework -FileName "$($Spec.Id).dll" })) {
                    throw "Package '$Path' is missing $($Spec.Id).dll for '$framework'."
                }
                if (-not ($entries | Where-Object { Test-FrameworkEntry -EntryName $_ -Framework $framework -FileName "$($Spec.Id).xml" })) {
                    throw "Package '$Path' is missing XML documentation for '$framework'."
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-SymbolArchive {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Spec,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        Assert-SafeArchiveEntries -Archive $archive -ArchivePath $Path
        $nuspec = Get-NuspecMetadata -Archive $archive -PackageId $Spec.Id
        $idNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='id']")
        $versionNode = $nuspec.Metadata.SelectSingleNode("*[local-name()='version']")
        if ($idNode.InnerText -cne $Spec.Id -or $versionNode.InnerText -cne $ExpectedVersion) {
            throw "Symbol package '$Path' has mismatched identity or version."
        }

        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($framework in $Spec.Frameworks) {
            if (-not ($entries | Where-Object { Test-FrameworkEntry -EntryName $_ -Framework $framework -FileName "$($Spec.Id).pdb" })) {
                throw "Symbol package '$Path' is missing $($Spec.Id).pdb for '$framework'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packageFiles = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -File | Where-Object { $_.Extension -ceq '.nupkg' })
$symbolFiles = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -File | Where-Object { $_.Extension -ceq '.snupkg' })
$expectedSymbolCount = @($packageSpecs | Where-Object Symbols).Count

if ($packageFiles.Count -ne $packageSpecs.Count) {
    throw "Expected $($packageSpecs.Count) .nupkg files, found $($packageFiles.Count)."
}
if ($symbolFiles.Count -ne $expectedSymbolCount) {
    throw "Expected $expectedSymbolCount .snupkg files, found $($symbolFiles.Count)."
}

foreach ($spec in $packageSpecs) {
    $packageName = "$($spec.Id).$ExpectedVersion.nupkg"
    $package = @($packageFiles | Where-Object Name -CEQ $packageName)
    if ($package.Count -ne 1) {
        throw "Expected exactly one '$packageName', found $($package.Count)."
    }
    Assert-PackageArchive -Spec $spec -Path $package[0].FullName

    $symbolName = "$($spec.Id).$ExpectedVersion.snupkg"
    $symbol = @($symbolFiles | Where-Object Name -CEQ $symbolName)
    if ($spec.Symbols) {
        if ($symbol.Count -ne 1) {
            throw "Expected exactly one '$symbolName', found $($symbol.Count)."
        }
        Assert-SymbolArchive -Spec $spec -Path $symbol[0].FullName
    }
    elseif ($symbol.Count -ne 0) {
        throw "Package '$($spec.Id)' must not produce a symbol package."
    }
}

Write-Host "Validated $($packageFiles.Count) NuGet packages and $($symbolFiles.Count) symbol packages at version $ExpectedVersion."
