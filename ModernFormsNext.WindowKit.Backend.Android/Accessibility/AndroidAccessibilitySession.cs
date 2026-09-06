using ModernFormsNext.WindowKit.Platform.Accessibility;
using static ModernFormsNext.WindowKit.Backend.Android.Accessibility.AndroidAccessibilityMapper;

namespace ModernFormsNext.WindowKit.Backend.Android.Accessibility;

/// <summary>
/// Holds only native identity, subscriptions, and accessibility focus for one host attachment.
/// All tree/property queries use the canonical objects on demand on the owning UI thread.
/// </summary>
internal sealed class AndroidAccessibilitySession : IDisposable
{
    internal const int HostId = -1, InvalidId = int.MinValue;
    private readonly IPlatformAccessibilityHost host;
    private readonly Dictionary<int, Entry> entries = [];
    private readonly Dictionary<long, int> identities = [];
    private readonly Dictionary<(int Id, int Type), int> pendingEvents = [];
    private int nextId;
    private bool attached;
    private bool disposed;

    internal AndroidAccessibilitySession(IPlatformAccessibilityHost host, int lastId = 0)
    {
        this.host = host;
        nextId = lastId;
    }

    internal event Action? EventsPending;
    internal int AccessibilityFocusId { get; private set; } = InvalidId;
    internal int HoveredId { get; private set; } = InvalidId;
    internal IPlatformAccessibleObject? Root => attached && !disposed ? host.AccessibilityRoot : null;
    internal int CachedNodeCount => entries.Count;
    internal int LastAllocatedId => nextId;

    internal void InvalidateGeometry() => Queue(HostId, 2048, 1);

    internal void Attach()
    {
        if (disposed || attached) return;
        attached = true;
        if (host is IPlatformAccessibilitySurface surface)
            surface.AccessibilityNotification += Notify;
        if (Root is { } root) Register(root);
        Queue(HostId, 2048, 1);
    }

    internal void Detach()
    {
        attached = false;
        if (host is IPlatformAccessibilitySurface surface)
            surface.AccessibilityNotification -= Notify;
        foreach (var entry in entries.Values) entry.Unsubscribe();
        entries.Clear();
        identities.Clear();
        pendingEvents.Clear();
        AccessibilityFocusId = HoveredId = InvalidId;
        // Do not reset nextId. A cached ID from an earlier attachment must never alias a new node.
    }

    internal int Register(IPlatformAccessibleObject node)
    {
        if (!IsAttached(node)) return InvalidId;
        long runtimeId = node.GetRuntimeId();
        if (runtimeId <= 0) return InvalidId;
        if (identities.TryGetValue(runtimeId, out int known))
            return entries[known].Target.TryGetTarget(out var existing) && ReferenceEquals(existing, node)
                ? known : InvalidId; // Reject a colliding custom transport instead of aliasing it.
        int id;
        if (ReferenceEquals(node, Root)) id = HostId;
        else
        {
            if (nextId == int.MaxValue) return InvalidId;
            id = ++nextId;
        }
        var entry = new Entry(node, runtimeId, (eventId, objectId, childId) => NotifyId(id, eventId, objectId, childId));
        entries.Add(id, entry);
        identities.Add(runtimeId, id);
        return id;
    }

    internal IPlatformAccessibleObject? Find(int id)
    {
        if (!attached || disposed) return null;
        if (id == HostId && !entries.ContainsKey(id) && Root is { } root) Register(root);
        if (!entries.TryGetValue(id, out var entry)) return null;
        if (entry.Target.TryGetTarget(out var node) && IsAttached(node)) return node;
        Remove(id);
        return null;
    }

    private bool IsAttached(IPlatformAccessibleObject node)
    {
        var root = Root;
        if (root is null) return false;
        var current = node;
        for (int depth = 0; depth < 512; depth++)
        {
            if (current.GetAccessibilityView() == 4 || (current.State & Invisible) != 0) return false;
            if (ReferenceEquals(current, root)) return true;
            if (current.Parent is not { } parent) return false;
            bool found = false;
            int count = parent.GetChildCount();
            for (int i = 0; i < count; i++)
                if (ReferenceEquals(parent.GetChild(i), current)) { found = true; break; }
            if (!found) return false;
            current = parent;
        }
        return false;
    }

    internal List<int> Children(IPlatformAccessibleObject node)
    {
        List<int> ids = [];
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++)
            if (node.GetChild(i) is { } child && Register(child) is var id && id != InvalidId)
                ids.Add(id);
        return ids;
    }

    internal bool Perform(int id, int action, object? parameter, bool visible)
    {
        if (Find(id) is not { } node) return false;
        if (action == ActionAccessibilityFocus)
        {
            if (!visible || parameter is not null || AccessibilityFocusId == id) return false;
            if (AccessibilityFocusId != InvalidId) Queue(AccessibilityFocusId, 65536);
            AccessibilityFocusId = id;
            Queue(id, 32768);
            return true;
        }
        if (action == ActionClearAccessibilityFocus)
        {
            if (AccessibilityFocusId != id || parameter is not null) return false;
            AccessibilityFocusId = InvalidId;
            Queue(id, 65536);
            return true;
        }
        if (!AndroidAccessibilityMapper.PerformAction(node, action, parameter)) return false;
        if (action == ActionClick) Queue(id, 1);
        // Notifications from standard controls and custom peers are coalesced into these same
        // per-node event slots. Actions that silently implement custom state still invalidate it.
        if (action == ActionFocus) Queue(id, 8);
        else if (action is ActionSelect or ActionClearSelection) Queue(id, 4);
        else Queue(id, 2048, action == ActionSetText
            ? node.GetIsSensitive() || (node.State & Protected) != 0 ? 0 : 2 : 64);
        return true;
    }

    internal int FindFocus(bool accessibility)
    {
        if (accessibility) return Find(AccessibilityFocusId) is null ? InvalidId : AccessibilityFocusId;
        if (Root?.GetFocused() is not { } node) return InvalidId;
        return Register(node);
    }

    internal int HitTest(int x, int y, Rect viewport)
    {
        if (Root is not { } root || root.HitTest(x, y) is not { } hit) return InvalidId;
        if (!AndroidAccessibilityBounds.Clip(hit, root, viewport).Contains(new Point(x, y))) return InvalidId;
        return Register(hit);
    }

    internal void Hover(int id)
    {
        if (HoveredId == id) return;
        if (id != InvalidId) Queue(id, 128);
        if (HoveredId != InvalidId) Queue(HoveredId, 256);
        HoveredId = id;
    }

    private void NotifyId(int id, int eventId, int objectId, int childId)
    {
        if (entries.TryGetValue(id, out var entry) && entry.Target.TryGetTarget(out var node))
            Notify(node, eventId, objectId, childId);
    }

    private void Notify(IPlatformAccessibleObject source, int eventId, int objectId, int childId)
    {
        if (!attached || disposed) return;
        // Child IDs in the legacy notification contract are one-based child positions, never
        // Android virtual IDs. Resolve them now; structure removals fall back to the host.
        if (childId > 0) source = source.GetChild(childId - 1) ?? source;
        int id = Register(source);
        if (id == InvalidId) id = HostId;
        if (eventId is 0x8000 or 0x8001 or 0x8002 or 0x8003 or 0x8004 or 0x800F)
        {
            Prune();
            Queue(id, 2048, 1);
        }
        else if (eventId == PlatformAccessibilitySurfaceEvents.Invoked) Queue(id, 1);
        else if (eventId == 0x8005) Queue(id, 8);
        else if (eventId is >= 0x8006 and <= 0x8009) Queue(id, 4);
        else if (eventId == 0x800E)
        {
            // Payloads never contain values. Sensitive changes invalidate metadata only.
            // A switch/range value updates state, not editable text. Combining Text with
            // StateDescription made TalkBack choose an empty text announcement after toggling.
            Queue(id, 2048, source.GetIsSensitive() || (source.State & Protected) != 0 ? 0
                : source.GetControlType() == 10 ? 2 : 64);
        }
        else Queue(id, 2048, eventId is 0x800C or 0x800D or 0x8010 ? 4 : eventId == 0x800B ? 1 : 64);
    }

    private void Prune()
    {
        foreach (int id in entries.Keys.ToArray()) Find(id);
    }

    private void Remove(int id)
    {
        if (!entries.Remove(id, out var entry)) return;
        identities.Remove(entry.RuntimeId);
        entry.Unsubscribe();
        if (AccessibilityFocusId == id)
        {
            AccessibilityFocusId = InvalidId;
            Queue(HostId, 2048, 1);
        }
        if (HoveredId == id) HoveredId = InvalidId;
    }

    private void Queue(int id, int type, int changes = 0)
    {
        if (!attached || disposed || id == InvalidId) return;
        var key = (id, type);
        pendingEvents[key] = pendingEvents.GetValueOrDefault(key) | changes;
        if (pendingEvents.Count > 128)
        {
            // Bound large dynamic updates. A subtree invalidation causes Android to re-query;
            // retain the latest focus location without accumulating thousands of event objects.
            pendingEvents.Clear();
            pendingEvents[(HostId, 2048)] = 1;
            if (AccessibilityFocusId != InvalidId) pendingEvents[(AccessibilityFocusId, 32768)] = 0;
        }
        EventsPending?.Invoke();
    }

    internal AndroidAccessibilityEvent[] DrainEvents()
    {
        var result = pendingEvents.Select(p => new AndroidAccessibilityEvent(p.Key.Id, p.Key.Type, p.Value)).ToArray();
        pendingEvents.Clear();
        return result;
    }

    public void Dispose()
    {
        Detach();
        disposed = true;
        EventsPending = null;
    }

    private sealed class Entry
    {
        internal readonly WeakReference<IPlatformAccessibleObject> Target;
        internal readonly long RuntimeId;
        private readonly Action<int, int, int> handler;

        internal Entry(IPlatformAccessibleObject node, long runtimeId, Action<int, int, int> handler)
        {
            Target = new(node);
            RuntimeId = runtimeId;
            this.handler = handler;
            if (node is IPlatformAccessibilityNotifications notifications)
                notifications.AccessibilityNotification += handler;
        }

        internal void Unsubscribe()
        {
            if (Target.TryGetTarget(out var node) && node is IPlatformAccessibilityNotifications notifications)
                notifications.AccessibilityNotification -= handler;
        }
    }
}

/// <summary>A bounded, value-free native event instruction, not a copy of semantic state.</summary>
internal readonly record struct AndroidAccessibilityEvent(int Id, int Type, int Changes);
