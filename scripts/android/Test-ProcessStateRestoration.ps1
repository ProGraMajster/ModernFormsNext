[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DeviceId,
    [Parameter(Mandatory)][string]$ApkPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$SdkRoot
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AndroidTools.psm1') -Force
$adb = Resolve-AndroidTool -Name adb -SdkRoot $SdkRoot
$package = 'com.programajster.modernformsnext.sample'
$component = "$package/.MainActivity"
$apk = (Resolve-Path -LiteralPath $ApkPath).Path
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh output directory.' }
$output = (New-Item -ItemType Directory -Path $OutputDirectory).FullName
$device = @(Get-AndroidDevice -SdkRoot $SdkRoot | Where-Object Serial -eq $DeviceId)
if ($device.Count -ne 1 -or $device[0].Status -ne 'device') { throw 'Select one ready Android target.' }

function Read-Adb([string[]]$Arguments, [string]$Record) {
    $lines = @(& $adb -s $DeviceId @Arguments 2>&1)
    $code = $LASTEXITCODE
    if ($Record) { $lines | Set-Content -LiteralPath (Join-Path $output $Record) -Encoding utf8 }
    if ($code -ne 0) { throw "ADB exited with $code during $($Arguments[0])." }
    return $lines
}
function Get-SamplePid {
    $value = (& $adb -s $DeviceId shell pidof $package | Out-String).Trim()
    if ($LASTEXITCODE -eq 1 -and !$value) { return $null }
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^\d+$') { throw 'Expected one sample process.' }
    return [int]$value
}
function Read-Probe([int]$SamplePid, [string]$Stage) {
    $pattern = "\s+$SamplePid\s+\d+\s+I\s+MfnLifecycleProbe:\s+create task=(?<task>\d+) bundle=(?<bundle>True|False) seeded=(?<seed>True|False) expectedFixtureState=(?<state>True|False) restore=(?<restore>\d+) schema=(?<schema>\d*) activation=(?<activation>\d+) kind=(?<kind>\w*)"
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $lines = @(Read-Adb -Arguments @('logcat','--pid',$SamplePid,'-d','-v','threadtime','-s','MfnLifecycleProbe:I','*:S'))
        $line = $lines | Select-String -Pattern $pattern | Select-Object -Last 1
        if ($line) {
            $lines | Set-Content (Join-Path $output "$Stage-probe.log") -Encoding utf8
            return ,$line.Matches[0].Groups
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    $lines | Set-Content (Join-Path $output "$Stage-probe.log") -Encoding utf8
    throw "Lifecycle fixture did not report $Stage."
}

$apkHash = (Get-FileHash -LiteralPath $apk).Hash.ToLowerInvariant()
$api = (Read-Adb -Arguments @('shell','getprop','ro.build.version.sdk')) -join ''
if (Get-SamplePid) { throw 'Close the running sample before starting the owned process fixture.' }
[void](Read-Adb -Arguments @('install','--no-streaming','-r',$apk) -Record 'install.log')
# NEW_TASK | MULTIPLE_TASK isolates this test from any older task of the sample.
[void](Read-Adb -Arguments @('shell','am','start','-W','-a','android.intent.action.MAIN','-c',
    'android.intent.category.LAUNCHER','-f','0x18000000','-n',$component,
    '--ez','MFN_LIFECYCLE_PROBE','true','--ez','MFN_LIFECYCLE_SEED','true') -Record 'launch.log')
$oldPid = Get-SamplePid
if (!$oldPid) { throw 'Initial sample process was not started.' }
$initial = Read-Probe $oldPid 'initial'
if ($initial['seed'].Value -ne 'True' -or $initial['bundle'].Value -ne 'False' -or
    $initial['restore'].Value -ne '0' -or $initial['state'].Value -ne 'True' -or
    $initial['kind'].Value -ne 'Launch') { throw 'Initial fixture was not seeded in a new task.' }
$task = [int]$initial['task'].Value

[void](Read-Adb -Arguments @('shell','input','keyevent','KEYCODE_HOME'))
$deadline = [DateTime]::UtcNow.AddSeconds(15)
$hasSavedState = $false
do {
    $activities = @(Read-Adb -Arguments @('shell','dumpsys','activity','activities',$package))
    $ownActivity = [Regex]::Match(($activities -join "`n"),
        "(?ms)^\s*\* Hist\s+#\d+:\s*ActivityRecord\{[^\r\n]*$([Regex]::Escape($package))/[^\r\n]*\bt$task\}.*?(?=\n\s*\* Hist\s+#|\z)")
    $hasSavedState = $ownActivity.Success -and $ownActivity.Value -match 'state=STOPPED' -and
        $ownActivity.Value -match 'mHaveState=true|haveState=true'
    if ($hasSavedState) { break }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)
$activities | Set-Content (Join-Path $output 'background-task.log') -Encoding utf8
if (!$hasSavedState -or (Get-SamplePid) -ne $oldPid) { throw 'The owned task did not stop with a saved Bundle.' }

[void](Read-Adb -Arguments @('shell','am','kill','--user','current',$package) -Record 'kill.log')
$deadline = [DateTime]::UtcNow.AddSeconds(15)
while ((Get-SamplePid) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
if (Get-SamplePid) { throw 'Activity Manager did not retire the background sample process.' }
# NEW_TASK | RESET_TASK_IF_NEEDED asks the launcher path to resume the retained task.
[void](Read-Adb -Arguments @('shell','am','start','-W','-a','android.intent.action.MAIN','-c',
    'android.intent.category.LAUNCHER','-f','0x10200000','-n',$component) -Record 'resume.log')
$newPid = Get-SamplePid
if (!$newPid -or $newPid -eq $oldPid) { throw 'Resume did not create a new process.' }
$restored = Read-Probe $newPid 'restored'
if ([int]$restored['task'].Value -ne $task -or $restored['seed'].Value -ne 'False' -or
    $restored['bundle'].Value -ne 'True' -or $restored['state'].Value -ne 'True' -or
    $restored['schema'].Value -ne '1' -or $restored['kind'].Value -ne 'Launch' -or
    [long]$restored['restore'].Value -le 0 -or [long]$restored['restore'].Value -ge [long]$restored['activation'].Value) {
    throw 'The same task did not restore its fixture state before launch notification in the new process.'
}
[pscustomobject]@{
    Kind = $device[0].Kind; Api = [int]$api; Model = $device[0].Model; ApkSha256 = $apkHash
    Task = $task; OldPid = $oldPid; NewPid = $newPid; Schema = [int]$restored['schema'].Value
    RestoreSequence = [long]$restored['restore'].Value; ActivationSequence = [long]$restored['activation'].Value
} | ConvertTo-Json | Set-Content (Join-Path $output 'result.json') -Encoding utf8
Write-Output "Cold process restoration passed for task $task."
