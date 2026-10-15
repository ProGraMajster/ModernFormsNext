using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

// Dispatcher-owned, independently testable native-identity lifetime. The timer in
// ShellBalloonNotificationBackend reconciles missing Shell callbacks; it does not
// assume Accepted means a balloon was shown or that a timeout event will arrive.
internal sealed class ShellNotificationLeases<T>(Action<T> release, TimeProvider? clock = null,
    int capacity = 32, TimeSpan? lifetime = null) : IDisposable where T : class
{
    private readonly Dictionary<SystemNotificationKey, (T Value, long Started)> entries = [];
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly TimeSpan lifetime = lifetime ?? TimeSpan.FromMinutes(2);
    internal int Count => entries.Count;
    internal SystemNotificationKey[] Keys => entries.Keys.ToArray();
    internal bool IsCurrent(SystemNotificationKey key, T value)
        => entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Value, value);
    internal bool CanAllocate(SystemNotificationKey key)
    {
        Expire();
        return entries.ContainsKey(key) || entries.Count < capacity;
    }
    internal void Replace(SystemNotificationKey key, T value)
    {
        if (!CanAllocate(key)) throw new InvalidOperationException("Shell identity capacity must be checked before allocation.");
        Remove(key);
        entries.Add(key, (value, clock.GetTimestamp()));
    }
    internal bool Remove(SystemNotificationKey key, T? expected = null)
    {
        if (!entries.TryGetValue(key, out var entry) || expected is not null && !ReferenceEquals(entry.Value, expected)) return false;
        entries.Remove(key);
        release(entry.Value);
        return true;
    }
    internal void Expire()
    {
        var now = clock.GetTimestamp();
        foreach (var pair in entries.ToArray())
            if (clock.GetElapsedTime(pair.Value.Started, now) >= lifetime) Remove(pair.Key, pair.Value.Value);
    }
    public void Dispose()
    {
        foreach (var key in Keys) Remove(key);
    }
}
