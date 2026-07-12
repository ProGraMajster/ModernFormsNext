using Android.App;
using Android.OS;

namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

/// <summary>
/// Tracks the current Android activity through application lifecycle callbacks.
/// </summary>
/// <remarks>
/// Activities are stored through <see cref="WeakReference{T}"/> only. A paused activity is retained
/// weakly for diagnostics but is not returned as an active UI host. Rotation clears the destroyed
/// instance and allows the next created activity to become current.
/// </remarks>
public sealed class AndroidActivityTracker : Java.Lang.Object, Application.IActivityLifecycleCallbacks
{
    private readonly object sync = new();
    private readonly Func<Activity?>? activityProvider;
    private readonly Action<string>? diagnosticSink;
    private WeakReference<Activity>? currentActivity;
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
                    currentActivity is null ||
                    !currentActivity.TryGetTarget(out var activity) ||
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
        lock (sync)
        {
            if (currentActivity is not null &&
                currentActivity.TryGetTarget(out var current) &&
                ReferenceEquals(current, activity))
            {
                currentActivity = null;
                state = AndroidApplicationLifecycleState.NoActivity;
            }
        }

        ActivityDestroyed?.Invoke(activity);
        AndroidLogger.Write($"Activity destroyed: {activity.GetType().FullName}.", diagnosticSink);
    }

    private void SetActivity(Activity activity, AndroidApplicationLifecycleState newState)
    {
        lock (sync)
        {
            currentActivity = new WeakReference<Activity>(activity);
            state = newState;
        }
    }

    private void SetStateIfCurrent(Activity activity, AndroidApplicationLifecycleState newState)
    {
        lock (sync)
        {
            if (currentActivity is not null &&
                currentActivity.TryGetTarget(out var current) &&
                ReferenceEquals(current, activity))
            {
                state = newState;
            }
        }
    }

    private static bool IsUsable(Activity? activity)
        => activity is not null && !activity.IsFinishing && !activity.IsDestroyed;
}
