namespace ModernFormsNext;

/// <summary>
/// Routes resource changes to weak listeners grouped by resource key.
/// </summary>
/// <remarks>
/// Dictionaries can be created lazily on any control in the visual ancestry. A central weak
/// registry lets an existing reference observe a newly created ancestor dictionary without
/// making every control allocate a resource dictionary or a descendant-listener collection.
/// </remarks>
internal static class ResourceChangeHub
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<object, List<WeakReference<IResourceChangeListener>>> Listeners = [];

    public static void Subscribe(object key, IResourceChangeListener listener)
    {
        lock (SyncRoot)
        {
            if (!Listeners.TryGetValue(key, out var registrations))
            {
                registrations = [];
                Listeners.Add(key, registrations);
            }

            registrations.Add(new WeakReference<IResourceChangeListener>(listener));
        }
    }

    public static void Unsubscribe(object key, IResourceChangeListener listener)
    {
        lock (SyncRoot)
        {
            if (!Listeners.TryGetValue(key, out var registrations))
                return;

            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                if (!registrations[i].TryGetTarget(out var target) || ReferenceEquals(target, listener))
                    registrations.RemoveAt(i);
            }

            if (registrations.Count == 0)
                Listeners.Remove(key);
        }
    }

    public static void Notify(ResourceDictionary source, object key)
    {
        IResourceChangeListener[] targets;

        lock (SyncRoot)
        {
            if (!Listeners.TryGetValue(key, out var registrations))
                return;

            var liveTargets = new List<IResourceChangeListener>(registrations.Count);
            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                if (registrations[i].TryGetTarget(out var target))
                    liveTargets.Add(target);
                else
                    registrations.RemoveAt(i);
            }

            if (registrations.Count == 0)
                Listeners.Remove(key);

            targets = liveTargets.ToArray();
        }

        // Applying a resource may run a user property setter. Never hold the registry lock while
        // invoking it, otherwise a setter that changes another resource could deadlock.
        foreach (var target in targets)
            target.OnResourceChanged(source, key);
    }
}
