#requires -Version 7.2
<# Opens only the controlled document in the exact experimental IDE selected by the collector. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$IdeProcessId,
    [Parameter(Mandatory)][long]$IdeStart,
    [Parameter(Mandatory)][string]$VisualStudioPath,
    [Parameter(Mandatory)][string]$RootSuffix,
    [Parameter(Mandatory)][string]$Document,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(10,120)][int]$TimeoutSeconds = 20
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$state=[ordered]@{status='Pending';stage='Prerequisites';idePid=$IdeProcessId;ideStart=$IdeStart;
    requestedFactory='c61567c8-f5ac-4f9e-9c6e-b4ec99c7ab31';logicalView='7651a702-06e5-11d1-8ebd-00a0c90f26ea';
    method='IVsUIShellOpenDocument.OpenSpecificEditor';skipDeferral=$true;
    actualFactory=$null;hresult=$null;failure=$null}
function Save-State {
    $state.at=[DateTimeOffset]::Now.ToString('o')
    # A same-directory rename keeps the parent's evidence read from observing partial JSON.
    $temporary=$OutputPath+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    [IO.File]::WriteAllText($temporary,($state|ConvertTo-Json))
    [IO.File]::Move($temporary,$OutputPath,$true)
}
try {
    Save-State
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'The editor helper requires STA.' }
    $ide=[Diagnostics.Process]::GetProcessById($IdeProcessId)
    if ($ide.StartTime.ToFileTimeUtc() -ne $IdeStart) { throw 'The IDE PID was reused.' }
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$IdeProcessId"
    $expected=Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe'
    if (![string]::Equals($process.ExecutablePath,$expected,[StringComparison]::OrdinalIgnoreCase) -or
        $process.CommandLine -notmatch ('/RootSuffix\s+"?'+[regex]::Escape($RootSuffix)+'(?:"|\s|$)')) {
        throw 'The IDE is outside the selected experimental profile.'
    }
    $interop=Join-Path $VisualStudioPath 'Common7/IDE/PublicAssemblies/Microsoft.VisualStudio.Interop.dll'
    if (!(Test-Path -LiteralPath $interop)) { throw 'The selected Visual Studio installation lacks its interop contracts.' }
    [void][Reflection.Assembly]::LoadFrom($interop)
    Add-Type -Path (Join-Path $PSScriptRoot 'DesignerStartup.VisualStudio.cs') -ReferencedAssemblies @(
        $interop,'System.Runtime.InteropServices.dll','mscorlib.dll')
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $state.stage='EditorOpen'
    Save-State
    do {
        if ($ide.HasExited) { throw 'The owned IDE exited before opening the Designer.' }
        try {
            $state.actualFactory=[DesignerStartupVisualStudio]::Open($IdeProcessId,
                (Get-Item -LiteralPath $expected).VersionInfo.FileMajorPart,$Document).ToString('D')
            $state.status='Opened'
            $state.hresult='0x00000000'
            $state.failure=$null
            Save-State
            exit 0
        }
        catch {
            $failure=$_.Exception.GetBaseException()
            $state.hresult='0x'+$failure.HResult.ToString('X8')
            # Startup can precede ROT registration, and the IDE can temporarily reject an
            # automation call while busy. Actual editor initialization failures end the probe.
            if ($failure.HResult -notin @(-2147221021,-2147418111,-2147417846)) { throw }
            $state.failure=$failure.Message
            Save-State
            Start-Sleep -Milliseconds 200
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    $state.status='BLOCKED'
    $state.failure='The owned IDE did not become available for the explicit editor request.'
    Save-State
    exit 2
}
catch {
    $failure=$_.Exception.GetBaseException()
    $state.status=if($state.stage -eq 'Prerequisites'){'BLOCKED'}else{'FAIL'}
    $state.hresult='0x'+$failure.HResult.ToString('X8')
    $state.failure=$failure.Message
    Save-State
    $_|Out-String|Set-Content -LiteralPath ($OutputPath+'.error.txt') -Encoding utf8
    if($state.status -eq 'FAIL'){exit 1}
    exit 2
}
finally { if ($null -ne (Get-Variable ide -ErrorAction SilentlyContinue)) { $ide.Dispose() } }
