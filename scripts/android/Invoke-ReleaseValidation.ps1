<#
.SYNOPSIS
Runs opt-in sample instrumentation on one explicitly selected Android target and archives evidence.
.DESCRIPTION
Does not build, wipe, uninstall or grant permissions. APK replacement uses
adb install -r and preserves sample data. Android UiAutomation temporarily owns accessibility;
original/final setting values are recorded and compared. This is synthetic native validation, not
manual gesture, vendor-IME, TalkBack speech or GPU/scanout validation.
SystemReducedMotion temporarily sets animator_duration_scale to zero, restoring the original
value (including an absent setting) in finally. Other scenarios do not write device settings.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DeviceId,
    [Parameter(Mandatory = $true)][string]$ApkPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$SdkRoot,
    [ValidateRange(30, 1800)][int]$TimeoutSeconds = 180,
    [ValidateSet('ReleaseMatrix', 'AccessibilityPhase4', 'SystemReducedMotion')][string]$Scenario = 'ReleaseMatrix'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
$device = @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable | Where-Object Serial -eq $DeviceId)
if ($device.Count -ne 1 -or $device[0].Status -ne 'device') { throw 'The explicitly selected device is not ready/authorized.' }
$apk = (Resolve-Path -LiteralPath $ApkPath).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a new output directory so previous evidence cannot be overwritten or mixed.' }
New-Item -ItemType Directory -Path $destination | Out-Null
$package = 'com.programajster.modernformsnext.sample'
$runner = if ($Scenario -eq 'AccessibilityPhase4') { 'AccessibilityPhase4Instrumentation' } else { 'ReleaseValidationInstrumentation' }

function Invoke-AdbRead {
    param([string[]]$Arguments)
    $value = & $adb -s $DeviceId @Arguments
    if ($LASTEXITCODE -ne 0) { throw "adb read failed: $($Arguments[0])" }
    return ($value -join "`n").Trim()
}

function Read-Settings {
    $values = [ordered]@{}
    foreach ($entry in @('global/animator_duration_scale', 'global/window_animation_scale',
        'global/transition_animation_scale', 'system/font_scale', 'system/accelerometer_rotation',
        'system/user_rotation', 'system/peak_refresh_rate', 'system/min_refresh_rate',
        'secure/accessibility_enabled', 'secure/enabled_accessibility_services', 'secure/default_input_method')) {
        $parts = $entry.Split('/')
        $values[$entry] = Invoke-AdbRead @('shell', 'settings', 'get', $parts[0], $parts[1])
    }
    return $values
}

$initialSettings = Read-Settings
$initialSettings | ConvertTo-Json | Set-Content (Join-Path $destination 'settings-before.json')
$facts = [ordered]@{
    TimestampUtc = [DateTime]::UtcNow.ToString('O')
    EvidenceKind = $device[0].Kind
    Device = $device[0]
    Scenario = $Scenario
    TimeoutSeconds = $TimeoutSeconds
    SourceBase = (& git -C (Join-Path $PSScriptRoot '../..') rev-parse HEAD)
    WorkingTree = @(& git -C (Join-Path $PSScriptRoot '../..') status --porcelain)
    ApkSha256 = (Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash
    Apk = $apk
    RunnerSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../../samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/ReleaseValidationInstrumentation.cs')).Hash
    AccessibilityRunnerSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../../samples/ModernFormsNext.CrossPlatform.Sample/Platforms/Android/AccessibilityPhase4Instrumentation.cs')).Hash
    ScriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash
    Properties = [ordered]@{}
}
foreach ($property in @('ro.product.manufacturer', 'ro.product.model', 'ro.build.version.sdk',
    'ro.build.version.release', 'ro.build.fingerprint', 'ro.product.cpu.abilist', 'ro.soc.model')) {
    $facts.Properties[$property] = Invoke-AdbRead @('shell', 'getprop', $property)
}
$facts | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $destination 'provenance.json')
(Invoke-AdbRead @('shell', 'dumpsys', 'SurfaceFlinger')) -split "`n" | Where-Object { $_ -match 'GLES:' } |
    Set-Content (Join-Path $destination 'gpu.txt')
(Invoke-AdbRead @('shell', 'dumpsys', 'display')) -split "`n" | Where-Object { $_ -match 'DisplayDeviceInfo\{|mActiveModeId=|mSupportedRefreshRates=' } |
    Set-Content (Join-Path $destination 'display.txt')

try {
    if ($Scenario -eq 'SystemReducedMotion') {
        Invoke-AdbRead @('shell', 'settings', 'put', 'global', 'animator_duration_scale', '0') | Out-Null
    }
    # Separate transfer from installation: adb's optional incremental deployment can stall on
    # vendor devices, and is not the standalone-package path this matrix intends to validate.
    & $adb -s $DeviceId install --no-streaming -r $apk > (Join-Path $destination 'install.log') 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'APK installation failed; existing sample data was not removed.' }
    $instrumentationArguments = @('shell', 'am', 'instrument', '-w')
    if ($Scenario -eq 'SystemReducedMotion') { $instrumentationArguments += @('-e', 'systemReducedMotionOnly', 'true') }
    $instrumentationArguments += "$package/$package.$runner"
    # Bound even native instrumentation calls whose Android implementation can wait forever.
    # Only the explicitly selected sample is stopped on timeout; no app data is removed.
    $start = [Diagnostics.ProcessStartInfo]::new($adb)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in (@('-s', $DeviceId) + $instrumentationArguments)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            & $adb -s $DeviceId shell am force-stop $package
            if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
        }
        $stdout.GetAwaiter().GetResult() | Set-Content (Join-Path $destination 'instrumentation.log')
        $stderr.GetAwaiter().GetResult() | Set-Content (Join-Path $destination 'instrumentation-stderr.log')
        $instrumentationExit = $process.ExitCode
        if ($timedOut) { throw 'Instrumentation timed out; only the selected sample process was stopped.' }
    } finally { $process.Dispose() }
    $result = Get-Content -LiteralPath (Join-Path $destination 'instrumentation.log') -Raw
    if ($Scenario -ne 'AccessibilityPhase4' -and $result -match '(?m)^ARTIFACT_DIRECTORY=(.+)\r?$') {
        $remote = $Matches[1].Trim()
        if (-not $remote.StartsWith("/storage/emulated/0/Android/data/$package/files/release-validation/", [StringComparison]::Ordinal)) {
            throw 'Runner returned an unexpected artifact directory.'
        }
        & $adb -s $DeviceId pull $remote (Join-Path $destination 'native') > (Join-Path $destination 'pull.log')
        if ($LASTEXITCODE -ne 0) { throw 'Native artifact retrieval failed.' }
    }
    $marker = if ($Scenario -eq 'AccessibilityPhase4') { 'ANDROID_ACCESSIBILITY_PHASE4_PASS' } else { 'ANDROID_RELEASE_MATRIX_PASS' }
    # Do not mistake numeric telemetry such as recorder_failures=0 for a failure marker.
    if ($instrumentationExit -ne 0 -or $result -notmatch $marker -or
        $result -match '(?m)^(FAIL(?:\s|$)|ANDROID_RELEASE_MATRIX_FAIL|ANDROID_ACCESSIBILITY_PHASE4_FAIL|INSTRUMENTATION_FAILED)') {
        throw "Native validation failed. Inspect $destination/instrumentation.log."
    }
    Write-Host "$Scenario passed on $($device[0].Kind); evidence: $destination"
}
finally {
    if ($Scenario -eq 'SystemReducedMotion') {
        $originalScale = $initialSettings['global/animator_duration_scale']
        if ($originalScale -eq 'null') {
            Invoke-AdbRead @('shell', 'settings', 'delete', 'global', 'animator_duration_scale') | Out-Null
        } else {
            Invoke-AdbRead @('shell', 'settings', 'put', 'global', 'animator_duration_scale', $originalScale) | Out-Null
        }
    }
    $finalSettings = Read-Settings
    $finalSettings | ConvertTo-Json | Set-Content (Join-Path $destination 'settings-after.json')
    $differences = @($initialSettings.Keys | Where-Object { $initialSettings[$_] -cne $finalSettings[$_] })
    if ($differences.Count) { throw "Settings differ after instrumentation: $($differences -join ', '). Original and final values are archived; inspect before accepting the run." }
}
