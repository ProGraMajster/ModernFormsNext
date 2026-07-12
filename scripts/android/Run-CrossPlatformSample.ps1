[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$DeviceId,
    [switch]$NoBuild,
    [switch]$Reinstall,
    [switch]$ClearLogcat,
    [switch]$FollowLogcat
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'samples\ModernFormsNext.CrossPlatform.Sample\ModernFormsNext.CrossPlatform.Sample.csproj'
$adb = & (Join-Path $PSScriptRoot 'Resolve-Adb.ps1') -PathOnly
$devices = @(& (Join-Path $PSScriptRoot 'Get-AndroidDevices.ps1'))

if ($DeviceId) {
    if (-not ($devices | Where-Object Serial -eq $DeviceId)) { throw "Device '$DeviceId' is not connected and ready." }
}
elseif ($devices.Count -eq 1) {
    $DeviceId = $devices[0].Serial
}
elseif ($devices.Count -eq 0) {
    throw 'No usable Android device is connected. Run Get-AndroidDevices.ps1 or Start-AndroidEmulator.ps1 first.'
}
else {
    throw "Multiple devices are connected. Pass -DeviceId. Available: $($devices.Serial -join ', ')"
}

if (-not $NoBuild) {
    & dotnet build $project -f net10.0-android -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Android build failed with exit code $LASTEXITCODE." }
}

$output = Join-Path (Split-Path $project) "bin\$Configuration\net10.0-android"
$apk = Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*-Signed.apk' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.apk' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}
if (-not $apk) { throw "No APK was found under '$output'." }

$package = 'com.programajster.modernformsnext.sample'
$activity = 'com.programajster.modernformsnext.sample.MainActivity'
if ($Reinstall) { & $adb -s $DeviceId uninstall $package | Out-Host }
if ($ClearLogcat) { & $adb -s $DeviceId logcat -c }

Write-Host "Installing $($apk.FullName) on $DeviceId..."
& $adb -s $DeviceId install -r $apk.FullName
if ($LASTEXITCODE -ne 0) { throw "adb install failed with exit code $LASTEXITCODE." }

& $adb -s $DeviceId shell am start -n "$package/$activity"
if ($LASTEXITCODE -ne 0) { throw "Activity launch failed with exit code $LASTEXITCODE." }

Write-Host "Launched $package/$activity on $DeviceId."
if ($FollowLogcat) {
    & (Join-Path $PSScriptRoot 'Watch-ModernFormsNextLogcat.ps1') -DeviceId $DeviceId
}
