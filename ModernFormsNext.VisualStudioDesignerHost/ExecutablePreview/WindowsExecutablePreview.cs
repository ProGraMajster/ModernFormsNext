using System.Buffers.Binary;
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
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                int port = 0;
                if (address is not null)
                {
                    listener.Start(backlog: 4);
                    port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    datagrams.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                    foreach (var local in new[] { IPAddress.Loopback, address })
                    {
                        using var client = new TcpClient(AddressFamily.InterNetwork);
                        var endpoint = new IPEndPoint(local, port);
                        await client.ConnectAsync(endpoint, deadline.Token).ConfigureAwait(false);
                        using var sender = new UdpClient(AddressFamily.InterNetwork);
                        await sender.SendAsync(new byte[] { 0x50 }, endpoint, deadline.Token).ConfigureAwait(false);
                        if ((await datagrams.ReceiveAsync(deadline.Token).ConfigureAwait(false)).Buffer is not [0x50])
                            throw new InvalidDataException("The local UDP positive control failed.");
                    }
                }
                else
                {
                    // Without the positive control this host cannot qualify the no-network policy.
                    throw new PlatformNotSupportedException();
                }
                var key = new PreviewKey(Guid.NewGuid(), 1, 0, 0);
                using var process = candidate.Start(key, address.ToString(), port);
                using var kill = deadline.Token.Register(process.Kill);
                PreviewPacket result;
                try { result = await PreviewWire.ReadAsync(process.Output, key, PreviewMessage.Result, deadline.Token).ConfigureAwait(false); }
                catch (EndOfStreamException)
                {
                    process.ExitedSuccessfully(200);
                    throw new InvalidDataException($"Sandbox policy probe ended before a result (0x{process.ExitStatus:x8}).");
                }
                if (result.Kind != PreviewMessage.Result || result.Payload is not [0] || !process.ExitedSuccessfully(1500))
                    throw new InvalidDataException(result.Payload is [byte step]
                        ? $"Sandbox policy probe did not pass (check {step})." : "Invalid sandbox probe reply.");
            }
            finally { listener.Stop(); }
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
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                while (true)
                {
                    var packet = await PreviewWire.ReadAsync(process.Output, key, PreviewMessage.Result, deadline.Token).ConfigureAwait(false);
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
