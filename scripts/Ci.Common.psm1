# Shared by the required PR job and its regression tests. Unknown changes must take
# the full path: this allowlist is a performance decision, never an authorization rule.
Set-StrictMode -Version Latest

function Invoke-CiGit {
    <# .SYNOPSIS
    Runs Git without shell interpolation and preserves NUL-delimited filenames.
    #>
    param([string]$RepositoryRoot, [string[]]$Arguments)

    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $RepositoryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false, $true)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Could not start Git.' }
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Git failed: $($errorOutput.GetAwaiter().GetResult())" }
        return $output.GetAwaiter().GetResult()
    }
    finally { $process.Dispose() }
}

function Get-CiChangeClassification {
    <# .SYNOPSIS
    Classifies the exact base-to-merge tree diff; defaults to full CI on uncertainty.
    .DESCRIPTION
    Only added or modified regular Markdown files in the explicit documentation
    locations are eligible. Deletions, renames, type/mode changes, empty diffs and
    unreadable revisions require full CI. No API pagination or path-filter limit
    is involved. Both revisions must be full commit IDs, never branch names.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit
    )

    $full = [pscustomobject]@{ Full = $true; Reason = 'Unclassified changes'; Paths = @() }
    try {
        if ($BaseCommit -cnotmatch '^[0-9a-f]{40}$' -or $HeadCommit -cnotmatch '^[0-9a-f]{40}$') {
            throw 'Classification requires full commit IDs.'
        }
        foreach ($commit in @($BaseCommit, $HeadCommit)) {
            $type = Invoke-CiGit $RepositoryRoot @('cat-file', '-t', $commit)
            if ($type.Trim() -cne 'commit') { throw 'Classification requires commit objects.' }
        }

        # Disable rename detection so a code-to-Markdown rename retains the deleted
        # code path. Raw records also expose symlinks and executable-mode changes.
        $raw = Invoke-CiGit $RepositoryRoot @('diff', '--raw', '--no-abbrev', '--no-renames', '-z', $BaseCommit, $HeadCommit, '--')
        $fields = $raw.Split([char]0)
        if ($fields.Count -lt 3) { throw 'An empty diff requires full validation.' }
        $paths = [Collections.Generic.List[string]]::new()
        for ($index = 0; $index -lt $fields.Count - 1; $index += 2) {
            if ($index + 1 -ge $fields.Count - 1 -or
                $fields[$index] -cnotmatch '^:(000000|100644) 100644 [0-9a-f]{40} [0-9a-f]{40} ([AM])$') {
                throw 'A deletion, rename, type/mode change or unexpected diff record requires full CI.'
            }
            $path = $fields[$index + 1]
            if ($path -match '[\x00-\x1f\\:]' -or $path -match '(^|/)\.\.?(/|$)') {
                throw 'An unusual path requires full CI.'
            }
            # Do not generalize to **/*.md: templates, test fixtures, packaged
            # assets, build scripts and workflow configuration are full-CI inputs.
            $allowed = $path -cin @('README.md', 'CHANGELOG.md', 'RELEASING.md', 'AGENTS.md', 'CONTRIBUTING.md', 'SECURITY.md') -or
                $path -cmatch '^docs/[^\r\n]+\.md$' -or
                $path -cin @('docs-site/index.md', 'docs-site/api-index.md')
            if (-not $allowed) { throw "Full CI input: $path" }
            $paths.Add($path)
        }
        return [pscustomobject]@{ Full = $false; Reason = 'Only allowlisted Markdown additions/modifications'; Paths = $paths.ToArray() }
    }
    catch {
        $full.Reason = $_.Exception.Message
        return $full
    }
}

function Test-CiDocumentation {
    <# .SYNOPSIS
    Checks source documentation without restoring or compiling the framework.
    .DESCRIPTION
    Requires nonempty UTF-8 Markdown with no merge-conflict markers, and the
    documentation entry points required by release packaging. Full offline HTML,
    API, archive and link validation remains mandatory in the release workflow.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string[]]$Paths
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($path in @('README.md', 'CHANGELOG.md', 'RELEASING.md', 'docs/getting-started.md',
        'docs-site/index.md', 'docs-site/api-index.md', 'docs-site/toc.yml')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $path) -PathType Leaf)) {
            throw "Required documentation input is missing: $path"
        }
    }
    foreach ($path in $Paths) {
        $fullPath = [IO.Path]::GetFullPath((Join-Path $root $path))
        if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Documentation path is outside the repository: $path"
        }
        $item = Get-Item -LiteralPath $fullPath -ErrorAction Stop
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Documentation cannot be a link: $path" }
        # Decode explicitly: ReadAllText auto-detects UTF-16 BOMs even when passed
        # UTF-8, which would silently accept a different source encoding.
        $content = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($fullPath)).TrimStart([char]0xfeff)
        if ($content.Contains([char]0)) { throw "Documentation contains a NUL character: $path" }
        if ([string]::IsNullOrWhiteSpace($content)) { throw "Documentation is empty: $path" }
        if ($content -match '(?m)^(<{7}|={7}|>{7})( |\r?$)') { throw "Unresolved conflict marker: $path" }
    }
    Write-Host "Validated $($Paths.Count) changed Markdown file(s) and release documentation entry points."
}

Export-ModuleMember -Function Invoke-CiGit, Get-CiChangeClassification, Test-CiDocumentation
