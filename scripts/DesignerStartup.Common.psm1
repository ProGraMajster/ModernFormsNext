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
    $match = [regex]::Match($Log, ' ProcessStart=' + $ProcessStart + '(?:\r?\n|$)')
    if (!$match.Success) { return '' }
    $lineStart = $Log.LastIndexOf("`n[", $match.Index, [StringComparison]::Ordinal)
    if ($lineStart -lt 0) { return $Log }
    return $Log.Substring($lineStart + 1)
}

Export-ModuleMember -Function Get-DesignerStartupStatus, Get-DesignerProcessLog
