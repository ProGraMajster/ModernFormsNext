<#
.SYNOPSIS
Exercises the versioned offline HTML bundle in an isolated headless browser.
.DESCRIPTION
Validates the existing release archives, then renders the extracted site from a loopback
origin. Uses Node 22 or later and a local Chromium-based browser. The output directory
must be new. Its browser.json and PNGs are the portable smoke evidence.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$ExpectedTag,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BrowserPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
if ([string]::IsNullOrEmpty($BrowserPath)) {
    $locations = foreach ($directory in @($env:ProgramFiles, [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'))) {
        if ($directory) {
            Join-Path $directory 'Google/Chrome/Application/chrome.exe'
            Join-Path $directory 'Microsoft/Edge/Application/msedge.exe'
        }
    }
    $BrowserPath = $locations | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (!$BrowserPath -or !(Test-Path -LiteralPath $BrowserPath -PathType Leaf)) {
    throw 'A Chromium browser is required. Supply its executable with -BrowserPath.'
}
$node = Get-Command node -CommandType Application -ErrorAction Stop | Select-Object -First 1
$nodeVersion = & $node.Source --version
if ($LASTEXITCODE -ne 0 -or $nodeVersion -notmatch '^v([0-9]+)\.' -or [int]$Matches[1] -lt 22) {
    throw 'Node 22 or later is required for the browser smoke test.'
}

& (Join-Path $PSScriptRoot 'Validate-ReleaseDocumentation.ps1') -ArtifactDirectory $ArtifactDirectory -ExpectedVersion $ExpectedVersion -ExpectedCommit $ExpectedCommit -ExpectedTag $ExpectedTag
$assets = Get-ReleaseAssetNames -Version $ExpectedVersion
$archive = Join-Path (Resolve-Path -LiteralPath $ArtifactDirectory).Path $assets.Html
$archiveSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a fresh browser evidence directory.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$extracted = Join-Path $destination 'extracted'
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $extracted)
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveSha256) {
    throw 'Documentation archive changed during extraction.'
}
$site = Join-Path $extracted ([IO.Path]::GetFileNameWithoutExtension($assets.Html))
& $node.Source (Join-Path $PSScriptRoot 'Test-DocumentationBrowser.mjs') (Resolve-Path -LiteralPath $BrowserPath).Path $site $destination $ExpectedTag $ExpectedCommit.ToLowerInvariant() $archiveSha256
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath (Join-Path $destination 'browser.json') -PathType Leaf)) {
    throw 'Offline documentation browser smoke did not pass.'
}
Write-Host "Offline documentation browser smoke passed. Evidence: $destination"
