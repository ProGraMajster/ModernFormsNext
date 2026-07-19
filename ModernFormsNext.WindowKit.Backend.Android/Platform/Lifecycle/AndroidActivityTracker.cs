using Android.App;
using Android.OS;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

/// <summary>
/// Tracks the current Android activity through application lifecycle callbacks.
/// </summary>
/// <remarks>
/// Activities are stored through <see cref="WeakReference{T}"/> only. A paused activity is retained
/// weakly for diagnostics but is not returned as an active UI host. Rotation clears the destroyed
/// instance and allows the next created activity to become current.
/// </remarks>
public sealed class AndroidActivityTracker : Java.Lang.Object, Application.IActivityLifecycleCallbacks, IPlatformApplicationLifecycle
{
    private readonly object sync = new();
    private readonly Func<Activity?>? activityProvider;
    private readonly Action<string>? diagnosticSink;
    private readonly WeakHostReference<Activity> currentActivity = new();
    private AndroidApplicationLifecycleState state = AndroidApplicationLifecycleState.Unknown;

    internal AndroidActivityTracker(Func<Activity?>? activityProvider, Action<string>? diagnosticSink)
    {
        this.activityProvider = activityProvider;
        this.diagnosticSink = diagnosticSink;
    }

    /// <summary>
    /// Gets the most recently resumed, non-destroyed activity, or <see langword="null"/> when the
    /// application is in the background or has no usable activity.
    /// </summary>
    public Activity? CurrentActivity
    {
        get
        {
            var provided = activityProvider?.Invoke();
            if (IsUsable(provided))
                return provided;

            lock (sync)
            {
                if (state != AndroidApplicationLifecycleState.Foreground ||
                    currentActivity.Target is not { } activity ||
                    !IsUsable(activity))
                {
                    return null;
                }

                return activity;
            }
        }
    }

    /// <summary>
    /// Gets the last observed application lifecycle state.
    /// </summary>
    public AndroidApplicationLifecycleState State
    {
        get
        {
            lock (sync)
                return state;
        }
    }

    internal event Action<Activity>? ActivityDestroyed;

    /// <inheritdoc/>
    event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? IPlatformApplicationLifecycle.StateChanged
    {
        add => LifecycleStateChanged += value;
        remove => LifecycleStateChanged -= value;
    }

    /// <inheritdoc/>
    PlatformApplicationLifecycleState IPlatformApplicationLifecycle.State => ToPlatformState(State);

    private event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? LifecycleStateChanged;

    internal void ObserveHostActivity(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        SetActivity(activity, AndroidApplicationLifecycleState.Foreground);
    }

    /// <inheritdoc/>
    public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
    {
        SetActivity(activity, AndroidApplicationLifecycleState.Created);
        AndroidLogger.Write($"Activity created: {activity.GetType().FullName}.", diagnosticSink);
    }

    /// <inheritdoc/>
    public void OnActivityStarted(Activity activity)
        => SetActivity(activity, AndroidApplicationLifecycleState.Background);

    /// <inheritdoc/>
    public void OnActivityResumed(Activity activity)
    {
        SetActivity(activity, AndroidApplicationLifecycleState.Foreground);
        AndroidLogger.Write($"Activity resumed: {activity.GetType().FullName}.", diagnosticSink);
    }

    /// <inheritdoc/>
    public void OnActivityPaused(Activity activity)
    {
        SetStateIfCurrent(activity, AndroidApplicationLifecycleState.Background);
        AndroidLogger.Write($"Activity paused: {activity.GetType().FullName}.", diagnosticSink);
    }

    /// <inheritdoc/>
    public void OnActivityStopped(Activity activity)
        => SetStateIfCurrent(activity, AndroidApplicationLifecycleState.Background);

    /// <inheritdoc/>
    public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
    {
    }

    /// <inheritdoc/>
    public void OnActivityDestroyed(Activity activity)
    {
        AndroidApplicationLifecycleState previousState;
        bool changed = false;
        lock (sync)
        {
            previousState = state;
            if (currentActivity.ClearIfCurrent(activity))
            {
                state = AndroidApplicationLifecycleState.NoActivity;
                changed = previousState != state;
            }
        }

        if (changed)
            RaiseLifecycleStateChanged(previousState, AndroidApplicationLifecycleState.NoActivity);
        ActivityDestroyed?.Invoke(activity);
        AndroidLogger.Write($"Activity destroyed: {activity.GetType().FullName}.", diagnosticSink);
    }

    private void SetActivity(Activity activity, AndroidApplicationLifecycleState newState)
    {
        AndroidApplicationLifecycleState previousState;
        lock (sync)
        {
            previousState = state;
            currentActivity.Set(activity);
            state = newState;
        }

        if (previousState != newState)
            RaiseLifecycleStateChanged(previousState, newState);
    }

    private void SetStateIfCurrent(Activity activity, AndroidApplicationLifecycleState newState)
    {
        AndroidApplicationLifecycleState previousState;
        bool changed = false;
        lock (sync)
        {
            previousState = state;
            if (ReferenceEquals(currentActivity.Target, activity))
            {
                state = newState;
                changed = previousState != newState;
            }
        }

        if (changed)
            RaiseLifecycleStateChanged(previousState, newState);
    }

    private void RaiseLifecycleStateChanged(
        AndroidApplicationLifecycleState previousState,
        AndroidApplicationLifecycleState currentState)
        => LifecycleStateChanged?.Invoke(
            this,
            new PlatformApplicationLifecycleChangedEventArgs(
                ToPlatformState(previousState),
                ToPlatformState(currentState)));

    private static PlatformApplicationLifecycleState ToPlatformState(AndroidApplicationLifecycleState value)
        => value switch
        {
            AndroidApplicationLifecycleState.Foreground => PlatformApplicationLifecycleState.Foreground,
            AndroidApplicationLifecycleState.Background or AndroidApplicationLifecycleState.Created =>
                PlatformApplicationLifecycleState.Background,
            AndroidApplicationLifecycleState.NoActivity => PlatformApplicationLifecycleState.NoHost,
            _ => PlatformApplicationLifecycleState.Unknown
        };

    private static bool IsUsable(Activity? activity)
        => activity is not null && !activity.IsFinishing && !activity.IsDestroyed;
}
