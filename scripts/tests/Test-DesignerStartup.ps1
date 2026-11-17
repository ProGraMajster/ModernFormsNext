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
$parseErrors = $null
$tokens = $null
[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../Test-DesignerStartup.ps1'),
    [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
Write-Host 'Designer startup evidence classification and collector syntax passed.'
