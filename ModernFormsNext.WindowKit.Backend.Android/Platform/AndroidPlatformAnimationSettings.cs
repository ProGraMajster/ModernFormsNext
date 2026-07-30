using Android.Content;
using Android.Provider;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal sealed class AndroidPlatformAnimationSettings : IPlatformAnimationSettings
{
    private readonly object sync = new();
    private readonly Context? applicationContext;
    private EventHandler<PlatformAnimationSettingsChangedEventArgs>? changed;
    private PlatformAnimationSettingsSnapshot current;

    public AndroidPlatformAnimationSettings(Context? applicationContext)
    {
        this.applicationContext = applicationContext;
        current = AndroidAnimationScaleEvaluator.CreateSnapshot(
            null,
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
            lock (sync)
                changed += value;
        }
        remove
        {
            lock (sync)
                changed -= value;
        }
    }

    public PlatformAnimationSettingsSnapshot Refresh()
    {
        DateTimeOffset update = DateTimeOffset.UtcNow;
        PlatformAnimationSettingsSnapshot next;
        try
        {
            ContentResolver? resolver = applicationContext?.ContentResolver;
            if (resolver is null)
            {
                next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                    null,
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
                float transitionScale = Settings.Global.GetFloat(
                    resolver,
                    Settings.Global.TransitionAnimationScale,
                    1f);
                next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                    animatorScale,
                    transitionScale,
                    update);
            }
        }
        catch (Exception exception)
        {
            next = AndroidAnimationScaleEvaluator.CreateSnapshot(
                null,
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
                || previous.FallbackUsed != next.FallbackUsed
                || previous.ProviderState != next.ProviderState
                || !string.Equals(previous.LastError, next.LastError, StringComparison.Ordinal);
            handlers = meaningfulChange ? changed : null;
        }

        handlers?.Invoke(this, new PlatformAnimationSettingsChangedEventArgs(previous, next));
        return next;
    }
}
