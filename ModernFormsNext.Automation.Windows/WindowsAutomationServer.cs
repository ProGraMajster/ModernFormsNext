using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
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
    /// <remarks>May be called on any thread after session initialization. Every call creates a distinct instance, never restarts an ended server.</remarks>
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
        try { return new(session, options); }
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
                var task = Serve(connected); connections[id] = task;
                _ = task.ContinueWith(_ => { connections.TryRemove(id, out var ignored); slots.Release(); },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception) { shutdown.Cancel(); }
        finally
        {
            listener?.Dispose();
            await Task.WhenAll(connections.Values).ConfigureAwait(false);
            AutomationDiscovery.Remove(Application.InstanceId);
        }
    }

    private async Task Serve(NamedPipeServerStream pipe)
    {
        using (pipe)
        using (var disconnected = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
        using (var writeLock = new SemaphoreSlim(1, 1))
        {
            bool ownsClient = false; long lastId = 0;
            Task? pending = null; CancellationTokenSource? requestCancellation = null;
            int responseLimit = options.MaxResponseBytes;
            async Task Reply(long id, AutomationTransportError error, object? result = null)
            {
                // Serialize the detached result directly into a capped buffer, without a second
                // unbounded JsonElement. Fallback contains only a fixed safe error.
                byte[] bytes;
                try { bytes = Protocol.Encode(new { Version = Protocol.Version, Id = id, Error = error, Result = result }, responseLimit); }
                catch (AutomationTransportException) { bytes = Protocol.Encode(new Response(Protocol.Version, id, AutomationTransportError.PayloadTooLarge, null), responseLimit); }
                await writeLock.WaitAsync(disconnected.Token).ConfigureAwait(false);
                try { await Protocol.WriteFrame(pipe, bytes, disconnected.Token).ConfigureAwait(false); }
                finally { writeLock.Release(); }
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
                        if (request.Id == lastId) requestCancellation?.Cancel();
                        continue;
                    }
                    if (request.Id <= lastId) { await Reply(request.Id, AutomationTransportError.InvalidRequest); break; }
                    if (request.Kind == RequestKind.Disconnect) break;
                    if (pending is { IsCompleted: false }) { await Reply(request.Id, AutomationTransportError.Busy); continue; }
                    lastId = request.Id;
                    if (request.DeadlineMilliseconds < 1 || request.DeadlineMilliseconds > options.MaxDeadlineMilliseconds)
                    { await Reply(lastId, AutomationTransportError.InvalidRequest); continue; }
                    requestCancellation?.Dispose();
                    requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(disconnected.Token);
                    requestCancellation.CancelAfter(request.DeadlineMilliseconds);
                    pending = Execute(request, info, requestCancellation.Token);
                }

                async Task Execute(Request operation, AutomationConnectionInfo connection, CancellationToken token)
                {
                    try { await Reply(operation.Id, AutomationTransportError.None, await Dispatch(operation, connection, token).ConfigureAwait(false)).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // A cancelled action response cannot certify whether canonical mutation was
                        // already entered. Conservatively preserve ambiguity at this boundary.
                        await Reply(operation.Id, operation.Kind == RequestKind.PerformAction ? AutomationTransportError.OutcomeUnknown
                            : disconnected.IsCancellationRequested ? AutomationTransportError.SessionEnded : AutomationTransportError.Cancelled).ConfigureAwait(false);
                    }
                    catch (AutomationTransportException error) { await Reply(operation.Id, error.Error).ConfigureAwait(false); }
                    catch (Exception) { await Reply(operation.Id, AutomationTransportError.ApplicationError).ConfigureAwait(false); }
                }
            }
            catch (AutomationTransportException error) { try { await Reply(lastId, error.Error).ConfigureAwait(false); } catch (Exception) { } }
            catch (JsonException) { try { await Reply(lastId, AutomationTransportError.InvalidRequest).ConfigureAwait(false); } catch (Exception) { } }
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
                => await session.PerformActionAsync(payload.RootId!, handle, payload.Action,
                    payload.Text is not null ? AutomationActionValue.FromText(payload.Text)
                    : payload.Number is { } number ? AutomationActionValue.FromNumber(number) : null, token).ConfigureAwait(false),
            RequestKind.WaitForCondition when payload.Condition is { } condition
                => await session.WaitForConditionAsync(payload.RootId!, condition, payload.WaitOptions, token).ConfigureAwait(false),
            _ => throw new AutomationTransportException(AutomationTransportError.InvalidRequest)
        };
    }
}
