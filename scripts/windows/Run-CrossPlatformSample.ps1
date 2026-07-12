[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'samples\ModernFormsNext.CrossPlatform.Sample\ModernFormsNext.CrossPlatform.Sample.csproj'
& dotnet run --project $project -f net10.0-windows -c $Configuration
exit $LASTEXITCODE
