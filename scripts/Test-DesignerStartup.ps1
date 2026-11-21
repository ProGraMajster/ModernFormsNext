#requires -Version 7.2
<#
.SYNOPSIS
Collects bounded Designer startup evidence from an installed experimental VSIX.
.DESCRIPTION
Checks the exact checkout, synthetic fixture and installed package bytes, compares standalone
and integrated native hosting outside Visual Studio, then opens a copy of the fixture in the
selected experimental profile. Leaves all previous evidence and profile settings intact.
PASS describes startup and a responsive IPC command only. It grants no LPAC qualification.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VsixPath,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-fA-F]{64}\z')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit,
    [ValidatePattern('\A[A-Za-z0-9]{1,40}\z')][string]$RootSuffix = 'MFN486',
    [string]$VisualStudioPath,
    [string]$OutputDirectory,
    [ValidateRange(10, 120)][int]$ObservationSeconds = 20,
    [switch]$ExpectExecutablePreview
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'DesignerStartup.Common.psm1') -Force
$repository = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repository ('artifacts/designer-startup/' +
        (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$started = Get-Date
$results = [Collections.Generic.List[object]]::new()
$observed = [Collections.Generic.HashSet[int]]::new()
$identity = [ordered]@{ requestedCommit = $Commit; rootSuffix = $RootSuffix; startedAt = $started.ToString('o');
    expectExecutablePreview = [bool]$ExpectExecutablePreview }
$status = 'BLOCKED'
$reason = ''
$editorHelper = $null
$editorRouting = $null

function Start-DiagnosticProcess([string]$Path, [string[]]$Arguments, [bool]$Visible, [bool]$CaptureOutput = $false) {
    $info = [Diagnostics.ProcessStartInfo]::new($Path)
    $info.UseShellExecute = $false
    $info.WorkingDirectory = $output
    $info.WindowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
    if ($CaptureOutput) { $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    if ($CaptureOutput) {
        # Drain both streams asynchronously so compiler/COM diagnostics cannot fill a pipe
        # and stall the separately bounded editor-selection helper.
        $script:helperOutput = $process.StandardOutput.ReadToEndAsync()
        $script:helperError = $process.StandardError.ReadToEndAsync()
    }
    return $process
}

function Stop-EditorHelper {
    if ($null -eq $editorHelper) { return }
    if (!$editorHelper.HasExited) {
        "Requested termination of owned editor helper $($editorHelper.Id) at $([DateTimeOffset]::Now.ToString('o'))" |
            Add-Content -LiteralPath (Join-Path $output 'termination.log')
        try { $editorHelper.Kill() }
        catch [InvalidOperationException] { if (!$editorHelper.HasExited) { throw } }
        [void]$editorHelper.WaitForExit(3000)
    }
    if ($editorHelper.HasExited) {
        [IO.File]::WriteAllText((Join-Path $output 'editor-helper.stdout.txt'), $helperOutput.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $output 'editor-helper.stderr.txt'), $helperError.GetAwaiter().GetResult())
    }
    $editorHelper.Dispose()
    $script:editorHelper = $null
}

function Copy-ProcessLog([int]$ProcessId, [string]$Kind, [string]$Label, [long]$ProcessStart) {
    $source = Join-Path ([IO.Path]::GetTempPath()) "ModernFormsNextDesigner$Kind-$ProcessId.log"
    if (Test-Path -LiteralPath $source) {
        $stream = [IO.File]::Open($source, [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        try { $text = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $text = Get-DesignerProcessLog $text $ProcessStart
        [IO.File]::WriteAllText((Join-Path $output "$Label-$Kind-$ProcessId.log"), $text)
        return $text
    }
    return ''
}

function Save-ProcessSample([Diagnostics.Process]$Process, [string]$Label) {
    $sample = [ordered]@{ at = [DateTimeOffset]::Now.ToString('o'); label = $Label; pid = $Process.Id }
    try {
        $Process.Refresh()
        $sample.exited = $Process.HasExited
        if ($Process.HasExited) { $sample.exitCode = $Process.ExitCode }
        else {
            $sample.cpuMilliseconds = $Process.TotalProcessorTime.TotalMilliseconds
            $sample.workingSet = $Process.WorkingSet64
            $sample.handles = $Process.HandleCount
            $sample.threads = @($Process.Threads | ForEach-Object {
                $thread = $_
                $entry = [ordered]@{ nativeTid = $thread.Id }
                try {
                    $entry.state = [string]$thread.ThreadState
                    if ($thread.ThreadState -eq 'Wait') { $entry.waitReason = [string]$thread.WaitReason }
                }
                catch [InvalidOperationException] { $entry.state = 'ExitedDuringSample' }
                $entry
            })
        }
    }
    catch {
        # Process/thread enumeration races with termination. Keep that observation while
        # allowing the owned-process cleanup below to run.
        $sample.observationError = $_.Exception.GetType().Name
        $sample.hresult = $_.Exception.HResult
    }
    $sample | ConvertTo-Json -Depth 5 -Compress | Add-Content -LiteralPath (Join-Path $output 'processes.jsonl') -Encoding utf8
}

function Send-DesignerCommand([string]$Pipe, [string]$Command, [string]$Document) {
    $client = [IO.Pipes.NamedPipeClientStream]::new('.', $Pipe, [IO.Pipes.PipeDirection]::InOut,
        [IO.Pipes.PipeOptions]::Asynchronous)
    $cancel = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
    try {
        $connection = $client.ConnectAsync($cancel.Token)
        while (!$connection.IsCompleted) { [MfnStartupNative]::Pump(); Start-Sleep -Milliseconds 10 }
        $connection.GetAwaiter().GetResult()
        $writer = [IO.StreamWriter]::new($client, [Text.UTF8Encoding]::new($false), 1024, $true)
        $reader = [IO.StreamReader]::new($client, [Text.Encoding]::UTF8, $true, 1024, $true)
        try {
            $writer.AutoFlush = $true
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Document))
            $writer.WriteLine("$Command`t$encoded`t`t")
            $response = $reader.ReadLineAsync().WaitAsync($cancel.Token)
            while (!$response.IsCompleted) { [MfnStartupNative]::Pump(); Start-Sleep -Milliseconds 10 }
            return $response.GetAwaiter().GetResult()
        }
        finally { $writer.Dispose(); $reader.Dispose() }
    }
    finally { $client.Dispose(); $cancel.Dispose() }
}

function Invoke-NativeProbe([string]$Mode, [string]$Label) {
    $pipe = 'ModernFormsNext-Startup-' + [guid]::NewGuid().ToString('N')
    $parent = [IntPtr]::Zero
    $process = $null
    $forced = $false
    $commandOk = $false
    $failure = ''
    try {
        $arguments = @('--design-file', $fixture, '--project', $fixtureProject, '--pipe', $pipe,
            '--host-mode', $Mode, '--owner-process', [string]$PID)
        if ($Mode -eq 'integrated') {
            $parent = [MfnStartupNative]::CreateParent()
            $arguments += @('--parent-window', $parent.ToInt64().ToString([Globalization.CultureInfo]::InvariantCulture))
        }
        $process = Start-DiagnosticProcess $hostExecutable $arguments $false
        [void]$observed.Add($process.Id)
        $deadline = [DateTime]::UtcNow.AddSeconds($ObservationSeconds)
        do {
            [MfnStartupNative]::Pump()
            Save-ProcessSample $process $Label
            $log = Copy-ProcessLog $process.Id 'Host' $Label $process.StartTime.ToFileTimeUtc()
            if ($process.HasExited -or ($log -match 'IPC_READY' -and $log -match 'FORM_SHOWN')) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
        if (!$process.HasExited -and $log -match 'IPC_READY') {
            $commandOk = (Send-DesignerCommand $pipe 'DIRTY' $fixture) -eq "DIRTY`t0"
            if ((Send-DesignerCommand $pipe 'SHUTDOWN' $fixture) -ne 'OK') { throw 'The shutdown command was rejected.' }
            $deadline = [DateTime]::UtcNow.AddSeconds(3)
            while (!$process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
                [MfnStartupNative]::Pump()
                Start-Sleep -Milliseconds 50
            }
        }
    }
    catch { $failure = $_.Exception.Message }
    finally {
        if ($null -ne $process) {
            Save-ProcessSample $process $Label
            if (!$process.HasExited) {
                $forced = $true
                "Requested termination of owned diagnostic host $($process.Id) at $([DateTimeOffset]::Now.ToString('o'))" |
                    Add-Content -LiteralPath (Join-Path $output 'termination.log')
                try { $process.Kill() }
                catch [InvalidOperationException] { if (!$process.HasExited) { throw } }
                [void]$process.WaitForExit(3000)
            }
            $log = Copy-ProcessLog $process.Id 'Host' $Label $process.StartTime.ToFileTimeUtc()
            $exited = $process.HasExited
            $exitCode = if ($exited) { $process.ExitCode } else { $null }
            $result = Get-DesignerStartupStatus $log $exited $exitCode $forced
            if (!$commandOk -and $result -eq 'PASS') {
                $result = 'FAIL'
                $failure = 'The direct host did not answer the DIRTY command correctly.'
            }
            if (!$failure -and $result -ne 'PASS') { $failure = 'The direct host did not complete bounded startup and clean shutdown.' }
            $results.Add([ordered]@{ label = $Label; status = $result; pid = $process.Id;
                forcedStop = $forced; exitCode = $exitCode; ipcResponsive = $commandOk; failure = $failure })
            $process.Dispose()
        }
        else { $results.Add([ordered]@{ label=$Label; status='BLOCKED'; failure=$failure }) }
        if ($parent -ne [IntPtr]::Zero) { [MfnStartupNative]::DestroyWindow($parent) | Out-Null }
    }
}

try {
    if (!$IsWindows) { throw 'This diagnostic requires an interactive Windows desktop.' }
    if (![Environment]::UserInteractive) { throw 'An interactive desktop is required.' }
    $identity.checkoutCommit = (git -C $repository rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $identity.checkoutCommit -cne $Commit) { throw 'Unexpected checkout commit.' }
    $fixtureRelative = 'scripts/tests/fixtures/DesignerExecutablePreview'
    $hashes = foreach ($name in 'PreviewExample.mfdesign', 'ProjectFixture.csproj', 'PreviewBadge.cs') {
        $relative = "$fixtureRelative/$name"
        $expected = (git -C $repository rev-parse "${Commit}:$relative").Trim()
        $actual = (git -C $repository hash-object -- $relative).Trim()
        if ($LASTEXITCODE -ne 0 -or $actual -cne $expected) { throw "Fixture source differs from commit ($name)." }
        Get-FileHash -LiteralPath (Join-Path $repository $relative) -Algorithm SHA256 |
            Select-Object @{Name='name';Expression={$name}},Hash
    }
    $identity.fixtureHashes = @($hashes)
    $vsix = (Resolve-Path -LiteralPath $VsixPath).Path
    $identity.vsixSha256 = (Get-FileHash -LiteralPath $vsix -Algorithm SHA256).Hash
    if ($identity.vsixSha256 -ine $ExpectedSha256) { throw 'VSIX SHA-256 differs from the expected candidate.' }
    $identity.windows = Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/Windows NT/CurrentVersion' |
        Select-Object ProductName,EditionID,DisplayVersion,CurrentBuildNumber,UBR
    $identity.architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $identity.dotnet = @(& dotnet --info 2>&1 | ForEach-Object { [string]$_ })
    if (!$VisualStudioPath) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $installations = @(& $vswhere -products '*' -version '[17.0,)' -format json | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0 -or $installations.Count -ne 1) { throw 'Specify -VisualStudioPath for the intended installation.' }
        $VisualStudioPath = $installations[0].installationPath
    }
    $devenv = Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe'
    $identity.visualStudioVersion = (Get-Item -LiteralPath $devenv).VersionInfo.FileVersion
    $major = (Get-Item -LiteralPath $devenv).VersionInfo.FileMajorPart
    $profileBase = Join-Path $env:LOCALAPPDATA 'Microsoft/VisualStudio'
    $profiles = @(Get-ChildItem -LiteralPath $profileBase -Directory |
        Where-Object { $_.Name -match ('^' + $major + '\.0_[0-9a-fA-F]+' + [regex]::Escape($RootSuffix) + '$') })
    if ($profiles.Count -ne 1) { throw 'Expected one existing experimental profile with the installed candidate.' }
    $extensions = Join-Path $profiles[0].FullName 'Extensions'
    $deployments = @(Get-ChildItem -LiteralPath $extensions -File -Recurse -Filter 'ModernFormsNext.VisualStudioExtension.dll')
    if ($deployments.Count -ne 1) { throw 'Expected one ModernFormsNext deployment in the selected profile.' }
    $deployment = $deployments[0].DirectoryName
    $zip = [IO.Compression.ZipFile]::OpenRead($vsix)
    try {
        Assert-DesignerStartupPackage $zip.Entries.FullName -ExpectExecutablePreview:$ExpectExecutablePreview
        $manifestEntry = $zip.GetEntry('extension.vsixmanifest')
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $identity.vsixVersion = $manifest.PackageManifest.Metadata.Identity.Version
        $compared = foreach ($entry in $zip.Entries) {
            if ($entry.FullName -notmatch '^(DesignerHost/|ModernFormsNext.VisualStudioExtension\.dll$)' -or
                $entry.FullName.EndsWith('/')) { continue }
            $installed = Join-Path $deployment $entry.FullName
            $stream = $entry.Open()
            try { $hash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
            finally { $stream.Dispose() }
            if (!(Test-Path -LiteralPath $installed -PathType Leaf) -or
                (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -cne $hash) {
                throw "Installed candidate differs from VSIX ($($entry.FullName)). Reinstall it in the selected profile."
            }
            [ordered]@{ file = $entry.FullName; sha256 = $hash }
        }
        $identity.installedHashes = @($compared)
    }
    finally { $zip.Dispose() }
    $hostExecutable = Join-Path $deployment 'DesignerHost/ModernFormsNext.VisualStudioDesignerHost.exe'
    $worker = Join-Path $deployment 'DesignerHost/PreviewWorker/ModernFormsNext.DesignerPreviewWorker.exe'
    if ($ExpectExecutablePreview) {
        $identity.workerRuntime = Get-Content -LiteralPath (Join-Path (Split-Path $worker) 'ModernFormsNext.DesignerPreviewWorker.runtimeconfig.json') -Raw | ConvertFrom-Json
    }
    elseif (Test-Path -LiteralPath (Split-Path $worker)) {
        throw 'The installed standard deployment contains a PreviewWorker directory. Use a clean experimental profile.'
    }
    $running = @(Get-CimInstance Win32_Process -Filter "Name='devenv.exe'" |
        Where-Object { $_.CommandLine -match ('/RootSuffix\s+"?' + [regex]::Escape($RootSuffix) + '(?:"|\s|$)') })
    if ($running.Count) { throw 'Close the selected experimental Visual Studio instance before running the diagnostic.' }
    $fixtureDirectory = Join-Path $output 'Fixture'
    $identity.copiedFixture = Copy-DesignerStartupFixture (Join-Path $repository $fixtureRelative) $fixtureDirectory
    $fixture = $identity.copiedFixture.document
    $fixtureProject = $identity.copiedFixture.project
    # Keep the controlled document beside its project and source, matching a normal IDE open.
    # Startup requires no build, consent or execution of project DLLs.
    $fixtureHash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash

    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class MfnStartupNative {
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct Message { public IntPtr Window; public uint Id; public UIntPtr W; public IntPtr L; public uint Time; public Point P; public uint Private; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool PeekMessageW(out Message msg,IntPtr window,uint min,uint max,uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref Message msg);
    public static IntPtr CreateParent() {
        var window=CreateWindowExW(0,"STATIC","ModernFormsNext startup probe",0x82000000,0,0,800,600,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(window==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        return window;
    }
    public static void Pump() { while(PeekMessageW(out var msg,IntPtr.Zero,0,0,1)) { TranslateMessage(ref msg); DispatchMessageW(ref msg); } }
}
'@
    Invoke-NativeProbe 'standalone' 'outside-vs-standalone'
    Invoke-NativeProbe 'integrated' 'outside-vs-integrated'
    Invoke-NativeProbe 'standalone' 'outside-vs-restart'

    $activity = Join-Path $output 'ActivityLog.xml'
    $ide = Start-DiagnosticProcess $devenv @($fixtureProject, '/RootSuffix', $RootSuffix, '/Log', $activity) $true
    [void]$observed.Add($ide.Id)
    $identity.idePid = $ide.Id
    $identity.ideStart = $ide.StartTime.ToFileTimeUtc()
    $routingPath = Join-Path $output 'editor-open.json'
    $editorHelper = Start-DiagnosticProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile', '-STA', '-File',
        (Join-Path $PSScriptRoot 'Invoke-DesignerStartupEditor.ps1'), '-IdeProcessId', [string]$ide.Id,
        '-IdeStart', [string]$identity.ideStart, '-VisualStudioPath', $VisualStudioPath, '-RootSuffix', $RootSuffix,
        '-Document', $fixture, '-OutputPath', $routingPath, '-TimeoutSeconds', [string]$ObservationSeconds) $false $true
    [void]$observed.Add($editorHelper.Id)
    Write-Host 'The copied project is opening; the collector explicitly requests the registered Designer factory.'
    Write-Host 'If a dialog blocks opening, leave it visible until collection finishes. Do not close the document.'
    $deadline = [DateTime]::UtcNow.AddSeconds($ObservationSeconds)
    $hostProcess = $null
    $hostLog = ''
    $ideCommandOk = $false
    $hostHashMismatch = $false
    do {
        Save-ProcessSample $ide 'visual-studio'
        Save-ProcessSample $editorHelper 'editor-automation'
        if ($editorHelper.HasExited -and $null -eq $editorRouting) {
            if (Test-Path -LiteralPath $routingPath) { $editorRouting = Get-Content -LiteralPath $routingPath -Raw | ConvertFrom-Json }
            if ($null -eq $editorRouting -or $editorHelper.ExitCode -ne 0 -and $editorRouting.status -eq 'Opened') {
                $editorRouting = [ordered]@{status='BLOCKED'; stage='EditorAutomation';
                    failure='The editor helper exited without a successful verified frame. Inspect helper logs.'}
            }
        }
        $editorLog = Copy-ProcessLog $ide.Id 'Editor' 'inside-vs' $ide.StartTime.ToFileTimeUtc()
        $candidates = @(Get-CimInstance Win32_Process -Filter "Name='ModernFormsNext.VisualStudioDesignerHost.exe'" |
            Where-Object { $_.CommandLine -match ('--owner-process\s+' + $ide.Id + '(?:\s|$)') -and
                $_.CommandLine.Contains($fixture) })
        if ($candidates.Count -eq 1 -and $null -eq $hostProcess) {
            if (![string]::Equals($candidates[0].ExecutablePath, $hostExecutable, [StringComparison]::OrdinalIgnoreCase)) {
                $hostHashMismatch = $true
                break
            }
            $hostProcess = [Diagnostics.Process]::GetProcessById($candidates[0].ProcessId)
            [void]$observed.Add($hostProcess.Id)
        }
        if ($null -ne $hostProcess) {
            Save-ProcessSample $hostProcess 'inside-vs'
            $hostLog = Copy-ProcessLog $hostProcess.Id 'Host' 'inside-vs' $hostProcess.StartTime.ToFileTimeUtc()
            if ($hostProcess.HasExited) { break }
            if ($hostLog -match 'FORM_SHOWN' -and $hostLog -match 'IPC_READY PipeName=(\S+)') {
                $pipe = $Matches[1]
                $ideCommandOk = (Send-DesignerCommand $pipe 'DIRTY' $fixture) -eq "DIRTY`t0"
                if ($null -ne $editorRouting) { break }
            }
        }
        elseif ($editorLog -match 'HOST_LAUNCH_OK ProcessId=(\d+) ChildStart=(\d+)') {
            # A host can fail and exit between process enumerations. Preserve its own log
            # using the launch record even when a Process handle can no longer be opened.
            $launched = [int]$Matches[1]
            $childStart = [long]$Matches[2]
            [void]$observed.Add($launched)
            $hostLog = Copy-ProcessLog $launched 'Host' 'inside-vs' $childStart
        }
        if ($null -ne $editorRouting -and $editorRouting.status -ne 'Opened') { break }
        if ($ide.HasExited) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $editorRouting) {
        # An RPC can remain blocked inside the IDE; terminate only our helper, leaving
        # the experimental IDE and its document available for inspection.
        $editorRouting = [ordered]@{status='BLOCKED'; stage='EditorAutomation';
            failure='Explicit editor selection did not complete within the observation interval.'}
    }
    Stop-EditorHelper
    $editorLog = Copy-ProcessLog $ide.Id 'Editor' 'inside-vs' $ide.StartTime.ToFileTimeUtc()
    $insideFailure = ''
    $insideStage = 'HostStartup'
    if ($null -ne $hostProcess) {
        $exited = $hostProcess.HasExited
        $exitCode = if ($exited) { $hostProcess.ExitCode } else { $null }
        $inside = Get-DesignerStartupStatus $hostLog $exited $exitCode $false
        if (!$ideCommandOk -and $inside -eq 'PASS') { $inside = 'FAIL' }
        $insidePid = $hostProcess.Id
        if ($inside -ne 'PASS') { $insideFailure = 'The owned host did not complete startup and responsive IPC. Inspect editor/host logs.' }
        # The IDE remains available for inspection. Record its live child's current native threads
        # before any later user action closes the document.
        $hostProcess.Dispose()
    }
    else {
        $missingStatus = Get-DesignerStartupStatus $hostLog $false $null $false
        # Readiness without a process/IPC response is incomplete evidence.
        if ($missingStatus -eq 'PASS') { $missingStatus = 'BLOCKED' }
        $inside = $missingStatus
        $insidePid = $null
        $exitCode = $null
        $insideFailure = 'No matching installed host was observed. Inspect the IDE dialog and editor/activity logs.'
    }
    if ($editorRouting.status -ne 'Opened') {
        if ($inside -ne 'FAIL') { $inside = if ($editorRouting.status -eq 'FAIL') { 'FAIL' } else { 'BLOCKED' } }
        $insideStage = $editorRouting.stage
        $insideFailure = $editorRouting.failure
    }
    $results.Add([ordered]@{label='inside-vs'; status=$inside; pid=$insidePid;
        exitCode=$exitCode; ipcResponsive=$ideCommandOk; forcedStop=$false;
        failureStage=$insideStage; failure=$insideFailure; editorRouting=$editorRouting})
    if ($hostHashMismatch) { throw 'Visual Studio started a host outside the verified deployment.' }
    if ((Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash -cne $fixtureHash) {
        throw 'The synthetic design document changed during the startup probe.'
    }
    foreach ($copied in $identity.copiedFixture.copiedHashes) {
        if ((Get-FileHash -LiteralPath (Join-Path $fixtureDirectory $copied.name) -Algorithm SHA256).Hash -cne $copied.Hash) {
            throw "The copied fixture changed during the startup probe ($($copied.name))."
        }
    }
    if (@($results | Where-Object { $_.status -eq 'FAIL' }).Count) { $status='FAIL' }
    elseif (@($results | Where-Object { $_.status -ne 'PASS' }).Count) { $status='BLOCKED' }
    else { $status='PASS' }
    $incomplete = @($results | Where-Object { $_.status -ne 'PASS' })
    $reason = if ($incomplete.Count) { "$($incomplete[0].label): $($incomplete[0].failure)" }
        else { 'All startup probes and explicit Designer selection completed. LPAC execution has not been qualified.' }
    $ide.Dispose()
}
catch {
    $reason = $_.Exception.Message
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $output 'collector-error.txt') -Encoding utf8
}
finally {
    Stop-EditorHelper
    $identity | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $output 'identity.json') -Encoding utf8
    # Query only relevant application events and owned processes. Never change WER or machine settings.
    try {
        $events = @(Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$started;
            Id=@(1000,1001,1002,1026)} -ErrorAction Stop | Where-Object {
                $_.Message -match 'ModernFormsNext.VisualStudioDesignerHost|devenv.exe'
            })
        @($events | ForEach-Object { $_.ToXml() }) | ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $output 'windows-events.json') -Encoding utf8
    }
    catch {
        $_.Exception.Message | Set-Content -LiteralPath (Join-Path $output 'windows-events-unavailable.txt') -Encoding utf8
    }
    [ordered]@{ status=$status; reason=$reason; startedAt=$started.ToString('o');
        completedAt=[DateTimeOffset]::Now.ToString('o'); probes=@($results.ToArray());
        observedProcessIds=@($observed); lpacQualification='NOT EXECUTED' } |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
    Write-Host "$status - $reason"
    Write-Host "Evidence directory $output"
    Write-Host 'Close the experimental IDE after inspection, then send the entire evidence directory.'
}
if ($status -eq 'PASS') { exit 0 }
if ($status -eq 'FAIL') { exit 1 }
exit 2
