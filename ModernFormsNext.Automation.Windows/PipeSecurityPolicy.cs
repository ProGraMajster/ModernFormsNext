using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace ModernFormsNext.Automation.Windows;

// All platform interop lives in this Windows adapter. In particular CurrentUserOnly alone
// does not set PIPE_REJECT_REMOTE_CLIENTS. ACL ownership uses User, not the token's Owner group.
internal static class PipeSecurityPolicy
{
    internal static SecurityIdentifier CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new AutomationTransportException(AutomationTransportError.AuthenticationFailed);
    }

    internal static NamedPipeServerStream Create(string endpoint, SecurityIdentifier user, bool first)
    {
        var security = new PipeSecurity();
        security.SetOwner(user); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        byte[] descriptor = security.GetSecurityDescriptorBinaryForm();
        var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(), SecurityDescriptor = pinned.AddrOfPinnedObject()
            };
            // PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | optional FILE_FLAG_FIRST_PIPE_INSTANCE.
            var handle = CreateNamedPipeW(@"\\.\pipe\" + endpoint,
                3u | 0x40000000u | (first ? 0x00080000u : 0u), 0x00000008u,
                8, 4096, 4096, 0, ref attributes);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
            catch { handle.Dispose(); throw; }
        }
        finally { pinned.Free(); }
    }

    internal static bool IsSameUser(NamedPipeServerStream pipe, SecurityIdentifier user)
    {
        bool matches = false;
        try
        {
            // Windows requires a preceding client write. Identification-level impersonation
            // permits checking identity without giving the server delegation authority.
            pipe.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent(true);
                matches = identity?.User == user;
            });
        }
        catch (Exception) { return false; }
        return matches;
    }

    internal static bool HasServerProcess(NamedPipeClientStream pipe, int processId)
        => GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint actual) && actual == processId;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal int Length;
        internal IntPtr SecurityDescriptor;
        internal int InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode,
        uint maxInstances, uint outBufferSize, uint inBufferSize, uint defaultTimeout, ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
