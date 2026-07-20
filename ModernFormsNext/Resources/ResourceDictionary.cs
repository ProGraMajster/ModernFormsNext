using System.Collections;

namespace ModernFormsNext;

/// <summary>
/// Stores keyed application, window, or control resources and reports changes at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Resource values can be of any type, including colors, brushes, fonts, spacing values, styles,
/// animation settings, and localized strings. Keys must be non-null and should have stable equality
/// and hash-code behavior while they are stored.
/// </para>
/// <para>
/// A dictionary is safe against concurrent collection corruption, but changing a dictionary that
/// is used by live controls applies CLR property setters on the calling thread. Applications must
/// therefore update live UI resources on the owning UI/dispatcher thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;
/// </code>
/// </example>
public sealed class ResourceDictionary : IDictionary<object, object?>, IReadOnlyDictionary<object, object?>
{
    private readonly object syncRoot = new();
    private readonly Dictionary<object, object?> entries = [];

    /// <summary>
    /// Occurs after a resource is added, replaced, removed, or cleared.
    /// </summary>
    /// <remarks>
    /// The event is raised on the thread that performed the mutation. Dynamic control references
    /// are updated before this event is raised.
    /// </remarks>
    public event EventHandler<ResourceChangedEventArgs>? ResourceChanged;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The non-null resource key.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown by the getter when the key is absent.</exception>
    public object? this[object key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            lock (syncRoot)
                return entries[key];
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);

            object? oldValue;
            ResourceChangeKind changeKind;
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out oldValue))
                {
                    if (Equals(oldValue, value))
                        return;

                    entries[key] = value;
                    changeKind = ResourceChangeKind.Replaced;
                }
                else
                {
                    entries.Add(key, value);
                    changeKind = ResourceChangeKind.Added;
                }
            }

            RaiseResourceChanged(key, oldValue, value, changeKind);
        }
    }

    /// <summary>
    /// Gets a snapshot of the keys currently stored in the dictionary.
    /// </summary>
    public ICollection<object> Keys
    {
        get
        {
            lock (syncRoot)
                return entries.Keys.ToArray();
        }
    }

    /// <summary>
    /// Gets a snapshot of the values currently stored in the dictionary.
    /// </summary>
    public ICollection<object?> Values
    {
        get
        {
            lock (syncRoot)
                return entries.Values.ToArray();
        }
    }

    IEnumerable<object> IReadOnlyDictionary<object, object?>.Keys => Keys;

    IEnumerable<object?> IReadOnlyDictionary<object, object?>.Values => Values;

    /// <summary>
    /// Gets the number of resources in the dictionary.
    /// </summary>
    public int Count
    {
        get
        {
            lock (syncRoot)
                return entries.Count;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the dictionary is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds a resource with the specified key.
    /// </summary>
    /// <param name="key">The non-null resource key.</param>
    /// <param name="value">The resource value, which may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the key already exists.</exception>
    public void Add(object key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
            entries.Add(key, value);

        RaiseResourceChanged(key, null, value, ResourceChangeKind.Added);
    }

    /// <summary>
    /// Adds the specified resource entry.
    /// </summary>
    /// <param name="item">The resource entry to add.</param>
    public void Add(KeyValuePair<object, object?> item) => Add(item.Key, item.Value);

    /// <summary>
    /// Removes every resource from the dictionary.
    /// </summary>
    public void Clear()
    {
        KeyValuePair<object, object?>[] removed;
        lock (syncRoot)
        {
            if (entries.Count == 0)
                return;

            removed = entries.ToArray();
            entries.Clear();
        }

        foreach (var entry in removed)
            RaiseResourceChanged(entry.Key, entry.Value, null, ResourceChangeKind.Cleared);
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified resource entry.
    /// </summary>
    /// <param name="item">The entry to locate.</param>
    /// <returns><see langword="true"/> when both the key and value match; otherwise, <see langword="false"/>.</returns>
    public bool Contains(KeyValuePair<object, object?> item)
    {
        lock (syncRoot)
            return entries.TryGetValue(item.Key, out var value) && Equals(value, item.Value);
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">The non-null resource key.</param>
    /// <returns><see langword="true"/> when the key exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public bool ContainsKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
            return entries.ContainsKey(key);
    }

    /// <summary>
    /// Copies a snapshot of the resource entries to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination index.</param>
    public void CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        KeyValuePair<object, object?>[] snapshot;
        lock (syncRoot)
            snapshot = entries.ToArray();

        snapshot.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Returns an enumerator over a stable snapshot of the dictionary.
    /// </summary>
    /// <returns>An enumerator that is unaffected by later dictionary changes.</returns>
    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator()
    {
        lock (syncRoot)
            return ((IEnumerable<KeyValuePair<object, object?>>)entries.ToArray()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Removes the resource with the specified key.
    /// </summary>
    /// <param name="key">The non-null resource key.</param>
    /// <returns><see langword="true"/> when a resource was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public bool Remove(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        object? oldValue;
        lock (syncRoot)
        {
            if (!entries.Remove(key, out oldValue))
                return false;
        }

        RaiseResourceChanged(key, oldValue, null, ResourceChangeKind.Removed);
        return true;
    }

    /// <summary>
    /// Removes the specified entry when both its key and value match.
    /// </summary>
    /// <param name="item">The entry to remove.</param>
    /// <returns><see langword="true"/> when the entry was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(KeyValuePair<object, object?> item)
    {
        object? oldValue;
        lock (syncRoot)
        {
            if (!entries.TryGetValue(item.Key, out oldValue) || !Equals(oldValue, item.Value))
                return false;

            entries.Remove(item.Key);
        }

        RaiseResourceChanged(item.Key, oldValue, null, ResourceChangeKind.Removed);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a resource value.
    /// </summary>
    /// <param name="key">The non-null resource key.</param>
    /// <param name="value">Receives the stored value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the key exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public bool TryGetValue(object key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
            return entries.TryGetValue(key, out value);
    }

    /// <summary>
    /// Replaces the complete dictionary state under one lock and returns deferred notifications.
    /// ThemeManager uses the deferred form so its resource and legacy-theme state become visible
    /// together before any dynamic property setter runs.
    /// </summary>
    internal ResourceDictionaryChange[] ReplaceSnapshot(IReadOnlyDictionary<object, object?> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var changes = new List<ResourceDictionaryChange>();

        lock (syncRoot)
        {
            foreach ((object key, object? oldValue) in entries)
            {
                if (!replacement.ContainsKey(key))
                    changes.Add(new ResourceDictionaryChange(key, oldValue, null, ResourceChangeKind.Removed));
            }

            foreach ((object key, object? newValue) in replacement)
            {
                if (entries.TryGetValue(key, out object? oldValue))
                {
                    if (!Equals(oldValue, newValue))
                        changes.Add(new ResourceDictionaryChange(key, oldValue, newValue, ResourceChangeKind.Replaced));
                }
                else
                {
                    changes.Add(new ResourceDictionaryChange(key, null, newValue, ResourceChangeKind.Added));
                }
            }

            entries.Clear();
            foreach ((object key, object? value) in replacement)
                entries.Add(key, value);
        }

        return changes.ToArray();
    }

    internal Dictionary<object, object?> GetSnapshot()
    {
        lock (syncRoot)
            return new Dictionary<object, object?>(entries);
    }

    internal void PublishChanges(IEnumerable<ResourceDictionaryChange> changes)
    {
        List<Exception>? failures = null;
        foreach (ResourceDictionaryChange change in changes)
        {
            try
            {
                RaiseResourceChanged(change.Key, change.OldValue, change.NewValue, change.Kind);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: > 0 })
            throw new AggregateException("One or more resource observers rejected an atomic theme update.", failures);
    }

    private void RaiseResourceChanged(
        object key,
        object? oldValue,
        object? newValue,
        ResourceChangeKind changeKind)
    {
        ResourceChangeHub.Notify(this, key);
        ResourceChanged?.Invoke(this, new ResourceChangedEventArgs(key, oldValue, newValue, changeKind));
    }
}

internal readonly record struct ResourceDictionaryChange(
    object Key,
    object? OldValue,
    object? NewValue,
    ResourceChangeKind Kind);
