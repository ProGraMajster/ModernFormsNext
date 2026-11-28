<#
.SYNOPSIS
Qualifies the candidate ModernFormsNext.Testing package in one isolated headless consumer.
.DESCRIPTION
Use a fresh output directory for each runtime. All framework dependencies resolve from the
candidate feed and are verified against its nupkg hashes. No packages are published.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet(10, 11)][int]$RuntimeMajor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path -Parent $PSScriptRoot
$feed = (Resolve-Path -LiteralPath $PackageDirectory).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh output directory.' }
[IO.Directory]::CreateDirectory($root) | Out-Null
$fixture = Join-Path $PSScriptRoot 'tests/fixtures/TestHostPackageConsumer'
Copy-Item -LiteralPath (Join-Path $fixture 'TestHostPackageConsumer.csproj'), (Join-Path $fixture 'Program.cs') -Destination $root

[xml]$props = Get-Content -Raw -LiteralPath (Join-Path $repo 'Directory.Build.props')
$version = $props.SelectSingleNode('//ModernFormsNextPackageVersion').InnerText
$tfm = "net$RuntimeMajor.0"
if ($RuntimeMajor -eq 10) {
    Copy-Item -LiteralPath (Join-Path $repo 'global.json') -Destination $root
} else {
    '{"sdk":{"version":"11.0.100","rollForward":"latestFeature","allowPrerelease":false}}' |
        Set-Content -LiteralPath (Join-Path $root 'global.json')
}

# Isolate MSBuild and NuGet discovery even when the output directory is under the repo.
'<Project />' | Set-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
'<Project />' | Set-Content -LiteralPath (Join-Path $root 'Directory.Build.targets')
$escapedFeed = [Security.SecurityElement]::Escape($feed)
@"
<configuration>
  <packageSources><clear /><add key="candidate" value="$escapedFeed" /><add key="public" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="candidate"><package pattern="ModernFormsNext*" /></packageSource>
    <packageSource key="public"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <config><add key="globalPackagesFolder" value="packages" /></config>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $root 'NuGet.Config')

function Invoke-ConsumerCommand([string]$Name, [string[]]$Arguments) {
    $log = Join-Path $root "$Name.log"
    & dotnet @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 30 | Write-Host
        throw "dotnet $Name failed ($LASTEXITCODE). See $log."
    }
}

Push-Location $root
try {
    $sdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdk -notlike "$RuntimeMajor.0.*") {
        throw "Expected SDK $RuntimeMajor.0, found $sdk."
    }

    $project = 'TestHostPackageConsumer.csproj'
    $parameters = @("-p:ConsumerTargetFramework=$tfm", "-p:CandidatePackageVersion=$version")
    Invoke-ConsumerCommand 'restore' (@('restore', $project, '--configfile', 'NuGet.Config', '--no-http-cache', '-m:1') + $parameters)
    $assets = Get-Content -Raw -LiteralPath 'obj/project.assets.json' | ConvertFrom-Json -AsHashtable
    $packages = [ordered]@{}
    $expectedPackages = @('ModernFormsNext.Testing', 'ModernFormsNext', 'ModernFormsNext.WindowKit', 'ModernFormsNext.WindowKit.Backend')
    foreach ($package in $expectedPackages) {
        if (!$assets.libraries.Contains("$package/$version")) { throw "Missing candidate $package/$version." }
        $entry = $assets.libraries["$package/$version"]
        $path = Join-Path $feed "$package.$version.nupkg"
        $hex = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash
        $hash = [Convert]::ToBase64String([Convert]::FromHexString($hex))
        if ($entry.type -cne 'package' -or $entry.sha512 -cne $hash) {
            throw "$package did not resolve to the expected candidate package bytes."
        }
        $packages[$package] = $hex
    }

    $frameworkEntries = @($assets.libraries.Keys | Where-Object { $_ -like 'ModernFormsNext*' })
    if ($frameworkEntries.Count -ne $expectedPackages.Count -or
        !$assets.targets[$tfm]["ModernFormsNext.Testing/$version"].runtime.Contains('lib/net10.0/ModernFormsNext.Testing.dll')) {
        throw 'Unexpected ModernFormsNext dependency closure or TestHost runtime asset.'
    }

    Invoke-ConsumerCommand 'build' (@('build', $project, '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false') + $parameters)
    Invoke-ConsumerCommand 'run' @("bin/Release/$tfm/TestHostPackageConsumer.dll", "$RuntimeMajor")
    $runtime = Get-Content -LiteralPath 'run.log'
    [ordered]@{
        Status = 'Passed'
        Sdk = $sdk
        TargetFramework = $tfm
        PackageVersion = $version
        PackageSha512 = $packages
        Output = @($runtime)
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath 'result.json'
    $runtime | Write-Host
}
finally { Pop-Location }
