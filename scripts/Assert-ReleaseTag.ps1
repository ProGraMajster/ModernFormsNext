[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$Commit,
    [switch]$CheckOrigin
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ReleaseDocumentation.Common.psm1') -Force
$version = ConvertTo-ReleaseVersion -Version $Tag
if ($Tag -cne "v$version") { throw 'The ref must be an exact version tag.' }
$reference = "refs/tags/$Tag"
$head = & git rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $Commit) { throw 'Checkout SHA does not match the release commit.' }
$tagCommit = & git rev-parse --verify "$reference^{commit}"
if ($LASTEXITCODE -ne 0 -or $tagCommit -cne $Commit) { throw 'Local release tag does not match the release commit.' }

if ($CheckOrigin) {
    $peeledReference = "$reference^{}"
    $refs = @(& git ls-remote --exit-code origin $reference $peeledReference)
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the release tag on origin.' }
    $advertised = @{}
    foreach ($line in $refs) {
        $sha, $name = $line -csplit [char]9, 2
        if ($sha -cnotmatch '\A[0-9a-f]{40}\z' -or $name -cnotin @($reference, $peeledReference) -or
            $advertised.ContainsKey($name)) { throw 'Unexpected release tag response from origin.' }
        $advertised[$name] = $sha
    }
    if (!$advertised.ContainsKey($reference) -or
        ($advertised[$peeledReference] ?? $advertised[$reference]) -cne $Commit) {
        throw 'The release tag on origin does not match the release commit.'
    }
}
