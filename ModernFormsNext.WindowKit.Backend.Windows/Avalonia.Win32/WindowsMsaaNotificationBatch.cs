using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

// UI-thread-owned WinEvent batch. Delivery is posted to the next dispatcher turn,
// with no timer/delay. Shared observers and native UIA delivery remain independent.
internal sealed class WindowsMsaaNotificationBatch(Action<Action> schedule, Action<IPlatformAccessibleObject, int, int> deliver)
{
    private Dictionary<IPlatformAccessibleObject, Dictionary<int, int>> pending = new(ReferenceEqualityComparer.Instance);
    private bool scheduled;

    internal void Add(IPlatformAccessibleObject source, int eventId, int nativeChildId)
    {
        if (!pending.TryGetValue(source, out var events)) pending[source] = events = [];
        if (eventId is 0x8002 or 0x8003)
        {
            events.Remove(0x8002); events.Remove(0x8003);
        }
        events[eventId] = nativeChildId;
        if (scheduled) return;
        scheduled = true;
        schedule(Flush);
    }

    private void Flush()
    {
        var batch = pending;
        pending = new(ReferenceEqualityComparer.Instance);
        scheduled = false;
        foreach (var (source, events) in batch)
            foreach (var (eventId, childId) in events)
            {
                // Per the WinEvent contract, a parent's SHOW/HIDE describes its whole
                // subtree and its invisible state. Repeating these for children is wrong.
                if (eventId is 0x8002 or 0x8003 or 0x800A)
                {
                    var current = eventId == 0x800A ? source : source.Parent;
                    bool covered = false;
                    for (int depth = 0; current is not null && depth < 512; depth++, current = current.Parent)
                        if (batch.TryGetValue(current, out var ancestorEvents) &&
                            (ancestorEvents.ContainsKey(0x8002) || ancestorEvents.ContainsKey(0x8003)))
                        { covered = true; break; }
                    if (covered) continue;
                }
                deliver(source, eventId, childId);
            }
    }
}
