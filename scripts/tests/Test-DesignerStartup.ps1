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
if (Get-DesignerProcessLog $oldLog 123) { throw 'A previous process creation time was accepted.' }
$newLog = $oldLog + "[new] START ProcessStart=123`n[new] DPI_SETUP_BEGIN ProcessStart=123`n"
$current = Get-DesignerProcessLog $newLog 123
if ($current -match 'FORM_SHOWN' -or $current -notmatch 'DPI_SETUP_BEGIN') {
    throw 'Readiness from a previous PID instance contaminated the new run.'
}
$complete = Get-DesignerProcessLog ($newLog + "[new] FORM_SHOWN ProcessStart=123`n") 123
if ($complete -notmatch 'FORM_SHOWN') { throw 'The current process readiness was lost.' }
$parseErrors = $null
$tokens = $null
[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../Test-DesignerStartup.ps1'),
    [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
Write-Host 'Designer startup evidence classification and collector syntax passed.'
