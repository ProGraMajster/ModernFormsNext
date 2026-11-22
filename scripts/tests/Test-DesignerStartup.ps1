#requires -Version 7.2
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../DesignerStartup.Common.psm1') -Force
$cases = @(
    @{log='DPI_SETUP_BEGIN'; exited=$false; code=$null; forced=$false; expected='BLOCKED'},
    @{log='DPI_SETUP_BEGIN'; exited=$true; code=-1; forced=$false; expected='FAIL'},
    @{log='FORM_SHOWN IPC_READY'; exited=$false; code=$null; forced=$false; expected='PASS'},
    @{log='FORM_SHOWN IPC_READY'; exited=$true; code=0; forced=$false; expected='PASS'},
    @{log='FORM_SHOWN IPC_READY'; exited=$true; code=-1073741819; forced=$false; expected='FAIL'},
    @{log='FORM_SHOWN IPC_READY'; exited=$true; code=-1; forced=$true; expected='FAIL'},
    @{log='MAIN_EXCEPTION'; exited=$false; code=$null; forced=$false; expected='FAIL'},
    @{log=''; exited=$true; code=0; forced=$false; expected='BLOCKED'}
)
foreach ($case in $cases) {
    $actual = Get-DesignerStartupStatus $case.log $case.exited $case.code $case.forced
    if ($actual -cne $case.expected) { throw "Expected $($case.expected), observed $actual." }
}
$oldLog = "[old] START ProcessStart=1234`n[old] FORM_SHOWN ProcessStart=1234`n"
Assert-DesignerStartupPackage @('DesignerHost/ModernFormsNext.VisualStudioDesignerHost.exe')
$worker = 'DesignerHost/PreviewWorker/ModernFormsNext.DesignerPreviewWorker.exe'
Assert-DesignerStartupPackage @($worker) -ExpectExecutablePreview
try { Assert-DesignerStartupPackage @($worker); throw 'A qualification package was accepted as standard.' }
catch { if ($_.Exception.Message -ne 'The standard VSIX contains executable preview worker files.') { throw } }
if (Get-DesignerProcessLog $oldLog 123) { throw 'A previous process creation time was accepted.' }
if (Get-DesignerProcessLog '[old] FORM_SHOWN ProcessStart=0' 0) { throw 'An unknown process creation time was accepted.' }
$newLog = $oldLog + "[new] START ProcessStart=123`n[new] DPI_SETUP_BEGIN ProcessStart=123`n"
$current = Get-DesignerProcessLog $newLog 123
if ($current -match 'FORM_SHOWN' -or $current -notmatch 'DPI_SETUP_BEGIN') {
    throw 'Readiness from a previous PID instance contaminated the new run.'
}
$complete = Get-DesignerProcessLog ($newLog + "[new] FORM_SHOWN ProcessStart=123`n") 123
if ($complete -notmatch 'FORM_SHOWN') { throw 'The current process readiness was lost.' }
$reused = Get-DesignerProcessLog ($newLog + "[later] FORM_SHOWN IPC_READY ProcessStart=4321`n") 123
if ($reused -match 'FORM_SHOWN|IPC_READY') { throw 'Readiness from a later PID instance was accepted.' }
$editorLog = "[now] HOST_LAUNCH_OK ProcessId=10 ChildStart=22 Pid=1 ProcessStart=33"
if (!(Get-DesignerHostLaunch $editorLog 10 22) -or (Get-DesignerHostLaunch $editorLog 10 2) -or
    (Get-DesignerHostLaunch $editorLog 11 22)) {
    throw 'The launch log did not identify the exact process instance.'
}
$shutdown = "[now] HOST_SHUTDOWN_END ProcessId=10 ExitCode=0 HResult=0x00000000 Pid=1 ProcessStart=33"
if (!(Test-DesignerSmokeShutdown $shutdown 10 0) -or (Test-DesignerSmokeShutdown $shutdown 1 0) -or
    (Test-DesignerSmokeShutdown $shutdown 10 -1) -or (Test-DesignerSmokeShutdown '' 10 0) -or
    (Test-DesignerSmokeShutdown ("[now] HOST_KILL_REQUESTED ProcessId=10 Pid=1 ProcessStart=33`n" + $shutdown) 10 0)) {
    throw 'A host was not required to shut down normally.'
}
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'fixtures/DesignerExecutablePreview'))
$before = @(Get-ChildItem -LiteralPath $source -File | Get-FileHash -Algorithm SHA256)
# Retain the tiny controlled test copy as evidence; never remove a caller's directory.
$destination = Join-Path ([IO.Path]::GetTempPath()) ('MfnStartupFixtureTest-' + [guid]::NewGuid().ToString('N'))
$copy = Copy-DesignerStartupFixture $source $destination
foreach ($name in 'PreviewExample.mfdesign', 'PreviewBadge.cs') {
    if ((Get-FileHash (Join-Path $source $name)).Hash -cne (Get-FileHash (Join-Path $destination $name)).Hash) {
        throw "Copied fixture differs ($name)."
    }
}
[xml]$originalProject = Get-Content (Join-Path $source 'ProjectFixture.csproj') -Raw
[xml]$copiedProject = Get-Content $copy.project -Raw
$expectedTarget = [IO.Path]::GetFullPath((Join-Path $source $originalProject.Project.ItemGroup.ProjectReference.Include))
if ($copiedProject.Project.ItemGroup.ProjectReference.Include -cne $expectedTarget) {
    throw 'The copied project no longer references the same framework.'
}
if ((Split-Path $copy.project) -cne (Split-Path $copy.document) -or $copy.copiedHashes.Count -ne 3) {
    throw 'The copied document is missing its project/source context or hash inventory.'
}
foreach ($entry in $before) {
    if ((Get-FileHash -LiteralPath $entry.Path).Hash -cne $entry.Hash) { throw 'Fixture source was modified.' }
}
try { Copy-DesignerStartupFixture $source $destination; throw 'An existing copy was overwritten.' }
catch { if ($_.Exception.Message -ne 'The copied fixture directory must be new.') { throw } }
foreach ($script in 'Test-DesignerStartup.ps1', 'Invoke-DesignerStartupEditor.ps1', 'Test-ReleaseDesigner.ps1') {
    $parseErrors = $null
    $tokens = $null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot "../$script"),
        [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
}
$standalone = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Test-ReleaseDesigner.ps1') -Raw
if ($standalone -notmatch "Start-OwnedProcess \`$devenv @\([^\r\n]*'/Edit', \`$fixture\)" -or
    $standalone -notmatch "Invoke-EditorCheck 'Observe' 'Observed'" -or
    $standalone -match "Invoke-EditorCheck 'OpenProject'") {
    throw 'The release smoke must use normal standalone editor selection and independently observe the frame.'
}
if ($IsWindows) {
    foreach ($wrongOwner in $false, $true) {
        $name = 'MfnSmokePipeTest-' + [guid]::NewGuid().ToString('N')
        $job = Start-Job -ArgumentList $name -ScriptBlock {
            param($Name)
            $server = [IO.Pipes.NamedPipeServerStream]::new($Name)
            try {
                $PID
                $server.WaitForConnection()
                $reader = [IO.StreamReader]::new($server, [Text.Encoding]::UTF8, $false, 1024, $true)
                try {
                    $request = $reader.ReadLine()
                    if ($null -ne $request) {
                        $request
                        $bytes = [Text.Encoding]::UTF8.GetBytes("DIRTY`t0`n")
                        $server.Write($bytes, 0, $bytes.Length)
                    }
                }
                finally { $reader.Dispose() }
            }
            finally { $server.Dispose() }
        }
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            do {
                $serverPid = Receive-Job $job
                if ($serverPid) { break }
                Start-Sleep -Milliseconds 100
            } while ([DateTime]::UtcNow -lt $deadline)
            if (!$serverPid) { throw 'The temporary Designer test pipe did not become available.' }
            $document = 'C:\Smoke Test\ReleaseSmoke.mfdesign'
            if ($wrongOwner) {
                try {
                    $null = Get-DesignerSmokeDirtyState $name $document $PID
                    throw 'A pipe from outside the verified host process was accepted.'
                }
                catch { if ($_.Exception.Message -ne 'The connected Designer pipe is outside the verified host process.') { throw } }
            }
            else {
                $reply = Get-DesignerSmokeDirtyState $name $document $serverPid
                if ($reply -cne "DIRTY`t0") { throw 'The clean fixture response was lost.' }
            }
            if (!(Wait-Job $job -Timeout 5)) { throw 'The temporary Designer test pipe did not stop.' }
            $request = Receive-Job $job
            if ($wrongOwner) {
                if ($null -ne $request) { throw 'A request was sent to the wrong pipe server.' }
            }
            else {
                $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($document))
                if ($request -cne "DIRTY`t$encoded`t`t") { throw 'The clean-state request did not identify the exact document.' }
            }
        }
        finally { Stop-Job $job; Remove-Job $job }
    }
}
Write-Host 'Designer startup classification, fixture context, standalone route and host identity checks passed.'
