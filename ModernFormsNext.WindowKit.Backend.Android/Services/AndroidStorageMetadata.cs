using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

// Provider columns are optional. Zero/negative/invalid modification values mean unknown,
// rather than invented creation dates or a failure that prevents reading a valid document.
internal static class AndroidStorageMetadata
{
    internal static StorageItemProperties Create(long? size, long? modifiedMilliseconds)
    {
        DateTimeOffset? modified = modifiedMilliseconds is > 0 and <= 253402300799999
            ? DateTimeOffset.FromUnixTimeMilliseconds(modifiedMilliseconds.Value) : null;
        return new(size is >= 0 ? (ulong)size.Value : null, null, modified);
    }
}
