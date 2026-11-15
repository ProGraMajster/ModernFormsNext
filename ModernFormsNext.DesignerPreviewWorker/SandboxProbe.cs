using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
        string address = Environment.GetEnvironmentVariable("MFN_PREVIEW_ADDRESS")!;
        int port = int.Parse(Environment.GetEnvironmentVariable("MFN_PREVIEW_PORT")!, System.Globalization.CultureInfo.InvariantCulture);
        int parent = int.Parse(Environment.GetEnvironmentVariable("MFN_PREVIEW_PARENT")!, System.Globalization.CultureInfo.InvariantCulture);
        byte failed = 0;
        Check(1, () =>
        {
            File.WriteAllText(scratch, "preview scratch");
            return File.ReadAllText(scratch) == "preview scratch";
        });
        Check(2, () => File.ReadAllText(snapshot) == "preview readonly" && Denied(() => File.WriteAllText(snapshot, "probe")));
        Check(3, () => Denied(() => File.ReadAllText(outside)) && Denied(() => File.AppendAllText(outside, "probe")));
        Check(4, () => TcpDenied(IPAddress.Loopback, port));
        Check(5, () => TcpDenied(IPAddress.Parse(address), port));
        Check(6, () => HostDenied(parent));
        Check(7, ChildDenied);
        Check(8, () => UdpDenied(IPAddress.Loopback, port));
        Check(9, () => UdpDenied(IPAddress.Parse(address), port));
        Program.Write(output, key, PreviewMessage.Result, [failed]);
        return failed == 0 ? 0 : 4;

        void Check(byte step, Func<bool> check)
        {
            if (failed != 0)
                return;
            try { if (!check()) failed = step; }
            catch { failed = step; }
        }
    }

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
