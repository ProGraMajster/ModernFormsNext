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

Export-ModuleMember -Function Get-DesignerStartupStatus, Get-DesignerProcessLog, Copy-DesignerStartupFixture
