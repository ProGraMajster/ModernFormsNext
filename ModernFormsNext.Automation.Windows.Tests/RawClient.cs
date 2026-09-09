using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace ModernFormsNext.Automation.Windows.Tests;

// Test-only raw wire access exercises failures that the public client deliberately cannot create.
internal sealed class RawClient : IAsyncDisposable
{
    internal NamedPipeClientStream Pipe { get; }
    internal List<string> Responses { get; } = [];
    private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
    internal CancellationToken Token => deadline.Token;
    private RawClient(string endpoint) => Pipe = new(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous,
        TokenImpersonationLevel.Identification, HandleInheritability.None);
    internal static async Task<RawClient> Connect(AutomationApplicationInfo application)
    {
        var client = new RawClient(application.EndpointName);
        try { await client.Pipe.ConnectAsync(client.Token); return client; }
        catch { await client.DisposeAsync(); throw; }
    }
    internal Task<Response> Handshake(AutomationApplicationInfo application, string? secret = null, bool omitSecret = false,
        int version = 1, string? instance = null, AutomationCapability capabilities = AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions)
        => Request(1, RequestKind.Handshake, new Handshake
        {
            InstanceId = instance ?? application.InstanceId, Secret = omitSecret ? null : secret ?? Convert.ToBase64String(AutomationDiscovery.ReadSecret(application)),
            Capabilities = capabilities
        }, version);

    internal async Task<Response> Request(long id, RequestKind kind, object payload, int version = 1, int milliseconds = 60000)
    {
        await Send(id, kind, payload, version, milliseconds); return await Read();
    }
    internal Task Send(long id, RequestKind kind, object payload, int version = 1, int milliseconds = 60000)
        => Protocol.WriteFrame(Pipe, Protocol.Encode(new { Version = version, Id = id, Kind = kind, DeadlineMilliseconds = milliseconds, Payload = payload }, 1048576), Token);
    internal async Task<Response> Read()
    {
        byte[] bytes = await Protocol.ReadFrame(Pipe, 16777216, Token) ?? throw new EndOfStreamException();
        Responses.Add(System.Text.Encoding.UTF8.GetString(bytes));
        return JsonSerializer.Deserialize<Response>(bytes, Protocol.Json)!;
    }
    public async ValueTask DisposeAsync() { deadline.Cancel(); await Pipe.DisposeAsync(); deadline.Dispose(); }
}
