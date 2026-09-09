using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Security")]
public sealed class WindowsSecurityEvidenceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RestrictedTokenIsDeniedByRealPipeAndSecretFileAcl()
    {
        await using var host = await ProcessFixture.Start();
        // A restricting SID requires a second OS access check. This proves real ACL denial,
        // without creating an account, changing ACLs or pretending this is a cross-user login.
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Duplicate | TokenAccessLevels.Query);
        var world = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        byte[] bytes = new byte[world.BinaryLength]; world.GetBinaryForm(bytes, 0);
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var sid = new SidAndAttributes { Sid = pin.AddrOfPinnedObject() };
            Assert.True(CreateRestrictedToken(identity.AccessToken, 1, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, ref sid, out var restricted),
                new Win32Exception(Marshal.GetLastWin32Error()).Message);
            using (restricted)
            {
                WindowsIdentity.RunImpersonated(restricted, () =>
                {
                    AssertDenied(@"\\.\pipe\" + host.Application!.EndpointName);
                    AssertDenied(AutomationDiscovery.AuthPath(host.Application.InstanceId));
                    AssertDenied(AutomationDiscovery.DescriptorPath(host.Application.InstanceId));
                });
            }
        }
        finally { pin.Free(); }
        await using var client = await host.Connect(); Assert.Equal(AutomationErrorCode.None, (await client.GetRootsAsync()).Error);
        output.WriteLine("PASS: actual Windows access denied (5) for restricted token: pipe, secret, descriptor. Ordinary current-user authentication succeeds. Different logged-on user: NOT EXECUTED.");
    }

    [Fact]
    public async Task RemotePathProbeRequiresWorkingSmbControlBeforeClaimingRejection()
    {
        await using var host = await ProcessFixture.Start();
        string name = "mfn-remote-control-" + Guid.NewGuid().ToString("N");
        using var controlServer = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var control = new NamedPipeClientStream(Environment.MachineName, name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var accepted = controlServer.WaitForConnectionAsync(timeout.Token);
        try
        {
            await control.ConnectAsync(timeout.Token); await accepted;
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            timeout.Cancel(); try { await accepted; } catch (Exception) { }
            output.WriteLine("NOT EXECUTED: remote-path rejection could not be distinguished from unavailable SMB/current-user loopback. Positive control failed. Local native PIPE_REJECT_REMOTE_CLIENTS creation and current-user auth are exercised separately.");
            return;
        }
        using var remote = new NamedPipeClientStream(Environment.MachineName, host.Application!.EndpointName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var rejectionDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var failure = await Record.ExceptionAsync(() => remote.ConnectAsync(rejectionDeadline.Token));
        Assert.NotNull(failure); Assert.False(remote.IsConnected);
        output.WriteLine("PASS: SMB loopback positive control connected; the bridge remote path was rejected. A second physical machine was NOT EXECUTED.");
    }

    private static void AssertDenied(string path)
    {
        using var handle = CreateFileW(path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        int error = Marshal.GetLastWin32Error(); Assert.True(handle.IsInvalid); Assert.Equal(5, error);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes { internal IntPtr Sid; internal uint Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(SafeAccessTokenHandle existing, uint flags, uint disabledCount, IntPtr disabled,
        uint privilegeCount, IntPtr privileges, uint restrictedCount, ref SidAndAttributes restricting, out SafeAccessTokenHandle token);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}
