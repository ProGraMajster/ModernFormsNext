using System.Collections;

namespace ModernFormsNext.Notifications;

/// <summary>Base for typed platform extensions. The common assembly never references their platform assemblies.</summary>
/// <remarks>Derived records must be immutable or override Snapshot to copy all mutable owned state.
/// Native borrowed resources retain their explicitly documented caller ownership.</remarks>
public abstract record SystemNotificationPlatformOptions
{
    /// <summary>Returns an immutable snapshot. Override when the derived record owns mutable collections.</summary>
    public virtual SystemNotificationPlatformOptions Snapshot() => this;
}

/// <summary>An immutable collection of platform settings, keyed by exact concrete type.</summary>
/// <remarks>Multiple platforms can coexist. Adding a new option type does not change the common content contract.
/// Unknown types are preserved by snapshots and ignored by unrelated providers.</remarks>
public sealed class SystemNotificationOptions : IReadOnlyCollection<SystemNotificationPlatformOptions>
{
    private readonly SystemNotificationPlatformOptions[] items;
    /// <summary>Gets the shared empty collection.</summary>
    public static SystemNotificationOptions Empty { get; } = new();
    /// <summary>Copies and snapshots settings; duplicate concrete types and null items are rejected.</summary>
    public SystemNotificationOptions(params SystemNotificationPlatformOptions[] options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Length > 32) throw new ArgumentException("At most 32 typed notification extensions are allowed.", nameof(options));
        var types = new HashSet<Type>();
        items = options.Select(option =>
        {
            ArgumentNullException.ThrowIfNull(option);
            var snapshot = option.Snapshot();
            if (snapshot is null || snapshot.GetType() != option.GetType() || !types.Add(snapshot.GetType()))
                throw new ArgumentException("Notification options must have unique exact types and type-preserving snapshots.", nameof(options));
            return snapshot;
        }).ToArray();
    }
    /// <inheritdoc/>
    public int Count => items.Length;
    /// <summary>Gets the exact requested type, or null if this content has no settings for that type.</summary>
    public T? Get<T>() where T : SystemNotificationPlatformOptions => (T?)items.FirstOrDefault(item => item.GetType() == typeof(T));
    /// <summary>Creates a collection replacing the same concrete type, preserving settings for all other platforms.</summary>
    public SystemNotificationOptions With<T>(T option) where T : SystemNotificationPlatformOptions
    {
        ArgumentNullException.ThrowIfNull(option);
        return new(items.Where(item => item.GetType() != option.GetType()).Append(option).ToArray());
    }
    /// <summary>Copies all extension-owned state according to each extension's snapshot contract.</summary>
    public SystemNotificationOptions Snapshot() => Count == 0 ? Empty : new(items);
    /// <inheritdoc/>
    public IEnumerator<SystemNotificationPlatformOptions> GetEnumerator() => ((IEnumerable<SystemNotificationPlatformOptions>)items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
