using Android.App;
using Android.Content;
using Android.Content.Res;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Backend.Android;

// This is the existing IPlatformSettings registration with an optional capability, not a second
// preference registry. Native listeners borrow only the application context; Activity is queried
// from the canonical weak tracker for every read and is never retained by a subscription.
internal sealed class AndroidPlatformSettings : DefaultPlatformSettings, IPlatformAccessibilitySettings, IPlatformThemeSettingsNotifications, IDisposable
{
    private readonly Context context;
    private readonly AndroidActivityTracker tracker;
    private readonly AndroidMainThreadDispatcher dispatcher;
    private readonly Action<string>? diagnosticSink;
    private readonly UiModeManager? uiMode;
    private PlatformAccessibilityPreferences current = new();
    private EventHandler<PlatformAccessibilityPreferences>? changed;
    private EventHandler? appearanceChanged;
    private PlatformColorScheme observedScheme;
    private NativeListener? listener;
    private bool configurationRegistered, contrastRegistered, disposed;
    private long listenerGeneration;

    internal AndroidPlatformSettings(Context context, AndroidActivityTracker tracker, AndroidMainThreadDispatcher dispatcher, Action<string>? diagnosticSink)
    {
        this.context = context;
        this.tracker = tracker;
        this.dispatcher = dispatcher;
        this.diagnosticSink = diagnosticSink;
        uiMode = context.GetSystemService(Context.UiModeService) as UiModeManager;
        tracker.Publisher.LifecycleChanged += OnLifecycleChanged;
    }

    public PlatformAccessibilityPreferences GetAccessibilityPreferences() { VerifyAccess(); Refresh(); return current; }
    public override PlatformColorValues GetColorValues() => current.ColorValues ?? base.GetColorValues();

    public event EventHandler<PlatformAccessibilityPreferences>? AccessibilityPreferencesChanged
    {
        add
        {
            VerifyAccess();
            changed += value;
            UpdateObservation();
        }
        remove
        {
            if (disposed) return;
            VerifyAccess();
            changed -= value;
            UpdateObservation();
        }
    }

    public PlatformColorScheme GetPreferredVariant()
    {
        // The process context is available before the first Activity and during recreation.
        try { return AndroidAppearanceMode.GetColorScheme((int?)context.Resources?.Configuration?.UiMode); }
        catch (Java.Lang.Exception) { return PlatformColorScheme.Unknown; }
    }

    public bool? GetReducedMotion() => null;

    public event EventHandler? PreferredVariantChanged
    {
        add
        {
            VerifyAccess();
            if (appearanceChanged is null) observedScheme = GetPreferredVariant();
            appearanceChanged += value;
            UpdateObservation();
        }
        remove
        {
            if (disposed) return;
            VerifyAccess();
            appearanceChanged -= value;
            UpdateObservation();
        }
    }

    private void OnLifecycleChanged(object? sender, PlatformApplicationLifecycleEventArgs e)
    {
        if (disposed) return;
        Report(UpdateObservation);
        RefreshObservedPreferences();
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!dispatcher.CheckAccess()) throw new InvalidOperationException("Android accessibility preferences require the UI thread.");
    }

    private void Refresh()
    {
        VerifyAccess();
        var activity = tracker.CurrentActivity;
        PlatformAccessibilityPreferences next;
        if (activity is null) next = new();
        else
        {
            float? contrast = null;
            if (OperatingSystem.IsAndroidVersionAtLeast(34) && uiMode is not null)
                try { contrast = uiMode.Contrast; } catch (Java.Lang.Exception) { }
            var configuration = activity.Resources?.Configuration;
            next = AndroidAccessibilityPreferences.Create(true, configuration?.FontScale, contrast,
                configuration is not null && (configuration.UiMode & UiMode.NightMask) == UiMode.NightYes);
        }
        if (next == current) return;
        var previous = current;
        current = next;
        var failures = new List<Exception>();
        if (changed is { } handlers)
            foreach (EventHandler<PlatformAccessibilityPreferences> handler in handlers.GetInvocationList())
            {
                if (disposed || current != next) break;
                try { handler(this, next); } catch (Exception error) { failures.Add(error); }
            }
        if (!disposed && current == next && previous.ColorValues != next.ColorValues && next.ColorValues is { } colors)
            try { OnColorValuesChanged(colors); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Preference observers failed.", failures);
    }

    private void RefreshObservedPreferences()
    {
        Report(Refresh);
        if (disposed || appearanceChanged is not { } handlers) return;
        PlatformColorScheme next = GetPreferredVariant();
        if (observedScheme == next) return;
        observedScheme = next;
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            if (disposed || observedScheme != next) break;
            Report(() => handler(this, EventArgs.Empty));
        }
    }

    private void UpdateObservation()
    {
        bool demand = !disposed && (appearanceChanged is not null || changed is not null && tracker.CurrentActivity is not null);
        if (!demand)
        {
            RetireObservation();
            return;
        }
        if (listener is not null) return;
        long version = ++listenerGeneration;
        listener = new NativeListener(dispatcher, () =>
        {
            if (!disposed && version == listenerGeneration) RefreshObservedPreferences();
        });
        try
        {
            // Mark attempted registrations first: a platform call may fail after taking
            // ownership. Rollback can then unregister both fully and partially added hooks.
            configurationRegistered = true;
            context.RegisterComponentCallbacks(listener);
            if (OperatingSystem.IsAndroidVersionAtLeast(34) && uiMode is not null && context.MainExecutor is { } executor)
            {
                contrastRegistered = true;
                uiMode.AddContrastChangeListener(executor, listener);
            }
        }
        catch (Exception error)
        {
            // A failed first registration must not leave a non-null, inactive listener that
            // suppresses subsequent lifecycle/subscription attempts. Revoke before logging.
            RetireObservation();
            AndroidLogger.Write($"Accessibility preference observation unavailable: {error.GetType().Name}", diagnosticSink);
        }
    }

    private void RetireObservation()
    {
        listenerGeneration++;
        var old = listener;
        listener = null;
        bool removeContrast = contrastRegistered, removeConfiguration = configurationRegistered;
        contrastRegistered = configurationRegistered = false;
        if (old is null) return;
        // Clear managed delivery before native unregister; a late Binder callback is inert.
        old.Retire();
        try
        {
            if (removeContrast)
                Report(() =>
                {
                    if (OperatingSystem.IsAndroidVersionAtLeast(34)) uiMode?.RemoveContrastChangeListener(old);
                });
            if (removeConfiguration) Report(() => context.UnregisterComponentCallbacks(old));
        }
        finally { old.Dispose(); }
    }

    public void Dispose()
    {
        if (disposed) return;
        VerifyAccess();
        disposed = true;
        changed = null;
        appearanceChanged = null;
        tracker.Publisher.LifecycleChanged -= OnLifecycleChanged;
        UpdateObservation();
        current = new();
    }

    private void Report(Action action)
    {
        try { action(); }
        catch (Exception error) { AndroidLogger.Write($"Accessibility preference notification: {error.GetType().Name}", diagnosticSink); }
    }

    private sealed class NativeListener(AndroidMainThreadDispatcher dispatcher, Action notify)
        : Java.Lang.Object, IComponentCallbacks, UiModeManager.IContrastChangeListener
    {
        private Action? notify = notify;
        private int pending;

        internal void Retire() => Interlocked.Exchange(ref notify, null);
        public void OnConfigurationChanged(Configuration newConfig) => PublishChange();
        public void OnLowMemory() { }
        public void OnContrastChanged(float contrast) => PublishChange();

        private void PublishChange()
        {
            if (Volatile.Read(ref notify) is null || Interlocked.Exchange(ref pending, 1) != 0) return;
            try
            {
                // Read the latest configuration on the UI thread. Multiple native reports can
                // describe the same change, and a retired listener cannot deliver queued work.
                dispatcher.Post(() =>
                {
                    Interlocked.Exchange(ref pending, 0);
                    Volatile.Read(ref notify)?.Invoke();
                });
            }
            catch (Exception error)
            {
                Interlocked.Exchange(ref pending, 0);
                System.Diagnostics.Debug.WriteLine($"Preference callback: {error.GetType().Name}");
            }
        }
    }
}
