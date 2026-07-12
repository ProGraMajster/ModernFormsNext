[CmdletBinding()]
param(
    [switch]$PathOnly
)

$candidates = [System.Collections.Generic.List[string]]::new()

$command = Get-Command adb -ErrorAction SilentlyContinue
if ($command -and $command.Source) {
    $candidates.Add($command.Source)
}

foreach ($root in @(
    $env:ANDROID_SDK_ROOT,
    $env:ANDROID_HOME,
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
    (Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk'),
    (Join-Path $env:ProgramFiles 'Android\android-sdk')
)) {
    if ($root) {
        $candidates.Add((Join-Path $root 'platform-tools\adb.exe'))
    }
}

foreach ($registryPath in @(
    'HKCU:\Software\Android Studio',
    'HKLM:\Software\Android Studio',
    'HKLM:\Software\WOW6432Node\Android Studio'
)) {
    try {
        $sdkPath = (Get-ItemProperty -Path $registryPath -ErrorAction Stop).SdkPath
        if ($sdkPath) {
            $candidates.Add((Join-Path $sdkPath 'platform-tools\adb.exe'))
        }
    }
    catch {
        # The registry key is optional; continue through deterministic SDK locations.
    }
}

$adb = $candidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
    ForEach-Object { (Resolve-Path -LiteralPath $_).Path } |
    Select-Object -Unique -First 1

if (-not $adb) {
    throw @'
Android Debug Bridge (adb) was not found.
Checked PATH, ANDROID_SDK_ROOT, ANDROID_HOME, the per-user Android SDK, the Visual Studio Android SDK location, and Android Studio registry keys.
Install/select an Android SDK with platform-tools or pass an appropriate SDK through ANDROID_SDK_ROOT for this shell.
'@
}

if (-not $PathOnly) {
    Write-Host "adb: $adb"
    & $adb version | ForEach-Object { Write-Host $_ }
}

$adb
