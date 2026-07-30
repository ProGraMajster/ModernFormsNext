using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal static class AndroidAnimationScaleEvaluator
{
    internal const string SourceName = "Android Settings.Global animation scales";

    public static PlatformAnimationSettingsSnapshot CreateSnapshot(
        float? animatorDurationScale,
        float? transitionAnimationScale,
        DateTimeOffset lastUpdate,
        string? error = null)
    {
        if (error is not null ||
            animatorDurationScale is not { } animator ||
            transitionAnimationScale is not { } transition ||
            !float.IsFinite(animator) ||
            !float.IsFinite(transition) ||
            animator < 0f ||
            transition < 0f)
        {
            return new PlatformAnimationSettingsSnapshot(
                SourceName,
                reducedMotion: false,
                animationsEnabled: true,
                lastUpdate,
                fallbackUsed: true,
                PlatformAnimationProviderState.Fallback,
                error ?? "Android animation scales are unavailable or invalid.");
        }

        bool enabled = animator > 0f && transition > 0f;
        return new PlatformAnimationSettingsSnapshot(
            SourceName,
            reducedMotion: !enabled,
            animationsEnabled: enabled,
            lastUpdate,
            fallbackUsed: false,
            PlatformAnimationProviderState.Ready,
            lastError: null);
    }
}
