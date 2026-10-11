<#
.SYNOPSIS
Validates the Experimental Android backend through isolated NuGet-only consumers.
.DESCRIPTION
Creates a fresh run below OutputDirectory, blocks parent MSBuild imports, and maps all
ModernFormsNext packages exclusively to the supplied local feed. No checkout assemblies or
ProjectReferences are used. Requires the repository SDK, Android workload, SDK 36 and JDK 21.
Builds both API surfaces and standalone Debug APK / Release APK and AAB with AOT and trimming.
Does not deploy, sign with a release key, create a tag, or publish any package.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$PackageDirectory,
    [Parameter(Mandatory = $true)] [string]$OutputDirectory,
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    [xml]$properties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
    $ExpectedVersion = $properties.Project.PropertyGroup.ModernFormsNextPackageVersion
}
$feed = (Resolve-Path -LiteralPath $PackageDirectory).Path
$runRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)
function Write-ConsumerFile([string]$Path, [string]$Content) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, $encoding)
}
function Invoke-ConsumerDotnet([string[]]$Arguments, [string]$LogName) {
    & dotnet @Arguments *> (Join-Path $runRoot "$LogName.log")
    if ($LASTEXITCODE -ne 0) {
        Get-Content (Join-Path $runRoot "$LogName.log") -Tail 35 | Write-Host
        throw "Consumer command failed: dotnet $($Arguments -join ' ')"
    }
}

# These files stop discovery of checkout-level Directory.Build imports and package caches.
Write-ConsumerFile (Join-Path $runRoot 'Directory.Build.props') '<Project />'
Write-ConsumerFile (Join-Path $runRoot 'Directory.Build.targets') '<Project />'
[IO.File]::Copy((Join-Path $repositoryRoot 'global.json'), (Join-Path $runRoot 'global.json'))
$escapedFeed = [Security.SecurityElement]::Escape($feed)
$cache = Join-Path $runRoot 'packages'
$escapedCache = [Security.SecurityElement]::Escape($cache)
Write-ConsumerFile (Join-Path $runRoot 'NuGet.Config') @"
<configuration>
  <packageSources><clear /><add key="local" value="$escapedFeed" /><add key="public" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="ModernFormsNext*" /></packageSource>
    <packageSource key="public"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <config><add key="globalPackagesFolder" value="$escapedCache" /></config>
</configuration>
"@
$neutral = Join-Path $runRoot 'neutral/Consumer.csproj'
Write-ConsumerFile $neutral @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><PackageReference Include="ModernFormsNext.WindowKit.Backend.Android" Version="$ExpectedVersion" /></ItemGroup>
</Project>
"@
Write-ConsumerFile (Join-Path $runRoot 'neutral/Program.cs') @'
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
if (AndroidDensityConverter.ToLogical(48, 2) != 24) throw new Exception("Density contract failed.");
Console.WriteLine(typeof(AndroidDensityConverter).Assembly.FullName);
'@
$android = Join-Path $runRoot 'android/Consumer.csproj'
Write-ConsumerFile $android @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework><OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <SupportedOSPlatformVersion>23.0</SupportedOSPlatformVersion>
    <ApplicationId>com.modernformsnext.packageconsumer</ApplicationId>
    <ApplicationVersion>1</ApplicationVersion><ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <AndroidManifest>AndroidManifest.xml</AndroidManifest>
    <EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
  </PropertyGroup>
  <PropertyGroup Condition="'`$(Configuration)' == 'Release'">
    <RunAOTCompilation>true</RunAOTCompilation><PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ModernFormsNext" Version="$ExpectedVersion" />
    <PackageReference Include="ModernFormsNext.WindowKit.Backend.Android" Version="$ExpectedVersion" />
    <!-- Same Android-compatible shared-text native dependency as the existing sample. -->
    <PackageReference Include="HarfBuzzSharp" Version="14.2.1.1" />
  </ItemGroup>
</Project>
"@
Write-ConsumerFile (Join-Path $runRoot 'android/AndroidManifest.xml') '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application android:label="Android package consumer" /></manifest>'
Write-ConsumerFile (Join-Path $runRoot 'android/MainActivity.cs') @'
using Android.App;
using Android.Runtime;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Backend.Android.Windowing;

[Application]
public sealed class ConsumerApplication : Android.App.Application
{
    public ConsumerApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }
    public override void OnCreate()
    {
        base.OnCreate();
        AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this));
    }
}
[Activity(MainLauncher = true, Exported = true)]
public sealed class MainActivity : AndroidWindowActivity
{
    protected override void OnStartApplication()
    {
        var form = new ModernFormsNext.Form { Text = "NuGet consumer" };
        form.Controls.Add(new ModernFormsNext.TextBox { Text = "Package-only Android host" });
        // Compile the public native-host contract needed by future WebView consumers.
        _ = typeof(AndroidNativeViewSite);
        ModernFormsNext.Application.Run(form);
    }
}
'@

Push-Location $runRoot
try {
    $common = @('-m:1', '/p:UseSharedCompilation=false')
    Invoke-ConsumerDotnet (@('restore', $neutral, '--configfile', (Join-Path $runRoot 'NuGet.Config'), '--no-cache') + $common) 'neutral-restore'
    Invoke-ConsumerDotnet (@('build', $neutral, '-c', 'Release', '--no-restore') + $common) 'neutral-build'
    Invoke-ConsumerDotnet @('run', '--project', $neutral, '-c', 'Release', '--no-build', '--no-restore') 'neutral-run'
    Invoke-ConsumerDotnet (@('restore', $android, '-r', 'android-arm64', '--configfile', (Join-Path $runRoot 'NuGet.Config'), '--no-cache') + $common) 'android-restore'
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-ConsumerDotnet (@('build', $android, '-c', $configuration, '-r', 'android-arm64', '--no-restore', '-t:SignAndroidPackage', '/p:AndroidPackageFormats=apk') + $common) "android-$configuration-apk"
        $apk = Get-ChildItem (Join-Path $runRoot "android/bin/$configuration") -Recurse -Filter '*-Signed.apk' | Select-Object -First 1
        if ($null -eq $apk) { throw "Missing $configuration APK." }
        # Switching AndroidPackageFormats to AAB removes stale APK outputs. Preserve each
        # successful package before the next SDK invocation rather than inspect stale bin files.
        [IO.File]::Copy($apk.FullName, (Join-Path $runRoot "$configuration.apk"))
    }
    Invoke-ConsumerDotnet (@('build', $android, '-c', 'Release', '-r', 'android-arm64', '--no-restore', '-t:SignAndroidPackage', '/p:AndroidPackageFormats=aab') + $common) 'android-Release-aab'

    foreach ($project in @($neutral, $android)) {
        [xml]$projectXml = Get-Content $project -Raw
        if (@($projectXml.SelectNodes('//ProjectReference')).Count) { throw 'Consumer contains a ProjectReference.' }
        $assetsPath = Join-Path (Split-Path -Parent $project) 'obj/project.assets.json'
        $assets = Get-Content $assetsPath -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -cne 'package') { throw "Non-package input: $($library.Key)" }
            if ($library.Key -like 'ModernFormsNext*' -and ($library.Key -split '/')[1] -cne $ExpectedVersion) { throw "Mismatched internal version: $($library.Key)" }
        }
        if ($assets.packageFolders.Count -ne 1 -or -not [IO.Path]::GetFullPath(@($assets.packageFolders.Keys)[0]).TrimEnd('\', '/').Equals($cache.TrimEnd('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Consumer did not use the private package cache.' }
    }
    $androidAssets = Get-Content (Join-Path $runRoot 'android/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($dependency in @('SkiaSharp.Views/3.119.2', 'System.Formats.Nrbf/10.0.10')) {
        if (-not $androidAssets.libraries.ContainsKey($dependency)) { throw "Missing Android dependency $dependency" }
    }
    $neutralAssets = Get-Content (Join-Path $runRoot 'neutral/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($neutralAssets.libraries.ContainsKey('SkiaSharp.Views/3.119.2') -or $neutralAssets.libraries.ContainsKey('System.Formats.Nrbf/10.0.10')) { throw 'Android-only dependencies leaked into the neutral consumer.' }

    $manifestPath = Join-Path $runRoot 'android/obj/Release/net10.0-android/android-arm64/android/AndroidManifest.xml'
    [xml]$manifest = Get-Content $manifestPath -Raw
    $androidNamespace = 'http://schemas.android.com/apk/res/android'
    if ($manifest.manifest.'uses-sdk'.GetAttribute('minSdkVersion', $androidNamespace) -cne '23') { throw 'Minimum Android API changed.' }
    if (@($manifest.SelectNodes('/manifest/uses-permission')).Count) { throw 'Release consumer unexpectedly requests permissions.' }
    if (@($manifest.SelectNodes('/manifest/queries/intent')).Count -lt 10) { throw 'Library manifest queries were lost.' }
    $activities = @($manifest.SelectNodes('/manifest/application/activity'))
    if ($activities.Count -ne 1 -or @($manifest.SelectNodes('/manifest/application/service')).Count) { throw 'Unexpected backend Activity/service contribution.' }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($configuration in @('Debug', 'Release')) {
        $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $runRoot "$configuration.apk"))
        try {
            foreach ($native in @('libSkiaSharp.so', 'libHarfBuzzSharp.so')) {
                if (-not @($archive.Entries | Where-Object FullName -Like "*/$native").Count) { throw "Missing native asset $native." }
            }
            if ($configuration -ceq 'Release' -and -not @($archive.Entries | Where-Object FullName -Like '*/libaot-*.so').Count) { throw 'Release APK contains no AOT output.' }
        }
        finally { $archive.Dispose() }
    }
    if (-not @(Get-ChildItem (Join-Path $runRoot 'android/bin/Release') -Recurse -Filter '*.aab').Count) { throw 'Missing Release AAB.' }
    Write-ConsumerFile (Join-Path $runRoot 'result.txt') "PASS: both TFMs, isolated matching-version packages, Debug APK, Release APK/AAB, AOT/trimming, min API 23, native assets, no permissions; run $runRoot"
    Write-Host "Android package consumers PASS: $runRoot"
}
finally { Pop-Location }
