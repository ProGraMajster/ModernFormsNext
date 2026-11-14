<#
.SYNOPSIS
Validates all repository GitHub workflows, including expression-context availability.
.DESCRIPTION
Runs pinned actionlint on Windows x64 before any release dispatch. The verified portable
tool is downloaded into an owned temporary directory and removed after validation; no
global installation, workflow execution or publication occurs. Optional shellcheck and
pyflakes integration is disabled; the dedicated PowerShell script tests remain separate.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (![OperatingSystem]::IsWindows() -or
    [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [Runtime.InteropServices.Architecture]::X64) {
    throw 'This validation wrapper requires PowerShell 7 on Windows x64.'
}

$version = '1.7.12'
# Pin the official release asset bytes as well as the version. Updating either is a reviewable change.
$expectedSha256 = '6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9'
$temporary = [IO.Directory]::CreateTempSubdirectory('mfn-actionlint-')
$root = $temporary.FullName
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $archive = Join-Path $root 'actionlint.zip'
    $tools = Join-Path $root 'tool'
    Invoke-WebRequest -Uri "https://github.com/rhysd/actionlint/releases/download/v$version/actionlint_${version}_windows_amd64.zip" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedSha256) {
        throw 'The actionlint release asset does not match its pinned SHA-256.'
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $tools
    & (Join-Path $tools 'actionlint.exe') -color=false -shellcheck= -pyflakes=
    if ($LASTEXITCODE -ne 0) { throw 'GitHub workflow syntax/context validation failed.' }
    Write-Host "Validated all GitHub workflows with actionlint $version."
}
finally {
    Pop-Location
    # Delete only this invocation's verified direct child of the system temporary directory.
    $resolved = Get-Item -LiteralPath $root
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($resolved.FullName -cne $root -or $resolved.Parent.FullName -ine $parent -or
        $resolved.Name -cnotlike 'mfn-actionlint-*' -or ($resolved.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected workflow-validator temporary directory.'
    }
    Remove-Item -LiteralPath $resolved.FullName -Recurse -Force
}
