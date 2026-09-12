using System.Collections.Immutable;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation.Windows;

/// <summary>Provides authenticated Windows IPC access to an application's existing semantic automation session.</summary>
/// <remarks>
/// Safe to call asynchronously from any thread. Concurrent ordinary calls fail with Busy; no unbounded
/// queue is retained. Cancellation is sent as a separate control frame. A mutation whose response is
/// lost returns OutcomeUnknown through AutomationTransportException and is never retried automatically.
/// Dispose/disconnect ends only this client; the server and its borrowed semantic session remain alive.
/// The caller owns the returned client and should await DisposeAsync. Disconnect is permanent for
/// this object: later requests report SessionEnded. ConnectAsync always returns a new client.
/// Connection loss closes this client. A server-reported OutcomeUnknown may leave it connected;
/// either way, reconcile observed application state and never automatically replay the mutation.
/// </remarks>
public sealed class WindowsAutomationClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream pipe;
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly CancellationTokenSource disconnected = new();
    private long nextId = 1;

    private WindowsAutomationClient(NamedPipeClientStream pipe, AutomationConnectionInfo info)
    { this.pipe = pipe; Info = info; }

    /// <summary>Gets the immutable negotiated connection policy; SessionId scopes handles and is not a credential.</summary>
    public AutomationConnectionInfo Info { get; }

    /// <summary>Connects to a specific live descriptor and authenticates using its separate protected secret file.</summary>
    /// <param name="application">An unambiguous instance returned by AutomationDiscovery.</param>
    /// <param name="capabilities">Requested operations; the server grants their policy intersection.</param>
    /// <param name="cancellationToken">Cancels connection/authentication; both are also bounded by five seconds.</param>
    /// <returns>An authenticated client. No semantic operation is sent before authentication succeeds.</returns>
    /// <exception cref="AutomationTransportException">A safe discovery, connection, authentication, version or Busy error.</exception>
    public static async Task<WindowsAutomationClient> ConnectAsync(AutomationApplicationInfo application,
        AutomationCapability capabilities = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!Protocol.ValidCapabilities(capabilities)) throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        NamedPipeClientStream? stream = null; byte[]? secret = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            if (application.ProtocolVersion != Protocol.Version) throw new AutomationTransportException(AutomationTransportError.ProtocolMismatch);
            secret = AutomationDiscovery.ReadSecret(application);
            stream = new NamedPipeClientStream(".", application.EndpointName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification, HandleInheritability.None);
            await stream.ConnectAsync(deadline.Token).ConfigureAwait(false);
            if (!PipeSecurityPolicy.HasServerProcess(stream, application.ProcessId) || !AutomationDiscovery.IsLive(application))
                throw new AutomationTransportException(AutomationTransportError.AuthenticationFailed);
            byte[] bytes = Protocol.Encode(new
            {
                Version = Protocol.Version, Id = 1L, Kind = RequestKind.Handshake, DeadlineMilliseconds = 5000,
                Payload = new Handshake { InstanceId = application.InstanceId, Secret = Convert.ToBase64String(secret), Capabilities = capabilities }
            }, Protocol.HandshakeLimit);
            try { await Protocol.WriteFrame(stream, bytes, deadline.Token).ConfigureAwait(false); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            var result = await ReadResponse(stream, 1, Protocol.HandshakeLimit, deadline.Token).ConfigureAwait(false);
            var info = Protocol.Read<AutomationConnectionInfo>(result);
            if (info.InstanceId != application.InstanceId || (info.Capabilities & ~capabilities) != 0
                || info.MaxRequestBytes is < 4096 or > 1048576 || info.MaxResponseBytes is < 4096 or > 16777216
                || info.MaxDeadlineMilliseconds is < 1 or > 60000)
                throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
            var client = new WindowsAutomationClient(stream, info); stream = null; return client;
        }
        catch (AutomationTransportException) { throw; }
        catch (OperationCanceledException) { throw new AutomationTransportException(cancellationToken.IsCancellationRequested
            ? AutomationTransportError.Cancelled : AutomationTransportError.DeadlineExceeded); }
        catch (Exception) { throw new AutomationTransportException(AutomationTransportError.ApplicationUnavailable); }
        finally { stream?.Dispose(); if (secret is not null) CryptographicOperations.ZeroMemory(secret); }
    }

    /// <summary>Reads current negotiated session metadata.</summary>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>Detached connection information.</returns>
    public async Task<AutomationConnectionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default)
        => Protocol.Read<AutomationConnectionInfo>(await Call(RequestKind.SessionInfo, new(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lists the application's explicitly registered inspectable roots.</summary>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The canonical detached root result, including completeness diagnostics.</returns>
    public async Task<AutomationResult<ImmutableArray<AutomationRootInfo>>> GetRootsAsync(CancellationToken cancellationToken = default)
        => Protocol.ReadResult<ImmutableArray<AutomationRootInfo>>(await Call(RequestKind.GetRoots, new(), cancellationToken).ConfigureAwait(false));

    /// <summary>Captures a currently reachable node in one exact root.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="handle">The full session/runtime handle.</param>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The canonical detached snapshot result.</returns>
    public async Task<AutomationResult<AutomationNodeSnapshot>> InspectAsync(string rootId, AutomationNodeHandle handle, CancellationToken cancellationToken = default)
        => Protocol.ReadResult<AutomationNodeSnapshot>(await Call(RequestKind.Inspect, new() { RootId = rootId, Handle = handle }, cancellationToken).ConfigureAwait(false));

    /// <summary>Captures direct canonical semantic children.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="handle">The full parent handle.</param>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The immutable canonical child result.</returns>
    public async Task<AutomationResult<ImmutableArray<AutomationNodeSnapshot>>> GetChildrenAsync(string rootId, AutomationNodeHandle handle, CancellationToken cancellationToken = default)
        => Protocol.ReadResult<ImmutableArray<AutomationNodeSnapshot>>(await Call(RequestKind.GetChildren, new() { RootId = rootId, Handle = handle }, cancellationToken).ConfigureAwait(false));

    /// <summary>Resolves exactly one canonical query match without guessing among duplicates.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="query">Existing exact semantic filters.</param>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The canonical unique-match result.</returns>
    public async Task<AutomationResult<AutomationNodeSnapshot>> FindOneAsync(string rootId, AutomationQuery query, CancellationToken cancellationToken = default)
        => Protocol.ReadResult<AutomationNodeSnapshot>(await Call(RequestKind.FindOne, new() { RootId = rootId, Query = query }, cancellationToken).ConfigureAwait(false));

    /// <summary>Queries bounded current canonical matches.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="query">Existing exact semantic filters.</param>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The immutable canonical match result and completeness diagnostics.</returns>
    public async Task<AutomationResult<ImmutableArray<AutomationNodeSnapshot>>> FindAllAsync(string rootId, AutomationQuery query, CancellationToken cancellationToken = default)
        => Protocol.ReadResult<ImmutableArray<AutomationNodeSnapshot>>(await Call(RequestKind.FindAll, new() { RootId = rootId, Query = query }, cancellationToken).ConfigureAwait(false));

    /// <summary>Requests a capability-checked canonical action; Accepted does not mean asynchronous business completion.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="handle">The full target handle.</param>
    /// <param name="action">One advertised canonical action.</param>
    /// <param name="value">Optional bounded text or finite numeric action value; never logged.</param>
    /// <param name="cancellationToken">Cancels work; an ambiguous mutation produces OutcomeUnknown.</param>
    /// <returns>The canonical action acceptance result.</returns>
    /// <exception cref="AutomationTransportException">InvalidRequest for a non-finite number, rejected before sending without closing the connection; other safe transport failures may also occur.</exception>
    public async Task<AutomationActionResult> PerformActionAsync(string rootId, AutomationNodeHandle handle, AccessibleActions action,
        AutomationActionValue? value = null, CancellationToken cancellationToken = default)
        => Protocol.Read<AutomationActionResult>(await Call(RequestKind.PerformAction,
            new() { RootId = rootId, Handle = handle, Action = action, Text = value?.Text, Number = value?.Number,
                Scroll = ScrollOperation.From(value?.Scroll) }, cancellationToken).ConfigureAwait(false));

    /// <summary>Waits on a canonical predicate in the application, using notifications and bounded reconciliation.</summary>
    /// <param name="rootId">The registration scope.</param>
    /// <param name="condition">A supported neutral core predicate.</param>
    /// <param name="options">Bounded monotonic timeout and reconciliation interval.</param>
    /// <param name="cancellationToken">Cancels the live wait and its UI observation.</param>
    /// <returns>The core's detached wait evidence and status.</returns>
    public async Task<AutomationWaitResult> WaitForConditionAsync(string rootId, AutomationWaitCondition condition,
        AutomationWaitOptions? options = null, CancellationToken cancellationToken = default)
        => Protocol.Read<AutomationWaitResult>(await Call(RequestKind.WaitForCondition,
            new() { RootId = rootId, Condition = condition, WaitOptions = options }, cancellationToken).ConfigureAwait(false));

    /// <summary>Awaits a normal-priority dispatcher checkpoint; does not promise global idle or command completion.</summary>
    /// <param name="cancellationToken">Cancels the queued checkpoint.</param>
    /// <returns>The semantic session status at that checkpoint.</returns>
    public async Task<AutomationErrorCode> CheckpointAsync(CancellationToken cancellationToken = default)
        => Protocol.Read<AutomationErrorCode>(await Call(RequestKind.Checkpoint, new(), cancellationToken).ConfigureAwait(false));

    /// <summary>Closes this connection and cancels its outstanding work without stopping the application server.</summary>
    /// <remarks>Repeated calls are safe. This object cannot reconnect; use ConnectAsync to obtain a new client.</remarks>
    /// <returns>A task completed after the local stream closes; server-side wait cleanup follows EOF on its dispatcher.</returns>
    public async Task DisconnectAsync()
    {
        disconnected.Cancel();
        await pipe.DisposeAsync().ConfigureAwait(false);
    }
    /// <summary>Disconnects this client. Repeated calls are safe.</summary>
    public ValueTask DisposeAsync() => new(DisconnectAsync());

    private async Task<JsonElement> Call(RequestKind kind, Operation payload, CancellationToken token)
    {
        if (disconnected.IsCancellationRequested) throw new AutomationTransportException(AutomationTransportError.SessionEnded);
        if (!await operation.WaitAsync(0).ConfigureAwait(false)) throw new AutomationTransportException(AutomationTransportError.Busy);
        bool writeStarted = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(disconnected.Token);
        // The client allows a small bounded cleanup/response grace after the server's deadline.
        deadline.CancelAfter(TimeSpan.FromMilliseconds(Info.MaxDeadlineMilliseconds + 2000));
        byte[]? bytes = null;
        try
        {
            token.ThrowIfCancellationRequested();
            // Protocol JSON has no NaN/infinity representation. Reject these caller values
            // before encoding/writing instead of misclassifying a serializer failure as EOF.
            if (payload.Number is double number && !double.IsFinite(number))
                throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
            long id = Interlocked.Increment(ref nextId);
            bytes = Protocol.Encode(new { Version = Protocol.Version, Id = id, Kind = kind,
                DeadlineMilliseconds = Info.MaxDeadlineMilliseconds, Payload = payload }, Info.MaxRequestBytes);
            // Decode locally for bounded strings/duplicate contract checks before sending any mutation.
            Protocol.DecodeRequest(bytes);
            writeStarted = true;
            await Protocol.WriteFrame(pipe, bytes, deadline.Token).ConfigureAwait(false);
            var response = ReadResponse(pipe, id, Info.MaxResponseBytes, deadline.Token);
            using var cancelSignal = CancellationTokenSource.CreateLinkedTokenSource(token);
            var cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancelSignal.Token);
            if (await Task.WhenAny(response, cancellation).ConfigureAwait(false) == cancellation)
            {
                using var grace = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                grace.CancelAfter(TimeSpan.FromSeconds(2));
                await Protocol.WriteFrame(pipe, Protocol.Encode(new { Version = Protocol.Version, Id = id, Kind = RequestKind.Cancel,
                    DeadlineMilliseconds = 0, Payload = new { } }, Protocol.HandshakeLimit), grace.Token).ConfigureAwait(false);
                try { return await response.WaitAsync(grace.Token).ConfigureAwait(false); }
                catch { await DisconnectAsync().ConfigureAwait(false); throw; }
            }
            cancelSignal.Cancel();
            return await response.ConfigureAwait(false);
        }
        catch (AutomationTransportException error)
        {
            if (writeStarted && !error.IsServerResponse)
            {
                await DisconnectAsync().ConfigureAwait(false);
                if (kind == RequestKind.PerformAction) throw new AutomationTransportException(AutomationTransportError.OutcomeUnknown);
            }
            throw;
        }
        catch (OperationCanceledException)
        {
            bool wasDisconnected = disconnected.IsCancellationRequested;
            if (writeStarted) await DisconnectAsync().ConfigureAwait(false);
            throw new AutomationTransportException(writeStarted && kind == RequestKind.PerformAction ? AutomationTransportError.OutcomeUnknown
                : token.IsCancellationRequested ? AutomationTransportError.Cancelled
                : wasDisconnected ? AutomationTransportError.SessionEnded : AutomationTransportError.DeadlineExceeded);
        }
        catch (Exception)
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw new AutomationTransportException(writeStarted && kind == RequestKind.PerformAction ? AutomationTransportError.OutcomeUnknown
                : AutomationTransportError.ApplicationUnavailable);
        }
        finally { if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); operation.Release(); }
    }

    private static async Task<JsonElement> ReadResponse(Stream stream, long id, int limit, CancellationToken token)
    {
        var bytes = await Protocol.ReadFrame(stream, limit, token).ConfigureAwait(false)
            ?? throw new EndOfStreamException();
        var response = JsonSerializer.Deserialize<Response>(bytes, Protocol.Json)
            ?? throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        if (response.Version != Protocol.Version) throw new AutomationTransportException(AutomationTransportError.ProtocolMismatch);
        if (response.Id != id || !Enum.IsDefined(response.Error)) throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        if (response.Error != AutomationTransportError.None) throw new AutomationTransportException(response.Error, true);
        return response.Result ?? throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
    }
}
