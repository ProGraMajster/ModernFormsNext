<#!
.SYNOPSIS
Exercises the registered sample's real COM cold and warm activation entry point.
.DESCRIPTION
Run the sample once with --smoke first. This invokes COM directly with synthetic payloads;
it is not evidence for native banner clicking, input rendering, focus or audio.
No installer keys are written. Close the sample before running this script.
Use -StartProcessManually to isolate SDK startup-payload handling from Windows COM process
launch. That mode starts the EXE with the SDK launch marker and is not a cold-launch certification.
!#>
[CmdletBinding()]
param([switch]$StartProcessManually)
$ErrorActionPreference = 'Stop'
$aumid = 'ProGraMajster.ModernFormsNext.NotificationSample'
$registration = Get-ItemProperty -LiteralPath "HKCU:\Software\Classes\AppUserModelId\$aumid"
$classId = [Guid]$registration.CustomActivator
if (Get-Process -Name SystemNotifications -ErrorAction SilentlyContinue) { throw 'Close the notification sample first.' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NotificationActivationProbe {
    [ComImport, Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICallback {
        void Activate([MarshalAs(UnmanagedType.LPWStr)] string appId, [MarshalAs(UnmanagedType.LPWStr)] string args, IntPtr data, uint count);
    }
    [DllImport("ole32.dll", PreserveSig=false)]
    static extern void CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out ICallback callback);
    public static void Invoke(Guid clsid, string appId, string args, bool inputs) {
        var iid = typeof(ICallback).GUID;
        ICallback callback;
        CoCreateInstance(ref clsid, IntPtr.Zero, 4, ref iid, out callback);
        IntPtr data = IntPtr.Zero;
        var strings = new IntPtr[4];
        try {
            if (inputs) {
                data = Marshal.AllocCoTaskMem(IntPtr.Size * 4);
                var values = new[] { "reply", "Native reply", "choice", "yes" };
                for (int i=0; i<4; i++) { strings[i] = Marshal.StringToCoTaskMemUni(values[i]); Marshal.WriteIntPtr(data, IntPtr.Size*i, strings[i]); }
            }
            callback.Activate(appId, args, data, inputs ? 2u : 0u);
        } finally {
            foreach (var value in strings) if (value != IntPtr.Zero) Marshal.FreeCoTaskMem(value);
            if (data != IntPtr.Zero) Marshal.FreeCoTaskMem(data);
            Marshal.FinalReleaseComObject(callback);
        }
    }
}
'@
function Wait-Activation([string]$expected) {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $sample = Get-Process -Name SystemNotifications -ErrorAction SilentlyContinue
        if ($sample -and $sample.MainWindowTitle -eq $expected) { return $sample }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Activation was not observed: $expected"
}
$sample = $null
try {
    if ($StartProcessManually) {
        $server = (Get-Item -LiteralPath "HKCU:\Software\Classes\CLSID\{$classId}\LocalServer32").GetValue('')
        if ($server -notmatch '^"([^"]+)"') { throw 'The registered executable path must be quoted.' }
        $sample = Start-Process -FilePath $Matches[1] -ArgumentList '----AppNotificationActivated:' -WindowStyle Hidden -PassThru
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        try {
            [NotificationActivationProbe]::Invoke($classId, $aumid, 'mfn=1&id=cold-probe&args=download-42&action=send', $true)
            break
        } catch {
            # Only the explicitly started process needs time to publish its live class factory.
            # Do not hide a real cold CoCreateInstance failure behind retries.
            if (-not $StartProcessManually -or $_.Exception.InnerException.HResult -ne -2147221164 -or [DateTime]::UtcNow -ge $deadline) { throw }
            Start-Sleep -Milliseconds 100
        }
    } while ($true)
    $sample = Wait-Activation 'Notification activation: cold-probe/send/2'
    $firstProcessId = $sample.Id
    [NotificationActivationProbe]::Invoke($classId, $aumid, 'mfn=1&id=warm-probe&args=download-42', $false)
    $sample = Wait-Activation 'Notification activation: warm-probe/body/0'
    if ($sample.Id -ne $firstProcessId) { throw 'Warm activation unexpectedly changed the sample process.' }
    if ($StartProcessManually) { Write-Output 'NATIVE_STARTUP_PAYLOAD_PASS SDK launch marker; action+inputs; warm body; Windows process launch bypassed' }
    else { Write-Output 'NATIVE_COM_ACTIVATION_PASS cold action+inputs; warm body; same live process' }
} finally {
    if ($sample) {
        $null = $sample.CloseMainWindow()
        if (-not $sample.WaitForExit(10000)) { throw 'Sample did not dispose and close within 10 seconds.' }
    }
}
