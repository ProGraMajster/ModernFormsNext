using System.ComponentModel;
using System.Diagnostics;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.Animations;

public sealed partial class AnimationScheduler
{
    private readonly Func<bool> isDesignMode;
    private IPlatformAnimationSettings? platformAnimationSettings;
    private PlatformAnimationSettingsSnapshot platformAnimationSnapshot = CreateUnavailableSnapshot();

    /// <summary>
    /// Refreshes native reduced-motion settings and schedules policy mutation on the UI thread.
    /// </summary>
    /// <remarks>
    /// This method is safe to call from any thread. In Designer mode it has no platform side
    /// effects. A posted policy update can complete active animations at their final value through
    /// the existing visual-invalidation batch; it does not request layout.
    /// </remarks>
    public void RefreshPlatformPolicy()
    {
        BindPlatformAnimationSettingsIfAvailable();

        bool designMode = isDesignMode();
        IPlatformAnimationSettings? provider;
        lock (sync)
        {
            if (isShutdown || designMode)
                return;
            provider = platformAnimationSettings;
        }

        if (provider is not null)
            ApplyPlatformAnimationSnapshot(provider, provider.Refresh());
    }

    /// <summary>Returns a thread-safe snapshot of native reduced-motion diagnostics.</summary>
    public AnimationPlatformDiagnostics GetPlatformDiagnostics()
    {
        BindPlatformAnimationSettingsIfAvailable();

        PlatformAnimationSettingsSnapshot snapshot;
        lock (sync)
            snapshot = platformAnimationSnapshot;

        bool reducedMotion = Policy.ReducedMotion;
        return new AnimationPlatformDiagnostics(
            snapshot.Source,
            reducedMotion,
            Policy.AnimationsEnabled && snapshot.AnimationsEnabled &&
                snapshot.DurationScale > 0d && !reducedMotion,
            snapshot.DurationScale,
            snapshot.LastPlatformUpdate,
            snapshot.FallbackUsed,
            snapshot.ProviderState,
            snapshot.LastError);
    }

    private void BindPlatformAnimationSettingsIfAvailable()
    {
        bool designMode = isDesignMode();
        lock (sync)
        {
            if (platformAnimationSettings is not null || isShutdown || designMode)
            {
                if (designMode)
                    platformAnimationSnapshot = CreateDesignerSnapshot();
                return;
            }
        }

        IPlatformAnimationSettings? provider = PlatformServiceRegistry.GetService<IPlatformAnimationSettings>();
        if (provider is not null)
            BindPlatformAnimationSettings(provider);
    }

    private void BindPlatformAnimationSettings(IPlatformAnimationSettings provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        bool designMode = isDesignMode();

        lock (sync)
        {
            if (platformAnimationSettings is not null || isShutdown || designMode)
            {
                if (designMode)
                    platformAnimationSnapshot = CreateDesignerSnapshot();
                return;
            }

            platformAnimationSettings = provider;
            provider.Changed += HandlePlatformAnimationSettingsChanged;
        }

        ApplyPlatformAnimationSnapshot(provider, provider.Current);
    }

    private void HandlePlatformAnimationSettingsChanged(
        object? sender,
        PlatformAnimationSettingsChangedEventArgs e)
    {
        if (sender is IPlatformAnimationSettings provider)
            ApplyPlatformAnimationSnapshot(provider, e.Current);
    }

    private void ApplyPlatformAnimationSnapshot(
        IPlatformAnimationSettings provider,
        PlatformAnimationSettingsSnapshot snapshot)
    {
        lock (sync)
        {
            if (isShutdown || !ReferenceEquals(platformAnimationSettings, provider))
                return;
            if (ReferenceEquals(platformAnimationSnapshot, snapshot))
                return;
            platformAnimationSnapshot = snapshot;
        }

        void ApplyPolicy()
        {
            lock (sync)
            {
                if (isShutdown || !ReferenceEquals(platformAnimationSettings, provider))
                    return;
            }

            Policy.SetPlatformAnimationSettings(
                snapshot.ReducedMotion || !snapshot.AnimationsEnabled,
                snapshot.DurationScale);
        }

        try
        {
            if (dispatcher.CheckAccess())
                ApplyPolicy();
            else
                dispatcher.Post(ApplyPolicy);
        }
        catch (Exception exception)
        {
            // A backend notification must never tear down its native message or lifecycle callback.
            // The next explicit refresh or animation start retries binding and policy application.
            Trace.TraceError($"Failed to dispatch the platform animation policy update: {exception}");
        }
    }

    private void RefreshPlatformPolicyAfterForeground()
        => RefreshPlatformPolicy();

    private void UnbindPlatformAnimationSettings()
    {
        IPlatformAnimationSettings? provider;
        lock (sync)
        {
            provider = platformAnimationSettings;
            platformAnimationSettings = null;
        }

        if (provider is not null)
            provider.Changed -= HandlePlatformAnimationSettingsChanged;
    }

    private static bool IsProcessInDesignMode()
        => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

    private static PlatformAnimationSettingsSnapshot CreateUnavailableSnapshot()
        => new(
            "No platform animation provider",
            reducedMotion: false,
            animationsEnabled: true,
            lastPlatformUpdate: null,
            fallbackUsed: true,
            PlatformAnimationProviderState.Unavailable,
            lastError: null);

    private static PlatformAnimationSettingsSnapshot CreateDesignerSnapshot()
        => new(
            "Designer (platform integration disabled)",
            reducedMotion: false,
            animationsEnabled: true,
            lastPlatformUpdate: null,
            fallbackUsed: false,
            PlatformAnimationProviderState.Disabled,
            lastError: null);
}
