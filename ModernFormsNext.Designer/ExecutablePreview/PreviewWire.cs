using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernFormsNext.Designer.ExecutablePreview;

// Linked into the worker. Only fixed-format records and raw BGRA cross the process boundary.
internal static class PreviewWire
{
    internal const int MaxEdge = 1024;
    internal const int MaxRaster = MaxEdge * MaxEdge * 4;
    internal const int MaxRequest = 64 * 1024;
    private const int HeaderSize = 64;
    private const uint Magic = 0x3150464d; // MFP1
    private static readonly JsonSerializerOptions Json = new()
    {
        MaxDepth = 4,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static async ValueTask WriteAsync(Stream stream, PreviewKey key, PreviewMessage kind, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsLengthAllowed(kind, payload.Length))
            throw new InvalidDataException("Invalid preview message size.");
        byte[] buffer = new byte[HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
        buffer[4] = (byte)kind;
        key.Session.TryWriteBytes(buffer.AsSpan(8, 16));
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(24), key.Request);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(32), key.Revision);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(40), key.Node);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(44), payload.Length);
        // Approval changes always create a new session. The request additionally carries its full digest.
        await stream.WriteAsync(buffer, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    internal static async ValueTask<PreviewPacket> ReadAsync(Stream stream, PreviewKey expected, PreviewMessage expectedKind, CancellationToken ct)
    {
        byte[] buffer = new byte[HeaderSize];
        await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != Magic ||
            buffer[5] != 0 || buffer[6] != 0 || buffer[7] != 0 ||
            buffer.AsSpan(48).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Invalid preview protocol header.");
        var key = new PreviewKey(new Guid(buffer.AsSpan(8, 16)), BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(24)),
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(32)), BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(40)));
        var kind = (PreviewMessage)buffer[4];
        int length = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(44));
        bool expectedResponse = expectedKind == PreviewMessage.Result &&
            kind is PreviewMessage.Phase or PreviewMessage.Result or PreviewMessage.Failure;
        if (key != expected || !IsLengthAllowed(kind, length) || (!expectedResponse && kind != expectedKind))
            throw new InvalidDataException("Unexpected preview message.");
        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return new PreviewPacket(kind, payload);
    }

    private static bool IsLengthAllowed(PreviewMessage kind, int length) => kind switch
    {
        PreviewMessage.Ready => length == 0,
        PreviewMessage.Request => length is > 0 and <= MaxRequest,
        PreviewMessage.Phase => length == 1,
        PreviewMessage.Failure => length == 6,
        PreviewMessage.Result => length is > 0 and <= MaxRaster,
        _ => false
    };

    internal static byte[] Encode(PreviewRequest request)
    {
        Validate(request);
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(request, Json);
        if (data.Length > MaxRequest)
            throw new InvalidDataException("Preview request exceeds its limit.");
        return data;
    }

    internal static PreviewRequest Decode(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > MaxRequest)
            throw new InvalidDataException("Invalid preview request size.");
        var request = JsonSerializer.Deserialize<PreviewRequest>(bytes, Json);
        if (request is null)
            throw new InvalidDataException("Preview request is missing.");
        Validate(request);
        return request;
    }

    private static void Validate(PreviewRequest value)
    {
        if (!IsIdentifier(value.Type, qualified: true) || value.Digest is not { Length: 64 } ||
            !value.Digest.All(Uri.IsHexDigit) ||
            value.Width is < 1 or > MaxEdge || value.Height is < 1 or > MaxEdge ||
            !double.IsFinite(value.Scale) || value.Scale is < 0.25 or > 8 ||
            value.Properties is null || value.Properties.Length > 32)
            throw new InvalidDataException("Unsupported preview dimensions, type or properties.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.Properties)
        {
            if (property is null || !IsIdentifier(property.Name) || !names.Add(property.Name) ||
                property.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String) ||
                (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length > 1024) ||
                (property.Value.ValueKind == JsonValueKind.Number && (!property.Value.TryGetDouble(out double number) || !double.IsFinite(number))))
                throw new InvalidDataException("Unsupported preview property.");
        }
    }

    internal static bool IsIdentifier(string? name, bool qualified = false)
    {
        if (name is not { Length: > 0 and <= 240 })
            return false;
        bool first = true;
        foreach (char character in name)
        {
            if (qualified && character is '.' or '+')
            {
                if (first)
                    return false;
                first = true;
                continue;
            }
            if (character != '_' && !char.IsLetter(character) && (first || !char.IsDigit(character)))
                return false;
            first = false;
        }
        return !first;
    }

    internal static bool IsRaster(byte[] bytes, PreviewRequest request)
    {
        if (bytes.Length != request.Width * request.Height * 4)
            return false;
        // Skia expects premultiplied BGRA. Reject malformed pixels instead of passing them to native drawing.
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            byte alpha = bytes[offset + 3];
            if (bytes[offset] > alpha || bytes[offset + 1] > alpha || bytes[offset + 2] > alpha)
                return false;
        }
        return true;
    }
}

internal enum PreviewMessage : byte { Ready = 1, Request, Phase, Result, Failure }
internal enum PreviewPhase : byte { Startup, Load, Construct, Properties, Layout, Paint, Dispose }
internal enum PreviewFault : byte { None, Unavailable, Exception, Timeout, Exited, InvalidReply, Unsupported }
internal readonly record struct PreviewKey(Guid Session, long Request, long Revision, int Node);
internal readonly record struct PreviewPacket(PreviewMessage Kind, byte[] Payload);
internal sealed record PreviewRequest(string Digest, string Type, int Width, int Height, double Scale, PreviewProperty[] Properties);
internal sealed record PreviewProperty(string Name, JsonElement Value);
internal sealed record PreviewOutcome(byte[]? Pixels, PreviewPhase Phase, PreviewFault Fault = PreviewFault.None, string? Source = null);
