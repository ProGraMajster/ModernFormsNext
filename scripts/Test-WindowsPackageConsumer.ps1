<#
.SYNOPSIS
Builds the packed ModernFormsNext template and launches a Windows NuGet consumer.
.DESCRIPTION
Uses a fresh template hive and NuGet cache. Selects the repository SDK for the
default template and an SDK 11 pin for the .NET 11 option. The generated app
must load the expected CLR and native SkiaSharp, then exit on window close.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet(10, 11)][int]$RuntimeMajor,
    [ValidateRange(1, 20)][int]$Launches = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'The consumer requires a Windows desktop.' }
$repo = Split-Path -Parent $PSScriptRoot
$feed = (Resolve-Path -LiteralPath $PackageDirectory).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh output directory.' }
[IO.Directory]::CreateDirectory($root) | Out-Null

[xml]$props = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
$version = $props.SelectSingleNode('//ModernFormsNextPackageVersion').InnerText
$template = Join-Path $feed "ModernFormsNext.Templates.$version.nupkg"
$tfm = "net$RuntimeMajor.0-windows"
if ($RuntimeMajor -eq 10) {
    Copy-Item -LiteralPath (Join-Path $repo 'global.json') -Destination $root
} else {
    '{"sdk":{"version":"11.0.100","rollForward":"latestPatch","allowPrerelease":false}}' |
        Set-Content -LiteralPath (Join-Path $root 'global.json')
}

# Stop parent MSBuild discovery and resolve every ModernFormsNext dependency
# from the candidate feed, including transitive ones.
'<Project />' | Set-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
'<Project />' | Set-Content -LiteralPath (Join-Path $root 'Directory.Build.targets')
$escapedFeed = [Security.SecurityElement]::Escape($feed)
$config = Join-Path $root 'NuGet.Config'
@"
<configuration>
  <packageSources><clear /><add key="candidate" value="$escapedFeed" /><add key="public" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="candidate"><package pattern="ModernFormsNext*" /></packageSource>
    <packageSource key="public"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <config><add key="globalPackagesFolder" value="packages" /></config>
</configuration>
"@ | Set-Content -LiteralPath $config

function Invoke-ConsumerCommand([string]$Name, [string[]]$Arguments) {
    $log = Join-Path $root "$Name.log"
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & dotnet @Arguments *> $log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 30 | Write-Host
        throw "dotnet $Name failed ($LASTEXITCODE). See $log."
    }
    return $clock.Elapsed.TotalMilliseconds
}

Push-Location $root
try {
    $sdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdk -notlike "$RuntimeMajor.0.*") {
        throw "Expected SDK $RuntimeMajor.0, found $sdk."
    }
    $hive = @('--debug:custom-hive', (Join-Path $root 'hive'))
    $null = Invoke-ConsumerCommand 'install-template' (@('new', 'install', $template) + $hive)
    $selection = @()
    if ($RuntimeMajor -eq 11) { $selection = @('--FrameworkVersion', $tfm) }
    $null = Invoke-ConsumerCommand 'generate' (@('new', 'mfn-app', '-n', 'Consumer', '-o', 'app') + $selection + $hive)
    $project = Join-Path $root 'app/Consumer.csproj'
    [xml]$generated = Get-Content -LiteralPath $project -Raw
    if ($generated.SelectSingleNode('//TargetFramework').InnerText -cne $tfm -or
        $generated.SelectSingleNode("//PackageReference[@Include='ModernFormsNext']").Version -cne $version -or
        $generated.SelectSingleNode('//ProjectReference')) {
        throw "Generated project must use $tfm and the $version NuGet package."
    }
    $restoreMs = Invoke-ConsumerCommand 'restore' @('restore', $project, '--configfile', $config, '-r', 'win-x64', '-m:1')
    $buildMs = Invoke-ConsumerCommand 'build' @('build', $project, '-c', 'Release', '-r', 'win-x64', '--no-restore', '-m:1', '-p:UseSharedCompilation=false')
    $assets = Get-Content -Raw -LiteralPath 'app/obj/project.assets.json' | ConvertFrom-Json -AsHashtable
    $ridTarget = @($assets.targets.Keys | Where-Object { $_.EndsWith('/win-x64') })
    if ($ridTarget.Count -ne 1) { throw 'Expected one Windows x64 restore target.' }
    $windowsTarget = $assets.targets[$ridTarget[0]]
    $core = $windowsTarget["ModernFormsNext/$version"]
    if (!$core -or @($core.runtime.Keys | Where-Object { $_ -like 'lib/net10.0-windows*/ModernFormsNext.dll' }).Count -ne 1 -or
        !$windowsTarget.Contains("ModernFormsNext.WindowKit.Backend.Windows/$version")) {
        throw 'NuGet failed to resolve the Windows framework assets and backend from the candidate.'
    }

    $exe = Join-Path $root "app/bin/Release/$tfm/win-x64/Consumer.exe"
    $samples = foreach ($launch in 1..$Launches) {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru
        try {
            $clr = $null
            $skia = $null
            do {
                if ($process.HasExited) { throw "The consumer exited during startup ($($process.ExitCode))." }
                $process.Refresh()
                if ($process.MainWindowHandle -ne 0) {
                    $clr = $process.Modules | Where-Object ModuleName -EQ 'coreclr.dll'
                    $skia = $process.Modules | Where-Object ModuleName -EQ 'libSkiaSharp.dll'
                }
                if ($clr -and $skia) { break }
                Start-Sleep -Milliseconds 10
            } while ($clock.Elapsed.TotalSeconds -lt 30)
            if (!$clr -or !$skia -or $clr.FileVersionInfo.FileMajorPart -ne $RuntimeMajor) {
                throw "The consumer did not load the .NET $RuntimeMajor CLR and native SkiaSharp."
            }
            $sample = [ordered]@{
                Launch = $launch
                WindowAndSkiaMs = $clock.Elapsed.TotalMilliseconds
                WorkingSetBytes = $process.WorkingSet64
                PrivateBytes = $process.PrivateMemorySize64
                CoreClr = $clr.FileVersionInfo.ProductVersion
                SkiaSha256 = (Get-FileHash -LiteralPath $skia.FileName).Hash
            }
            if (!$process.CloseMainWindow() -or !$process.WaitForExit(10000) -or $process.ExitCode -ne 0) {
                throw 'The generated application failed to close normally.'
            }
            $sample
        }
        finally {
            if (!$process.HasExited) {
                $process.Kill($true)
                $null = $process.WaitForExit(5000)
            }
            $process.Dispose()
        }
    }

    [ordered]@{
        Status = 'Passed'
        Sdk = $sdk
        TargetFramework = $tfm
        PackageVersion = $version
        TemplateSha256 = (Get-FileHash -LiteralPath $template).Hash
        FrameworkSha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $exe) 'ModernFormsNext.dll')).Hash
        RestoreMs = $restoreMs
        BuildMs = $buildMs
        Samples = @($samples)
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath 'result.json'
    Write-Host "Generated $tfm app loaded CLR $RuntimeMajor and native Skia, then closed normally. Evidence: $root"
}
finally { Pop-Location }
