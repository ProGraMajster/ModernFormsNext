<#
.SYNOPSIS
Runs the versioned Windows software workloads in new processes and preserves metadata.
.DESCRIPTION
Use the assembly parameter for a built development candidate. Use the published baseline
switch to build the same harness against verified, package-only v1.12.1 dependencies.
Each trial starts a new application process. File and driver caches are left to Windows.
#>
[CmdletBinding(DefaultParameterSetName='Candidate')]
param(
    [Parameter(Mandatory, ParameterSetName='Candidate')][string]$LabAssembly,
    [Parameter(Mandatory, ParameterSetName='Published')][switch]$PublishedBaseline,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'This qualification script requires Windows.' }
$repo = Split-Path -Parent $PSScriptRoot
$root = [IO.Path]::GetFullPath($OutputDirectory)
$assembly = if ($LabAssembly) { (Resolve-Path -LiteralPath $LabAssembly).Path } else { $null }
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh output directory.' }
[IO.Directory]::CreateDirectory($root) | Out-Null
$harness = Join-Path $repo 'samples/ModernFormsNext.PerformanceLab'
$packageFacts = $null
$publishedCommit = $null

function Invoke-Dotnet([string]$Phase, [string[]]$Arguments) {
    $log = Join-Path $root "$Phase.log"
    & dotnet @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 24 | Write-Host
        throw "dotnet $Phase failed ($LASTEXITCODE)."
    }
}

if ($PublishedBaseline) {
    $publishedCommit = (git -C $repo rev-parse 'v1.12.1^{commit}').Trim()
    if ($LASTEXITCODE -ne 0 -or $publishedCommit -ne '7c8e25c4955b3a1cfedae5c7355e076735f7bd9d') {
        throw 'Expected the exact v1.12.1 commit.'
    }
    $projectDir = Join-Path $root 'consumer'
    [IO.Directory]::CreateDirectory($projectDir) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'global.json') -Destination $projectDir
    Copy-Item -LiteralPath @(Get-ChildItem -LiteralPath $harness -Filter '*.cs' -File | Select-Object -ExpandProperty FullName) -Destination $projectDir
    '<Project />' | Set-Content -LiteralPath (Join-Path $projectDir 'Directory.Build.props')
    '<Project />' | Set-Content -LiteralPath (Join-Path $projectDir 'Directory.Build.targets')
    @'
<configuration>
  <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <config><add key="globalPackagesFolder" value="packages" /></config>
</configuration>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'NuGet.Config')
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>ModernFormsNext.PerformanceLab</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ModernFormsNext" Version="[1.12.1]" />
    <PackageReference Include="ModernFormsNext.WindowKit.Backend.Windows" Version="[1.12.1]" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'PublishedConsumer.csproj')
    Push-Location $projectDir
    try {
        $sdk = (& dotnet --version).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the consumer SDK.' }
        Invoke-Dotnet 'published-restore' @('restore', 'PublishedConsumer.csproj', '--configfile', 'NuGet.Config', '-m:1')
        $resolved = Get-Content -Raw -LiteralPath 'obj/project.assets.json' | ConvertFrom-Json -AsHashtable
        $packageFacts = foreach ($key in $resolved.libraries.Keys | Where-Object { $_ -like 'ModernFormsNext/*' -or $_ -like 'ModernFormsNext.*/*' }) {
            $id, $version = $key.Split('/')
            $entry = $resolved.libraries[$key]
            if ($version -ne '1.12.1' -or $entry.type -ne 'package') { throw "Unexpected framework dependency $key." }
            $folder = Join-Path 'packages' $entry.path
            [xml]$nuspec = Get-Content -Raw -LiteralPath (Join-Path $folder "$($id.ToLowerInvariant()).nuspec")
            if ($nuspec.package.metadata.repository.commit -ne $publishedCommit) { throw "Published provenance mismatch for $key." }
            $archive = Join-Path $folder "$($id.ToLowerInvariant()).$version.nupkg"
            [ordered]@{ Identity = $key; Source = $publishedCommit; Sha256 = (Get-FileHash -LiteralPath $archive).Hash }
        }
        if (!($packageFacts.Identity -contains 'ModernFormsNext/1.12.1')) { throw 'The published core package did not resolve.' }
        Invoke-Dotnet 'published-build' @('build', 'PublishedConsumer.csproj', '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false')
        $assembly = (Resolve-Path 'bin/Release/net10.0-windows/ModernFormsNext.PerformanceLab.dll').Path
    }
    finally { Pop-Location }
}
else {
    Push-Location $repo
    try {
        $sdk = (& dotnet --version).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the repository SDK.' }
    }
    finally { Pop-Location }
}

function Read-Environment {
    [ordered]@{
        Adapters = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate)
        Monitors = @(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorID | Where-Object Active | ForEach-Object {
            [ordered]@{
                Model = -join ($_.UserFriendlyName | Where-Object { $_ -ne 0 } | ForEach-Object { [char]$_ })
                PnpProduct = $_.InstanceName.Split('\')[1]
            }
        })
        PowerPlan = (powercfg /getactivescheme)
    }
}

$before = Read-Environment
foreach ($scenario in 'startup', 'virtualized-scroll') {
    $count = if ($scenario -eq 'startup') { 5 } else { 3 }
    foreach ($trial in 1..$count) {
        $id = "$scenario-$trial"
        Invoke-Dotnet $id @($assembly, '--scenario', $scenario, '--backend', 'software', '--output', (Join-Path $root $id))
    }
}
$after = Read-Environment
if (($before | ConvertTo-Json -Depth 8 -Compress) -cne ($after | ConvertTo-Json -Depth 8 -Compress)) {
    throw 'Display or power metadata changed during the trial series.'
}
$sourceFiles = @(Get-ChildItem -LiteralPath $harness -Filter '*.cs' -File | ForEach-Object {
    [ordered]@{ Name=$_.Name; Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
})
$results = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object Name -match '^(startup|virtualized-scroll)-\d+$' | ForEach-Object {
    $manifest = Join-Path $_.FullName 'results.json'
    [ordered]@{ Trial=$_.Name; Sha256=(Get-FileHash -LiteralPath $manifest).Hash }
})
[ordered]@{
    SourceTag = if ($PublishedBaseline) { 'v1.12.1' } else { $null }
    PublishedSourceCommit = $publishedCommit
    PackageFacts = $packageFacts
    HarnessSourceFiles = $sourceFiles
    HarnessAssemblySha256 = (Get-FileHash -LiteralPath $assembly).Hash
    SDK = $sdk
    Environment = $before
    StartupRegime = 'new-process; no OS cache reset; first trial retained separately'
    RawResults = $results
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'series.json')
Write-Host "Recorded $($results.Count) trials. Evidence is in $root."
