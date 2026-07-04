[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$VsixPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$Configuration,

    [Parameter(Mandatory = $true)]
    [string]$ProjectDirectory
)

$ErrorActionPreference = "Stop"

function Read-VsixManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "VSIX file was not generated: $Path"
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $manifestEntry = $zip.GetEntry("extension.vsixmanifest")
        if ($null -eq $manifestEntry) {
            throw "VSIX does not contain extension.vsixmanifest: $Path"
        }

        $stream = $manifestEntry.Open()
        try {
            $reader = [System.IO.StreamReader]::new($stream)
            try {
                [xml]$manifestXml = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        [pscustomobject]@{
            Path = $Path
            Xml = $manifestXml
            Entries = $entries
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Get-VsixLogicalManifest {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest
    )

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($Manifest.Xml.NameTable)
    $namespaceManager.AddNamespace("vsix", "http://schemas.microsoft.com/developer/vsx-schema/2011")

    $identity = $Manifest.Xml.SelectSingleNode("/vsix:PackageManifest/vsix:Metadata/vsix:Identity", $namespaceManager)
    if ($null -eq $identity) {
        throw "VSIX manifest does not contain Metadata/Identity: $($Manifest.Path)"
    }

    $targets = @($Manifest.Xml.SelectNodes("/vsix:PackageManifest/vsix:Installation/vsix:InstallationTarget", $namespaceManager) |
        ForEach-Object {
            $productArchitectureNode = $_.SelectSingleNode("vsix:ProductArchitecture", $namespaceManager)
            $productArchitecture = if ($null -eq $productArchitectureNode) { "" } else { $productArchitectureNode.InnerText }
            [pscustomobject]@{
                Id = $_.GetAttribute("Id")
                Version = $_.GetAttribute("Version")
                ProductArchitecture = $productArchitecture
            }
        } | Sort-Object Id)

    $prerequisites = @($Manifest.Xml.SelectNodes("/vsix:PackageManifest/vsix:Prerequisites/vsix:Prerequisite", $namespaceManager) |
        ForEach-Object {
            [pscustomobject]@{
                Id = $_.GetAttribute("Id")
                Version = $_.GetAttribute("Version")
            }
        } | Sort-Object Id)

    $assets = @($Manifest.Xml.SelectNodes("/vsix:PackageManifest/vsix:Assets/vsix:Asset", $namespaceManager) |
        ForEach-Object {
            [pscustomobject]@{
                Type = $_.GetAttribute("Type")
                Path = $_.GetAttribute("Path")
            }
        } | Sort-Object Type, Path)

    [pscustomobject]@{
        IdentityId = $identity.GetAttribute("Id")
        IdentityVersion = $identity.GetAttribute("Version")
        Targets = $targets
        Prerequisites = $prerequisites
        Assets = $assets
    }
}

function Assert-ModernFormsNextVsix {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest,

        [Parameter(Mandatory = $true)]
        [object]$LogicalManifest,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    if ($LogicalManifest.IdentityId -ne "ModernFormsNext.Designer") {
        throw "Unexpected VSIX identity '$($LogicalManifest.IdentityId)' in $($Manifest.Path)."
    }

    if ($LogicalManifest.IdentityVersion -ne $ExpectedVersion) {
        throw "VSIX version '$($LogicalManifest.IdentityVersion)' does not match expected '$ExpectedVersion' in $($Manifest.Path)."
    }

    foreach ($target in $LogicalManifest.Targets) {
        if ($target.Version -match "^\[18\.0") {
            throw "VSIX InstallationTarget '$($target.Id)' uses lower bound 18.0. Use [17.0,) for VS 2026 API compatibility."
        }

        if ($target.Version -ne "[17.0,)") {
            throw "VSIX InstallationTarget '$($target.Id)' has version '$($target.Version)'. Expected [17.0,)."
        }

        if ($target.ProductArchitecture -ne "amd64") {
            throw "VSIX InstallationTarget '$($target.Id)' has ProductArchitecture '$($target.ProductArchitecture)'. Expected amd64."
        }
    }

    $coreEditor = @($LogicalManifest.Prerequisites | Where-Object { $_.Id -eq "Microsoft.VisualStudio.Component.CoreEditor" })
    if ($coreEditor.Count -ne 1 -or $coreEditor[0].Version -ne "[17.0,)") {
        throw "VSIX must contain Microsoft.VisualStudio.Component.CoreEditor prerequisite with Version=[17.0,)."
    }

    $assetTypes = @($LogicalManifest.Assets | ForEach-Object { $_.Type })
    if ($assetTypes -notcontains "Microsoft.VisualStudio.VsPackage") {
        throw "VSIX does not contain a Microsoft.VisualStudio.VsPackage asset."
    }

    if ($assetTypes -notcontains "Microsoft.VisualStudio.ItemTemplate") {
        throw "VSIX does not contain a Microsoft.VisualStudio.ItemTemplate asset."
    }

    if (-not ($Manifest.Entries | Where-Object { $_ -like "ItemTemplates/*" })) {
        throw "VSIX does not contain physical ItemTemplates files."
    }
}

function Get-ComparableJson {
    param(
        [Parameter(Mandatory = $true)]
        [object]$LogicalManifest
    )

    $LogicalManifest | ConvertTo-Json -Depth 6 -Compress
}

$resolvedVsixPath = (Resolve-Path -LiteralPath $VsixPath).Path
$manifest = Read-VsixManifest -Path $resolvedVsixPath
$logicalManifest = Get-VsixLogicalManifest -Manifest $manifest
Assert-ModernFormsNextVsix -Manifest $manifest -LogicalManifest $logicalManifest -ExpectedVersion $ExpectedVersion

$otherConfiguration = if ($Configuration -eq "Debug") { "Release" } else { "Debug" }
$otherVsixPath = Join-Path $ProjectDirectory "bin\$otherConfiguration\net472\ModernFormsNextDesigner.vsix"
if (Test-Path -LiteralPath $otherVsixPath) {
    $otherManifest = Read-VsixManifest -Path $otherVsixPath
    $otherLogicalManifest = Get-VsixLogicalManifest -Manifest $otherManifest
    Assert-ModernFormsNextVsix -Manifest $otherManifest -LogicalManifest $otherLogicalManifest -ExpectedVersion $ExpectedVersion

    $currentJson = Get-ComparableJson -LogicalManifest $logicalManifest
    $otherJson = Get-ComparableJson -LogicalManifest $otherLogicalManifest
    if ($currentJson -ne $otherJson) {
        throw "Debug and Release VSIX manifests differ logically. Current: $currentJson Other: $otherJson"
    }
}

Write-Host "Validated ModernFormsNext Designer VSIX: $resolvedVsixPath"
