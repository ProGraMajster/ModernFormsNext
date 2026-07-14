namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

// Android activities are short-lived hosts. Keeping the weak-reference replacement and
// clear-if-current rules independent of Android types makes recreation behavior deterministic and
// testable without constructing a native Activity.
internal sealed class WeakHostReference<T> where T : class
{
    private WeakReference<T>? reference;

    internal T? Target
        => reference is not null && reference.TryGetTarget(out var target) ? target : null;

    internal void Set(T target)
    {
        ArgumentNullException.ThrowIfNull(target);
        reference = new WeakReference<T>(target);
    }

    internal bool ClearIfCurrent(T target)
    {
        if (Target is not { } current || !ReferenceEquals(current, target))
            return false;

        reference = null;
        return true;
    }
}
