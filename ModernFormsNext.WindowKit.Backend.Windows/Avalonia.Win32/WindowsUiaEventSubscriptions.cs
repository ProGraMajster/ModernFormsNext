namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

// Per-fragment subscriptions, not the process-global UiaClientsAreListening flag.
// Multiple clients can subscribe to the same event/property independently.
internal sealed class WindowsUiaEventSubscriptions(IWindowsUiaEventSink sink) : IWindowsUiaEventSink
{
    internal const int PropertyChangedEvent = 20004;
    internal const int StructureChangedEvent = 20002;
    private readonly object sync = new();
    private readonly Dictionary<(int Event, int Property), int> listeners = [];

    public bool ClientsAreListening
    {
        get { lock (sync) { if (listeners.Count == 0) return false; } return sink.ClientsAreListening; }
    }

    internal void Add(int eventId, int[]? properties) => Change(eventId, properties, 1);
    internal void Remove(int eventId, int[]? properties) => Change(eventId, properties, -1);
    internal void Clear() { lock (sync) listeners.Clear(); }

    private void Change(int eventId, int[]? properties, int delta)
    {
        lock (sync)
        {
            if (eventId == PropertyChangedEvent && properties is { Length: > 0 })
            {
                foreach (int property in properties) ChangeOne((eventId, property), delta);
            }
            else ChangeOne((eventId, 0), delta);
        }
    }

    private void ChangeOne((int Event, int Property) key, int delta)
    {
        listeners.TryGetValue(key, out int count);
        if (count + delta > 0) listeners[key] = checked(count + delta);
        else listeners.Remove(key);
    }

    private bool Has(int eventId, int property = 0)
    {
        lock (sync)
            return listeners.ContainsKey((eventId, 0)) || listeners.ContainsKey((eventId, property));
    }

    public void RaiseAutomationEvent(WindowsUiaProvider provider, int eventId)
    {
        if (Has(eventId)) sink.RaiseAutomationEvent(provider, eventId);
    }

    public void RaiseAutomationPropertyChangedEvent(WindowsUiaProvider provider, int propertyId, object? oldValue, object? newValue)
    {
        if (Has(PropertyChangedEvent, propertyId)) sink.RaiseAutomationPropertyChangedEvent(provider, propertyId, oldValue, newValue);
    }

    public void RaiseStructureChangedEvent(WindowsUiaProvider provider, StructureChangeType changeType, int[]? runtimeId)
    {
        if (Has(StructureChangedEvent)) sink.RaiseStructureChangedEvent(provider, changeType, runtimeId);
    }
}
