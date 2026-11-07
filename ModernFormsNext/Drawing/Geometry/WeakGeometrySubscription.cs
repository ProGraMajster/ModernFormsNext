namespace ModernFormsNext.Drawing;

// A shared geometry must not retain a discarded control or composition owner. Callbacks must
// be static and receive the weak target explicitly, rather than capturing it in a closure.
internal sealed class WeakGeometrySubscription<T> : IDisposable where T : class
{
    private readonly WeakReference<T> target;
    private readonly Action<T> changed;
    private Geometry? source;

    internal WeakGeometrySubscription(T target, Geometry source, Action<T> changed)
    {
        this.target = new(target);
        this.source = source;
        this.changed = changed;
        source.Changed += HandleChanged;
    }

    public void Dispose()
    {
        var current = source;
        if (current is null) return;
        source = null;
        current.Changed -= HandleChanged;
    }

    private void HandleChanged(object? sender, EventArgs e)
    {
        if (target.TryGetTarget(out var current)) changed(current);
        else Dispose();
    }
}
