using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace ModernFormsNext.Automation.Windows;

/// <summary>Owns an explicitly enabled, local Windows Named Pipe endpoint over a borrowed semantic session.</summary>
/// <remarks>
/// Start only for application-authorized development/testing. No static initializer starts a server.
/// One authenticated client is allowed, with one outstanding semantic operation. Stop/DisposeAsync
/// cancel connections and waits without stopping the borrowed AutomationSession. Await shutdown while
/// its UI dispatcher remains alive; never synchronously block that dispatcher on bridge operations.
/// The current User SID is allowed across elevation levels; this is not an elevation boundary.
/// </remarks>
public sealed class WindowsAutomationServer : IAsyncDisposable
{
    // A weak session-keyed lease prevents duplicate controlling endpoints over the same core.
    // The lease is released only after shutdown cleanup, so two listeners cannot overlap.
    private static readonly ConditionalWeakTable<AutomationSession, WindowsAutomationServer> servers = new();
    private readonly AutomationSession session;
    private readonly WindowsAutomationOptions options;
    private readonly SecurityIdentifier user;
    private readonly byte[] secret = RandomNumberGenerator.GetBytes(32);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentDictionary<int, Task> connections = new();
    private readonly Task listenerTask;
    private readonly object stopLock = new();
    private Task? stopTask;
    private int activeClient;
    private int nextConnection;

    private WindowsAutomationServer(AutomationSession session, WindowsAutomationOptions options)
    {
        this.session = session; this.options = options; user = PipeSecurityPolicy.CurrentUser();
        using var process = Process.GetCurrentProcess(); string nonce = Guid.NewGuid().ToString("N");
        Application = new(nonce, options.ApplicationName, process.Id, AutomationDiscovery.ProcessStart(process),
            AutomationDiscovery.Endpoint(process.Id, nonce), Protocol.Version, options.Capabilities & session.Capabilities, DateTimeOffset.UtcNow);
        var first = PipeSecurityPolicy.Create(Application.EndpointName, user, true);
        try { AutomationDiscovery.Publish(Application, secret, user); }
        catch { first.Dispose(); CryptographicOperations.ZeroMemory(secret); shutdown.Dispose(); throw; }
        listenerTask = Listen(first);
    }

    /// <summary>Gets detached public metadata for this server instance; contains no token.</summary>
    public AutomationApplicationInfo Application { get; }
    /// <summary>Gets whether server shutdown has begun; safe to read on any thread.</summary>
    public bool IsStopped => shutdown.IsCancellationRequested;

    /// <summary>Creates a listener, restrictive discovery files and a fresh cryptographic credential.</summary>
    /// <param name="session">The application's existing initialized semantic session, borrowed until shutdown.</param>
    /// <param name="options">Explicit immutable application policy; null selects bounded defaults.</param>
    /// <returns>The started server, which the application must stop before its dispatcher shuts down.</returns>
    /// <remarks>May be called on any thread after session initialization. A session can have one server; duplicate Start returns Busy until StopAsync finishes. A later Start creates a fresh instance and credential.</remarks>
    /// <exception cref="AutomationTransportException">Secure discovery or endpoint startup failed.</exception>
    /// <example><code>
    /// using var semantic = new AutomationSession();
    /// using var root = semantic.RegisterRoot(form);
    /// await using var server = WindowsAutomationServer.Start(semantic, new() { ApplicationName = "My development app" });
    /// </code></example>
    public static WindowsAutomationServer Start(AutomationSession session, WindowsAutomationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(session); options ??= new(); options.Validate();
        if (session.IsStopped) throw new AutomationTransportException(AutomationTransportError.SessionEnded);
        try
        {
            lock (servers)
            {
                if (servers.TryGetValue(session, out _)) throw new AutomationTransportException(AutomationTransportError.Busy);
                var server = new WindowsAutomationServer(session, options); servers.Add(session, server); return server;
            }
        }
        catch (AutomationTransportException) { throw; }
        catch (Exception) { throw new AutomationTransportException(AutomationTransportError.ApplicationUnavailable); }
    }

    /// <summary>Cancels listeners/requests/waits and removes this instance's descriptor and secret. Repeated calls share completion.</summary>
    /// <returns>A task completed after connection work and canonical wait cleanup finish.</returns>
    public Task StopAsync()
    {
        lock (stopLock) return stopTask ??= StopCore();
    }

    /// <summary>Awaits server shutdown without disposing the borrowed semantic session or application roots.</summary>
    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task StopCore()
    {
        shutdown.Cancel(); AutomationDiscovery.Remove(Application.InstanceId);
        await listenerTask.ConfigureAwait(false);
        await Task.WhenAll(connections.Values).ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(secret);
        lock (servers) servers.Remove(session);
        // Keep the cancelled CTS readable for idempotent IsStopped/StopAsync after disposal.
    }

    private async Task Listen(NamedPipeServerStream first)
    {
        NamedPipeServerStream? listener = first;
        using var slots = new SemaphoreSlim(7, 7);
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                await slots.WaitAsync(shutdown.Token).ConfigureAwait(false);
                try { await listener.WaitForConnectionAsync(shutdown.Token).ConfigureAwait(false); }
                catch { slots.Release(); throw; }
                var connected = listener; listener = null;
                // Keep the namespace occupied by the connected handle while creating the next
                // listener. First-instance protection applies only to initial namespace creation.
                try { listener = PipeSecurityPolicy.Create(Application.EndpointName, user, false); }
                catch { connected.Dispose(); slots.Release(); throw; }
                int id = Interlocked.Increment(ref nextConnection);
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connections[id] = completion.Task;
                _ = ServeTracked(connected, id, completion);
            }
        }
        catch (Exception) { shutdown.Cancel(); }
        finally
        {
            listener?.Dispose();
            await Task.WhenAll(connections.Values).ConfigureAwait(false);
            AutomationDiscovery.Remove(Application.InstanceId);
        }

        async Task ServeTracked(NamedPipeServerStream connected, int id, TaskCompletionSource completion)
        {
            try { await Serve(connected).ConfigureAwait(false); }
            catch (Exception) { /* Connection faults never become unobserved application exceptions. */ }
            finally
            {
                // Release before removing tracking so listener disposal cannot race this semaphore.
                slots.Release(); connections.TryRemove(id, out _); completion.TrySetResult();
            }
        }
    }

    private async Task Serve(NamedPipeServerStream pipe)
    {
        using (pipe)
        using (var disconnected = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
        using (var writeLock = new SemaphoreSlim(1, 1))
        {
            bool ownsClient = false; long lastId = 0, activeRequestId = 0;
            Task? pending = null; CancellationTokenSource? requestCancellation = null;
            int responseLimit = options.MaxResponseBytes, responseStarted = 0;
            async Task Reply(long id, AutomationTransportError error, object? result = null)
            {
                // Serialize the detached result directly into a capped buffer, without a second
                // unbounded JsonElement. Fallback contains only a fixed safe error.
                byte[] bytes;
                try { bytes = Protocol.Encode(new { Version = Protocol.Version, Id = id, Error = error, Result = result }, responseLimit); }
                catch (AutomationTransportException) { bytes = Protocol.Encode(new Response(Protocol.Version, id, AutomationTransportError.PayloadTooLarge, null), responseLimit); }
                using var writeDeadline = CancellationTokenSource.CreateLinkedTokenSource(disconnected.Token);
                writeDeadline.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await writeLock.WaitAsync(writeDeadline.Token).ConfigureAwait(false);
                    try { await Protocol.WriteFrame(pipe, bytes, writeDeadline.Token).ConfigureAwait(false); }
                    finally { writeLock.Release(); }
                }
                catch
                {
                    // A partial frame cannot be repaired by appending another error response.
                    // Close this connection instead; mutating clients retain OutcomeUnknown.
                    disconnected.Cancel(); throw;
                }
            }
            try
            {
                using var handshakeDeadline = CancellationTokenSource.CreateLinkedTokenSource(disconnected.Token);
                handshakeDeadline.CancelAfter(TimeSpan.FromSeconds(5));
                byte[]? bytes = await Protocol.ReadFrame(pipe, Protocol.HandshakeLimit, handshakeDeadline.Token).ConfigureAwait(false);
                if (bytes is null) return;
                var request = Protocol.DecodeRequest(bytes); lastId = request.Id;
                if (request.Version != Protocol.Version) { await Reply(lastId, AutomationTransportError.ProtocolMismatch); return; }
                if (request.Kind != RequestKind.Handshake || !PipeSecurityPolicy.IsSameUser(pipe, user))
                { await Reply(lastId, AutomationTransportError.AuthenticationFailed); return; }
                var auth = Protocol.Read<Handshake>(request.Payload);
                byte[] candidate = new byte[32];
                bool valid = auth.InstanceId == Application.InstanceId && auth.Secret is not null
                    && Convert.TryFromBase64String(auth.Secret, candidate, out int written) && written == 32
                    && CryptographicOperations.FixedTimeEquals(candidate, secret);
                CryptographicOperations.ZeroMemory(candidate);
                if (!valid) { await Reply(lastId, AutomationTransportError.AuthenticationFailed); return; }
                if (!Protocol.ValidCapabilities(auth.Capabilities) || auth.MaxResponseBytes is < 4096 or > 16777216)
                { await Reply(lastId, AutomationTransportError.InvalidRequest); return; }
                if (Interlocked.CompareExchange(ref activeClient, 1, 0) != 0) { await Reply(lastId, AutomationTransportError.Busy); return; }
                ownsClient = true; responseLimit = Math.Min(options.MaxResponseBytes, auth.MaxResponseBytes);
                var info = new AutomationConnectionInfo(Application.InstanceId, session.SessionId, auth.Capabilities & Application.Capabilities,
                    options.MaxRequestBytes, responseLimit, options.MaxDeadlineMilliseconds);
                await Reply(lastId, AutomationTransportError.None, info).ConfigureAwait(false);
                while (!disconnected.IsCancellationRequested)
                {
                    bytes = await Protocol.ReadFrame(pipe, options.MaxRequestBytes, disconnected.Token).ConfigureAwait(false);
                    if (bytes is null) break;
                    request = Protocol.DecodeRequest(bytes);
                    if (request.Version != Protocol.Version) { await Reply(request.Id, AutomationTransportError.ProtocolMismatch); break; }
                    if (request.Kind == RequestKind.Cancel)
                    {
                        if (request.Id == activeRequestId) requestCancellation?.Cancel();
                        continue;
                    }
                    if (request.Id <= lastId) { await Reply(request.Id, AutomationTransportError.InvalidRequest); break; }
                    lastId = request.Id;
                    if (request.Kind == RequestKind.Disconnect) break;
                    if (pending is { IsCompleted: false })
                    {
                        if (Volatile.Read(ref responseStarted) == 0) { await Reply(request.Id, AutomationTransportError.Busy); continue; }
                        // The peer can consume the reply before the writer's async continuation
                        // completes. Finish that bounded write before admitting its next request;
                        // semantic work is already done, so this is not a second operation slot.
                        await pending.ConfigureAwait(false);
                    }
                    if (request.DeadlineMilliseconds < 1 || request.DeadlineMilliseconds > options.MaxDeadlineMilliseconds)
                    { await Reply(lastId, AutomationTransportError.InvalidRequest); continue; }
                    requestCancellation?.Dispose();
                    requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(disconnected.Token);
                    activeRequestId = request.Id;
                    Volatile.Write(ref responseStarted, 0);
                    pending = Execute(request, info, requestCancellation.Token);
                }

                async Task Execute(Request operation, AutomationConnectionInfo connection, CancellationToken manualCancellation)
                {
                    using var requestDeadline = new CancellationTokenSource(operation.DeadlineMilliseconds);
                    using var combined = CancellationTokenSource.CreateLinkedTokenSource(manualCancellation, requestDeadline.Token);
                    var token = combined.Token;
                    bool Expired() => requestDeadline.IsCancellationRequested;
                    Task Outcome(AutomationTransportError error, object? value = null)
                    {
                        Volatile.Write(ref responseStarted, 1);
                        return Reply(operation.Id, error, value);
                    }
                    try
                    {
                        object result = await Dispatch(operation, connection, token).ConfigureAwait(false);
                        if (result is AutomationWaitResult { Status: AutomationWaitStatus.Cancelled } && Expired())
                            await Outcome(AutomationTransportError.DeadlineExceeded).ConfigureAwait(false);
                        else await Outcome(AutomationTransportError.None, result).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // A cancelled action response cannot certify whether canonical mutation was
                        // already entered. Conservatively preserve ambiguity at this boundary.
                        await Outcome(operation.Kind == RequestKind.PerformAction ? AutomationTransportError.OutcomeUnknown
                            : disconnected.IsCancellationRequested ? AutomationTransportError.SessionEnded
                            : Expired() ? AutomationTransportError.DeadlineExceeded : AutomationTransportError.Cancelled).ConfigureAwait(false);
                    }
                    catch (AutomationTransportException error) { await Outcome(error.Error).ConfigureAwait(false); }
                    catch (Exception) { await Outcome(AutomationTransportError.ApplicationError).ConfigureAwait(false); }
                }
            }
            // An invalid frame has no validated correlation ID; zero explicitly denotes an
            // uncorrelated terminal rejection instead of misattributing it to the prior request.
            catch (AutomationTransportException error) { try { await Reply(0, error.Error).ConfigureAwait(false); } catch (Exception) { } }
            catch (JsonException) { try { await Reply(0, AutomationTransportError.InvalidRequest).ConfigureAwait(false); } catch (Exception) { } }
            catch (Exception) { }
            finally
            {
                disconnected.Cancel();
                if (pending is not null) { try { await pending.ConfigureAwait(false); } catch (Exception) { } }
                requestCancellation?.Dispose();
                if (ownsClient) Interlocked.Exchange(ref activeClient, 0);
            }
        }
    }

    private async Task<object> Dispatch(Request request, AutomationConnectionInfo info, CancellationToken token)
    {
        if (session.IsStopped) throw new AutomationTransportException(AutomationTransportError.SessionEnded);
        var required = request.Kind switch
        {
            RequestKind.GetRoots or RequestKind.Inspect => AutomationCapability.Inspect,
            RequestKind.GetChildren or RequestKind.FindOne or RequestKind.FindAll => AutomationCapability.Query,
            RequestKind.PerformAction => AutomationCapability.Actions,
            _ => (AutomationCapability)0
        };
        var payload = Protocol.Read<Operation>(request.Payload);
        if (request.Kind == RequestKind.WaitForCondition)
            required = payload.Condition?.Query is not null ? AutomationCapability.Query : AutomationCapability.Inspect;
        if ((info.Capabilities & required) != required) throw new AutomationTransportException(AutomationTransportError.CapabilityDenied);
        if (request.Kind is not (RequestKind.SessionInfo or RequestKind.GetRoots or RequestKind.Checkpoint) && payload.RootId is null)
            throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        return request.Kind switch
        {
            RequestKind.SessionInfo => info,
            RequestKind.GetRoots => await session.GetRootsAsync(token).ConfigureAwait(false),
            RequestKind.Checkpoint => await session.CheckpointAsync(token).ConfigureAwait(false),
            RequestKind.Inspect when payload.Handle is { } handle => await session.InspectAsync(payload.RootId!, handle, token).ConfigureAwait(false),
            RequestKind.GetChildren when payload.Handle is { } handle => await session.GetChildrenAsync(payload.RootId!, handle, token).ConfigureAwait(false),
            RequestKind.FindOne when payload.Query is { } query => await session.FindOneAsync(payload.RootId!, query, token).ConfigureAwait(false),
            RequestKind.FindAll when payload.Query is { } query => await session.FindAllAsync(payload.RootId!, query, token).ConfigureAwait(false),
            RequestKind.PerformAction when payload.Handle is { } handle && !(payload.Text is not null && payload.Number is not null)
                && (payload.Scroll is null || payload.Text is null && payload.Number is null)
                => await session.PerformActionAsync(payload.RootId!, handle, payload.Action,
                    payload.Text is not null ? AutomationActionValue.FromText(payload.Text)
                    : payload.Number is { } number ? AutomationActionValue.FromNumber(number)
                    : payload.Scroll is { } scroll ? AutomationActionValue.FromScroll(scroll.ToRequest()) : null, token).ConfigureAwait(false),
            RequestKind.WaitForCondition when payload.Condition is { } condition
                => await session.WaitForConditionAsync(payload.RootId!, condition, payload.WaitOptions, token).ConfigureAwait(false),
            _ => throw new AutomationTransportException(AutomationTransportError.InvalidRequest)
        };
    }
}
