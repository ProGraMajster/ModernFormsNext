using System.Collections.ObjectModel;

namespace ModernFormsNext.WindowKit.Backend.Lifecycle;

/// <summary>Identifies why the application is asked to save or restore its own state.</summary>
public enum PlatformApplicationStateReason
{
    /// <summary>The platform is suspending or resuming application work.</summary>
    Suspend,
    /// <summary>A native host is being recreated while the application identity is preserved.</summary>
    Recreation,
    /// <summary>The application is shutting down or restoring a prior session.</summary>
    Exit
}

/// <summary>Contains a versioned, bounded immutable map of application-owned primitive string data.</summary>
/// <remarks>
/// This is an opt-in persistence handoff, not a serializer or storage service. Applications choose
/// their keys and convert other primitive values using their own versioned format. Control trees,
/// arbitrary objects, native handles and platform lifecycle objects cannot be stored here.
/// </remarks>
public sealed class PlatformApplicationStateData
{
    /// <summary>Gets the maximum number of state entries.</summary>
    public const int MaximumEntryCount = 64;
    /// <summary>Gets the maximum UTF-16 character count of a key.</summary>
    public const int MaximumKeyLength = 128;
    /// <summary>Gets the maximum UTF-16 character count of a value.</summary>
    public const int MaximumValueLength = 4096;
    /// <summary>Gets the maximum combined UTF-16 character count of keys and values.</summary>
    public const int MaximumTotalLength = 65536;

    /// <summary>Creates a state handoff with a positive application format version and copied values.</summary>
    /// <param name="version">A positive application-defined schema version.</param>
    /// <param name="values">Optional ordinal keys and string values copied by this instance.</param>
    /// <exception cref="ArgumentException">A key/value is invalid, duplicated, or exceeds a bound.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public PlatformApplicationStateData(int version, IReadOnlyDictionary<string, string>? values = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalLength = 0;
        if (values is not null)
        {
            foreach ((string key, string value) in values)
            {
                if (copy.Count == MaximumEntryCount || string.IsNullOrWhiteSpace(key) ||
                    key.Length > MaximumKeyLength || value is null || value.Length > MaximumValueLength ||
                    key.Length + value.Length > MaximumTotalLength - totalLength)
                    throw new ArgumentException("The application state map contains invalid or oversized data.", nameof(values));
                if (!copy.TryAdd(key, value))
                    throw new ArgumentException("State keys must be unique using ordinal comparison.", nameof(values));
                totalLength += key.Length + value.Length;
            }
        }
        Version = version;
        Values = new ReadOnlyDictionary<string, string>(copy);
    }

    /// <summary>Gets the application-defined schema version.</summary>
    public int Version { get; }
    /// <summary>Gets the immutable ordinal map of state values.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }
}
