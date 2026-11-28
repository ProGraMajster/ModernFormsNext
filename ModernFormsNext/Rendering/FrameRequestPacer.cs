using ModernFormsNext.WindowKit;

namespace ModernFormsNext.Rendering;

/// <summary>Experimental UI-thread rate cap for dirty regions. Owns no frame or native surface.</summary>
/// <remarks>
/// A wake is demand driven and carries no old frame. The current UI tree and backing are
/// acquired only by the native paint. The refresh interval is a budget, with no phase lock.
/// </remarks>
internal sealed class FrameRequestPacer(
    Func<TimeSpan> now,
    Action<TimeSpan?> setWake,
    Action<Rect> invalidate) : IDisposable
{
    private Rect? dirty;
    private TimeSpan? lastSignal;
    private TimeSpan? armedFor;
    private TimeSpan interval;
    private bool active = true;
    private bool disposed;
    private long requests, signals, merged, retired, wakes;

    internal FrameRequestCounts Counts => new(requests, signals, merged, retired, wakes);

    /// <summary>Updates the budget and recomputes the next wake without keeping the old deadline.</summary>
    internal void SetRefreshRate(double? hz)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var next = hz is double rate && rate > 1 && double.IsFinite(rate)
            ? TimeSpan.FromTicks((long)Math.Ceiling(TimeSpan.TicksPerSecond / rate))
            : TimeSpan.Zero;
        if (next == interval) return;
        interval = next;
        ReleaseOrArm();
    }

    internal void Request(Rect damage)
    {
        if (disposed || !active || damage.Width <= 0 || damage.Height <= 0) return;
        requests++;
        if (dirty is { } previous)
        {
            merged++;
            damage = previous.Union(damage);
        }
        dirty = damage;
        ReleaseOrArm();
    }

    /// <summary>Called by the owning UI dispatcher after the one-shot wake fires.</summary>
    internal void Wake()
    {
        // Cancellation is safe even if the dispatcher had already started delivering the wake.
        if (disposed || !active || armedFor is null) return;
        wakes++;
        armedFor = null;
        ReleaseOrArm();
    }

    internal void SetActive(bool value)
    {
        if (disposed || active == value) return;
        active = value;
        if (!value) RetireDamage();
    }

    /// <summary>Forgets damage mapped to a previous backing generation.</summary>
    /// <returns>Whether the caller should invalidate the whole current backing.</returns>
    internal bool RetireDamage()
    {
        bool hadDamage = dirty.HasValue;
        if (hadDamage) retired++;
        dirty = null;
        lastSignal = null;
        CancelWake();
        return hadDamage;
    }

    private void ReleaseOrArm()
    {
        if (!active || dirty is not { } damage) return;
        TimeSpan current = now();
        if (lastSignal is { } previous && current - previous < interval)
        {
            TimeSpan due = previous + interval;
            if (armedFor != due)
            {
                armedFor = due;
                setWake(due - current);
            }
            return;
        }

        CancelWake();
        dirty = null;
        lastSignal = current;
        signals++;
        // Commit before calling the backend. Reentrant invalidation belongs to the next budget.
        invalidate(damage);
    }

    private void CancelWake()
    {
        if (armedFor is null) return;
        armedFor = null;
        setWake(null);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RetireDamage();
    }
}

internal readonly record struct FrameRequestCounts(long Requests, long Signals, long Merged, long Retired, long Wakes)
{
    internal FrameRequestCounts Since(FrameRequestCounts baseline) =>
        new(Requests - baseline.Requests, Signals - baseline.Signals, Merged - baseline.Merged,
            Retired - baseline.Retired, Wakes - baseline.Wakes);
}
