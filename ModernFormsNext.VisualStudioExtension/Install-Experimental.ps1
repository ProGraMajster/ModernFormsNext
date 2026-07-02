[CmdletBinding()]
param(
    [string]$VisualStudioRoot = "C:\Program Files\Microsoft Visual Studio\18\Community",
    [string]$VsixPath = (Join-Path $PSScriptRoot "..\ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix"),
    [string]$RootSuffix = "Exp",
    [switch]$SkipUninstall
)

$ErrorActionPreference = "Stop"

function Resolve-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VisualStudioRoot,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $path = Join-Path $VisualStudioRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required Visual Studio tool was not found: $path"
    }

    return $path
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [string]$FailureMessage = "Process failed."
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

$vsixInstaller = Resolve-ToolPath -VisualStudioRoot $VisualStudioRoot -RelativePath "Common7\IDE\VSIXInstaller.exe"
$devenv = Resolve-ToolPath -VisualStudioRoot $VisualStudioRoot -RelativePath "Common7\IDE\devenv.exe"
$resolvedVsix = Resolve-Path -LiteralPath $VsixPath

$runningDevenv = Get-Process -Name devenv -ErrorAction SilentlyContinue
if ($runningDevenv) {
    Write-Warning "One or more Visual Studio instances are running. Close the Experimental Instance before installing the VSIX."
}

if (-not $SkipUninstall) {
    Write-Host "Uninstalling any existing ModernFormsNext Designer from the $RootSuffix hive..."
    & $vsixInstaller "/rootSuffix:$RootSuffix" "/uninstall:ModernFormsNext.Designer"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Uninstall returned exit code $LASTEXITCODE. Continuing because the extension may not be installed yet."
    }
}

Invoke-CheckedProcess `
    -FilePath $vsixInstaller `
    -Arguments @("/rootSuffix:$RootSuffix", $resolvedVsix.Path) `
    -FailureMessage "VSIX installation failed."

# Visual Studio can keep stale package CodeBase entries in the experimental hive.
# updateconfiguration rebuilds package registration so the package points at the
# freshly installed random extension folder instead of an older deleted one.
Invoke-CheckedProcess `
    -FilePath $devenv `
    -Arguments @("/RootSuffix", $RootSuffix, "/updateconfiguration") `
    -FailureMessage "Visual Studio configuration update failed."

Write-Host "ModernFormsNext Designer installed into the $RootSuffix hive."
Write-Host "Launch with: `"$devenv`" /RootSuffix $RootSuffix"
