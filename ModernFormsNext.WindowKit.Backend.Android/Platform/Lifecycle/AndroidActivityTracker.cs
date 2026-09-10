using Android.App;
using Android.Content;
using Android.OS;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

/// <summary>Tracks weak Android activity identities and publishes normalized application lifecycle.</summary>
/// <remarks>
/// Activity callbacks run on the Android UI thread. Multiple resumed activities are aggregated;
/// destroying an old activity never ends the process application or clears its replacement.
/// State bundles contain only application-provided versioned strings, never native objects or controls.
/// Saved application state is restored only for the first Activity created in this process. Later
/// Activity recreation keeps the live managed application state rather than replaying an older Bundle.
/// </remarks>
public sealed class AndroidActivityTracker : Java.Lang.Object, Application.IActivityLifecycleCallbacks, IPlatformApplicationLifecycle
{
    private const string StateVersionKey = "ModernFormsNext.Lifecycle.State.Version";
    private const string StateValuesKey = "ModernFormsNext.Lifecycle.State.Values";
    private readonly Func<Activity?>? activityProvider;
    private readonly Action<string>? diagnosticSink;
    private readonly object sync = new();
    private readonly AndroidActivityLifecycleReducer<Activity> activities = new();
    private bool initialActivationDelivered;

    internal AndroidActivityTracker(Func<Activity?>? activityProvider, Action<string>? diagnosticSink)
    {
        this.activityProvider = activityProvider;
        this.diagnosticSink = diagnosticSink;
        Publisher = new PlatformApplicationLifecyclePublisher(VerifyAccess);
        Publisher.StateChanged += (_, args) =>
        {
            if (LifecycleStateChanged is not { } handlers) return;
            foreach (EventHandler<PlatformApplicationLifecycleChangedEventArgs> handler in handlers.GetInvocationList())
                Report(() => handler(this, args));
        };
    }

    internal PlatformApplicationLifecyclePublisher Publisher { get; }

    /// <summary>Gets the most recently resumed, usable activity, or null while all hosts are paused.</summary>
    /// <remarks>The optional host provider must identify an activity already observed as resumed.</remarks>
    public Activity? CurrentActivity
    {
        get
        {
            Activity? provided = activityProvider?.Invoke();
            // Permission requests can inspect availability before dispatching their actual
            // dialog to the main Looper. Preserve that thread-safe observation contract.
            lock (sync)
            {
                if (IsUsable(provided) && activities.IsResumed(provided!)) return provided;
                Activity? current = activities.CurrentResumedHost;
                return IsUsable(current) ? current : null;
            }
        }
    }

    /// <summary>Gets the aggregated Android host availability on the UI thread.</summary>
    public AndroidApplicationLifecycleState State => Publisher.State switch
    {
        PlatformApplicationLifecycleState.Foreground => AndroidApplicationLifecycleState.Foreground,
        PlatformApplicationLifecycleState.Background => Publisher.Snapshot.Phase == PlatformApplicationPhase.Starting
            ? AndroidApplicationLifecycleState.Created : AndroidApplicationLifecycleState.Background,
        PlatformApplicationLifecycleState.NoHost => AndroidApplicationLifecycleState.NoActivity,
        _ => AndroidApplicationLifecycleState.Unknown
    };

    internal event Action<Activity>? ActivityDestroyed;
    private event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? LifecycleStateChanged;

    /// <inheritdoc/>
    event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? IPlatformApplicationLifecycle.StateChanged
    {
        add => LifecycleStateChanged += value;
        remove => LifecycleStateChanged -= value;
    }

    /// <inheritdoc/>
    PlatformApplicationLifecycleState IPlatformApplicationLifecycle.State => Publisher.State;

    internal void ObserveHostActivity(Activity activity)
    {
        VerifyAccess();
        Observe(activity, AndroidActivityPhase.Resumed);
    }

    /// <inheritdoc/>
    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
    {
        bool accepted = false;
        bool initialCreation = false;
        Report(() =>
        {
            PlatformApplicationLifecycleSnapshot snapshot;
            lock (sync)
            {
                accepted = activities.TryObserveCreation(activity, out initialCreation);
                snapshot = activities.Snapshot;
            }
            if (accepted) Publisher.Publish(snapshot);
        });
        if (!accepted) return;
        // The application is process-scoped. A Bundle from an earlier Activity can be older
        // than current managed state, so only a cold process consumes application restoration.
        if (initialCreation) Report(() => Restore(savedInstanceState));
        // A replacement Activity must not replay its original launch Intent. A new process
        // still receives initial activation even when restoring a saved state bundle.
        if (!initialActivationDelivered || savedInstanceState is null)
        {
            initialActivationDelivered = true;
            Report(() => Activate(activity.Intent));
        }
    }

    /// <inheritdoc/>
    public void OnActivityStarted(Activity activity) => Report(() => Observe(activity, AndroidActivityPhase.Started));

    /// <inheritdoc/>
    public void OnActivityResumed(Activity activity) => Report(() => Observe(activity, AndroidActivityPhase.Resumed));

    /// <inheritdoc/>
    public void OnActivityPaused(Activity activity) => Report(() => Observe(activity, AndroidActivityPhase.Paused));

    /// <inheritdoc/>
    public void OnActivityStopped(Activity activity)
    {
        Report(() =>
        {
            bool wasSuspended = Publisher.Snapshot.Phase == PlatformApplicationPhase.Suspended;
            bool isSuspended;
            lock (sync)
            {
                if (!activities.Observe(activity, AndroidActivityPhase.Stopped)) return;
                isSuspended = activities.Snapshot.Phase == PlatformApplicationPhase.Suspended;
            }
            try
            {
                if (!wasSuspended && isSuspended)
                    Publisher.RequestSaveState(PlatformApplicationStateReason.Suspend);
            }
            finally
            {
                // Save precedes suspension, as on Windows. Commit the latest native state
                // even if an observer throws or reenters with a newer host transition.
                PlatformApplicationLifecycleSnapshot snapshot;
                lock (sync) snapshot = activities.Snapshot;
                Publisher.Publish(snapshot);
            }
        });
    }

    /// <inheritdoc/>
    public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
        => Report(() =>
        {
            lock (sync) if (!activities.IsKnown(activity)) return;
            PlatformApplicationStateData? data = Publisher.RequestSaveState(PlatformApplicationStateReason.Recreation);
            if (data is null) return;
            outState.PutInt(StateVersionKey, data.Version);
            outState.PutString(StateValuesKey, AndroidLifecycleStateCodec.Serialize(data));
        });

    /// <inheritdoc/>
    public void OnActivityDestroyed(Activity activity)
    {
        Report(() =>
        {
            PlatformApplicationLifecycleSnapshot snapshot;
            lock (sync)
            {
                activities.Destroy(activity);
                snapshot = activities.Snapshot;
            }
            Publisher.Publish(snapshot);
        });
        // Permission dialog ownership must be released even when a lifecycle observer failed.
        if (ActivityDestroyed is { } handlers)
            foreach (Action<Activity> handler in handlers.GetInvocationList())
                Report(() => handler(activity));
    }

    internal void HandleNewIntent(Activity activity, Intent? intent)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(activity);
        lock (sync) if (!activities.IsKnown(activity) || !IsUsable(activity)) return;
        Report(() => Activate(intent));
    }

    private void Observe(Activity activity, AndroidActivityPhase phase)
    {
        VerifyAccess();
        PlatformApplicationLifecycleSnapshot snapshot;
        lock (sync)
        {
            activities.Observe(activity, phase);
            snapshot = activities.Snapshot;
        }
        Publisher.Publish(snapshot);
    }

    private void Activate(Intent? intent) => Publisher.Activate(AndroidIntentActivation.Read(intent));

    private void Restore(Bundle? bundle)
    {
        string? serialized = bundle?.GetString(StateValuesKey);
        if (serialized is null) return;
        Publisher.RestoreState(AndroidLifecycleStateCodec.Deserialize(bundle!.GetInt(StateVersionKey), serialized),
            PlatformApplicationStateReason.Recreation);
    }

    private void Report(Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            // Native lifecycle completion cannot be prevented by application observers. Log no
            // activation/state payloads, and finish the remaining mandatory cleanup steps.
            try { AndroidLogger.Write($"Application lifecycle callback failed ({exception.GetType().Name}).", diagnosticSink); }
            catch { /* A diagnostic observer must not interrupt native lifecycle cleanup. */ }
        }
    }

    private static void VerifyAccess()
    {
        if (!ReferenceEquals(Looper.MyLooper(), Looper.MainLooper))
            throw new InvalidOperationException("Android lifecycle operations require the main UI thread.");
    }

    private static bool IsUsable(Activity? activity) => activity is not null && !activity.IsFinishing && !activity.IsDestroyed;
}
