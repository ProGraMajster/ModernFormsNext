<#
.SYNOPSIS
Publishes and executes the bounded DynamicResource deployment consumer.
.DESCRIPTION
Runs ordinary JIT and a real self-contained full-trim publish, then checks an independent
negative publish for the legacy-name IL2026 diagnostic. Keeps every diagnostic and output;
does not assert whole-framework trim safety or Windows NativeAOT support. Android Mono AOT
is validated separately on the existing native sample. Run sequentially with other builds.
#>
[CmdletBinding()]
param([string]$OutputRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This consumer is qualified for win-x64; Android has a separate native runner.' }
$repository = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repository ('artifacts/dynamic-resource-publish/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a fresh output directory; previous evidence must be retained.' }
[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$project = Join-Path $repository 'scripts/validation/DynamicResourcePublish/DynamicResourcePublish.csproj'
$marker = 'DYNAMIC_RESOURCE_PUBLISH_PASS: 10 assertions;'
$common = @('-c', 'Release', '-m:1', '/p:UseSharedCompilation=false')

function Invoke-Checked([string[]]$Arguments, [string]$LogName) {
    $log = Join-Path $OutputRoot $LogName
    & dotnet @Arguments *> $log
    if ($LASTEXITCODE -ne 0) { throw "Deployment command failed; retained log: $log" }
}

Push-Location $repository
try {
    $head = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision.' }
    Invoke-Checked (@('build', $project) + $common + @('/p:PublishTrimmed=false', '/p:ResourceLegacyDiagnostic=false')) 'jit-build.log'
    $jitAssembly = Join-Path (Split-Path -Parent $project) 'bin/Release/net10.0/DynamicResourcePublish.dll'
    Invoke-Checked @($jitAssembly) 'jit.log'
    if (-not (Select-String -LiteralPath (Join-Path $OutputRoot 'jit.log') -SimpleMatch $marker -Quiet)) {
        throw 'Ordinary JIT execution did not complete the resource assertions.'
    }
    $trimmed = Join-Path $OutputRoot 'trimmed'
    $publish = @('publish', $project) + $common + @('-r', 'win-x64', '--self-contained', 'true',
        '/p:PublishTrimmed=true', '/p:TrimMode=link', '/p:TrimmerSingleWarn=false')
    Invoke-Checked ($publish + @('/p:ResourceLegacyDiagnostic=false', '--output', $trimmed)) 'trimmed.log'
    $warnings = @(Select-String -LiteralPath (Join-Path $OutputRoot 'trimmed.log') -Pattern '(?:warning|Trim analysis warning) IL\d+')
    $resourceWarnings = @($warnings | Where-Object Line -Match '\\ModernFormsNext\\(?:Control[.]Resources[.]cs|Resources\\)|[/\\]DynamicResourcePublish[/\\]Program[.]cs')
    if ($resourceWarnings.Count) { throw 'The typed resource path emitted trim diagnostics; inspect trimmed.log.' }
    & (Join-Path $trimmed 'DynamicResourcePublish.exe') *> (Join-Path $OutputRoot 'trimmed-run.log')
    if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath (Join-Path $OutputRoot 'trimmed-run.log') -SimpleMatch $marker -Quiet)) {
        throw 'The actual trimmed executable failed its resource assertions.'
    }
    Invoke-Checked ($publish + @('/p:ResourceLegacyDiagnostic=true', '--output', (Join-Path $OutputRoot 'legacy-diagnostic'))) 'legacy-diagnostic.log'
    $diagnostic = @(Select-String -LiteralPath (Join-Path $OutputRoot 'legacy-diagnostic.log') -Pattern 'Program[.]cs.*IL2026.*ResourceProperty<TControl, TValue>')
    if (-not $diagnostic.Count) { throw 'Legacy name-based reference did not produce actionable IL2026 guidance.' }
    $result = [ordered]@{
        Head = $head; FinishedUtc = [DateTime]::UtcNow.ToString('O')
        JitAssertions = 10; TrimmedAssertions = 10; LegacyDiagnostic = 'IL2026'
        ResourceTrimWarnings = 0; AllRetainedTrimWarnings = $warnings.Count
        Scope = 'Bounded resource consumer only; other framework trim warnings retained; no Windows NativeAOT claim.'
        ExecutableSha256 = (Get-FileHash -LiteralPath (Join-Path $trimmed 'DynamicResourcePublish.exe') -Algorithm SHA256).Hash
        ConsumerAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $trimmed 'DynamicResourcePublish.dll') -Algorithm SHA256).Hash
        FrameworkAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $trimmed 'ModernFormsNext.dll') -Algorithm SHA256).Hash
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'result.json') -Encoding utf8
    Write-Host "DYNAMIC_RESOURCE_DEPLOYMENT_PASS; evidence: $OutputRoot; retained trim diagnostics: $($warnings.Count)"
}
finally { Pop-Location }
