using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.WindowKit.Backend.Android;

internal static class AndroidAnimationScaleEvaluator
{
    private const float MaximumSupportedScale = 100f;
    internal const string SourceName = "Android Settings.Global animator_duration_scale";

    public static PlatformAnimationSettingsSnapshot CreateSnapshot(
        float? animatorDurationScale,
        DateTimeOffset lastUpdate,
        string? error = null)
    {
        if (error is not null ||
            animatorDurationScale is not { } animator ||
            !float.IsFinite(animator) ||
            animator < 0f ||
            animator > MaximumSupportedScale)
        {
            return new PlatformAnimationSettingsSnapshot(
                SourceName,
                reducedMotion: false,
                animationsEnabled: true,
                durationScale: 1d,
                lastUpdate,
                fallbackUsed: true,
                PlatformAnimationProviderState.Fallback,
                error ?? "Android animation scales are unavailable or invalid.");
        }

        bool enabled = animator > 0f;
        return new PlatformAnimationSettingsSnapshot(
            SourceName,
            reducedMotion: !enabled,
            animationsEnabled: enabled,
            durationScale: animator,
            lastUpdate,
            fallbackUsed: false,
            PlatformAnimationProviderState.Ready,
            lastError: null);
    }
}
