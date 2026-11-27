<#
.SYNOPSIS
Runs a VSTest filter, project, component or explicit full solution on Windows.
.DESCRIPTION
Writes separate logs and TRX files per invocation and a JSON summary. Use -Build
to compile against existing assets, or -Restore to restore and compile. Running
the wrapper is local validation. Release qualification is performed by CI.
#>
[CmdletBinding(DefaultParameterSetName = 'Project')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Project')][string]$Project,
    [Parameter(Mandatory, ParameterSetName = 'Component')]
    [ValidateSet('Core', 'Windows', 'Android', 'Designer', 'Vsix', 'Testing', 'Automation', 'CrossPlatformSample')]
    [string]$Component,
    [Parameter(Mandatory, ParameterSetName = 'Full')][switch]$Full,
    [Parameter(ParameterSetName = 'Project')][Parameter(ParameterSetName = 'Component')]
    [ValidateNotNullOrEmpty()][string]$Filter,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Build,
    [switch]$Restore,
    [ValidateRange(1, 7200)][int]$TimeoutSeconds = 900,
    [ValidatePattern('^[1-9][0-9]*(ms|s|min|h)$')][string]$HangTimeout,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'This wrapper requires Windows job objects. Use dotnet test directly on other hosts.' }
$repo = Split-Path -Parent $PSScriptRoot

$projects = @{
    Core = 'ModernFormsNext.Tests'
    Windows = 'ModernFormsNext.WindowKit.Backend.Windows.Tests'
    Android = 'ModernFormsNext.WindowKit.Backend.Android.Tests'
    Designer = 'ModernFormsNext.Designer.Tests'
    Vsix = 'ModernFormsNext.VisualStudioExtension.Vsix.Tests'
    Testing = 'ModernFormsNext.Testing.Tests'
    Automation = @('ModernFormsNext.Automation.Tests', 'ModernFormsNext.Automation.Windows.Tests')
    CrossPlatformSample = 'ModernFormsNext.CrossPlatform.Sample.Tests'
}

if ($Full) {
    $targets = @(Join-Path $repo 'ModernFormsNext.slnx')
} elseif ($Component) {
    $targets = @($projects[$Component] | ForEach-Object { Join-Path $repo "$_/$_.csproj" })
} else {
    $targets = @((Resolve-Path -LiteralPath $Project).Path)
    if ([IO.Path]::GetExtension($targets[0]) -ine '.csproj') { throw 'Specify a test project file or use -Full.' }
}
if ($Filter -and $targets.Count -gt 1) { throw 'Use -Project to filter within a component with several test projects.' }

if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('artifacts/selective-tests/' + [guid]::NewGuid().ToString('N'))
}
$root = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh output directory for the test results.' }
[IO.Directory]::CreateDirectory($root) | Out-Null

# Closing the non-inherited job handle terminates all associated processes,
# even if the runner exits before a child or the invoking shell is killed.
if (!('ModernFormsNext.LocalTesting.ProcessJob' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ModernFormsNext.LocalTesting
{
    public static class ProcessJob
    {
        // Native JOBOBJECT_EXTENDED_LIMIT_INFORMATION, including pointer-sized
        // working set fields and the six 64-bit IO_COUNTERS entries.
        [StructLayout(LayoutKind.Sequential)]
        private struct JobLimits
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint Priority, Scheduling;
            public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
            public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob;
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, IntPtr name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref JobLimits limits, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

        /// <summary>Creates a private job whose last handle closes the test process tree.</summary>
        public static SafeFileHandle Create()
        {
            const uint KillOnJobClose = 0x2000;
            const int ExtendedLimitInformation = 9;
            SafeFileHandle job = CreateJobObjectW(IntPtr.Zero, IntPtr.Zero);
            if (job.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Create test job");

            var limits = new JobLimits { Flags = KillOnJobClose };
            if (SetInformationJobObject(job, ExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<JobLimits>()))
                return job;

            int error = Marshal.GetLastWin32Error();
            job.Dispose();
            throw new Win32Exception(error, "Set test job lifetime");
        }

        /// <summary>Associates the gate process with the job before it can launch tests.</summary>
        public static void Attach(SafeFileHandle job, IntPtr process)
        {
            if (!AssignProcessToJobObject(job, process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Attach test command to job");
        }
    }
}
'@
}

# Stream output to files as the process runs so an interrupted run keeps its
# diagnostics. Polling lets Ctrl+C reach finally while an owned child is active.
function Invoke-TestProcess([string[]]$Arguments, [string]$Directory, [psobject]$Result) {
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'))
    $start.WorkingDirectory = $repo
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['DOTNET_TEST_RUNNER'] = 'VSTest'
    $start.Environment['MSBUILDDISABLENODEREUSE'] = '1'
    # The gate waits for arguments until job assignment succeeds. Passing JSON
    # on stdin preserves VSTest filter operators without shell interpolation.
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-Command', @'
$ErrorActionPreference = 'Stop'
$encoded = [Console]::ReadLine()
if ([string]::IsNullOrEmpty($encoded)) { exit 1 }
$testArguments = ConvertFrom-Json -InputObject $encoded
& dotnet @testArguments
exit $LASTEXITCODE
'@)) { $start.ArgumentList.Add($argument) }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $started = $false
    $attached = $false
    $stdout = $null
    $stderr = $null
    $copies = @()
    $job = $null
    try {
        $job = [ModernFormsNext.LocalTesting.ProcessJob]::Create()
        $stdout = [IO.File]::Create((Join-Path $Directory 'stdout.log'))
        $stderr = [IO.File]::Create((Join-Path $Directory 'stderr.log'))
        $started = $process.Start()
        if (!$started) { throw 'Failed to start dotnet.' }
        [ModernFormsNext.LocalTesting.ProcessJob]::Attach($job, $process.Handle)
        $attached = $true
        $copies = @($process.StandardOutput.BaseStream.CopyToAsync($stdout),
            $process.StandardError.BaseStream.CopyToAsync($stderr))
        $process.StandardInput.WriteLine((ConvertTo-Json -Compress -InputObject $Arguments))
        $process.StandardInput.Close()
        while (!$process.WaitForExit(200)) {
            if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $Result.Status = 'TimedOut'
                return
            }
        }
        $Result.ExitCode = $process.ExitCode
        $Result.Status = if ($process.ExitCode -eq 0) { 'Completed' } else { 'Failed' }
    }
    finally {
        try {
            if ($job) { $job.Dispose() }
            # A gate outside the job has received no command. Stop it directly.
            if ($started -and !$attached -and !$process.HasExited) { $process.Kill() }
            if ($started -and !$process.WaitForExit(5000)) { throw 'The test gate failed to exit after cleanup.' }
            if ($copies.Count) {
                [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]$copies).
                    WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()
            }
        }
        finally {
            if ($stdout) { $stdout.Dispose() }
            if ($stderr) { $stderr.Dispose() }
            $process.Dispose()
            $Result.ElapsedMs = [Math]::Round($clock.Elapsed.TotalMilliseconds)
            if ($Result.Status -eq 'Running') { $Result.Status = 'Interrupted' }
        }
    }
}

$results = foreach ($target in $targets) {
    [pscustomobject][ordered]@{
        Target = $target
        Status = 'NotRun'
        ExitCode = $null
        ElapsedMs = $null
        Passed = 0
        Failed = 0
        Skipped = 0
        OtherReported = 0
        Reports = @()
    }
}
$summary = [ordered]@{
    Scope = if ($Full) { 'LocalSolution' } else { 'Selective' }
    Configuration = $Configuration
    Filter = $Filter
    Build = [bool]($Build -or $Restore)
    Restore = [bool]$Restore
    Results = @($results)
}
try {
    foreach ($result in $summary.Results) {
        $directory = Join-Path $root ([IO.Path]::GetFileNameWithoutExtension($result.Target))
        [IO.Directory]::CreateDirectory($directory) | Out-Null
        $arguments = @('test', $result.Target, '-c', $Configuration, '-m:1', '-p:UseSharedCompilation=false',
            '--results-directory', $directory, '--logger', 'trx;LogFilePrefix=test')
        if (!$Restore) { $arguments += '--no-restore' }
        if (!$Build -and !$Restore) { $arguments += '--no-build' }
        if ($Filter) { $arguments += @('--filter', $Filter) }
        if ($HangTimeout) { $arguments += @('--blame-hang', '--blame-hang-timeout', $HangTimeout, '--blame-hang-dump-type', 'mini') }
        $arguments += @('--', 'RunConfiguration.TreatNoTestsAsError=true')
        $result.Status = 'Running'
        Invoke-TestProcess $arguments $directory $result

        foreach ($report in Get-ChildItem -LiteralPath $directory -Filter '*.trx' -Recurse -File) {
            [xml]$trx = Get-Content -LiteralPath $report.FullName -Raw
            $outcomes = @($trx.SelectNodes("//*[local-name()='UnitTestResult']") | ForEach-Object { $_.GetAttribute('outcome') })
            $result.Passed += @($outcomes -eq 'Passed').Count
            $result.Failed += @($outcomes -eq 'Failed').Count
            $result.Skipped += @($outcomes | Where-Object { $_ -in @('NotExecuted', 'Skipped') }).Count
            $result.OtherReported += @($outcomes | Where-Object { $_ -notin @('Passed', 'Failed', 'NotExecuted', 'Skipped') }).Count
            $result.Reports += $report.FullName
        }
        if ($result.Status -eq 'Completed') {
            $result.Status = if ($result.Failed -or $result.OtherReported) { 'Failed' }
                elseif (!$result.Passed) { 'NoTestsPassed' } else { 'Passed' }
        }
        Write-Host "$($result.Status) $([IO.Path]::GetFileName($result.Target)) ($($result.ElapsedMs) ms): passed $($result.Passed), failed $($result.Failed), skipped $($result.Skipped), other $($result.OtherReported)."
        if ($result.Status -ne 'Passed') { throw "Validation failed. See $directory." }
    }
}
finally {
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'summary.json')
    Write-Host "Local test evidence: $root"
}
