using System.Collections;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext;

public sealed partial class ResourceDictionary
{
    // Protect topology independently from local entries. No user callback runs under either lock.
    // One topology lock prevents concurrent additions from admitting a cycle in two dictionaries.
    private static readonly object GraphSync = new();
    private List<ResourceDictionary>? merged;
    private List<WeakReference<ResourceDictionary>>? parents;
    private MergedList? mergedView;

    /// <summary>Gets the ordered dictionaries used after local resource lookup.</summary>
    /// <remarks>
    /// <para>
    /// A local entry, including null, wins; otherwise the last merged dictionary containing the key
    /// wins, recursively. Indexer reads, <see cref="TryGetValue"/> and <see cref="ContainsKey"/>
    /// use this effective lookup. Count, Keys, Values, enumeration and all entry mutations retain
    /// their local-only dictionary semantics; Clear does not remove merges. Remove a local override
    /// to reveal a merged value. This preserves local snapshot/serialization and ThemeManager behavior.
    /// </para>
    /// <para>
    /// Add/remove/replace on the owning UI thread when live controls use these dictionaries.
    /// Duplicate direct children and cycles are rejected before publication. Every root is limited
    /// to 256 distinct dictionaries and 64 dictionaries along a path, including changes made to an
    /// already merged child. A child holds only weak references to its parents; removing it detaches
    /// propagation. Dictionaries and their values remain application-owned and are never disposed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// resources.MergedDictionaries.Add(common);
    /// resources.MergedDictionaries.Add(pageOverrides); // wins over common
    /// resources["Spacing"] = 12; // wins over both
    /// </code>
    /// </example>
    public IList<ResourceDictionary> MergedDictionaries
    {
        get
        {
            lock (GraphSync)
                return mergedView ??= new MergedList(this);
        }
    }

    private bool TryResolve(object key, LookupOverride? previous, out object? value)
    {
        bool overrideLocal = previous is { Children: null } && ReferenceEquals(previous.Dictionary, this);
        if (overrideLocal && previous!.Existed)
        {
            value = previous.Value;
            return true;
        }
        if (!overrideLocal)
        {
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out value))
                    return true;
            }
        }
        ResourceDictionary[] order;
        lock (GraphSync)
        {
            if ((merged?.Count ?? 0) == 0 && !(ReferenceEquals(previous?.Dictionary, this) && previous.Children is { Length: > 0 }))
            {
                value = null;
                return false;
            }
            order = LookupOrder(this, previous);
        }

        foreach (ResourceDictionary dictionary in order)
        {
            if (previous is { Children: null } && ReferenceEquals(previous.Dictionary, dictionary))
            {
                if (previous.Existed)
                {
                    value = previous.Value;
                    return true;
                }
                continue;
            }
            lock (dictionary.syncRoot)
            {
                if (dictionary.entries.TryGetValue(key, out value))
                    return true;
            }
        }
        value = null;
        return false;
    }

    private static ResourceDictionary[] LookupOrder(ResourceDictionary root, LookupOverride? previous)
    {
        var result = new List<ResourceDictionary>();
        var visited = new HashSet<ResourceDictionary>();
        var pending = new Stack<ResourceDictionary>();
        pending.Push(root);
        while (pending.TryPop(out ResourceDictionary? current))
        {
            if (!visited.Add(current))
                continue;
            result.Add(current);
            IReadOnlyList<ResourceDictionary>? children = ReferenceEquals(previous?.Dictionary, current)
                && previous.Children is not null ? previous.Children : current.merged;
            if (children is null)
                continue;
            // Stack order visits the last merge first, after its parent's local entries.
            for (int i = 0; i < children.Count; i++)
                pending.Push(children[i]);
        }
        return result.ToArray();
    }

    private ResourceDictionary[] GetAffectedDictionaries()
    {
        // A diamond reaches each ancestor once. Dead parents are pruned without rooting owners.
        var affected = new List<ResourceDictionary>();
        var seen = new HashSet<ResourceDictionary>();
        var pending = new Queue<ResourceDictionary>();
        pending.Enqueue(this);
        while (pending.TryDequeue(out ResourceDictionary? current))
        {
            if (!seen.Add(current))
                continue;
            affected.Add(current);
            if (current.parents is null)
                continue;
            for (int i = current.parents.Count - 1; i >= 0; i--)
            {
                if (current.parents[i].TryGetTarget(out ResourceDictionary? parent))
                    pending.Enqueue(parent);
                else
                    current.parents.RemoveAt(i);
            }
        }
        return affected.ToArray();
    }

    private void PublishEffectiveChange(object key, LookupOverride previous, ResourceChangeKind kind)
    {
        ResourceDictionary[] affected;
        lock (GraphSync)
            affected = GetAffectedDictionaries();

        var notifications = new List<(ResourceDictionary Dictionary, ResourceChangedEventArgs Args)>();
        foreach (ResourceDictionary dictionary in affected)
        {
            bool existed = dictionary.TryResolve(key, previous, out object? oldValue);
            bool exists = dictionary.TryResolve(key, null, out object? newValue);
            if (existed == exists && Equals(oldValue, newValue))
                continue;
            var effectiveKind = existed && exists ? ResourceChangeKind.Replaced
                : exists ? ResourceChangeKind.Added
                : ReferenceEquals(dictionary, this) && kind == ResourceChangeKind.Cleared ? kind
                : ResourceChangeKind.Removed;
            notifications.Add((dictionary, new ResourceChangedEventArgs(key, oldValue, newValue, effectiveKind)));
        }

        List<Exception>? failures = null;
        // All references observe the committed state before any public dictionary event runs.
        if (notifications.Count != 0)
            Invoke(() => ResourceChangeHub.Notify(notifications.Select(static item => item.Dictionary).ToArray(), key));
        foreach (var notification in notifications)
            Invoke(() => notification.Dictionary.ResourceChanged?.Invoke(notification.Dictionary, notification.Args));

        if (failures is { Count: 1 })
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 })
            throw new AggregateException("One or more resource observers rejected a dictionary change.", failures);

        void Invoke(Action action)
        {
            try { action(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
    }

    private void ChangeMerged(Action<List<ResourceDictionary>> change)
    {
        ResourceDictionary[] oldChildren;
        HashSet<object> keys;
        lock (GraphSync)
        {
            oldChildren = merged?.ToArray() ?? [];
            keys = GetEffectiveKeys();
            merged ??= [];
            try
            {
                change(merged);
                if (merged.Any(static child => child is null) || merged.Distinct().Count() != merged.Count)
                    throw new ArgumentException("Merged dictionaries must be non-null and distinct.");
                // Validate every existing ancestor: extending a deep child must not silently make
                // an already composed root exceed its bounds. Nothing is published on rejection.
                foreach (ResourceDictionary root in GetAffectedDictionaries())
                    ValidateGraph(root);
                if (merged.SequenceEqual(oldChildren))
                    return;
            }
            catch
            {
                merged = [.. oldChildren];
                throw;
            }

            foreach (ResourceDictionary child in oldChildren)
                child.parents?.RemoveAll(parent => !parent.TryGetTarget(out var target) || ReferenceEquals(target, this));
            foreach (ResourceDictionary child in merged)
                (child.parents ??= []).Add(new WeakReference<ResourceDictionary>(this));
            keys.UnionWith(GetEffectiveKeys());
            lock (syncRoot)
                mutationVersion++;
        }
        var previous = new LookupOverride(this, false, null, oldChildren);
        List<Exception>? failures = null;
        foreach (object key in keys)
        {
            try { PublishEffectiveChange(key, previous, ResourceChangeKind.Replaced); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is { Count: > 0 })
            throw new AggregateException("One or more observers rejected the committed merged dictionaries.", failures);
    }

    private HashSet<object> GetEffectiveKeys()
    {
        var keys = new HashSet<object>();
        foreach (ResourceDictionary dictionary in LookupOrder(this, null))
        {
            lock (dictionary.syncRoot)
                keys.UnionWith(dictionary.entries.Keys);
        }
        return keys;
    }

    private static void ValidateGraph(ResourceDictionary root)
    {
        var path = new HashSet<ResourceDictionary>();
        var visited = new HashSet<ResourceDictionary>();
        var heights = new Dictionary<ResourceDictionary, int>();
        Visit(root);

        int Visit(ResourceDictionary current)
        {
            if (!path.Add(current))
                throw new InvalidOperationException("Merged resource dictionaries cannot contain cycles.");
            if (path.Count > 64 || visited.Add(current) && visited.Count > 256)
                throw new InvalidOperationException("Merged resources are limited to 256 dictionaries and a path depth of 64.");
            if (heights.TryGetValue(current, out int cached))
            {
                path.Remove(current);
                return cached;
            }
            int height = 1;
            if (current.merged is not null)
            {
                foreach (ResourceDictionary child in current.merged)
                    height = Math.Max(height, 1 + Visit(child));
            }
            path.Remove(current);
            if (height > 64)
                throw new InvalidOperationException("Merged resources cannot exceed a path depth of 64.");
            heights[current] = height;
            return height;
        }
    }

    private sealed record LookupOverride(ResourceDictionary Dictionary, bool Existed,
        object? Value, ResourceDictionary[]? Children);

    private sealed class MergedList(ResourceDictionary owner) : IList<ResourceDictionary>
    {
        public int Count { get { lock (GraphSync) return owner.merged?.Count ?? 0; } }
        public bool IsReadOnly => false;
        public ResourceDictionary this[int index]
        {
            get { lock (GraphSync) return (owner.merged ?? [])[index]; }
            set { ArgumentNullException.ThrowIfNull(value); owner.ChangeMerged(items => items[index] = value); }
        }
        public void Add(ResourceDictionary item)
        {
            ArgumentNullException.ThrowIfNull(item);
            owner.ChangeMerged(items => items.Add(item));
        }
        public void Clear() => owner.ChangeMerged(static items => items.Clear());
        public bool Contains(ResourceDictionary item) => IndexOf(item) >= 0;
        public int IndexOf(ResourceDictionary item) { lock (GraphSync) return owner.merged?.IndexOf(item) ?? -1; }
        public void CopyTo(ResourceDictionary[] array, int arrayIndex) => Snapshot().CopyTo(array, arrayIndex);
        public IEnumerator<ResourceDictionary> GetEnumerator() => ((IEnumerable<ResourceDictionary>)Snapshot()).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Insert(int index, ResourceDictionary item)
        {
            ArgumentNullException.ThrowIfNull(item);
            owner.ChangeMerged(items => items.Insert(index, item));
        }
        public bool Remove(ResourceDictionary item)
        {
            bool removed = false;
            owner.ChangeMerged(items => removed = items.Remove(item));
            return removed;
        }
        public void RemoveAt(int index) => owner.ChangeMerged(items => items.RemoveAt(index));
        private ResourceDictionary[] Snapshot() { lock (GraphSync) return owner.merged?.ToArray() ?? []; }
    }
}
