<#
.SYNOPSIS
Runs a candidate VSIX startup smoke in a newly named Visual Studio profile.
.DESCRIPTION
Pass the candidate VSIX SHA-256 from release-evidence.json. The script uses the
installed Visual Studio SDK and checks that the opened Designer host belongs to the
new IDE process and to the candidate package. Inspect the opened document before
recording the visual observation in the release approval.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VsixPath,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-fA-F]{64}\z')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9]{1,9}(?:\.[0-9]{1,9}){2,3}\z')][string]$ExpectedVsixVersion,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(30, 300)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
$version = ConvertTo-ReleaseVersion -Version $Tag
if ($Tag -cne "v$version") { throw 'Use the exact tag from the release evidence.' }
$repository = Split-Path -Parent $PSScriptRoot
$vsix = (Resolve-Path -LiteralPath $VsixPath).Path
if ((Get-FileHash -LiteralPath $vsix -Algorithm SHA256).Hash -ne $ExpectedSha256) {
    throw 'VSIX bytes do not match the candidate evidence.'
}
$project = Join-Path $repository 'ModernFormsNext.VisualStudioExtension.Vsix'
& (Join-Path $project 'Validate-Vsix.ps1') -VsixPath $vsix -ExpectedVersion $ExpectedVsixVersion -Configuration Release -ProjectDirectory $project

if (![Environment]::UserInteractive -or @(Get-Process devenv,VSIXInstaller -ErrorAction SilentlyContinue).Count) {
    throw 'The VSIX installer requires an interactive desktop with Visual Studio and VSIX Installer closed.'
}
$vswhere = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Microsoft Visual Studio/Installer/vswhere.exe'
$installations = @(& $vswhere -products '*' -format json | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0 -or $installations.Count -ne 1) {
    throw 'Use a Windows validation environment with one Visual Studio installation for this installer smoke.'
}
$installation = $installations[0]
$vs = $installation.installationPath
$devenv = Join-Path $vs 'Common7/IDE/devenv.exe'
$installer = Join-Path $vs 'Common7/IDE/VSIXInstaller.exe'
$create = Join-Path $vs 'VSSDK/VisualStudioIntegration/Tools/Bin/CreateExpInstance.exe'
foreach ($tool in $devenv, $installer, $create) {
    if (!(Test-Path -LiteralPath $tool -PathType Leaf)) { throw 'Visual Studio and its extensibility SDK are required.' }
}
$suffix = 'MfnSmoke' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$profileBase = Join-Path $env:LOCALAPPDATA 'Microsoft/VisualStudio'
$majorVersion = ([version]$installation.installationVersion).Major.ToString() + '.0'
$baseProfile = "${majorVersion}_$($installation.instanceId)"
$ownedProfile = Join-Path $profileBase "$baseProfile$suffix"
$msbuild = Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
$sdkTasks = Join-Path $vs "MSBuild/Microsoft/VisualStudio/v$majorVersion/VSSDK/Microsoft.VisualStudio.Sdk.BuildTasks.17.0.dll"
foreach ($tool in $msbuild, $sdkTasks) {
    if (!(Test-Path -LiteralPath $tool -PathType Leaf)) { throw 'Visual Studio SDK deployment tools are required.' }
}
if (Test-Path -LiteralPath $ownedProfile) { throw 'The generated profile already exists.' }

$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh Designer smoke output directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$fixture = Join-Path $output 'ReleaseSmoke.mfdesign'
Copy-Item -LiteralPath (Join-Path $repository 'ModernFormsNext.Templates/templates/modernformsnext-app/MainForm.mfdesign') -Destination $fixture
$activity = Join-Path $output 'activity.xml'
$installLog = Join-Path $output 'vsix-install.log'

function Start-OwnedProcess([string]$Path, [string[]]$Arguments, [bool]$ShowWindow) {
    $info = [Diagnostics.ProcessStartInfo]::new($Path)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = !$ShowWindow
    $info.WorkingDirectory = $output
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    return [Diagnostics.Process]::Start($info)
}

function Invoke-Setup([string]$Path, [string[]]$Arguments) {
    $process = Start-OwnedProcess $Path $Arguments $false
    try {
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw 'Private Visual Studio setup timed out.'
        }
        if ($process.ExitCode -ne 0) { throw "Private Visual Studio setup failed with exit code $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
}

Invoke-Setup $create @('/Create', "/VSInstance=$baseProfile", "/RootSuffix=$suffix")
if (!(Test-Path -LiteralPath $ownedProfile -PathType Container)) { throw 'The SDK did not create the expected private profile.' }
Invoke-Setup $installer @('/quiet', "/rootSuffix:$suffix", "/logFile:$installLog", $vsix)

$hostPath = 'DesignerHost/ModernFormsNext.VisualStudioDesignerHost.exe'
$shellPath = 'ModernFormsNext.VisualStudioExtension.dll'
$entryHashes = @{}
$zip = [IO.Compression.ZipFile]::OpenRead($vsix)
try {
    foreach ($path in $hostPath, $shellPath) {
        $entry = $zip.GetEntry($path)
        if (!$entry) { throw 'The candidate is missing a Designer executable.' }
        $stream = $entry.Open()
        try { $entryHashes[$path] = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
        finally { $stream.Dispose() }
    }
}
finally { $zip.Dispose() }

# A fresh SDK profile can inherit a disabled extension registration. Enable only the
# installed candidate in that profile and refresh its editor registration before opening a file.
$extensions = (Join-Path $ownedProfile 'Extensions') + [IO.Path]::DirectorySeparatorChar
$deployments = @(Get-ChildItem -LiteralPath $extensions -Recurse -File -Filter $shellPath | Where-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash -ceq $entryHashes[$shellPath]
})
if ($deployments.Count -ne 1) { throw 'Expected one candidate extension deployment in the private profile.' }
$enable = Join-Path $output 'enable-vsix.proj'
@'
<Project>
  <UsingTask TaskName="EnableExtension" AssemblyFile="$(SmokeSdkTasks)" Architecture="x86" />
  <Target Name="EnableCandidate">
    <EnableExtension VsixIdentifier="ModernFormsNext.Designer" VsixVersion="$(SmokeVersion)"
                     DevenvInstallationDirectory="$(SmokeInstallation)" RootSuffix="$(SmokeProfile)"
                     FailIfNotInstalled="true" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $enable -Encoding utf8NoBOM
Invoke-Setup $msbuild @($enable, '/t:EnableCandidate', '/nologo', '/v:minimal', "/p:SmokeSdkTasks=$sdkTasks",
    "/p:SmokeInstallation=$vs", "/p:SmokeProfile=$suffix", "/p:SmokeVersion=$ExpectedVsixVersion")
Invoke-Setup $devenv @('/RootSuffix', $suffix, '/updateconfiguration')

$ide = Start-OwnedProcess $devenv @($fixture, '/RootSuffix', $suffix, '/Log', $activity) $true
$deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
$designArgument = '--design-file\s+"?' + [regex]::Escape($fixture) + '(?:"|\s|$)'
$ownerArgument = '--owner-process\s+' + $ide.Id + '(?:\s|$)'
while ([datetime]::UtcNow -lt $deadline -and !$ide.HasExited) {
    $hosts = @(Get-CimInstance Win32_Process -Filter "Name='ModernFormsNext.VisualStudioDesignerHost.exe'" |
        Where-Object { $_.CommandLine -match $designArgument -and $_.CommandLine -match $ownerArgument })
    foreach ($hostProcess in $hosts) {
        if (!$hostProcess.ExecutablePath -or !$hostProcess.ExecutablePath.StartsWith($extensions, [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $hostProcess.ExecutablePath -Algorithm SHA256).Hash -cne $entryHashes[$hostPath]) {
            throw 'The Designer opened from outside the qualified private deployment.'
        }
        $deployedShell = Join-Path (Split-Path -Parent (Split-Path -Parent $hostProcess.ExecutablePath)) $shellPath
        if ((Get-FileHash -LiteralPath $deployedShell -Algorithm SHA256).Hash -cne $entryHashes[$shellPath]) {
            throw 'The installed Designer extension does not match the candidate VSIX.'
        }
        $log = Join-Path ([IO.Path]::GetTempPath()) "ModernFormsNextDesignerHost-$($hostProcess.ProcessId).log"
        if ((Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -SimpleMatch 'IPC_READY' -Quiet)) {
            Copy-Item -LiteralPath $log -Destination (Join-Path $output 'designer-host.log')
            [ordered]@{
                tag = $Tag; commit = $Commit; vsixVersion = $ExpectedVsixVersion; vsixSha256 = $ExpectedSha256.ToLowerInvariant()
                status = 'host-ready'; idePid = $ide.Id; designerPid = $hostProcess.ProcessId
                profileSuffix = $suffix; observedAtUtc = [datetime]::UtcNow.ToString('o')
            } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'designer-smoke.json') -Encoding utf8NoBOM
            Write-Host "Inspect ReleaseSmoke.mfdesign in the isolated Visual Studio window. Evidence: $output"
            return
        }
    }
    Start-Sleep -Milliseconds 500
}
throw "The candidate Designer host did not signal readiness. Inspect the isolated IDE and logs in $output."
