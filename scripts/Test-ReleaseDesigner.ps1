#requires -Version 7.2
<#
.SYNOPSIS
Runs a candidate VSIX startup smoke in a newly named Visual Studio profile.
.DESCRIPTION
Pass the candidate VSIX SHA-256 from release-evidence.json. The script uses the
installed Visual Studio SDK and checks editor selection, the installed host's IPC
and its normal shutdown. Use -Inspect to pause for the release visual observation
before the script closes the copied document.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VsixPath,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-fA-F]{64}\z')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9]{1,9}(?:\.[0-9]{1,9}){2,3}\z')][string]$ExpectedVsixVersion,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$Inspect,
    [ValidateRange(30, 300)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'DesignerStartup.Common.psm1') -Force
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
    Assert-DesignerStartupPackage $zip.Entries.FullName
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
$extensions = Join-Path $ownedProfile 'Extensions'
$deployments = @(Get-ChildItem -LiteralPath $extensions -Recurse -File -Filter $shellPath)
if ($deployments.Count -ne 1 -or (Get-FileHash -LiteralPath $deployments[0].FullName).Hash -cne $entryHashes[$shellPath]) {
    throw 'Expected one exact candidate extension deployment in the private profile.'
}
$deployedHost = Join-Path $deployments[0].DirectoryName $hostPath
if ((Get-FileHash -LiteralPath $deployedHost).Hash -cne $entryHashes[$hostPath] -or
    (Test-Path -LiteralPath (Join-Path (Split-Path $deployedHost) 'PreviewWorker'))) {
    throw 'The installed Designer host does not match the standard candidate VSIX.'
}
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

function Invoke-EditorCheck([string]$Operation, [string]$ExpectedStatus, [string]$OutputFile) {
    $path = Join-Path $output $OutputFile
    $helper = Start-OwnedProcess (Join-Path $PSHOME 'pwsh.exe') @(
        '-NoProfile', '-STA', '-File', (Join-Path $PSScriptRoot 'Invoke-DesignerStartupEditor.ps1'),
        '-IdeProcessId', [string]$result.idePid, '-IdeStart', [string]$result.ideStart,
        '-VisualStudioPath', $vs, '-RootSuffix', $suffix, '-Document', $fixture,
        '-Operation', $Operation, '-OutputPath', $path, '-TimeoutSeconds', [string][Math]::Min(120, $TimeoutSeconds)
    ) $false
    try {
        if (!$helper.WaitForExit($TimeoutSeconds * 1000)) {
            throw "The $Operation editor check timed out. Inspect $path."
        }
        if ($helper.ExitCode -ne 0 -or !(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Visual Studio did not complete the $Operation editor check. Inspect $path."
        }
        $evidence = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($evidence.status -cne $ExpectedStatus -or $evidence.idePid -ne $result.idePid -or
            $evidence.ideStart -ne $result.ideStart -or $evidence.document -ine $fixture -or
            $evidence.actualFactory -cne 'c61567c8-f5ac-4f9e-9c6e-b4ec99c7ab31') {
            throw 'The exact IDE and registered Designer frame were not confirmed.'
        }
        return $evidence.actualFactory
    }
    finally {
        if (!$helper.HasExited) {
            try { $helper.Kill() }
            catch [InvalidOperationException] { if (!$helper.HasExited) { throw } }
        }
        $helper.Dispose()
    }
}

function Copy-CurrentLog([string]$Role, [int]$ProcessId, [long]$ProcessStart) {
    $source = Join-Path ([IO.Path]::GetTempPath()) "ModernFormsNextDesigner$Role-$ProcessId.log"
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { return '' }
    $runLog = Get-DesignerProcessLog (Get-Content -LiteralPath $source -Raw) $ProcessStart
    if ($runLog) {
        Set-Content -LiteralPath (Join-Path $output "designer-$($Role.ToLowerInvariant()).log") -Value $runLog -Encoding utf8NoBOM
    }
    return $runLog
}

$result = [ordered]@{
    tag = $Tag; commit = $Commit; vsixVersion = $ExpectedVsixVersion; vsixSha256 = $ExpectedSha256.ToLowerInvariant()
    installedSha256 = $entryHashes; visualStudioVersion = $installation.installationVersion; profileSuffix = $suffix
    status = 'FAIL'; stage = 'Open'; idePid = $null; ideStart = $null; editorFactory = $null
    designerPid = $null; designerStart = $null; dirtyReply = $null; designerExitCode = $null
    normalHostShutdown = $false; ideExited = $false; ideExitCode = $null; failure = $null
}
$ide = $null
$verifiedHost = $null
try {
    # /Edit exercises the normal standalone editor selection. Forcing the SDK
    # factory without a project hierarchy can crash the shell before factory entry.
    $ide = Start-OwnedProcess $devenv @('/RootSuffix', $suffix, '/Log', $activity, '/Edit', $fixture) $true
    $result.idePid = $ide.Id
    $result.ideStart = $ide.StartTime.ToFileTimeUtc()
    $result.editorFactory = Invoke-EditorCheck 'Observe' 'Observed' 'editor-open.json'
    $result.stage = 'HostReady'
    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    $designArgument = '--design-file\s+"?' + [regex]::Escape($fixture) + '(?:"|\s|$)'
    $ownerArgument = '--owner-process\s+' + $ide.Id + '(?:\s|$)'
    while ([datetime]::UtcNow -lt $deadline -and !$ide.HasExited) {
        $hosts = @(Get-CimInstance Win32_Process -Filter "Name='ModernFormsNext.VisualStudioDesignerHost.exe'" |
            Where-Object { $_.CommandLine -match $designArgument -and $_.CommandLine -match $ownerArgument })
        if ($hosts.Count -gt 1) { throw 'More than one Designer host claims the owned fixture.' }
        if ($hosts.Count -eq 1) {
            $candidate = $hosts[0]
            if ($candidate.ExecutablePath -ine $deployedHost -or $candidate.ParentProcessId -ne $ide.Id -or
                (Get-FileHash -LiteralPath $deployedHost).Hash -cne $entryHashes[$hostPath]) {
                throw 'The Designer host is outside the qualified private deployment.'
            }
            $process = Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
            if ($process) {
                try {
                    $creation = $process.StartTime.ToFileTimeUtc()
                    $editorLog = Copy-CurrentLog 'Editor' $ide.Id $result.ideStart
                    if (Get-DesignerHostLaunch $editorLog $process.Id $creation) {
                        $result.designerPid = $process.Id
                        $result.designerStart = $creation
                        $hostLog = Copy-CurrentLog 'Host' $process.Id $creation
                        $pipe = [regex]::Match($hostLog, '(?m)^\[[^\r\n]*\] IPC_READY PipeName=(ModernFormsNextDesignerHost-[0-9A-F]{16}) ')
                        if ($hostLog -match 'MAIN_EXCEPTION|APPDOMAIN_UNHANDLED_EXCEPTION') {
                            throw 'The verified Designer host logged an unhandled exception.'
                        }
                        if ($pipe.Success -and $hostLog -cmatch '(?m)^\[[^\r\n]*\] FORM_SHOWN\b') {
                            $expectedPipe = '--pipe\s+"?' + $pipe.Groups[1].Value + '(?:"|\s|$)'
                            if ($candidate.CommandLine -cnotmatch $expectedPipe) { throw 'The verified Designer pipe does not match its launch arguments.' }
                            # Keep a handle so the exit code can be obtained after closing the frame.
                            $null = $process.SafeHandle
                            $verifiedHost = $process
                            break
                        }
                    }
                }
                finally { if ($null -eq $verifiedHost) { $process.Dispose() } }
            }
        }
        Start-Sleep -Milliseconds 250
    }
    if (!$verifiedHost -or $verifiedHost.HasExited) {
        throw 'The candidate Designer host did not reach FORM_SHOWN and IPC_READY.'
    }
    $result.stage = 'Ipc'
    $result.dirtyReply = Get-DesignerSmokeDirtyState $pipe.Groups[1].Value $fixture $result.designerPid
    if ($Inspect) {
        Write-Host 'Inspect ReleaseSmoke.mfdesign in the isolated Visual Studio profile.'
        $null = Read-Host 'Record your visual observation, then press Enter to close the fixture'
    }
    $result.stage = 'Shutdown'
    $null = Invoke-EditorCheck 'Close' 'Closed' 'editor-close.json'
    if (!$verifiedHost.WaitForExit($TimeoutSeconds * 1000)) { throw 'The Designer host did not exit after closing its editor.' }
    $result.designerExitCode = $verifiedHost.ExitCode
    $editorLog = Copy-CurrentLog 'Editor' $ide.Id $result.ideStart
    $hostLog = Copy-CurrentLog 'Host' $verifiedHost.Id $result.designerStart
    if (!(Test-DesignerSmokeShutdown $editorLog $verifiedHost.Id $verifiedHost.ExitCode) -or
        $hostLog -cnotmatch '(?m)^\[[^\r\n]*\] SHUTDOWN_RECEIVED\b') {
        throw 'The verified Designer host did not acknowledge and complete normal shutdown.'
    }
    $result.normalHostShutdown = $true
    $result.stage = 'Complete'
    $result.status = 'PASS'
}
catch {
    $result.failure = $_.Exception.Message
    throw
}
finally {
    try {
        if ($result.designerStart) { $null = Copy-CurrentLog 'Host' $result.designerPid $result.designerStart }
        if ($result.ideStart) { $null = Copy-CurrentLog 'Editor' $result.idePid $result.ideStart }
        if ($ide) {
            if (!$ide.HasExited -and $ide.CloseMainWindow()) { $null = $ide.WaitForExit(5000) }
            $result.ideExited = $ide.HasExited
            if ($result.ideExited) {
                $result.ideExitCode = $ide.ExitCode
                if ($result.status -eq 'PASS' -and $ide.ExitCode -ne 0) {
                    $result.status = 'FAIL'
                    $result.stage = 'IdeExit'
                    $result.failure = "The private Visual Studio instance exited with code $($ide.ExitCode)."
                    throw $result.failure
                }
            }
            else { Write-Warning "Close only the private Visual Studio instance with suffix $suffix." }
        }
    }
    finally {
        if ($verifiedHost) { $verifiedHost.Dispose() }
        if ($ide) { $ide.Dispose() }
        $result.observedAtUtc = [datetime]::UtcNow.ToString('o')
        $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'designer-smoke.json') -Encoding utf8NoBOM
        Write-Host "Designer smoke evidence: $output"
    }
}
