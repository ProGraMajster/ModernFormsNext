using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ModernFormsNext.Designer.ExecutablePreview;
using FilePath = System.IO.Path;

namespace ModernFormsNext.DesignerPreviewWorker;

// The broker runs this trusted branch before passing an approved project snapshot to the worker.
internal static class SandboxProbe
{
    internal static int Run(Stream output, PreviewKey key)
    {
        string outside = Environment.GetEnvironmentVariable("MFN_PREVIEW_CANARY")!;
        string scratch = FilePath.GetFullPath(FilePath.Combine(AppContext.BaseDirectory, "..", "scratch", "canary.txt"));
        string snapshot = FilePath.Combine(AppContext.BaseDirectory, "canary.txt");
        string profile = Environment.GetEnvironmentVariable("MFN_PREVIEW_PROFILE_ROOT")!;
        string profileName = Environment.GetEnvironmentVariable("MFN_PREVIEW_PROFILE_NAME")!;
        string address = Environment.GetEnvironmentVariable("MFN_PREVIEW_ADDRESS")!;
        int tcp4 = ReadNumber("MFN_PREVIEW_TCP4");
        int udp4 = ReadNumber("MFN_PREVIEW_UDP4");
        int tcp6 = ReadNumber("MFN_PREVIEW_TCP6");
        int udp6 = ReadNumber("MFN_PREVIEW_UDP6");
        string pipe = Environment.GetEnvironmentVariable("MFN_PREVIEW_PIPE")!;
        int parent = ReadNumber("MFN_PREVIEW_PARENT");
        byte failed = 0;
        int nativeError = 0;
        Check(1, () => File.ReadAllText(scratch) == "preview scratch");
        Check(2, () => File.ReadAllText(snapshot) == "preview readonly" && Denied(() => File.WriteAllText(snapshot, "probe")));
        Check(3, () => Denied(() => File.ReadAllText(outside)) && Denied(() => File.AppendAllText(outside, "probe")));
        Check(4, () => TcpDenied(IPAddress.Loopback, tcp4));
        Check(5, () => TcpDenied(IPAddress.Parse(address), tcp4));
        Check(6, () => HostDenied(parent));
        Check(7, ChildDenied);
        Check(8, () => UdpDenied(IPAddress.Loopback, udp4));
        Check(9, () => UdpDenied(IPAddress.Parse(address), udp4));
        string removedProfileFile = FilePath.Combine(profile, "AC", "preview-canary");
        Check(10, () => NativeWriteDenied(removedProfileFile, create: true, out nativeError) || nativeError == 3);
        Check(11, () => CannotRecreateProfileDirectory(profile, out nativeError));
        Check(12, () => NativeWriteDenied(scratch, create: false, out nativeError));
        Check(13, () => NativeWriteDenied(scratch + ".new", create: true, out nativeError));
        Check(14, () => CannotRecreateProfile(profileName, out nativeError));
        Check(15, () => NoWritableProfileRegistry(out nativeError));
        Check(16, () => TcpDenied(IPAddress.IPv6Loopback, tcp6));
        Check(17, () => UdpDenied(IPAddress.IPv6Loopback, udp6));
        Check(18, () => DesktopPipeDenied(pipe, out nativeError));
        byte[] result = new byte[PreviewWire.ProbeResultLength];
        result[0] = failed;
        if (failed != 0)
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(1), nativeError);
        Program.Write(output, key, PreviewMessage.Result, result);
        return failed == 0 ? 0 : 4;

        void Check(byte step, Func<bool> check)
        {
            if (failed != 0)
                return;
            nativeError = 0;
            try { if (!check()) failed = step; }
            catch (Exception exception)
            {
                failed = step;
                nativeError = exception.HResult;
            }
        }
    }

    private static int ReadNumber(string name)
        => int.Parse(Environment.GetEnvironmentVariable(name)!, System.Globalization.CultureInfo.InvariantCulture);

    private static bool NativeWriteDenied(string path, bool create, out int error)
    {
        using var file = CreateFile(path, 0x40000000, 7, 0, create ? 1U : 3U, 0, 0);
        error = file.IsInvalid ? Marshal.GetLastPInvokeError() : 0;
        // Windows can report either an access denial or a missing privilege when the
        // AppContainer profile redirects a write. A missing file or path fails admission.
        return error is 5 or 1314;
    }

    private static bool DesktopPipeDenied(string name, out int error)
    {
        using var pipe = CreateFile(@"\\.\pipe\" + name, 0xc0000000, 0, 0, 3, 0, 0);
        error = pipe.IsInvalid ? Marshal.GetLastPInvokeError() : 0;
        // Windows may hide a pipe in a different namespace or return explicit access denial.
        return error is 2 or 5;
    }

    private static bool CannotRecreateProfileDirectory(string profile, out int error)
    {
        error = CreateDirectory(profile, 0) ? 0 : Marshal.GetLastPInvokeError();
        return error == 5;
    }

    private static bool CannotRecreateProfile(string name, out int error)
    {
        error = CreateAppContainerProfile(name, name, "Preview policy probe", 0, 0, out nint sid);
        if (sid != 0)
            FreeSid(sid);
        return IsMissingOrDeniedProfile(error);
    }

    private static bool NoWritableProfileRegistry(out int error)
    {
        error = GetAppContainerRegistryLocation(0x20006, out nint key); // KEY_WRITE
        if (key != 0)
            RegCloseKey(key);
        return IsMissingOrDeniedProfile(error);
    }

    private static bool IsMissingOrDeniedProfile(int result)
        => (uint)result is 0x80070002 or 0x80070003 or 0x80070005 or 0x80070032 or 0x80070522;

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateDirectory(string path, nint security);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int CreateAppContainerProfile(string name, string display, string description, nint capabilities,
        uint capabilityCount, out nint sid);
    [DllImport("userenv.dll")]
    private static extern int GetAppContainerRegistryLocation(uint rights, out nint key);
    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(nint key);
    [DllImport("advapi32.dll")]
    private static extern nint FreeSid(nint sid);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share,
        nint security, uint disposition, uint flags, nint template);

    private static bool Denied(Action attempt)
    {
        try { attempt(); return false; }
        catch (UnauthorizedAccessException) { return true; }
    }

    private static bool TcpDenied(IPAddress address, int port)
    {
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            socket.ConnectAsync(new IPEndPoint(address, port), deadline.Token).AsTask().GetAwaiter().GetResult();
            return false;
        }
        catch (SocketException error) when (error.NativeErrorCode is 10013 or 10107) { return true; }
    }

    private static bool UdpDenied(IPAddress address, int port)
    {
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.SendTo([0x46], new IPEndPoint(address, port));
            return false;
        }
        catch (SocketException error) when (error.NativeErrorCode is 10013 or 10107) { return true; }
    }

    private static bool HostDenied(int parent)
    {
        nint handle = OpenProcess(0x80 | 0x20, false, parent); // PROCESS_CREATE_PROCESS | PROCESS_VM_WRITE
        if (handle == 0)
            return Marshal.GetLastWin32Error() == 5;
        CloseHandle(handle);
        return false;
    }

    private static bool ChildDenied()
    {
        try
        {
            // Direct start without a request is inert. A successful creation is still a policy failure.
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
            return false;
        }
        catch (Win32Exception error) when (error.NativeErrorCode is 5 or 367) { return true; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
