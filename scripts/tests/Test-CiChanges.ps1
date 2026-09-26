<# .SYNOPSIS
Regression tests for the required CI gate, using real Git tree diffs.
.DESCRIPTION
Runs in an isolated temporary repository; never modifies the caller's index or
working files. Covers mixed changes, renames, modes, unusual filenames, missing
history, large diffs and failures of the lightweight documentation checks.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'Ci.Common.psm1') -Force
$script:assertions = 0
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$root = Join-Path $tempParent "ModernFormsNext-ci-tests-$([Guid]::NewGuid().ToString('N'))"
[IO.Directory]::CreateDirectory($root) | Out-Null

function Assert-True([bool]$Condition, [string]$Message) {
    $script:assertions++
    if (-not $Condition) { throw $Message }
}

function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    $script:assertions++
    try { & $Action }
    catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw }
        return
    }
    throw "Expected a failure matching '$Pattern'."
}

function Write-Fixture([string]$Path, [string]$Content = '# Documentation') {
    $destination = Join-Path $root $Path
    [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
    [IO.File]::WriteAllText($destination, $Content, [Text.UTF8Encoding]::new($false))
}

function Save-Fixture {
    Invoke-CiGit $root @('add', '--all') | Out-Null
    Invoke-CiGit $root @('-c', 'user.name=CI tests', '-c', 'user.email=ci@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Fixture') | Out-Null
    return (Invoke-CiGit $root @('rev-parse', 'HEAD')).Trim()
}

function Assert-Classification([string]$Base, [string]$Head, [bool]$Full, [string]$Message) {
    $result = Get-CiChangeClassification -RepositoryRoot $root -BaseCommit $Base -HeadCommit $Head
    Assert-True ($result.Full -eq $Full) "$Message ($($result.Reason))"
    return $result
}

try {
    Invoke-CiGit $root @('init', '--quiet') | Out-Null
    Invoke-CiGit $root @('config', 'core.autocrlf', 'false') | Out-Null
    foreach ($path in @('README.md', 'CHANGELOG.md', 'RELEASING.md', 'docs/getting-started.md',
        'docs-site/index.md', 'docs-site/api-index.md', 'docs-site/toc.yml')) {
        Write-Fixture $path
    }
    Write-Fixture 'Control.cs' '// Code'
    $initial = Save-Fixture

    Write-Fixture 'CHANGELOG.md' '# Updated changelog'
    $head = Save-Fixture
    $result = Assert-Classification $initial $head $false 'Changelog-only changes must use the light path.'
    Assert-True ($result.Paths.Count -eq 1 -and $result.Paths[0] -ceq 'CHANGELOG.md') 'The classifier must return exact paths.'
    Test-CiDocumentation -RepositoryRoot $root -Paths $result.Paths

    foreach ($path in @('docs/guide.md', 'docs/nested/guide with spaces.md', 'docs/zażółć.md',
        'docs/literal-$(command).md', 'docs-site/api-index.md')) {
        $previous = $head
        Write-Fixture $path '# New content'
        $head = Save-Fixture
        $null = Assert-Classification $previous $head $false "Documentation path should be allowed: $path"
    }

    foreach ($path in @('Control.cs', 'Project.csproj', 'Directory.Build.props', 'build/MicroCom.targets',
        'ModernFormsNext.slnx', 'global.json', 'NuGet.Config', '.config/dotnet-tools.json',
        '.github/workflows/dotnet.yml', '.github/README.md', 'scripts/build.ps1', 'scripts/README.md',
        'ModernFormsNext.Templates/templates/app/README.md', 'samples/Assets/README.md',
        'docs/example.csproj', 'docs/setup.ps1', 'docs-site/toc.yml', 'unknown.txt', 'license.md',
        'third-party-licenses.md')) {
        $previous = $head
        Write-Fixture $path 'Changed input'
        $head = Save-Fixture
        $null = Assert-Classification $previous $head $true "Build/unknown input must require full CI: $path"
    }

    $codeHead = $head
    Write-Fixture 'README.md' '# Docs after code'
    $head = Save-Fixture
    $null = Assert-Classification $codeHead $head $false 'A docs-only revision is light by itself.'
    $null = Assert-Classification $initial $head $true 'The full PR diff must still include earlier code changes.'

    $previous = $head
    Invoke-CiGit $root @('mv', 'Control.cs', 'docs/renamed.md') | Out-Null
    $head = Save-Fixture
    $null = Assert-Classification $previous $head $true 'Renaming code to Markdown must not hide a code deletion.'

    $previous = $head
    Invoke-CiGit $root @('mv', 'docs/guide.md', 'docs/renamed-guide.md') | Out-Null
    $head = Save-Fixture
    $null = Assert-Classification $previous $head $true 'Renamed docs can break incoming links and require full CI.'

    $previous = $head
    Invoke-CiGit $root @('rm', 'docs/renamed.md') | Out-Null
    $head = Save-Fixture
    $null = Assert-Classification $previous $head $true 'Deletion must require full CI.'

    $previous = $head
    # Git index modes exercise executable/symlink handling without requiring Windows
    # symlink privileges or executing any fixture contents.
    Invoke-CiGit $root @('update-index', '--chmod=+x', 'README.md') | Out-Null
    Invoke-CiGit $root @('-c', 'user.name=CI tests', '-c', 'user.email=ci@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Executable mode') | Out-Null
    $head = (Invoke-CiGit $root @('rev-parse', 'HEAD')).Trim()
    $null = Assert-Classification $previous $head $true 'Executable Markdown must require full CI.'
    $blob = (Invoke-CiGit $root @('rev-parse', "${head}:README.md")).Trim()
    Invoke-CiGit $root @('update-index', '--add', '--cacheinfo', "120000,$blob,docs/link.md") | Out-Null
    Invoke-CiGit $root @('-c', 'user.name=CI tests', '-c', 'user.email=ci@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Link mode') | Out-Null
    $linkHead = (Invoke-CiGit $root @('rev-parse', 'HEAD')).Trim()
    $null = Assert-Classification $head $linkHead $true 'Symlinks must require full CI.'

    # Compare a large real diff, beyond GitHub's workflow path-filter file limit.
    $previous = Save-Fixture # Normalize the index-only mode fixtures to working files.
    foreach ($number in 1..350) { Write-Fixture "docs/large/$number.md" }
    $head = Save-Fixture
    $result = Assert-Classification $previous $head $false 'Large documentation diffs must be complete.'
    Assert-True ($result.Paths.Count -eq 350) 'All changed files must be inspected.'
    Write-Fixture 'z-last-code.cs' '// Must not be lost after the first 300 paths'
    $head = Save-Fixture
    $null = Assert-Classification $previous $head $true 'Code beyond the first 300 files must require full CI.'

    $null = Assert-Classification $head $head $true 'An empty diff must fail closed.'
    $null = Assert-Classification ('0' * 40) $head $true 'Missing base history must fail closed.'
    $null = Assert-Classification 'master' $head $true 'Mutable branch names must fail closed.'

    Write-Fixture 'docs/invalid.md' ''
    Assert-Throws { Test-CiDocumentation $root @('docs/invalid.md') } 'empty'
    Write-Fixture 'docs/invalid.md' "<<<<<<< branch`nConflict`n=======`nContent`n>>>>>>> base"
    Assert-Throws { Test-CiDocumentation $root @('docs/invalid.md') } 'conflict'
    [IO.File]::WriteAllBytes((Join-Path $root 'docs/invalid.md'), [byte[]]@(0xc3, 0x28))
    Assert-Throws { Test-CiDocumentation $root @('docs/invalid.md') } 'translate|Unable|invalid|convert'
    Assert-Throws { Test-CiDocumentation $root @('../outside.md') } 'outside'
    Invoke-CiGit $root @('rm', '--force', 'docs/getting-started.md') | Out-Null
    Assert-Throws { Test-CiDocumentation $root @('README.md') } 'Required documentation input'

    Write-Host "CI classification tests passed ($script:assertions assertions)."
}
finally {
    # Only remove the exact GUID-scoped fixture under the verified temp parent.
    $resolved = [IO.Path]::GetFullPath($root)
    $prefix = $tempParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved) -match '^ModernFormsNext-ci-tests-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
