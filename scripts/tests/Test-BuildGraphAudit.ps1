<#
.SYNOPSIS
Checks the binlog isolation detector against deliberate collisions and isolated builds.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$AuditTool)
$ErrorActionPreference = 'Stop'
$AuditTool = (Resolve-Path -LiteralPath $AuditTool).Path
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$parent = Join-Path $root 'artifacts/build-graph-tests'
$fixture = Join-Path $parent ([Guid]::NewGuid().ToString('N'))
$checks = 0
try {
    $null = New-Item -ItemType Directory -Path $fixture
    $project = Join-Path $fixture 'Leaf.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable>
    <IntermediateOutputPath Condition="'$(Isolated)' == 'true'">obj\$(Flavor)\$(Configuration)\</IntermediateOutputPath>
    <OutputPath Condition="'$(Isolated)' == 'true'">bin\$(Flavor)\$(Configuration)\</OutputPath>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile Condition="'$(SharedDocumentation)' == 'true'">obj\shared.xml</DocumentationFile>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath $project
    '/// <summary>Fixture for build isolation validation.</summary>', 'public sealed class Leaf { }' |
        Set-Content (Join-Path $fixture 'Leaf.cs')
    $driver = Join-Path $fixture 'Driver.proj'
    @'
<Project>
  <PropertyGroup><RequestedTargets Condition="'$(RequestedTargets)' == ''">Rebuild</RequestedTargets></PropertyGroup>
  <Target Name="Check">
    <MSBuild Projects="Leaf.csproj" Targets="$(RequestedTargets)" Properties="Flavor=one" />
    <MSBuild Projects="Leaf.csproj" Targets="$(RequestedTargets)" Properties="Flavor=two" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $driver
    $output = & dotnet restore $project -v:q 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
    foreach ($case in @(
        @{Name='collision'; Isolated='false'; Shared='false'; Expected=1; Error='IntermediateOutputPath'; Incremental=$false},
        @{Name='incremental-collision'; Isolated='false'; Shared='false'; Expected=1; Error='IntermediateOutputPath'; Incremental=$true},
        @{Name='isolated'; Isolated='true'; Shared='false'; Expected=0; Error=''; Incremental=$false},
        @{Name='escaped-documentation'; Isolated='true'; Shared='true'; Expected=1; Error='DocumentationFile'; Incremental=$false}
    )) {
        $binlog = Join-Path $fixture ($case.Name + '.binlog')
        $json = Join-Path $fixture ($case.Name + '.json')
        # Run the intentionally broken fixture serially; detecting the dangerous
        # graph must not require provoking a sharing violation on the machine.
        $extraBuild = if ($case.Incremental) { @('-p:RequestedTargets=Build', '-p:SkipCompilerExecution=true') } else { @() }
        $output = & dotnet msbuild $driver -t:Check -m:1 -v:q /p:UseSharedCompilation=false "/p:Isolated=$($case.Isolated)" "/p:SharedDocumentation=$($case.Shared)" "-bl:$binlog;ProjectImports=None" @extraBuild 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
        [string[]]$extraAudit = if ($case.Incremental) { @('--allow-incremental') } else { @() }
        $output = & dotnet $AuditTool $binlog $json @extraAudit 2>&1
        if ($LASTEXITCODE -ne $case.Expected) { throw "$($case.Name): unexpected detector result: $($output -join [Environment]::NewLine)" }
        $report = Get-Content -Raw $json | ConvertFrom-Json
        if ($case.Error -and -not ($report.Failures -match $case.Error)) { throw "$($case.Name): expected collision was not reported." }
        if ($case.Incremental -and $report.Compilations -ne 0) { throw 'The incremental collision must be detected without a compiler execution.' }
        $checks++
    }
    Write-Host "Build graph detector: $checks scenarios passed."
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $boundary = [IO.Path]::GetFullPath($parent) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
