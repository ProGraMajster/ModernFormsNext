using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal sealed class AndroidPlatformAnimationSettings : IPlatformAnimationSettings, IDisposable
{
    private readonly object sync = new();
    private readonly object observerLifecycleSync = new();
    private readonly Context? applicationContext;
    private EventHandler<PlatformAnimationSettingsChangedEventArgs>? changed;
    private PlatformAnimationSettingsSnapshot current;
    private AnimationScaleContentObserver? observer;
    private bool hostActive;
    private bool observerRegistered;
    private bool disposed;
    private string? lastObserverError;

    public AndroidPlatformAnimationSettings(Context? applicationContext)
    {
        this.applicationContext = applicationContext;
        current = AndroidAnimationScaleEvaluator.CreateSnapshot(
            null,
            DateTimeOffset.UtcNow,
            "Android application context is unavailable.");
        Refresh();
    }

    public PlatformAnimationSettingsSnapshot Current
    {
        get
        {
            lock (sync)
                return current;
        }
    }

    public event EventHandler<PlatformAnimationSettingsChangedEventArgs>? Changed
    {
        add
        {
            bool updateObservation;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                updateObservation = changed is null;
                changed += value;
            }
            if (updateObservation)
                UpdateObserverRegistration();
        }
        remove
        {
            bool updateObservation;
            lock (sync)
            {
                changed -= value;
                updateObservation = changed is null;
            }
            if (updateObservation)
                UpdateObserverRegistration();
        }
    }

    internal bool IsObserverRegistered
    {
        get
        {
            lock (sync)
                return observerRegistered;
        }
    }

    internal string? LastObserverError
    {
        get
        {
            lock (sync)
                return lastObserverError;
        }
    }

    internal void SetHostActive(bool active)
    {
        bool changedState;
        lock (sync)
        {
            if (disposed)
                return;
            changedState = hostActive != active;
            hostActive = active;
        }

        if (!changedState)
            return;

        UpdateObserverRegistration();
        if (active)
            Refresh();
    }

    public PlatformAnimationSettingsSnapshot Refresh()
    {
        lock (sync)
        {
            if (disposed)
                return current;
        }

        DateTimeOffset update = DateTimeOffset.UtcNow;
        PlatformAnimationSettingsSnapshot next;
        try
        {
            ContentResolver? resolver = applicationContext?.ContentResolver;
            if (resolver is null)
            {
                next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                    null,
                    update,
                    "Android application context is unavailable.");
            }
            else
            {
                float animatorScale = Settings.Global.GetFloat(
                    resolver,
                    Settings.Global.AnimatorDurationScale,
                    1f);
                next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                    animatorScale,
                    update);
            }
        }
        catch (Exception exception)
        {
            next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                null,
                update,
                exception.Message);
        }

        EventHandler<PlatformAnimationSettingsChangedEventArgs>? handlers;
        PlatformAnimationSettingsSnapshot previous;
        bool meaningfulChange;
        lock (sync)
        {
            previous = current;
            current = next;
            meaningfulChange = previous.ReducedMotion != next.ReducedMotion
                || previous.AnimationsEnabled != next.AnimationsEnabled
                || previous.DurationScale != next.DurationScale
                || previous.FallbackUsed != next.FallbackUsed
                || previous.ProviderState != next.ProviderState
                || !string.Equals(previous.LastError, next.LastError, StringComparison.Ordinal);
            handlers = meaningfulChange ? changed : null;
        }

        handlers?.Invoke(this, new PlatformAnimationSettingsChangedEventArgs(previous, next));
        return next;
    }

    public void Dispose()
    {
        lock (observerLifecycleSync)
        {
            AnimationScaleContentObserver? observerToDispose;
            bool unregister;
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;
                hostActive = false;
                changed = null;
                unregister = observerRegistered;
                observerRegistered = false;
                observerToDispose = observer;
                observer = null;
            }

            if (unregister && applicationContext?.ContentResolver is { } resolver && observerToDispose is not null)
            {
                try
                {
                    resolver.UnregisterContentObserver(observerToDispose);
                }
                catch (Exception)
                {
                    // Android owns no Activity reference here. A failed final unregister is safe
                    // to ignore during process shutdown after managed callbacks were released.
                }
            }

            observerToDispose?.Dispose();
        }
    }

    private void UpdateObserverRegistration()
    {
        // Subscriber and lifecycle notifications can arrive on different threads. Serialize the
        // native transition as well as the managed decision so an unregister cannot overtake an
        // in-flight register and leave an untracked ContentObserver behind.
        lock (observerLifecycleSync)
        {
            ContentResolver? resolver = applicationContext?.ContentResolver;
            AnimationScaleContentObserver? currentObserver;
            bool register;
            lock (sync)
            {
                bool shouldRegister = !disposed && hostActive && changed is not null && resolver is not null;
                if (shouldRegister == observerRegistered)
                    return;

                observer ??= shouldRegister ? new AnimationScaleContentObserver(this) : null;
                currentObserver = observer;
                register = shouldRegister;
                observerRegistered = shouldRegister;
                lastObserverError = null;
            }

            if (resolver is null || currentObserver is null)
                return;

            try
            {
                if (register)
                {
                    global::Android.Net.Uri uri = Settings.Global.GetUriFor(Settings.Global.AnimatorDurationScale)
                        ?? throw new InvalidOperationException(
                            "Android did not expose the animator-duration setting URI.");
                    resolver.RegisterContentObserver(
                        uri,
                        false,
                        currentObserver);
                }
                else
                {
                    resolver.UnregisterContentObserver(currentObserver);
                }
            }
            catch (Exception exception)
            {
                lock (sync)
                {
                    // A failed registration did not create a live subscription. A failed
                    // unregistration may still have one, so retain that state and retry when the
                    // lifecycle or subscriber set changes again.
                    observerRegistered = !register;
                    lastObserverError = exception.Message;
                }
            }
        }
    }

    private sealed class AnimationScaleContentObserver : ContentObserver
    {
        private readonly AndroidPlatformAnimationSettings owner;

        public AnimationScaleContentObserver(AndroidPlatformAnimationSettings owner)
            : base(new Handler(Looper.MainLooper
                ?? throw new InvalidOperationException("Android did not provide a main Looper.")))
        {
            this.owner = owner;
        }

        public override void OnChange(bool selfChange)
        {
            base.OnChange(selfChange);
            owner.Refresh();
        }
    }
}
