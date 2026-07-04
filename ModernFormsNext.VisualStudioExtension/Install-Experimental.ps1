[CmdletBinding()]
param(
    [string]$VisualStudioRoot = "C:\Program Files\Microsoft Visual Studio\18\Community",
    [string]$VsixPath = "",
    [string]$RootSuffix = "Exp",
    [switch]$SkipUninstall,
    [switch]$SkipCacheClean,
    [switch]$SkipTemplateRefresh,
    [switch]$ForceCloseVisualStudio
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
    $processSucceeded = $?
    $exitCode = $LASTEXITCODE
    if (-not $processSucceeded -or ($null -ne $exitCode -and $exitCode -ne 0)) {
        $exitCode = if ($null -eq $LASTEXITCODE) { "<unknown>" } else { $LASTEXITCODE }
        throw "$FailureMessage Exit code: $exitCode"
    }
}

function Get-VisualStudioMajorVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VisualStudioRoot
    )

    $parent = Split-Path -Path $VisualStudioRoot -Parent
    $major = Split-Path -Path $parent -Leaf
    if ([string]::IsNullOrWhiteSpace($major)) {
        throw "Unable to infer the Visual Studio major version from: $VisualStudioRoot"
    }

    return $major
}

function Get-VisualStudioHiveDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,

        [Parameter(Mandatory = $true)]
        [string]$MajorVersion,

        [Parameter(Mandatory = $true)]
        [string]$RootSuffix
    )

    if (-not (Test-Path -LiteralPath $RootPath)) {
        return @()
    }

    $suffixPattern = if ([string]::IsNullOrWhiteSpace($RootSuffix)) {
        "$MajorVersion.*"
    }
    else {
        "$MajorVersion.*$RootSuffix"
    }

    return @(Get-ChildItem -LiteralPath $RootPath -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $suffixPattern })
}

function Remove-SafePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $rootFullPath = [System.IO.Path]::GetFullPath($AllowedRoot)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $targetFullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the expected Visual Studio hive. Path: $targetFullPath Root: $rootFullPath"
    }

    Write-Host "Removing stale Visual Studio cache path: $targetFullPath"
    Remove-Item -LiteralPath $targetFullPath -Recurse -Force
}

function Test-ModernFormsNextExtensionDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo]$Directory
    )

    $manifestPath = Join-Path $Directory.FullName "extension.vsixmanifest"
    if ((Test-Path -LiteralPath $manifestPath) -and
        (Select-String -LiteralPath $manifestPath -Pattern "ModernFormsNext.Designer|ModernFormsNext Designer" -Quiet)) {
        return $true
    }

    $pkgdefFiles = @(Get-ChildItem -LiteralPath $Directory.FullName -Filter "*.pkgdef" -File -ErrorAction SilentlyContinue)
    foreach ($pkgdefFile in $pkgdefFiles) {
        if (Select-String -LiteralPath $pkgdefFile.FullName -Pattern "ModernFormsDesignerPackage|ModernFormsNext.VisualStudioExtension" -Quiet) {
            return $true
        }
    }

    return $false
}

function Clear-ModernFormsNextExperimentalCache {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VisualStudioRoot,

        [Parameter(Mandatory = $true)]
        [string]$RootSuffix
    )

    if ($RootSuffix -ne "Exp") {
        Write-Warning "Skipping private hive cleanup because this script only cleans the Experimental Instance by default. RootSuffix: $RootSuffix"
        return
    }

    $majorVersion = Get-VisualStudioMajorVersion -VisualStudioRoot $VisualStudioRoot
    $localVisualStudioRoot = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio"
    $roamingVisualStudioRoot = Join-Path $env:APPDATA "Microsoft\VisualStudio"
    $localHives = Get-VisualStudioHiveDirectories -RootPath $localVisualStudioRoot -MajorVersion $majorVersion -RootSuffix $RootSuffix
    $roamingHives = Get-VisualStudioHiveDirectories -RootPath $roamingVisualStudioRoot -MajorVersion $majorVersion -RootSuffix $RootSuffix

    foreach ($hive in $localHives) {
        Write-Host "Cleaning ModernFormsNext Designer state from local VS hive: $($hive.FullName)"

        $extensionsPath = Join-Path $hive.FullName "Extensions"
        if (Test-Path -LiteralPath $extensionsPath) {
            $extensionDirectories = @(Get-ChildItem -LiteralPath $extensionsPath -Directory -ErrorAction SilentlyContinue |
                Where-Object { Test-ModernFormsNextExtensionDirectory -Directory $_ })

            foreach ($extensionDirectory in $extensionDirectories) {
                Remove-SafePath -Path $extensionDirectory.FullName -AllowedRoot $extensionsPath
            }

            $extensionCacheFiles = @(
                Join-Path $extensionsPath "ExtensionMetadataCache.mpack"
                Join-Path $extensionsPath "ExtensionMetadata2.0.mpack"
                Join-Path $extensionsPath "extensions.configurationchanged"
            )

            foreach ($cacheFile in $extensionCacheFiles) {
                Remove-SafePath -Path $cacheFile -AllowedRoot $extensionsPath
            }
        }

        $cacheDirectories = @(
            Join-Path $hive.FullName "ComponentModelCache"
            Join-Path $hive.FullName "PackageCache"
            Join-Path $hive.FullName "TemplateEngineHost\ReleaseCache"
        )

        foreach ($cacheDirectory in $cacheDirectories) {
            Remove-SafePath -Path $cacheDirectory -AllowedRoot $hive.FullName
        }

        $templateCaches = @(Get-ChildItem -LiteralPath $hive.FullName -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "ItemTemplatesCache_*" -or $_.Name -like "ProjectTemplatesCache_*" })
        foreach ($templateCache in $templateCaches) {
            Remove-SafePath -Path $templateCache.FullName -AllowedRoot $hive.FullName
        }

        # The Experimental Instance keeps package CodeBase values in privateregistry.bin.
        # Deleting it is the reliable way to clear stale paths to removed random extension folders.
        $privateRegistryFiles = @(Get-ChildItem -LiteralPath $hive.FullName -File -Filter "privateregistry.bin*" -ErrorAction SilentlyContinue)
        foreach ($privateRegistryFile in $privateRegistryFiles) {
            Remove-SafePath -Path $privateRegistryFile.FullName -AllowedRoot $hive.FullName
        }
    }

    foreach ($hive in $roamingHives) {
        Write-Host "Cleaning ModernFormsNext Designer state from roaming VS hive: $($hive.FullName)"

        # Remove the old diagnostic log so the next Visual Studio launch records
        # only the current package-registration state. Keeping stale package-load
        # failures here makes it look like the freshly installed VSIX still uses
        # a deleted random extension folder.
        $activityLogPath = Join-Path $hive.FullName "ActivityLog.xml"
        Remove-SafePath -Path $activityLogPath -AllowedRoot $hive.FullName

        $cacheDirectories = @(Get-ChildItem -LiteralPath $hive.FullName -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "ItemTemplatesCache_*" -or $_.Name -like "ProjectTemplatesCache_*" })
        foreach ($cacheDirectory in $cacheDirectories) {
            Remove-SafePath -Path $cacheDirectory.FullName -AllowedRoot $hive.FullName
        }
    }
}

function Get-RunningVisualStudioInstance {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootSuffix
    )

    $processes = @(Get-CimInstance Win32_Process -Filter "Name='devenv.exe'" -ErrorAction SilentlyContinue)
    foreach ($process in $processes) {
        $commandLine = [string]$process.CommandLine
        $escapedRootSuffix = [System.Text.RegularExpressions.Regex]::Escape($RootSuffix)
        $isTargetRootSuffix = -not [string]::IsNullOrWhiteSpace($RootSuffix) -and
            $commandLine -match "(?i)(/RootSuffix(:|\s+)$escapedRootSuffix\b)"

        [pscustomobject]@{
            ProcessId = [int]$process.ProcessId
            CommandLine = $commandLine
            IsTargetRootSuffix = $isTargetRootSuffix
        }
    }
}

function Stop-VisualStudioInstance {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Instances,

        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    foreach ($instance in $Instances) {
        Write-Host "Stopping Visual Studio process $($instance.ProcessId): $Reason"
        Stop-Process -Id $instance.ProcessId -Force -ErrorAction Stop
    }
}

$vsixInstaller = Resolve-ToolPath -VisualStudioRoot $VisualStudioRoot -RelativePath "Common7\IDE\VSIXInstaller.exe"
$devenv = Resolve-ToolPath -VisualStudioRoot $VisualStudioRoot -RelativePath "Common7\IDE\devenv.exe"
$vsixInstallLog = Join-Path $env:TEMP "ModernFormsNextDesignerVsixInstall-$RootSuffix.log"

if ([string]::IsNullOrWhiteSpace($VsixPath)) {
    $scriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $PSScriptRoot
    }
    else {
        Split-Path -Path $MyInvocation.MyCommand.Path -Parent
    }

    $VsixPath = Join-Path $scriptRoot "..\ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix"
}

$resolvedVsix = Resolve-Path -LiteralPath $VsixPath

$runningVisualStudioInstances = @(Get-RunningVisualStudioInstance -RootSuffix $RootSuffix)
$targetRootSuffixInstances = @($runningVisualStudioInstances | Where-Object { $_.IsTargetRootSuffix })
$otherVisualStudioInstances = @($runningVisualStudioInstances | Where-Object { -not $_.IsTargetRootSuffix })

if ($targetRootSuffixInstances.Count -gt 0) {
    Stop-VisualStudioInstance -Instances $targetRootSuffixInstances -Reason "the $RootSuffix hive is being reinstalled."
    Start-Sleep -Seconds 2
}

if ($otherVisualStudioInstances.Count -gt 0) {
    if ($ForceCloseVisualStudio) {
        Stop-VisualStudioInstance -Instances $otherVisualStudioInstances -Reason "-ForceCloseVisualStudio was specified."
        Start-Sleep -Seconds 2
    }
    else {
        $runningList = ($otherVisualStudioInstances |
            ForEach-Object { "PID $($_.ProcessId): $($_.CommandLine)" }) -join [Environment]::NewLine
        throw "Visual Studio is still running outside the $RootSuffix hive. Close it before installing, or rerun this script with -ForceCloseVisualStudio if it is safe to close automatically.$([Environment]::NewLine)$runningList"
    }
}

if (-not $SkipUninstall -and -not $SkipCacheClean) {
    Write-Host "Skipping VSIXInstaller uninstall because Experimental Instance cache cleanup removes stale ModernFormsNext Designer registration."
}
elseif (-not $SkipUninstall) {
    Write-Host "Uninstalling any existing ModernFormsNext Designer from the $RootSuffix hive..."
    & $vsixInstaller "/quiet" "/rootSuffix:$RootSuffix" "/uninstall:ModernFormsNext.Designer" "/logFile:$vsixInstallLog"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Uninstall returned exit code $LASTEXITCODE. Continuing because the extension may not be installed yet."
    }
}

if (-not $SkipCacheClean) {
    Clear-ModernFormsNextExperimentalCache -VisualStudioRoot $VisualStudioRoot -RootSuffix $RootSuffix
}

Invoke-CheckedProcess `
    -FilePath $vsixInstaller `
    -Arguments @("/quiet", "/rootSuffix:$RootSuffix", $resolvedVsix.Path, "/logFile:$vsixInstallLog") `
    -FailureMessage "VSIX installation failed."

# Visual Studio can keep stale package CodeBase entries in the experimental hive.
# updateconfiguration rebuilds package registration so the package points at the
# freshly installed random extension folder instead of an older deleted one.
Invoke-CheckedProcess `
    -FilePath $devenv `
    -Arguments @("/RootSuffix", $RootSuffix, "/updateconfiguration") `
    -FailureMessage "Visual Studio configuration update failed."

if (-not $SkipTemplateRefresh) {
    Invoke-CheckedProcess `
        -FilePath $devenv `
        -Arguments @("/RootSuffix", $RootSuffix, "/installvstemplates") `
        -FailureMessage "Visual Studio item-template refresh failed."
}

Write-Host "ModernFormsNext Designer installed into the $RootSuffix hive."
Write-Host "Launch with: `"$devenv`" /RootSuffix $RootSuffix"
