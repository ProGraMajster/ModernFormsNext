[CmdletBinding()]
param()

$modulePath = Join-Path $PSScriptRoot 'AndroidTools.psm1'
Import-Module $modulePath -Force
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { $failures.Add($Message) }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) { $failures.Add("$Message Expected '$Expected', got '$Actual'.") }
}

$deviceOutput = @(
    'List of devices attached',
    'emulator-5554 device product:sdk_gphone64_x86_64 model:Pixel_7 device:emu64xa transport_id:1',
    'R58M offline usb:1-1 product:dream model:Phone device:dream transport_id:2',
    'ABC unauthorized usb:2-1'
)
$devices = @($deviceOutput | ConvertFrom-AdbDevicesOutput)
Assert-Equal 3 $devices.Count 'adb device parser should retain all states.'
Assert-Equal 'Pixel_7' $devices[0].Model 'adb device parser should capture properties.'
Assert-Equal 'Emulator' $devices[0].Kind 'emulator serial should be classified.'
Assert-Equal 'offline' $devices[1].Status 'offline status should remain visible.'
Assert-Equal 'emulator-5554' (Select-AndroidDevice -Device $devices).Serial 'single ready device should be selected.'

$multipleError = $false
try {
    Select-AndroidDevice -Device @($devices[0], [pscustomobject]@{ Serial = 'USB1'; Status = 'device' }) | Out-Null
}
catch { $multipleError = $_.Exception.Message -like '*Multiple Android devices*' }
Assert-True $multipleError 'ambiguous device selection should require -DeviceId.'

$avds = @(@('Pixel_7_API_34', '', 'WARNING | ignored') | ConvertFrom-AvdListOutput)
Assert-Equal 1 $avds.Count 'AVD parser should ignore warnings and blank lines.'
Assert-Equal 'Pixel_7_API_34' $avds[0] 'AVD parser should preserve names.'

$install = Get-AdbInstallArguments -Serial 'emulator-5554' -ApkPath 'C:\path with spaces\sample.apk'
Assert-Equal '-s' $install[0] 'install arguments should begin with serial selection.'
Assert-Equal 'C:\path with spaces\sample.apk' $install[-1] 'APK path should remain one argument.'
Assert-True ($install -contains '-r') 'install should preserve application data by default.'
Assert-True (-not ($install -contains '-g')) 'install should not silently grant runtime permissions.'

$resolved = @('priority=0', 'com.programajster.modernformsnext.sample/.MainActivity') | ConvertFrom-AdbResolvedActivity
Assert-Equal 'com.programajster.modernformsnext.sample/.MainActivity' $resolved 'launcher parser should select the resolved component.'
$launch = Get-AdbLaunchArguments -Serial 'emulator-5554' -Component $resolved
Assert-True ($launch -contains $resolved) 'launch arguments should preserve the resolved manifest component.'
Assert-True (-not ($launch -ccontains '-S')) 'launch should not force-stop unless requested.'
$forcedLaunch = Get-AdbLaunchArguments -Serial 'emulator-5554' -Component $resolved -ForceStop
Assert-True ($forcedLaunch -ccontains '-S') 'explicit force-stop should be represented.'

$scripts = Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.ps1'
foreach ($script in $scripts) {
    $content = Get-Content -LiteralPath $script.FullName -Raw
    Assert-True ($content -notmatch 'C:\\Users\\[^\\]+') "$($script.Name) must not contain a machine-specific user path."
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "$($failures.Count) Android tooling test(s) failed."
}
Write-Host "Android tooling tests passed ($($devices.Count) parsed devices, $($avds.Count) parsed AVD)."
