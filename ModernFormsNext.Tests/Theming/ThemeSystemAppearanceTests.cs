using System.Drawing;
using ModernFormsNext.WindowKit.Backend;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ThemeSystemAppearanceTests
{
    [Fact]
    public async Task CoalescedUpdatesApplyTheLatestPaletteWithAuthoredOverrides()
    {
        var dispatcher = new QueuedThemeDispatcher();
        using var harness = new ThemeManagerTestHarness(dispatcher: dispatcher);
        var signals = new AppearanceSignals();
        harness.Environment.Notifications = signals;
        var definition = new ThemeDefinition("sample.system", "System") { Variant = ThemeVariant.System };
        definition.Colors[ThemeTokens.Colors.Primary.Name] = Color.Purple;
        var options = new ThemeApplyOptions { FollowSystemAppearance = true, TextScale = 1.25 };
        Task<ThemeApplyResult> apply = harness.Manager.ApplyAsync(definition, options);
        dispatcher.Drain();
        Assert.True((await apply).Success);
        dispatcher.Drain();

        int changes = 0;
        harness.Manager.ThemeChanged += (_, _) => changes++;
        options.TextScale = 1;
        definition.Colors[ThemeTokens.Colors.Primary.Name] = Color.Green;
        harness.Environment.SystemVariant = ThemeVariant.Dark;
        await Task.Run(() => { signals.Raise(); signals.Raise(); });
        Assert.Equal(1, dispatcher.PendingCount);
        Assert.Equal(ThemeVariant.Light, harness.Manager.ActiveVariant);
        dispatcher.Drain();

        ThemeResolvedSnapshot snapshot = Assert.IsType<ThemeResolvedSnapshot>(harness.Manager.ActiveSnapshot);
        Assert.Equal(ThemeVariant.Dark, snapshot.Variant);
        Assert.Equal(BuiltInThemes.Dark.Colors[ThemeTokens.Colors.Background.Name], snapshot.Colors[ThemeTokens.Colors.Background.Name]);
        Assert.Equal(Color.Purple, snapshot.Colors[ThemeTokens.Colors.Primary.Name]);
        Assert.Equal(17.5f, snapshot.Typography[ThemeTokens.Typography.Body.Name].Size);
        Assert.Null(harness.Manager.ActiveTheme!.BaseTheme);
        Assert.Equal(1, changes);

        signals.Raise();
        dispatcher.Drain();
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task PendingExplicitThemeWinsOverQueuedAppearanceAndRetiresSubscription()
    {
        var dispatcher = new QueuedThemeDispatcher();
        using var harness = new ThemeManagerTestHarness(dispatcher: dispatcher);
        var signals = new AppearanceSignals();
        harness.Environment.Notifications = signals;
        Task<ThemeApplyResult> first = harness.Manager.ApplyAsync(
            new ThemeDefinition("sample.system", "System") { Variant = ThemeVariant.System },
            new ThemeApplyOptions { FollowSystemAppearance = true });
        dispatcher.Drain();
        Assert.True((await first).Success);
        dispatcher.Drain();
        Assert.Equal(1, signals.Observers);

        harness.Environment.SystemVariant = ThemeVariant.Dark;
        signals.Raise();
        Task<ThemeApplyResult> explicitLight = harness.Manager.ApplyAsync(BuiltInThemes.Light);
        dispatcher.Drain();
        Assert.True((await explicitLight).Success);
        dispatcher.Drain();

        Assert.Equal(ThemeVariant.Light, harness.Manager.ActiveVariant);
        Assert.Equal(BuiltInThemes.LightThemeId, harness.Manager.ActiveTheme!.Id);
        Assert.Equal(0, signals.Observers);
        Assert.Equal(2, harness.Manager.GetDiagnostics().SuccessfulSwitches);
    }

    [Fact]
    public void UnknownAppearanceUsesConfiguredFallbackAndRespectsExplicitBase()
    {
        using var harness = new ThemeManagerTestHarness(systemVariant: ThemeVariant.Custom);
        var theme = new ThemeDefinition("sample.system", "System") { Variant = ThemeVariant.System };
        var options = new ThemeApplyOptions
        {
            FollowSystemAppearance = true,
            SystemFallbackVariant = ThemeVariant.Dark
        };

        ThemeApplyResult applied = harness.Manager.Apply(theme, options);
        Assert.True(applied.Success);
        Assert.Contains(applied.Diagnostics, item => item.Code == "THEME_SYSTEM_APPEARANCE_UNAVAILABLE");
        Assert.Equal(ThemeVariant.Dark, harness.Manager.ActiveVariant);
        Assert.Equal(BuiltInThemes.Dark.Colors[ThemeTokens.Colors.Background.Name],
            harness.Manager.ActiveSnapshot!.Colors[ThemeTokens.Colors.Background.Name]);

        theme.BaseTheme = BuiltInThemes.LightThemeId;
        Assert.True(harness.Manager.Apply(theme, options).Success);
        Assert.Equal(BuiltInThemes.Light.Colors[ThemeTokens.Colors.Background.Name],
            harness.Manager.ActiveSnapshot!.Colors[ThemeTokens.Colors.Background.Name]);
    }

    private sealed class AppearanceSignals : IPlatformThemeSettingsNotifications
    {
        public int Observers => PreferredVariantChanged?.GetInvocationList().Length ?? 0;
        public event EventHandler? PreferredVariantChanged;
        public PlatformColorScheme GetPreferredVariant() => PlatformColorScheme.Unknown;
        public bool? GetReducedMotion() => null;
        public void Raise() => PreferredVariantChanged?.Invoke(this, EventArgs.Empty);
    }
}
