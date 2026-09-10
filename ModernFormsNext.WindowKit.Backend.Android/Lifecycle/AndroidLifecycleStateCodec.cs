using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Text.Json;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

internal static class AndroidLifecycleStateCodec
{
    internal static string Serialize(PlatformApplicationStateData data) => JsonSerializer.Serialize(data.Values);

    internal static PlatformApplicationStateData Deserialize(int version, string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        // JSON escaping can expand each permitted UTF-16 code unit to six characters.
        if (serialized.Length > PlatformApplicationStateData.MaximumTotalLength * 6 + 1024)
            throw new ArgumentException("The saved state exceeds the restoration limit.", nameof(serialized));
        using JsonDocument json = JsonDocument.Parse(serialized);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Saved state must contain an object of strings.", nameof(serialized));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in json.RootElement.EnumerateObject())
        {
            if (values.Count == PlatformApplicationStateData.MaximumEntryCount ||
                property.Value.ValueKind != JsonValueKind.String ||
                !values.TryAdd(property.Name, property.Value.GetString()!))
                throw new ArgumentException("Saved state contains invalid or duplicate entries.", nameof(serialized));
        }
        return new PlatformApplicationStateData(version, values);
    }
}
