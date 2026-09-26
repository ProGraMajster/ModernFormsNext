<#
.SYNOPSIS
Checks Android packaging-property normalization, including late transitive references.
.DESCRIPTION
Uses an SDK project chain without requiring an Android workload. The root simulates
the inspected Android SDK's packaging request; referenced libraries reject leaked
application properties. Real Android solution builds remain a separate validation.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$parent = Join-Path $root 'artifacts/build-boundary-tests'
$fixture = Join-Path $parent ([Guid]::NewGuid().ToString('N'))
function Invoke-Fixture([string[]]$Arguments) {
    $output = & dotnet @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
    return ($output -join [Environment]::NewLine)
}
try {
    foreach ($name in @('App', 'Middle', 'Leaf')) {
        $directory = Join-Path $fixture $name
        $null = New-Item -ItemType Directory -Path $directory -Force
        $reference = switch ($name) {
            'App' { '<ItemGroup><ProjectReference Include="../Middle/Middle.csproj" /></ItemGroup>' }
            'Middle' { '<ItemGroup><ProjectReference Include="../Leaf/Leaf.csproj" /></ItemGroup>' }
            default { '' }
        }
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup>
  $reference
  <Target Name="AssertLibraryBoundary" BeforeTargets="Build" Condition="'`$(MSBuildProjectName)' != 'App'">
    <Error Condition="'`$(_ComputeFilesToPublishForRuntimeIdentifiers)' != '' or '`$(_OuterIntermediateAssembly)' != '' or '`$(SkipCompilerExecution)' != '' or '`$(DesignTimeBuild)' != ''"
           Text="Application packaging properties leaked into a library instance." />
  </Target>
</Project>
"@ | Set-Content (Join-Path $directory "$name.csproj")
    }
    $app = Join-Path $fixture 'App/App.csproj'
    $null = Invoke-Fixture @('restore', $app, '-v:q')
    $null = Invoke-Fixture @('build', $app, '--no-restore', '-v:q', '/p:UseSharedCompilation=false')
    $packaging = @('-p:_ComputeFilesToPublishForRuntimeIdentifiers=true', '-p:_OuterIntermediateAssembly=app.dll',
        '-p:SkipCompilerExecution=true', '-p:DesignTimeBuild=false')
    $result = Invoke-Fixture (@('msbuild', $app, '-t:ResolveProjectReferences', '-getItem:ProjectReference',
        '/p:UseSharedCompilation=false') + $packaging) | ConvertFrom-Json
    $references = @($result.Items.ProjectReference)
    if ($references.Count -ne 2 -or -not ($references.Identity -like '*Leaf.csproj')) {
        throw 'The fixture must exercise an SDK-added transitive Leaf reference.'
    }
    foreach ($reference in $references) {
        if ($reference.GlobalPropertiesToRemove -notmatch '_OuterIntermediateAssembly') {
            throw "Missing boundary metadata on $($reference.Identity)."
        }
    }
    $ide = Invoke-Fixture @('msbuild', $app, '-p:DesignTimeBuild=true', '-t:AssignProjectConfiguration',
        '-getProperty:DesignTimeBuild', '-getItem:ProjectReference') | ConvertFrom-Json
    if ($ide.Properties.DesignTimeBuild -ne 'true' -or
        $ide.Items.ProjectReference.GlobalPropertiesToRemove -match 'DesignTimeBuild') {
        throw 'Ordinary IDE design-time properties must not be normalized.'
    }
    Write-Host 'Build graph boundary: direct, late transitive and ordinary IDE scenarios passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $boundary = [IO.Path]::GetFullPath($parent) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
