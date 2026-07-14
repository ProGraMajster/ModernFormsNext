Set-StrictMode -Version Latest

$script:SamplePackage = 'com.programajster.modernformsnext.sample'

function Get-AndroidSdkCandidates {
    [CmdletBinding()]
    param([string]$SdkRoot)

    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in @($SdkRoot, $env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates.Add($candidate)
        }
    }

    $adbCommand = Get-Command adb -ErrorAction SilentlyContinue
    if ($adbCommand -and $adbCommand.Source) {
        $candidates.Add((Split-Path (Split-Path $adbCommand.Source -Parent) -Parent))
    }

    if ($env:LOCALAPPDATA) {
        $candidates.Add((Join-Path $env:LOCALAPPDATA 'Android\Sdk'))
    }
    if (${env:ProgramFiles(x86)}) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'Android\android-sdk'))
    }
    if ($env:ProgramFiles) {
        $candidates.Add((Join-Path $env:ProgramFiles 'Android\android-sdk'))
    }

    foreach ($registryPath in @(
        'HKCU:\Software\Android Studio',
        'HKLM:\Software\Android Studio',
        'HKLM:\Software\WOW6432Node\Android Studio'
    )) {
        try {
            $properties = Get-ItemProperty -Path $registryPath -ErrorAction Stop
            foreach ($propertyName in @('SdkPath', 'Path')) {
                $value = $properties.$propertyName
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $candidates.Add($value)
                }
            }
        }
        catch {
            # Android Studio is optional. Missing registry keys are expected on VS-only machines.
        }
    }

    $seen = @{}
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $expanded = [Environment]::ExpandEnvironmentVariables($candidate.Trim().Trim('"'))
        try {
            $fullPath = [IO.Path]::GetFullPath($expanded)
        }
        catch {
            continue
        }
        if (-not $seen.ContainsKey($fullPath)) {
            $seen[$fullPath] = $true
            $fullPath
        }
    }
}

function Resolve-AndroidSdkRoot {
    [CmdletBinding()]
    param(
        [string]$SdkRoot,
        [switch]$RequireEmulator
    )

    $checked = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in Get-AndroidSdkCandidates -SdkRoot $SdkRoot) {
        $checked.Add($candidate)
        $adb = Join-Path $candidate 'platform-tools\adb.exe'
        $emulator = Join-Path $candidate 'emulator\emulator.exe'
        if ((Test-Path -LiteralPath $adb -PathType Leaf) -and
            (-not $RequireEmulator -or (Test-Path -LiteralPath $emulator -PathType Leaf))) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $requirement = if ($RequireEmulator) { 'platform-tools\adb.exe and emulator\emulator.exe' } else { 'platform-tools\adb.exe' }
    throw "Android SDK was not found. Required: $requirement. Checked: $($checked -join '; '). Install the Android SDK through Visual Studio/Android Studio or pass -SdkRoot."
}

function Resolve-AndroidTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('adb', 'emulator')]
        [string]$Name,
        [string]$SdkRoot
    )

    $root = Resolve-AndroidSdkRoot -SdkRoot $SdkRoot -RequireEmulator:($Name -eq 'emulator')
    $relativePath = if ($Name -eq 'adb') { 'platform-tools\adb.exe' } else { 'emulator\emulator.exe' }
    $resolved = (Resolve-Path -LiteralPath (Join-Path $root $relativePath)).Path

    # Emulator 35.5.x starts emulator-check.exe through an unquoted child command on Windows.
    # Launching emulator.exe through its 8.3 path prevents Program Files (x86) from being split,
    # while every script argument remains an independently quoted process argument.
    if ($Name -eq 'emulator' -and $resolved.Contains(' ') -and $env:OS -eq 'Windows_NT') {
        $fileSystem = $null
        try {
            $fileSystem = New-Object -ComObject Scripting.FileSystemObject
            $shortPath = $fileSystem.GetFile($resolved).ShortPath
            if (-not [string]::IsNullOrWhiteSpace($shortPath)) { return $shortPath }
        }
        catch {
            # 8.3 names can be disabled. The normal quoted path remains the best available path.
        }
        finally {
            if ($fileSystem) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($fileSystem) | Out-Null }
        }
    }

    return $resolved
}

function ConvertFrom-AdbDevicesOutput {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][AllowEmptyString()][string[]]$Line)

    begin { $lines = [System.Collections.Generic.List[string]]::new() }
    process { foreach ($item in $Line) { $lines.Add($item) } }
    end {
        foreach ($item in $lines) {
            if ([string]::IsNullOrWhiteSpace($item) -or $item -match '^List of devices attached') { continue }
            if ($item -match '^\* daemon') { continue }
            $parts = $item.Trim() -split '\s+'
            if ($parts.Count -lt 2) { continue }

            $properties = @{}
            foreach ($part in $parts | Select-Object -Skip 2) {
                $separator = $part.IndexOf(':')
                if ($separator -gt 0) {
                    $properties[$part.Substring(0, $separator)] = $part.Substring($separator + 1)
                }
            }

            [pscustomobject]@{
                Serial = $parts[0]
                Status = $parts[1]
                Kind = if ($parts[0] -like 'emulator-*') { 'Emulator' } else { 'Physical' }
                Model = $properties['model']
                Product = $properties['product']
                Device = $properties['device']
                TransportId = $properties['transport_id']
            }
        }
    }
}

function Get-AndroidDevice {
    [CmdletBinding()]
    param(
        [string]$SdkRoot,
        [switch]$IncludeUnavailable,
        [switch]$RestartServer
    )

    $adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
    if ($RestartServer) {
        & $adb kill-server | Out-Null
        & $adb start-server | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "adb server restart failed with exit code $LASTEXITCODE." }
    }

    $output = @(& $adb devices -l)
    if ($LASTEXITCODE -ne 0) { throw "adb devices failed with exit code $LASTEXITCODE." }
    $devices = @($output | ConvertFrom-AdbDevicesOutput)
    if ($IncludeUnavailable) { return $devices }
    return @($devices | Where-Object Status -eq 'device')
}

function Select-AndroidDevice {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Device,
        [string]$Serial
    )

    $usable = @($Device | Where-Object Status -eq 'device')
    if ($Serial) {
        $selected = @($usable | Where-Object Serial -eq $Serial)
        if ($selected.Count -ne 1) {
            $known = @($Device | ForEach-Object { "$($_.Serial) [$($_.Status)]" }) -join ', '
            throw "Android device '$Serial' is not connected and ready. Detected: $known"
        }
        return $selected[0]
    }
    if ($usable.Count -eq 1) { return $usable[0] }
    if ($usable.Count -eq 0) {
        $unavailable = @($Device | ForEach-Object { "$($_.Serial) [$($_.Status)]" }) -join ', '
        throw "No usable Android device is connected. Detected: $unavailable. Start an AVD or authorize a USB device."
    }
    throw "Multiple Android devices are ready. Pass -DeviceId. Available: $($usable.Serial -join ', ')"
}

function ConvertFrom-AvdListOutput {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][AllowEmptyString()][string[]]$Line)

    process {
        foreach ($item in $Line) {
            $name = $item.Trim()
            if ($name -and -not $name.StartsWith('INFO', [StringComparison]::OrdinalIgnoreCase) -and
                -not $name.StartsWith('WARNING', [StringComparison]::OrdinalIgnoreCase)) {
                $name
            }
        }
    }
}

function Get-RunningAndroidAvd {
    [CmdletBinding()]
    param([string]$SdkRoot)

    $adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
    foreach ($device in @(Get-AndroidDevice -SdkRoot $SdkRoot -IncludeUnavailable | Where-Object Serial -like 'emulator-*')) {
        $nameOutput = @(& $adb -s $device.Serial emu avd name 2>$null)
        $name = @($nameOutput | Where-Object { $_ -and $_ -ne 'OK' } | Select-Object -First 1)
        [pscustomobject]@{
            Name = if ($name.Count) { $name[0].Trim() } else { $null }
            Serial = $device.Serial
            Status = $device.Status
        }
    }
}

function Get-AndroidAvd {
    [CmdletBinding()]
    param([string]$SdkRoot)

    $emulator = Resolve-AndroidTool -Name emulator -SdkRoot $SdkRoot
    $running = @(Get-RunningAndroidAvd -SdkRoot $SdkRoot)
    $names = @(& $emulator -list-avds | ConvertFrom-AvdListOutput)
    if ($LASTEXITCODE -ne 0) { throw "emulator -list-avds failed with exit code $LASTEXITCODE." }
    foreach ($name in $names) {
        $instance = $running | Where-Object Name -eq $name | Select-Object -First 1
        [pscustomobject]@{
            Name = $name
            IsRunning = $null -ne $instance
            Serial = if ($instance) { $instance.Serial } else { $null }
            Status = if ($instance) { $instance.Status } else { $null }
            EmulatorPath = $emulator
        }
    }
}

function Wait-AndroidDeviceReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string]$Serial,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 300,
        [ValidateRange(1, 30)][int]$PollSeconds = 2,
        [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $CancellationToken.ThrowIfCancellationRequested()
        $status = @(& $AdbPath -s $Serial get-state 2>$null | Select-Object -First 1)
        if ($status.Count -and $status[0].Trim() -eq 'device') {
            $booted = @(& $AdbPath -s $Serial shell getprop sys.boot_completed 2>$null | Select-Object -First 1)
            $bootAnimation = @(& $AdbPath -s $Serial shell getprop init.svc.bootanim 2>$null | Select-Object -First 1)
            if ($booted.Count -and $booted[0].Trim() -eq '1' -and
                (-not $bootAnimation.Count -or $bootAnimation[0].Trim() -ne 'running')) {
                return $Serial
            }
        }
        Start-Sleep -Seconds $PollSeconds
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Android device '$Serial' did not finish booting within $TimeoutSeconds seconds. Inspect 'adb -s $Serial devices -l' and emulator logs."
}

function Get-AdbInstallArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Serial,
        [Parameter(Mandatory)][string]$ApkPath,
        [switch]$AllowDowngrade
    )

    $arguments = @('-s', $Serial, 'install', '-r')
    if ($AllowDowngrade) { $arguments += '-d' }
    $arguments += $ApkPath
    return ,$arguments
}

function Get-AdbLaunchArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Serial,
        [Parameter(Mandatory)][string]$Component,
        [switch]$ForceStop
    )

    $arguments = @('-s', $Serial, 'shell', 'am', 'start', '-W')
    if ($ForceStop) { $arguments += '-S' }
    $arguments += @('-n', $Component)
    return ,$arguments
}

function ConvertFrom-AdbResolvedActivity {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][AllowEmptyString()][string[]]$Line)

    begin { $lines = [System.Collections.Generic.List[string]]::new() }
    process { foreach ($item in $Line) { $lines.Add($item) } }
    end {
        $component = @($lines |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -match '^[^\s/]+/[^\s/]+$' } |
            Select-Object -Last 1)
        if ($component.Count) { return $component[0] }
    }
}

function Resolve-AndroidLaunchActivity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string]$Serial,
        [string]$PackageName = $script:SamplePackage
    )

    $output = @(& $AdbPath -s $Serial shell cmd package resolve-activity --brief $PackageName 2>$null)
    $component = @($output | ConvertFrom-AdbResolvedActivity | Select-Object -First 1)
    if (-not $component.Count) {
        $output = @(& $AdbPath -s $Serial shell pm resolve-activity --brief $PackageName 2>$null)
        $component = @($output | ConvertFrom-AdbResolvedActivity | Select-Object -First 1)
    }

    if (-not $component.Count) {
        throw "Android did not resolve a launcher activity for installed package '$PackageName' on '$Serial'. Verify the manifest launcher intent filter and installation result."
    }

    return $component[0]
}

function Resolve-CrossPlatformSampleApk {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
    )

    $output = Join-Path $RepositoryRoot "samples\ModernFormsNext.CrossPlatform.Sample\bin\$Configuration\net10.0-android"
    if (-not (Test-Path -LiteralPath $output -PathType Container)) {
        throw "Android sample output directory '$output' does not exist. Build the sample first."
    }
    $signed = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*-Signed.apk' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($signed.Count) { return $signed[0] }
    $apk = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.apk' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending)
    if (-not $apk.Count) { throw "No APK was found under '$output'. Run Build-CrossPlatformSample.ps1." }
    return $apk[0]
}

function Invoke-CheckedNativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [Parameter(Mandatory)][string]$Operation
    )

    & $FilePath @ArgumentList | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE. Command: $FilePath $($ArgumentList -join ' ')"
    }
}

Export-ModuleMember -Function @(
    'Get-AndroidSdkCandidates',
    'Resolve-AndroidSdkRoot',
    'Resolve-AndroidTool',
    'ConvertFrom-AdbDevicesOutput',
    'Get-AndroidDevice',
    'Select-AndroidDevice',
    'ConvertFrom-AvdListOutput',
    'Get-RunningAndroidAvd',
    'Get-AndroidAvd',
    'Wait-AndroidDeviceReady',
    'Get-AdbInstallArguments',
    'Get-AdbLaunchArguments',
    'ConvertFrom-AdbResolvedActivity',
    'Resolve-AndroidLaunchActivity',
    'Resolve-CrossPlatformSampleApk',
    'Invoke-CheckedNativeCommand'
)
