#Requires -Version 7.0
<#
.SYNOPSIS
  Synchronize ModernFormsNext release Milestones and GitHub Project #3 from issue labels.
.DESCRIPTION
  Idempotent: preserves issue labels, Project Status/Priority and unrelated fields.
  Defaults to read-only dry run. Apply writes only with -Apply.
  Requires authenticated GitHub CLI with repository Issues write and Project access.
.EXAMPLE
  pwsh ./scripts/sync-release-roadmap.ps1
  pwsh ./scripts/sync-release-roadmap.ps1 -Apply
#>
[CmdletBinding()]
param([switch]$Apply)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = 'ProGraMajster/ModernFormsNext'
$Owner = 'ProGraMajster'
$Project = '3'
$FieldName = 'Target release'
$MilestoneStages = @('2.0.0', '2.1.0', '2.2.0')
$PossibleStages = @('1.12.0', '2.0.0', '2.1.0', '2.2.0', 'Later')
$LabelToStage = @{
    'target: 2.0.0' = '2.0.0'
    'target: 2.1.0' = '2.1.0'
    'target: 2.2.0' = '2.2.0'
    'roadmap: later' = 'Later'
}

function Invoke-Gh {
    param([string[]]$CommandArgs)
    $output = & gh @CommandArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($CommandArgs -join ' ') failed: $($output -join '`n')"
    }
    return ($output -join "`n")
}

function Invoke-GhJson {
    param([string[]]$CommandArgs)
    $raw = Invoke-Gh -CommandArgs $CommandArgs
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return (ConvertFrom-Json -InputObject $raw -Depth 100)
}

# Read and validate live issue inventory; never blindly trust a stale issue-number list.
& gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Authenticate first: gh auth login (and gh auth refresh -s project for Projects).' }

$issues = @(Invoke-GhJson -CommandArgs @('issue', 'list', '--repo', $Repo,
    '--state', 'open', '--limit', '1000',
    '--json', 'number,title,url,labels,milestone'))
if ($issues.Count -eq 0) { throw 'No open issues returned. Aborting to avoid an unverified sync.' }

$plan = @()
foreach ($issue in $issues) {
    $labels = @($issue.labels | ForEach-Object { $_.name })
    $matches = @($labels | Where-Object { $LabelToStage.ContainsKey($_) })
    $existingMilestone = if ($null -ne $issue.milestone) { [string]$issue.milestone.title } else { '' }

    if ($existingMilestone -eq 'ModernFormsNext 1.12.0') {
        if ($matches.Count -gt 0) { throw "Issue #$($issue.number) has 1.12.0 and a later-stage label." }
        $stage = '1.12.0'
    } else {
        if ($matches.Count -ne 1) {
            throw "Issue #$($issue.number) needs exactly one release/future label; found $($matches.Count)."
        }
        $stage = $LabelToStage[$matches[0]]
    }
    $plan += [pscustomobject]@{
        Number = [int]$issue.number
        Url = [string]$issue.url
        Stage = $stage
        CurrentMilestone = $existingMilestone
    }
}

Write-Host "Open issues: $($plan.Count)"
$plan | Group-Object Stage | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0}: {1}" -f $_.Name, $_.Count)
}

if (-not $Apply) {
    Write-Host 'DRY RUN: no writes. Use -Apply to create milestones, sync issue milestones,'
    Write-Host '         add missing Project #3 items and set Target release without changing Status.'
    exit 0
}

# Create exactly the known release milestones if missing (never invent milestone IDs).
$milestones = @(Invoke-GhJson -CommandArgs @('api',
    "repos/$Repo/milestones?state=all&per_page=100"))
$milestoneNumbers = @{}
foreach ($stage in $MilestoneStages) {
    $title = "ModernFormsNext $stage"
    $entry = $milestones | Where-Object { $_.title -eq $title } | Select-Object -First 1
    if ($null -eq $entry) {
        Write-Host "Creating milestone $title"
        $entry = Invoke-GhJson -CommandArgs @('api', '-X', 'POST',
            "repos/$Repo/milestones", '-f', "title=$title",
            '-f', 'description=Planned release scope; subject to readiness and dependency reviews.')
    }
    $milestoneNumbers[$stage] = [int]$entry.number
}

# Apply only the three 2.x milestones; retain the existing 1.12.0 milestone.
foreach ($item in $plan) {
    if (-not $milestoneNumbers.ContainsKey($item.Stage)) { continue }
    $expectedTitle = "ModernFormsNext $($item.Stage)"
    if ($item.CurrentMilestone -eq $expectedTitle) { continue }
    Write-Host "Issue #$($item.Number) -> milestone $($item.Stage)"
    Invoke-Gh -CommandArgs @('api', '-X', 'PATCH',
        "repos/$Repo/issues/$($item.Number)",
        '-F', "milestone=$($milestoneNumbers[$item.Stage])") | Out-Null
}

# Add a release single-select field only if absent; never overwrite Status, Priority,
# Iteration or another unrelated Project field.
$fieldResult = Invoke-GhJson -CommandArgs @('project', 'field-list', $Project,
    '--owner', $Owner, '--limit', '100', '--format', 'json')
$field = $fieldResult.fields | Where-Object { $_.name -eq $FieldName } | Select-Object -First 1
if ($null -eq $field) {
    Write-Host "Creating '$FieldName' field in Project #$Project"
    Invoke-Gh -CommandArgs @('project', 'field-create', $Project,
        '--owner', $Owner, '--name', $FieldName, '--data-type', 'SINGLE_SELECT',
        '--single-select-options', ($PossibleStages -join ',')) | Out-Null
    $fieldResult = Invoke-GhJson -CommandArgs @('project', 'field-list', $Project,
        '--owner', $Owner, '--limit', '100', '--format', 'json')
    $field = $fieldResult.fields | Where-Object { $_.name -eq $FieldName } | Select-Object -First 1
}
if ($null -eq $field -or [string]$field.dataType -ne 'SINGLE_SELECT') {
    throw "'$FieldName' must be a SINGLE_SELECT field; refusing to modify an incompatible field."
}
# A pre-existing field should provide these options. Do not silently add/rewrite its choices.
if ($null -ne $field.options) {
    $optionNames = @($field.options | ForEach-Object { $_.name })
    foreach ($wanted in $PossibleStages) {
        if ($wanted -notin $optionNames) {
            throw "Existing '$FieldName' field is missing option '$wanted'; configure this option before syncing."
        }
    }
}

$projectListing = Invoke-GhJson -CommandArgs @('project', 'item-list', $Project,
    '--owner', $Owner, '--limit', '1000', '--format', 'json')
$knownUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($projectItem in @($projectListing.items)) {
    if ($null -ne $projectItem.content -and $null -ne $projectItem.content.url) {
        [void]$knownUrls.Add([string]$projectItem.content.url)
    }
}
foreach ($item in $plan) {
    if (-not $knownUrls.Contains($item.Url)) {
        Write-Host "Adding #$($item.Number) to Project #$Project"
        Invoke-Gh -CommandArgs @('project', 'item-add', $Project,
            '--owner', $Owner, '--url', $item.Url) | Out-Null
    }
    Write-Host "Project #$Project / Target release: #$($item.Number) -> $($item.Stage)"
    Invoke-Gh -CommandArgs @('project', 'item-edit', $Project,
        '--owner', $Owner, '--url', $item.Url,
        '--field', $FieldName, '--value', $item.Stage) | Out-Null
}
Write-Host 'Synchronization complete. Verify counts and configure Project view grouping by Target release.'
