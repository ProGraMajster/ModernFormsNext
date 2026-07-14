[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$NoRestore,
    [switch]$NoAot,
    [ValidateSet('arm64-v8a', 'armeabi-v7a', 'x86', 'x86_64')][string]$AndroidAbi
)

Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repositoryRoot 'samples\ModernFormsNext.CrossPlatform.Sample\ModernFormsNext.CrossPlatform.Sample.csproj'

# Debug uses fast deployment for Visual Studio F5. A standalone adb install cannot consume those
# external assemblies, so command-line packaging intentionally embeds them into the signed APK.
$arguments = @(
    'build',
    $project,
    '-f', 'net10.0-android',
    '-c', $Configuration,
    '-t:SignAndroidPackage',
    '/p:AndroidPackageFormats=apk',
    '/p:EmbedAssembliesIntoApk=true'
)
if ($NoRestore) { $arguments += '--no-restore' }
if ($NoAot) { $arguments += '/p:RunAOTCompilation=false' }
if ($AndroidAbi) { $arguments += "/p:AndroidSupportedAbis=$AndroidAbi" }

Write-Host "Building standalone Android APK ($Configuration)..."
Invoke-CheckedNativeCommand -FilePath 'dotnet' -ArgumentList $arguments -Operation 'Android sample build'
$apk = Resolve-CrossPlatformSampleApk -RepositoryRoot $repositoryRoot -Configuration $Configuration
Write-Host "APK: $($apk.FullName)"
$apk
