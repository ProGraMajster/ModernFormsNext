[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AdbPath,
    [Parameter(Mandatory)][string]$DeviceId,
    [Parameter(Mandatory)][string]$ApkPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$ReplaceFixture,
    [ValidateRange(1, 5)][int]$Trials = 3
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$adb = (Resolve-Path -LiteralPath $AdbPath).Path
$apk = (Resolve-Path -LiteralPath $ApkPath).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
$package = 'com.modernformsnext.skiaqualification'
if ((Split-Path -Leaf $apk) -cne "$package-Signed.apk") {
    throw 'Choose the signed APK from the isolated qualification fixture.'
}
$activity = "$package/$package.QualificationActivity"
$data = "/sdcard/Android/data/$package/files/results.json"
if (Test-Path -LiteralPath $root) { throw 'Choose a fresh output directory.' }
[IO.Directory]::CreateDirectory($root) | Out-Null

function Invoke-Adb([string[]]$Arguments) {
    $result = & $adb -s $DeviceId @Arguments
    if ($LASTEXITCODE -ne 0) { throw "ADB $($Arguments[0]) failed ($LASTEXITCODE)." }
    $result
}

$existing = & $adb -s $DeviceId shell pm path $package
if ($existing -like 'package:*' -and !$ReplaceFixture) {
    throw 'The qualification fixture is already installed. Verify ownership before replacing it.'
}
$argsForInstall = if ($ReplaceFixture) { @('install', '-r', $apk) } else { @('install', $apk) }
Invoke-Adb $argsForInstall
[ordered]@{
    ApkSha256 = (Get-FileHash -LiteralPath $apk).Hash
    Model = Invoke-Adb @('shell','getprop','ro.product.model')
    Api = Invoke-Adb @('shell','getprop','ro.build.version.sdk')
    Abi = Invoke-Adb @('shell','getprop','ro.product.cpu.abilist')
    PageSize = Invoke-Adb @('shell','getconf','PAGE_SIZE')
    Resolution = Invoke-Adb @('shell','wm','size')
    Density = Invoke-Adb @('shell','wm','density')
    PowerSave = Invoke-Adb @('shell','settings','get','global','low_power')
} | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'device.json')

try {
    foreach ($trial in 1..$Trials) {
        Invoke-Adb @('shell', 'am', 'force-stop', $package) | Out-Null
        Invoke-Adb @('shell', 'rm', '-f', $data) | Out-Null
        Invoke-Adb @('shell', 'am', 'start', '-W', '-n', $activity) |
            Set-Content -Encoding utf8 -LiteralPath (Join-Path $root "launch-$trial.txt")
        $deadline = [DateTime]::UtcNow.AddSeconds(35)
        do {
            & $adb -s $DeviceId shell test -s $data
            if ($LASTEXITCODE -eq 0) { break }
            Start-Sleep -Milliseconds 400
        } while ([DateTime]::UtcNow -lt $deadline)
        & $adb -s $DeviceId shell test -s $data
        if ($LASTEXITCODE -ne 0) {
            Invoke-Adb @('logcat','-d','-s','MfnSkiaQualification') |
                Select-Object -Last 60 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root "failure-$trial.log")
            throw "The workload did not complete in trial $trial."
        }
        $result = Join-Path $root "results-$trial.json"
        Invoke-Adb @('pull', $data, $result) | Out-Null
        $run = Get-Content -Raw -LiteralPath $result | ConvertFrom-Json
        if ($run.workload -ne 'android-text-collection-v1' -or
            $run.steps -ne 120 -or @($run.frames).Count -lt 2) {
            throw "Incomplete frame or workload evidence in trial $trial."
        }
        Write-Host "Trial $trial measured $(@($run.frames).Count) Android callbacks for $($run.steps) updates."
    }
    # PowerShell 7.4+ preserves the byte stream from native redirection.
    & $adb -s $DeviceId exec-out screencap -p > (Join-Path $root 'device.png')
    if ($LASTEXITCODE -ne 0) { throw 'Android screenshot failed.' }
}
finally {
    & $adb -s $DeviceId shell am force-stop $package
}
