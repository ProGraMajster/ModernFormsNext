using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ModernFormsNext.Designer.ExecutablePreview;

namespace ModernFormsNext.VisualStudioDesignerHost.ExecutablePreview;

internal sealed class WindowsExecutablePreview(string workerPath) : IExecutablePreviewBackend
{
    internal static bool Available =>
#if EXECUTABLE_PREVIEW_QUALIFICATION
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) &&
        RuntimeInformation.ProcessArchitecture == Architecture.X64;
#else
        false;
#endif

    public IExecutablePreviewSnapshot Capture(string project, IReadOnlyList<string> files)
        => new Approval(PreviewSnapshot.Capture(project, workerPath, files));

    private sealed class Approval(PreviewSnapshot snapshot) : IExecutablePreviewSnapshot
    {
        private LpacPreviewLauncher? launcher;
        private readonly List<FileSystemWatcher> watchers = [];

        public string Digest => snapshot.Digest;
        public IReadOnlyList<string> Summary => snapshot.Summary.ToArray();
        public event Action? SourcesChanged;

        public async Task VerifyAsync(CancellationToken ct)
        {
            if (launcher is not null)
                throw new InvalidOperationException();
            var candidate = new LpacPreviewLauncher(snapshot);
            try
            {
                await VerifyPolicyAsync(candidate, ct).ConfigureAwait(false);
                candidate.SealScratch();
                WatchSources();
                if (!snapshot.HasCurrentSources())
                    throw new InvalidDataException();
                launcher = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        private void WatchSources()
        {
            foreach (string path in snapshot.Paths)
            {
                var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security
                };
                watchers.Add(watcher);
                watcher.Changed += (_, _) => SourcesChanged?.Invoke();
                watcher.Created += (_, _) => SourcesChanged?.Invoke();
                watcher.Deleted += (_, _) => SourcesChanged?.Invoke();
                watcher.Renamed += (_, _) => SourcesChanged?.Invoke();
                watcher.Error += (_, _) => SourcesChanged?.Invoke();
                watcher.EnableRaisingEvents = true;
            }
        }

        private static async Task VerifyPolicyAsync(LpacPreviewLauncher candidate, CancellationToken ct)
        {
            // Both TCP targets are local and controlled. The unrestricted positive controls establish
            // reachability before the worker must receive a platform-level failure to connect.
            var address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .SelectMany(item => item.GetIPProperties().UnicastAddresses)
                .Select(item => item.Address)
                .FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(item));
            var listener = new TcpListener(IPAddress.Any, 0);
            using var datagrams = new UdpClient(AddressFamily.InterNetwork);
            var listener6 = new TcpListener(IPAddress.IPv6Loopback, 0);
            using var datagrams6 = new UdpClient(AddressFamily.InterNetworkV6);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                if (address is null)
                    throw new PlatformNotSupportedException("No local IPv4 interface for the admission controls.");
                listener.Start(backlog: 4);
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                // TCP and UDP use independent port namespaces. Let each bind to an available
                // ephemeral port, including on machines with another protocol already using the TCP port.
                datagrams.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                int datagramPort = ((IPEndPoint)datagrams.Client.LocalEndPoint!).Port;
                foreach (var local in new[] { IPAddress.Loopback, address })
                    await VerifyReachableAsync(local, port, datagramPort, datagrams, deadline.Token).ConfigureAwait(false);

                listener6.Start(backlog: 2);
                int port6 = ((IPEndPoint)listener6.LocalEndpoint).Port;
                datagrams6.Client.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
                int datagramPort6 = ((IPEndPoint)datagrams6.Client.LocalEndPoint!).Port;
                await VerifyReachableAsync(IPAddress.IPv6Loopback, port6, datagramPort6, datagrams6, deadline.Token).ConfigureAwait(false);

                string pipeName = "ModernFormsNext.PreviewProbe." + Guid.NewGuid().ToString("N");
                using var desktopPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                using (var positive = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    var accept = desktopPipe.WaitForConnectionAsync(deadline.Token);
                    await positive.ConnectAsync(deadline.Token).ConfigureAwait(false);
                    await accept.ConfigureAwait(false);
                }
                desktopPipe.Disconnect();

                var key = new PreviewKey(Guid.NewGuid(), 1, 0, 0);
                using var process = candidate.Start(key, new(address.ToString(), port, datagramPort, port6, datagramPort6, pipeName));
                using var kill = deadline.Token.Register(process.Kill);
                PreviewPacket result;
                try
                {
                    result = await PreviewWire.ReadAsync(process.Output, key, PreviewMessage.Result, deadline.Token,
                        expectedResultLength: PreviewWire.ProbeResultLength).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    process.ExitedSuccessfully(200);
                    throw new InvalidDataException($"Sandbox policy probe ended before a result (0x{process.ExitStatus:x8}).");
                }
                if (result.Kind != PreviewMessage.Result || result.Payload is not [0, 0, 0, 0, 0] || !process.ExitedSuccessfully(1500))
                    throw new InvalidDataException(result.Payload is [byte step, _, _, _, _]
                        ? $"Sandbox policy probe did not pass (check {step}, code 0x{BinaryPrimitives.ReadInt32LittleEndian(result.Payload.AsSpan(1)):x8})." : "Invalid sandbox probe reply.");
                await PreviewWire.RequireEndOfStreamAsync(process.Output, deadline.Token).ConfigureAwait(false);
            }
            finally
            {
                listener6.Stop();
                listener.Stop();
            }
        }

        private static async Task VerifyReachableAsync(IPAddress address, int tcpPort, int udpPort, UdpClient listener, CancellationToken ct)
        {
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, tcpPort, ct).ConfigureAwait(false);
            using var sender = new UdpClient(address.AddressFamily);
            await sender.SendAsync(new byte[] { 0x50 }, new IPEndPoint(address, udpPort), ct).ConfigureAwait(false);
            if ((await listener.ReceiveAsync(ct).ConfigureAwait(false)).Buffer is not [0x50])
                throw new InvalidDataException("The local UDP positive control failed.");
        }

        public async Task<PreviewOutcome> RenderAsync(PreviewKey key, PreviewRequest request, CancellationToken ct)
        {
            PreviewPhase phase = PreviewPhase.Startup;
            if (launcher is null || !snapshot.HasCurrentSources())
                return new(null, phase, PreviewFault.Unavailable);
            byte[] requestBytes = PreviewWire.Encode(request);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            using var process = launcher.Start(key);
            using var kill = deadline.Token.Register(process.Kill);
            try
            {
                await PreviewWire.ReadAsync(process.Output, key, PreviewMessage.Ready, deadline.Token).ConfigureAwait(false);
                await PreviewWire.WriteAsync(process.Input, key, PreviewMessage.Request, requestBytes, deadline.Token).ConfigureAwait(false);
                process.Input.Dispose(); // Only one request is valid for this worker.
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                while (true)
                {
                    var packet = await PreviewWire.ReadAsync(process.Output, key, PreviewMessage.Result, deadline.Token,
                        expectedResultLength: request.Width * request.Height * 4).ConfigureAwait(false);
                    if (packet.Kind == PreviewMessage.Phase)
                    {
                        var next = (PreviewPhase)packet.Payload[0];
                        if (next <= phase || next > PreviewPhase.Dispose || (next != PreviewPhase.Dispose && (int)next != (int)phase + 1) ||
                            (next == PreviewPhase.Dispose && phase < PreviewPhase.Properties))
                            return new(null, phase, PreviewFault.InvalidReply);
                        phase = next;
                        deadline.CancelAfter(TimeSpan.FromSeconds(3));
                        continue;
                    }
                    deadline.CancelAfter(TimeSpan.FromSeconds(2));
                    if (!process.ExitedSuccessfully(1500))
                        return new(null, phase, PreviewFault.Exited);
                    await PreviewWire.RequireEndOfStreamAsync(process.Output, deadline.Token).ConfigureAwait(false);
                    if (packet.Kind == PreviewMessage.Failure)
                    {
                        var failed = (PreviewPhase)packet.Payload[0];
                        if (failed < PreviewPhase.Load || failed > phase || (failed != phase && phase != PreviewPhase.Dispose))
                            return new(null, phase, PreviewFault.InvalidReply);
                        string? origin = snapshot.ResolveMethod(packet.Payload[1], BinaryPrimitives.ReadInt32LittleEndian(packet.Payload.AsSpan(2)));
                        return new(null, failed, PreviewFault.Exception, origin);
                    }
                    return phase == PreviewPhase.Dispose && PreviewWire.IsRaster(packet.Payload, request)
                        ? new(packet.Payload, phase)
                        : new(null, phase, PreviewFault.InvalidReply);
                }
            }
            catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException)
            {
                ct.ThrowIfCancellationRequested();
                return new(null, phase, deadline.IsCancellationRequested ? PreviewFault.Timeout :
                    error is InvalidDataException ? PreviewFault.InvalidReply : PreviewFault.Exited);
            }
        }

        public void Dispose()
        {
            foreach (var watcher in watchers)
                watcher.Dispose();
            try { launcher?.Dispose(); }
            finally { snapshot.Dispose(); }
        }
    }
}
