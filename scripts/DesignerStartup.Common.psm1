Set-StrictMode -Version Latest

function Get-DesignerStartupStatus {
    <# Classify observations without inferring a deadlock or a native crash from a missing marker. #>
    param([string]$Log, [bool]$Exited, [Nullable[int]]$ExitCode, [bool]$ForcedStop)
    if ($ForcedStop) { return 'FAIL' }
    if ($Log -match 'MAIN_EXCEPTION|APPDOMAIN_UNHANDLED_EXCEPTION') { return 'FAIL' }
    if ($Exited -and ($null -eq $ExitCode -or $ExitCode -ne 0)) { return 'FAIL' }
    if ($Log -match 'FORM_SHOWN' -and $Log -match 'IPC_READY') { return 'PASS' }
    return 'BLOCKED'
}

function Get-DesignerProcessLog {
    param([string]$Log, [long]$ProcessStart)
    if ($ProcessStart -le 0) { return '' }
    # Every diagnostic entry ends with its process creation time. Match complete
    # entries so earlier or later occupants of the same PID cannot satisfy a gate.
    $entries = [regex]::Split($Log, '(?m)(?=^\[[^\r\n]*\] )')
    return (($entries -cmatch (' ProcessStart=' + $ProcessStart + '\s*\z')) -join '').TrimEnd()
}

function Get-DesignerHostLaunch {
    param([string]$Log, [int]$ProcessId, [long]$ProcessStart)
    if ($ProcessId -le 0 -or $ProcessStart -le 0) { return $false }
    return $Log -cmatch ('(?m)^\[[^\r\n]*\] HOST_LAUNCH_OK ProcessId=' + $ProcessId +
        ' ChildStart=' + $ProcessStart + ' Pid=')
}

function Test-DesignerSmokeShutdown {
    param([string]$EditorLog, [string]$HostLog, [int]$ProcessId, [int]$ExitCode)
    $linePrefix = '(?m)^\[[^\r\n]*\] '
    # The editor's ExitCode field includes human-readable text. Use the owned
    # process handle and the host's structured exit record for the numeric code.
    return $ExitCode -eq 0 -and
        $EditorLog -cmatch ($linePrefix + 'HOST_SHUTDOWN_IPC Acknowledged=True Pid=') -and
        $EditorLog -cmatch ($linePrefix + 'HOST_SHUTDOWN_END ProcessId=' + $ProcessId + ' ExitCode=[^\r\n]* HResult=0x00000000 Pid=') -and
        $EditorLog -cnotmatch ($linePrefix + 'HOST_(KILL_REQUESTED|SHUTDOWN_FAILED)\b') -and
        $HostLog -cmatch ($linePrefix + 'SHUTDOWN_RECEIVED Pid=' + $ProcessId + ' ') -and
        $HostLog -cmatch ($linePrefix + 'PROCESS_EXIT ExitCode=0 Pid=' + $ProcessId + ' ')
}

function Get-DesignerSmokeDirtyState {
    param([string]$Pipe, [string]$Document, [int]$ProcessId)
    if (!('DesignerSmokePipe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class DesignerSmokePipe
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
}
'@
    }
    $client = [IO.Pipes.NamedPipeClientStream]::new('.', $Pipe, [IO.Pipes.PipeDirection]::InOut,
        [IO.Pipes.PipeOptions]::Asynchronous)
    $cancel = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
    try {
        [void]$client.ConnectAsync($cancel.Token).GetAwaiter().GetResult()
        [uint32]$serverId = 0
        # Check the connected server before sending even the synthetic document path.
        if (![DesignerSmokePipe]::GetNamedPipeServerProcessId($client.SafePipeHandle, [ref]$serverId) -or
            $serverId -ne $ProcessId) {
            throw 'The connected Designer pipe is outside the verified host process.'
        }
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Document))
        $request = [Text.Encoding]::UTF8.GetBytes("DIRTY`t$encoded`t`t`n")
        [void]$client.WriteAsync([ReadOnlyMemory[byte]]::new($request), $cancel.Token).AsTask().GetAwaiter().GetResult()
        $reader = [IO.StreamReader]::new($client, [Text.Encoding]::UTF8, $true, 1024, $true)
        try {
            $reply = $reader.ReadLineAsync($cancel.Token).AsTask().GetAwaiter().GetResult()
            if ($reply -cne "DIRTY`t0") { throw 'The Designer did not report the expected clean fixture over IPC.' }
            return $reply
        }
        finally { $reader.Dispose() }
    }
    finally { $client.Dispose(); $cancel.Dispose() }
}

function Copy-DesignerStartupFixture {
    <# Preserve project discovery next to the copied document without writing build outputs into source.
       Only this known synthetic fixture is supported; project code is never built or approved here. #>
    param([string]$SourceDirectory, [string]$Destination)
    if (Test-Path -LiteralPath $Destination) { throw 'The copied fixture directory must be new.' }
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($name in 'PreviewExample.mfdesign', 'PreviewBadge.cs') {
        Copy-Item -LiteralPath (Join-Path $SourceDirectory $name) -Destination (Join-Path $Destination $name)
    }
    $sourceProject = Join-Path $SourceDirectory 'ProjectFixture.csproj'
    [xml]$project = Get-Content -LiteralPath $sourceProject -Raw
    $references = @($project.SelectNodes('//ProjectReference'))
    if ($references.Count -ne 1) { throw 'The controlled fixture must reference exactly one framework project.' }
    $target = [IO.Path]::GetFullPath((Join-Path $SourceDirectory $references[0].GetAttribute('Include')))
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw 'The fixture framework project does not exist.' }
    # Moving just the JSON loses project discovery. Rebase the copied project's reference
    # so its own new location does not change which framework the fixture describes.
    $references[0].SetAttribute('Include', $target)
    $copiedProject = Join-Path $Destination 'ProjectFixture.csproj'
    $project.Save($copiedProject)
    return [ordered]@{
        project = $copiedProject; document = (Join-Path $Destination 'PreviewExample.mfdesign')
        rebasedProjectReference = $target
        copiedHashes = @(Get-ChildItem -LiteralPath $Destination -File | Get-FileHash -Algorithm SHA256 |
            Select-Object @{Name='name';Expression={Split-Path $_.Path -Leaf}}, Hash)
    }
}

function Assert-DesignerStartupPackage {
    param([string[]]$EntryNames, [switch]$ExpectExecutablePreview)
    $worker = 'DesignerHost/PreviewWorker/ModernFormsNext.DesignerPreviewWorker.exe'
    if ($ExpectExecutablePreview) {
        if ($worker -notin $EntryNames) { throw 'The candidate VSIX lacks the qualification worker.' }
    }
    elseif ($EntryNames -match '(^|/)PreviewWorker/|(^|/)ModernFormsNext\.DesignerPreviewWorker\.') {
        throw 'The standard VSIX contains executable preview worker files.'
    }
}

Export-ModuleMember -Function Get-DesignerStartupStatus, Get-DesignerProcessLog, Copy-DesignerStartupFixture,
    Assert-DesignerStartupPackage, Get-DesignerHostLaunch, Test-DesignerSmokeShutdown, Get-DesignerSmokeDirtyState
