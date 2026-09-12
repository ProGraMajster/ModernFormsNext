using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext.Automation.Windows;

internal enum RequestKind { Handshake, SessionInfo, GetRoots, Inspect, GetChildren, FindOne, FindAll, PerformAction, WaitForCondition, Checkpoint, Cancel, Disconnect }
internal sealed record Request(int Version, long Id, RequestKind Kind, int DeadlineMilliseconds, JsonElement Payload);
internal sealed record Response(int Version, long Id, AutomationTransportError Error, JsonElement? Result);
internal sealed class Handshake
{
    public string? InstanceId { get; init; }
    public string? Secret { get; init; }
    public AutomationCapability Capabilities { get; init; }
    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
}
internal sealed class Operation
{
    public string? RootId { get; init; }
    public AutomationNodeHandle? Handle { get; init; }
    public AutomationQuery? Query { get; init; }
    public AccessibleActions Action { get; init; }
    public string? Text { get; init; }
    public double? Number { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScrollOperation? Scroll { get; init; }
    public AutomationWaitCondition? Condition { get; init; }
    public AutomationWaitOptions? WaitOptions { get; init; }
}

internal static partial class Protocol
{
    internal const int Version = 1;
    internal const int HandshakeLimit = 4096;
    internal static readonly JsonSerializerOptions Json = CreateJson();
    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 32,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new SnapshotConverter());
        return options;
    }
    internal static bool ValidCapabilities(AutomationCapability value)
        => (value & ~(AutomationCapability.Inspect | AutomationCapability.Query | AutomationCapability.Actions)) == 0;
    internal static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Json);
    internal static T Read<T>(JsonElement element) => element.Deserialize<T>(Json)
        ?? throw new AutomationTransportException(AutomationTransportError.InvalidRequest);

    internal static async Task<byte[]?> ReadFrame(Stream stream, int limit, CancellationToken token)
    {
        byte[] header = new byte[4];
        int first = await stream.ReadAsync(header.AsMemory(0, 4), token).ConfigureAwait(false);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(first), token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0) throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        if (length > limit) throw new AutomationTransportException(AutomationTransportError.PayloadTooLarge);
        byte[] bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return bytes;
    }

    internal static byte[] Encode<T>(T value, int limit)
    {
        using var stream = new BoundedBuffer(limit);
        JsonSerializer.Serialize(stream, value, Json);
        return stream.ToArray();
    }

    internal static async Task WriteFrame(Stream stream, byte[] bytes, CancellationToken token)
    {
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        await stream.WriteAsync(prefix, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    internal static Request DecodeRequest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
        CheckStrings(document.RootElement);
        var request = Read<Request>(document.RootElement);
        if (request.Id <= 0 || !Enum.IsDefined(request.Kind)) throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
        return request;
    }

    private static void CheckStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && element.GetString()!.Length > 4096)
            throw new AutomationTransportException(AutomationTransportError.PayloadTooLarge);
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new AutomationTransportException(AutomationTransportError.InvalidRequest);
                CheckStrings(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckStrings(item);
    }

    internal static AutomationResult<T> ReadResult<T>(JsonElement element)
    {
        var value = element.GetProperty("value");
        return new(value.ValueKind == JsonValueKind.Null ? default : Read<T>(value),
            Read<AutomationErrorCode>(element.GetProperty("error")), element.GetProperty("captureId").GetString()!,
            element.GetProperty("truncated").GetBoolean(), Read<ImmutableArray<AutomationIssue>>(element.GetProperty("issues")));
    }

    // Capped growth checks before allocation, including serializer flushes. No complete unbounded
    // JSON element is constructed for responses; the core's already-detached DTO streams here.
    private sealed class BoundedBuffer(int limit) : MemoryStream
    {
        private void Reserve(int count)
        {
            if (count > limit - Position) throw new AutomationTransportException(AutomationTransportError.PayloadTooLarge);
            if (Position + count > Capacity) Capacity = Math.Min(limit, Math.Max((int)Position + count, Math.Max(256, Capacity * 2)));
        }
        public override void Write(byte[] buffer, int offset, int count) { Reserve(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Reserve(buffer.Length); base.Write(buffer); }
    }

    private sealed class SnapshotConverter : JsonConverter<AutomationNodeSnapshot>
    {
        public override AutomationNodeSnapshot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader); var e = document.RootElement;
            T Field<T>(string name) => e.GetProperty(name).Deserialize<T>(options)!;
            var range = e.GetProperty("rangeValue");
            AccessibleRangeValue? rangeValue = range.ValueKind == JsonValueKind.Null ? null : new(
                range.GetProperty("value").GetDouble(), range.GetProperty("minimum").GetDouble(), range.GetProperty("maximum").GetDouble(),
                range.GetProperty("smallChange").GetDouble(), range.GetProperty("largeChange").GetDouble(), range.GetProperty("isReadOnly").GetBoolean());
            return new(Field<AutomationNodeHandle>("handle"), Field<string>("rootId"), Field<string?>("automationId"),
                Field<string?>("name"), Field<AccessibleRole>("role"), Field<AccessibleControlType>("controlType"),
                Field<AccessibleStates>("states"), Field<AccessibleActions>("supportedActions"), Field<string?>("value"),
                rangeValue, Field<AutomationBounds>("bounds"), Field<string?>("parentRuntimeId"),
                Field<ImmutableArray<string>>("childRuntimeIds"), Field<AutomationRedaction>("redaction"),
                Field<string>("captureId"), Field<bool>("truncated"), ReadScroll(e),
                e.TryGetProperty("gridInfo", out var grid) ? grid.Deserialize<AutomationGridInfo>(options) : null,
                e.TryGetProperty("gridCell", out var cell) ? cell.Deserialize<AutomationGridCellInfo>(options) : null);
        }

        public override void Write(Utf8JsonWriter writer, AutomationNodeSnapshot value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            void Field<T>(string name, T data) { writer.WritePropertyName(name); JsonSerializer.Serialize(writer, data, options); }
            Field("handle", value.Handle); Field("rootId", value.RootId); Field("automationId", value.AutomationId);
            Field("name", value.Name); Field("role", value.Role); Field("controlType", value.ControlType);
            Field("states", value.States); Field("supportedActions", value.SupportedActions); Field("value", value.Value);
            Field("rangeValue", value.RangeValue); Field("bounds", value.Bounds); Field("parentRuntimeId", value.ParentRuntimeId);
            if (value.ScrollInfo is { } scroll) Field("scrollInfo", scroll);
            if (value.GridInfo is { } grid) Field("gridInfo", grid);
            if (value.GridCell is { } cell) Field("gridCell", cell);
            Field("childRuntimeIds", value.ChildRuntimeIds); Field("redaction", value.Redaction);
            Field("captureId", value.CaptureId); Field("truncated", value.Truncated); writer.WriteEndObject();
        }
    }
}
