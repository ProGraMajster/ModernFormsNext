<# .SYNOPSIS
Exercises the package validator's platform-TFM matching contract without invoking pack/publication.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$validatorPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Validate-ReleasePackages.ps1'
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($validatorPath, [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Package validator has parse errors.' }
$definition = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Test-FrameworkEntry'
}, $true)
if ($null -eq $definition) { throw 'Validator is missing Test-FrameworkEntry.' }
# Load exactly the real helper, avoiding the validator's archive/publication-sized entry point.
Invoke-Expression $definition.Extent.Text
$cases = @(
    @{ Entry = 'lib/net10.0/P.dll'; Framework = 'net10.0'; Expected = $true },
    @{ Entry = 'lib/net10.0-android/P.dll'; Framework = 'net10.0-android'; Expected = $true },
    @{ Entry = 'lib/net10.0-android36.0/P.dll'; Framework = 'net10.0-android'; Expected = $true },
    @{ Entry = 'lib/net10.0-windows10.0.26100/P.dll'; Framework = 'net10.0-windows'; Expected = $true },
    @{ Entry = 'lib/net10.0-android36.0/P.dll'; Framework = 'net10.0'; Expected = $false },
    @{ Entry = 'lib/net10.0-windows/P.dll'; Framework = 'net10.0-android'; Expected = $false },
    @{ Entry = 'lib/net10.0-androidInvalid/P.dll'; Framework = 'net10.0-android'; Expected = $false },
    @{ Entry = 'lib/net10.0-android36.0/Other.dll'; Framework = 'net10.0-android'; Expected = $false }
)
foreach ($case in $cases) {
    if ((Test-FrameworkEntry -EntryName $case.Entry -Framework $case.Framework -FileName 'P.dll') -ne $case.Expected) {
        throw "Unexpected platform matching: $($case.Entry) for $($case.Framework)."
    }
}
Write-Host "Release package script tests passed ($($cases.Count) assertions)."
