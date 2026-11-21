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
Write-Host 'Designer startup classification, fixture context and collector/helper syntax passed.'
