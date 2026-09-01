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
        $pkgDefEntry = $zip.GetEntry("ModernFormsNext.VisualStudioExtension.pkgdef")
        if ($null -eq $pkgDefEntry) {
            throw "VSIX does not contain ModernFormsNext.VisualStudioExtension.pkgdef: $Path"
        }

        $pkgDefStream = $pkgDefEntry.Open()
        try {
            $pkgDefReader = [System.IO.StreamReader]::new($pkgDefStream)
            try {
                $pkgDef = $pkgDefReader.ReadToEnd()
            }
            finally {
                $pkgDefReader.Dispose()
            }
        }
        finally {
            $pkgDefStream.Dispose()
        }

        [pscustomobject]@{
            Path = $Path
            Xml = $manifestXml
            Entries = $entries
            PkgDef = $pkgDef
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

    if ($Manifest.Entries -notcontains "ModernFormsNext.VisualStudioExtension.Shared.dll") {
        throw "VSIX does not contain the shared Visual Studio command-contract assembly."
    }

    $logicalViewsSection = '[$RootKey$\Editors\{c61567c8-f5ac-4f9e-9c6e-b4ec99c7ab31}\LogicalViews]'
    $designerLogicalView = '"{7651a702-06e5-11d1-8ebd-00a0c90f26ea}"=""'
    if (-not $Manifest.PkgDef.Contains($logicalViewsSection) -or
        -not $Manifest.PkgDef.Contains($designerLogicalView)) {
        throw "VSIX does not register LOGVIEWID_Designer as the editor factory's single primary physical view."
    }

    $requiredUserControlTemplateEntries = @(
        "ItemTemplates/CSharp/ModernFormsNext/ModernFormsNextUserControl/ModernFormsNextUserControl.vstemplate",
        "ItemTemplates/CSharp/ModernFormsNext/ModernFormsNextUserControl/ModernFormsNextUserControl.cs",
        "ItemTemplates/CSharp/ModernFormsNext/ModernFormsNextUserControl/ModernFormsNextUserControl.Designer.cs",
        "ItemTemplates/CSharp/ModernFormsNext/ModernFormsNextUserControl/ModernFormsNextUserControl.mfdesign"
    )

    foreach ($entry in $requiredUserControlTemplateEntries) {
        if ($Manifest.Entries -notcontains $entry) {
            throw "VSIX does not contain required ModernFormsNext UserControl template entry '$entry'."
        }
    }
}

function Assert-UserControlTemplateSource {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectDirectory
    )

    $templateDirectory = Join-Path $ProjectDirectory "ItemTemplates\CSharp\ModernFormsNext\ModernFormsNextUserControl"
    $templatePath = Join-Path $templateDirectory "ModernFormsNextUserControl.vstemplate"
    $codePath = Join-Path $templateDirectory "ModernFormsNextUserControl.cs"
    $designerPath = Join-Path $templateDirectory "ModernFormsNextUserControl.Designer.cs"
    $designPath = Join-Path $templateDirectory "ModernFormsNextUserControl.mfdesign"

    foreach ($path in @($templatePath, $codePath, $designerPath, $designPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "ModernFormsNext UserControl template source file is missing: $path"
        }
    }

    [xml]$templateXml = Get-Content -LiteralPath $templatePath -Raw
    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($templateXml.NameTable)
    $namespaceManager.AddNamespace("vst", "http://schemas.microsoft.com/developer/vstemplate/2005")
    $projectItems = @($templateXml.SelectNodes("/vst:VSTemplate/vst:TemplateContent/vst:ProjectItem", $namespaceManager))
    if ($projectItems.Count -ne 3) {
        throw "ModernFormsNext UserControl template must declare exactly three ProjectItem entries."
    }

    $expectedProjectItems = @{
        "ModernFormsNextUserControl.cs" = '$fileinputname$.cs'
        "ModernFormsNextUserControl.Designer.cs" = '$fileinputname$.Designer.cs'
        "ModernFormsNextUserControl.mfdesign" = '$fileinputname$.mfdesign'
    }
    foreach ($sourceName in $expectedProjectItems.Keys) {
        $projectItem = @($projectItems | Where-Object { $_.InnerText.Trim() -eq $sourceName })
        if ($projectItem.Count -ne 1) {
            throw "ModernFormsNext UserControl template must contain one ProjectItem for '$sourceName'."
        }

        if ($projectItem[0].GetAttribute("TargetFileName") -ne $expectedProjectItems[$sourceName]) {
            throw "ModernFormsNext UserControl ProjectItem '$sourceName' has an invalid TargetFileName."
        }

        if ($projectItem[0].GetAttribute("ReplaceParameters") -ne "true") {
            throw "ModernFormsNext UserControl ProjectItem '$sourceName' must replace template parameters."
        }
    }

    $rootProjectItem = @($projectItems | Where-Object { $_.InnerText.Trim() -eq "ModernFormsNextUserControl.cs" })[0]
    if ($rootProjectItem.GetAttribute("SubType") -ne "ModernFormsNextUserControl") {
        throw "ModernFormsNext UserControl root ProjectItem must declare SubType=ModernFormsNextUserControl."
    }

    $designDocument = Get-Content -LiteralPath $designPath -Raw | ConvertFrom-Json
    if ($designDocument.rootKind -ne "userControl") {
        throw "ModernFormsNext UserControl .mfdesign template must declare rootKind=userControl."
    }

    if ($designDocument.namespace -ne '$rootnamespace$' -or
        $designDocument.className -ne '$safeitemname$' -or
        $designDocument.formName -ne '$safeitemname$') {
        throw "ModernFormsNext UserControl .mfdesign template must preserve namespace and class-name parameters."
    }

    $code = Get-Content -LiteralPath $codePath -Raw
    $designerCode = Get-Content -LiteralPath $designerPath -Raw

    if ($code -notmatch 'namespace\s+\$rootnamespace\$\s*;') {
        throw "ModernFormsNext UserControl code template must use the root namespace parameter."
    }

    if ($code -notmatch 'public\s+partial\s+class\s+\$safeitemname\$\s*:\s*UserControl\b') {
        throw "ModernFormsNext UserControl code template must declare the parameterized public partial UserControl class."
    }

    if ($code -notmatch 'public\s+\$safeitemname\$\s*\(\s*\)' -or
        $code -notmatch 'InitializeComponent\s*\(\s*\)\s*;') {
        throw "ModernFormsNext UserControl code template must call InitializeComponent from its constructor."
    }

    if ($designerCode -notmatch 'namespace\s+\$rootnamespace\$\s*;' -or
        $designerCode -notmatch 'public\s+partial\s+class\s+\$safeitemname\$') {
        throw "ModernFormsNext UserControl designer template must declare the matching namespace and partial class."
    }

    if ($designerCode -notmatch 'private\s+void\s+InitializeComponent\s*\(\s*\)' -or
        $designerCode -notmatch 'this\.Name\s*=\s*"\$safeitemname\$"\s*;' -or
        $designerCode -notmatch 'this\.Size\s*=\s*new\s+System\.Drawing\.Size\s*\(\s*480\s*,\s*320\s*\)\s*;') {
        throw "ModernFormsNext UserControl designer template must initialize the generated control identity and size."
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
Assert-UserControlTemplateSource -ProjectDirectory $ProjectDirectory
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
