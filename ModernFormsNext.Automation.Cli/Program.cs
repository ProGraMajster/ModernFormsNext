using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Automation;
using ModernFormsNext.Automation.Windows;

return await Cli.Run(args);

// A small nonpackable diagnostic executable. All transport and semantics use the public client.
// No pipe handle, credential, control, reflection dispatch, UIA or input simulation is used here.
internal static class Cli
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly HashSet<string> ValueOptions = ["--instance", "--pid", "--root", "--session", "--node",
        "--automation-id", "--name", "--action", "--condition", "--equals", "--states", "--timeout-ms", "--depth", "--number"];

    internal static async Task<int> Run(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.Ordinal); bool json = args.Contains("--json");
        using var cancelled = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancel;
        void OnCancel(object? sender, ConsoleCancelEventArgs e) { e.Cancel = true; cancelled.Cancel(); }
        try
        {
            if (args.Length == 0 || args[0] == "help")
            {
                Console.WriteLine("mfn-automation list|info|connect|roots|tree|find|inspect|action|wait --instance ID|--pid PID [--root ID] [--json]");
                Console.WriteLine("find: --automation-id ID|--name NAME; inspect/action: --session ID --node ID; action: --action Invoke|SetValue [--value-stdin|--number N]");
                Console.WriteLine("wait: --condition KIND [--automation-id ID|--session ID --node ID] [--equals TEXT|--value-stdin] [--timeout-ms 10000]; tree: --depth 3");
                return args.Length == 0 ? 2 : 0;
            }
            string command = args[0];
            if (command is not ("list" or "connect" or "info" or "roots" or "tree" or "find" or "inspect" or "action" or "wait")) return Error("Usage", 2);
            for (int index = 1; index < args.Length; index++)
            {
                string key = args[index];
                if (options.ContainsKey(key)) return Error("Usage", 2);
                if (key is "--json" or "--value-stdin") options.Add(key, null);
                else if (ValueOptions.Contains(key) && index + 1 < args.Length) options.Add(key, args[++index]);
                else return Error("Usage", 2);
            }
            var applications = await AutomationDiscovery.DiscoverAsync(cancelled.Token);
            if (command == "list")
            {
                if (json) Print(applications);
                else foreach (var app in applications) Console.WriteLine($"{app.ApplicationName}\t{app.ProcessId}\t{app.InstanceId}\t{app.Capabilities}");
                return 0;
            }
            string? instance = Get("--instance"), pidText = Get("--pid");
            if ((instance is null) == (pidText is null)) return Error("InstanceOrPidRequired", 2);
            int pid = 0;
            if (pidText is not null && (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out pid) || pid <= 0)) return Error("Usage", 2);
            var matches = applications.Where(app => instance is not null ? app.InstanceId == instance : app.ProcessId == pid).ToArray();
            if (matches.Length != 1) return Error(matches.Length == 0 ? "ApplicationUnavailable" : "AmbiguousInstance", 4);
            await using var client = await WindowsAutomationClient.ConnectAsync(matches[0], cancellationToken: cancelled.Token);
            if (command is "connect" or "info") { Print(await client.GetSessionInfoAsync(cancelled.Token)); return 0; }
            if (command == "roots") { var roots = await client.GetRootsAsync(cancelled.Token); Print(roots); return SemanticExit(roots.Error); }
            string? rootId = Get("--root");
            if (rootId is null)
            {
                var roots = await client.GetRootsAsync(cancelled.Token);
                if (roots.Error != AutomationErrorCode.None || roots.Value.Length != 1) return Error("RootRequired", 2);
                rootId = roots.Value[0].RootId;
            }
            var query = new AutomationQuery { AutomationId = Get("--automation-id"), Name = Get("--name") };
            if (command == "find") { var found = await client.FindOneAsync(rootId, query, cancelled.Token); Print(found); return SemanticExit(found.Error); }
            if (command == "tree")
            {
                int depthLimit = Integer("--depth", 3, 0, 32);
                var captured = await client.FindAllAsync(rootId, new(), cancelled.Token);
                var depths = new Dictionary<string, int>(StringComparer.Ordinal); var rows = new List<TreeRow>();
                foreach (var node in captured.Value)
                {
                    // This indexes only detached output indentation, never live peers or a
                    // second semantic identity. Core preorder and parent IDs remain authoritative.
                    int depth = node.ParentRuntimeId is { } parent && depths.TryGetValue(parent, out int parentDepth) ? parentDepth + 1 : 0;
                    depths[node.RuntimeId] = depth;
                    if (depth <= depthLimit) rows.Add(new(depth, node));
                }
                if (json) Print(new { captured.Error, captured.Truncated, captured.Issues, Nodes = rows });
                else
                {
                    foreach (var row in rows) Console.WriteLine($"{new string(' ', row.Depth * 2)}{row.Node.RuntimeId} {row.Node.ControlType} {SafeText(row.Node.AutomationId)} {SafeText(row.Node.Name)}");
                    if (captured.Error != AutomationErrorCode.None) Console.WriteLine($"Error: {captured.Error}");
                }
                return SemanticExit(captured.Error);
            }
            AutomationNodeHandle? handle = Get("--node") is { } runtime && Get("--session") is { } session ? new(session, runtime) : null;
            if (command == "inspect")
            {
                if (handle is null) return Error("FullHandleRequired", 2);
                var inspected = await client.InspectAsync(rootId, handle.Value, cancelled.Token); Print(inspected); return SemanticExit(inspected.Error);
            }
            if (command == "action")
            {
                if (handle is null || !Enum.TryParse<AccessibleActions>(Get("--action"), false, out var action)) return Error("Usage", 2);
                if (options.ContainsKey("--value-stdin") && options.ContainsKey("--number")) return Error("Usage", 2);
                AutomationActionValue? value = options.ContainsKey("--value-stdin") ? AutomationActionValue.FromText(await ReadValue(cancelled.Token)) : null;
                if (Get("--number") is { } number)
                {
                    if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeric) || !double.IsFinite(numeric)) return Error("Usage", 2);
                    value = AutomationActionValue.FromNumber(numeric);
                }
                var result = await client.PerformActionAsync(rootId, handle.Value, action, value, cancelled.Token);
                Print(result); return SemanticExit(result.Error);
            }
            if (!Enum.TryParse<AutomationWaitKind>(Get("--condition"), false, out var kind) || !Enum.IsDefined(kind)) return Error("Usage", 2);
            if (options.ContainsKey("--value-stdin") && options.ContainsKey("--equals")) return Error("Usage", 2);
            AccessibleStates states = AccessibleStates.None;
            if (Get("--states") is { } stateText && !Enum.TryParse(stateText, false, out states)) return Error("Usage", 2);
            var condition = new AutomationWaitCondition
            {
                Kind = kind, Handle = handle, Query = kind == AutomationWaitKind.RootEnded || handle is not null ? null : query,
                Value = options.ContainsKey("--value-stdin") ? await ReadValue(cancelled.Token) : Get("--equals"), States = states
            };
            var waited = await client.WaitForConditionAsync(rootId, condition,
                new() { Timeout = TimeSpan.FromMilliseconds(Integer("--timeout-ms", 10000, 0, 60000)) }, cancelled.Token);
            Print(waited);
            return waited.Status == AutomationWaitStatus.Satisfied ? 0 : waited.Status == AutomationWaitStatus.Cancelled ? 130 : 3;
        }
        catch (AutomationTransportException error) { return Error(error.Error.ToString(), error.Error == AutomationTransportError.OutcomeUnknown ? 5 : error.Error == AutomationTransportError.Cancelled ? 130 : 4); }
        catch (OperationCanceledException) { return Error("Cancelled", 130); }
        catch (ArgumentException) { return Error("Usage", 2); }
        catch (Exception) { return Error("ApplicationUnavailable", 4); }
        finally { Console.CancelKeyPress -= OnCancel; }

        string? Get(string key) => options.GetValueOrDefault(key);
        int Integer(string key, int fallback, int minimum, int maximum)
        {
            if (Get(key) is not { } text) return fallback;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < minimum || value > maximum) throw new ArgumentException();
            return value;
        }
        void Print<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
        int Error(string code, int exit) { if (json) Print(new { Error = code }); else Console.Error.WriteLine(code); return exit; }
    }

    private static int SemanticExit(AutomationErrorCode error) => error == AutomationErrorCode.None ? 0 : 3;
    private static string SafeText(string? text) => text is null ? "" : JsonSerializer.Serialize(text);
    private static async Task<string> ReadValue(CancellationToken token)
    {
        char[] buffer = new char[4097]; int length = 0;
        while (length < buffer.Length)
        {
            int count = await Console.In.ReadAsync(buffer.AsMemory(length), token);
            if (count == 0) return new string(buffer, 0, length);
            length += count;
        }
        throw new ArgumentException();
    }
    private sealed record TreeRow(int Depth, AutomationNodeSnapshot Node);
}
