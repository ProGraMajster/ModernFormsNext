using ModernFormsNext.WindowKit.Backend.Lifecycle;
using System.Runtime.CompilerServices;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

// Native Activities stay outside this reducer. Weak identity lets the same deterministic
// aggregation run in net10.0 tests without retaining destroyed/replaced platform hosts.
internal sealed class AndroidActivityLifecycleReducer<THost> where THost : class
{
    private readonly List<Entry> hosts = [];
    private readonly ConditionalWeakTable<THost, object> retired = new();
    private long generation;
    private long resumeOrder;
    private bool observed;
    private bool hasResumed;
    private bool hasCreated;

    internal PlatformApplicationLifecycleSnapshot Snapshot
    {
        get
        {
            RemoveCollected();
            bool active = hosts.Any(entry => entry.Phase == AndroidActivityPhase.Resumed);
            PlatformApplicationLifecycleState state = !observed ? PlatformApplicationLifecycleState.Unknown
                : hosts.Count == 0 ? PlatformApplicationLifecycleState.NoHost
                : active ? PlatformApplicationLifecycleState.Foreground : PlatformApplicationLifecycleState.Background;
            PlatformApplicationPhase phase = !observed ? PlatformApplicationPhase.NotStarted
                : hosts.Count > 0 && hosts.All(entry => entry.Phase == AndroidActivityPhase.Stopped)
                    ? PlatformApplicationPhase.Suspended
                : hasResumed ? PlatformApplicationPhase.Running : PlatformApplicationPhase.Starting;
            return new(phase, state, active, hosts.Count, generation);
        }
    }

    internal THost? CurrentResumedHost
    {
        get
        {
            RemoveCollected();
            return hosts.Where(entry => entry.Phase == AndroidActivityPhase.Resumed)
                .OrderByDescending(entry => entry.ResumeOrder)
                .Select(entry => entry.Host.TryGetTarget(out THost? host) ? host : null).FirstOrDefault();
        }
    }

    internal bool IsResumed(THost host)
        => hosts.Any(entry => entry.Phase == AndroidActivityPhase.Resumed &&
            entry.Host.TryGetTarget(out THost? target) && ReferenceEquals(target, host));

    internal bool IsKnown(THost host)
        => hosts.Any(entry => entry.Host.TryGetTarget(out THost? target) && ReferenceEquals(target, host));

    internal bool TryObserveCreation(THost host, out bool isInitialCreation)
    {
        ArgumentNullException.ThrowIfNull(host);
        isInitialCreation = false;
        if (hosts.Any(item => item.CreationReported && item.Host.TryGetTarget(out THost? target) && ReferenceEquals(target, host)))
            return false;
        if (!Observe(host, AndroidActivityPhase.Created)) return false;
        Entry entry = hosts.First(item => item.Host.TryGetTarget(out THost? target) && ReferenceEquals(target, host));
        entry.CreationReported = true;
        isInitialCreation = !hasCreated;
        hasCreated = true;
        return true;
    }

    internal bool Observe(THost host, AndroidActivityPhase phase)
    {
        ArgumentNullException.ThrowIfNull(host);
        RemoveCollected();
        // Retired identities remain weakly marked until collected. No late callback may
        // resurrect a destroyed host or acquire a new host generation for that identity.
        if (retired.TryGetValue(host, out _)) return false;
        Entry? entry = hosts.FirstOrDefault(item => item.Host.TryGetTarget(out THost? target) && ReferenceEquals(target, host));
        // Late pause/stop callbacks from a retired Activity cannot recreate a usable host.
        if (entry is null && phase is AndroidActivityPhase.Paused or AndroidActivityPhase.Stopped) return false;
        observed = true;
        if (entry is null)
        {
            entry = new Entry(host, phase);
            hosts.Add(entry);
            generation++;
        }
        entry.Phase = phase;
        if (phase == AndroidActivityPhase.Resumed)
        {
            hasResumed = true;
            entry.ResumeOrder = ++resumeOrder;
        }
        return true;
    }

    internal void Destroy(THost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        retired.GetValue(host, static _ => new object());
        hosts.RemoveAll(entry => !entry.Host.TryGetTarget(out THost? target) || ReferenceEquals(target, host));
    }

    private void RemoveCollected() => hosts.RemoveAll(entry => !entry.Host.TryGetTarget(out _));

    private sealed class Entry(THost host, AndroidActivityPhase phase)
    {
        internal WeakReference<THost> Host { get; } = new(host);
        internal AndroidActivityPhase Phase { get; set; } = phase;
        internal long ResumeOrder { get; set; }
        internal bool CreationReported { get; set; }
    }
}

internal enum AndroidActivityPhase { Created, Started, Resumed, Paused, Stopped }
