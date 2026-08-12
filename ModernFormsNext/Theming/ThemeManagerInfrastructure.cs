using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Threading;
using SkiaSharp;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

internal interface IThemeDispatcher
{
    bool CheckAccess();
    Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken);
    void Post(Action action);
}

internal sealed class DefaultThemeDispatcher : IThemeDispatcher
{
    public bool CheckAccess()
        => PlatformServiceRegistry.GetService<IPlatformDispatcher>()?.CheckAccess() ?? Dispatcher.UIThread.CheckAccess();

    public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();
        if (CheckAccess())
            return Task.FromResult(function());

        IPlatformDispatcher? platformDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        if (platformDispatcher is not null)
            return platformDispatcher.InvokeAsync(function, cancellationToken);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                completion.TrySetResult(function());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        IPlatformDispatcher? platformDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        if (platformDispatcher is not null)
            platformDispatcher.Post(action);
        else
            Dispatcher.UIThread.Post(action);
    }
}

internal interface IThemeEnvironment
{
    bool IsDesignMode { get; }
    ThemeVariant GetSystemVariant(ThemeVariant fallback);
    bool IsReducedMotionRequested { get; }
}

internal interface IThemeLegacyStore
{
    Dictionary<string, object> GetSnapshot();
    void Replace(IReadOnlyDictionary<string, object> values);
    void NotifyChanged();
}

internal sealed class DefaultThemeLegacyStore : IThemeLegacyStore
{
    public Dictionary<string, object> GetSnapshot() => Theme.GetValueSnapshot();

    public void Replace(IReadOnlyDictionary<string, object> values)
        => Theme.ReplaceValuesWithoutNotification(values);

    public void NotifyChanged() => Theme.NotifyChanged();
}

internal sealed class DefaultThemeEnvironment : IThemeEnvironment
{
    public bool IsDesignMode => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

    public ThemeVariant GetSystemVariant(ThemeVariant fallback)
    {
        if (IsDesignMode)
            return fallback;

        return PlatformServiceRegistry.GetService<IPlatformThemeSettings>()?.GetPreferredVariant() switch
        {
            PlatformColorScheme.Light => ThemeVariant.Light,
            PlatformColorScheme.Dark => ThemeVariant.Dark,
            _ => fallback
        };
    }

    public bool IsReducedMotionRequested
        => !IsDesignMode && PlatformServiceRegistry.GetService<IPlatformThemeSettings>()?.GetReducedMotion() == true;
}

internal static class ThemeLegacyProjector
{
    public static Dictionary<string, object> Create(
        ThemeResolvedSnapshot snapshot,
        IReadOnlyDictionary<string, object> currentValues)
    {
        var values = new Dictionary<string, object>(currentValues, StringComparer.Ordinal);
        SetColor(values, snapshot, nameof(Theme.BackgroundColor), ThemeTokens.Colors.Background.Name);
        SetColor(values, snapshot, nameof(Theme.BorderLowColor), ThemeTokens.Colors.Border.Name);
        SetColor(values, snapshot, nameof(Theme.BorderMidColor), ThemeTokens.Colors.Divider.Name);
        SetColor(values, snapshot, nameof(Theme.BorderHighColor), ThemeTokens.Colors.TextSecondary.Name);
        SetColor(values, snapshot, nameof(Theme.ControlLowColor), ThemeTokens.Colors.Surface.Name);
        SetColor(values, snapshot, nameof(Theme.ControlMidColor), ThemeTokens.Colors.SurfaceVariant.Name);
        SetColor(values, snapshot, nameof(Theme.ControlMidHighColor), ThemeTokens.Colors.Divider.Name);
        SetColor(values, snapshot, nameof(Theme.ControlHighColor), ThemeTokens.Colors.Border.Name);
        SetColor(values, snapshot, nameof(Theme.ControlVeryHighColor), ThemeTokens.Colors.TextSecondary.Name);
        SetColor(values, snapshot, nameof(Theme.ControlHighlightLowColor), ThemeTokens.Colors.PrimaryHover.Name);
        SetColor(values, snapshot, nameof(Theme.ControlHighlightMidColor), ThemeTokens.Colors.Primary.Name);
        SetColor(values, snapshot, nameof(Theme.ControlHighlightHighColor), ThemeTokens.Colors.PrimaryPressed.Name);
        SetColor(values, snapshot, nameof(Theme.ForegroundColor), ThemeTokens.Colors.TextPrimary.Name);
        SetColor(values, snapshot, nameof(Theme.ForegroundDisabledColor), ThemeTokens.Colors.TextDisabled.Name);
        SetColor(values, snapshot, nameof(Theme.ForegroundColorOnAccent), ThemeTokens.Colors.PrimaryText.Name);
        SetColor(values, snapshot, nameof(Theme.AccentColor), ThemeTokens.Colors.Primary.Name);
        SetColor(values, snapshot, nameof(Theme.AccentColor2), ThemeTokens.Colors.Secondary.Name);
        SetColor(values, snapshot, nameof(Theme.TextSelectionBackgroundColor), ThemeTokens.Colors.Selection.Name);
        SetColor(values, snapshot, nameof(Theme.WarningHighlightColor), ThemeTokens.Colors.Error.Name);

        if (snapshot.Typography.TryGetValue(ThemeTokens.Typography.Body.Name, out ThemeTypography? body))
        {
            values[nameof(Theme.UIFont)] = body.ToFont().ToTypeface();
            values[nameof(Theme.FontSize)] = Math.Max(1, (int)Math.Round(body.Size));
        }
        if (snapshot.Typography.TryGetValue(ThemeTokens.Typography.Caption.Name, out ThemeTypography? caption))
            values[nameof(Theme.ItemFontSize)] = Math.Max(1, (int)Math.Round(caption.Size));
        if (snapshot.Typography.TryGetValue(ThemeTokens.Typography.Heading.Name, out ThemeTypography? heading))
            values[nameof(Theme.UIFontBold)] = heading.ToFont().ToTypeface();
        return values;
    }

    private static void SetColor(
        Dictionary<string, object> values,
        ThemeResolvedSnapshot snapshot,
        string legacyName,
        string semanticName)
    {
        if (!snapshot.Colors.TryGetValue("Legacy." + legacyName, out Color color) &&
            !snapshot.Colors.TryGetValue(semanticName, out color))
        {
            return;
        }

        values[legacyName] = new SKColor(color.R, color.G, color.B, color.A);
    }
}

internal sealed class ThemeTransitionPlan
{
    private readonly List<ResourceColorTransition> resourceColors = [];
    private readonly List<ResourceNumberTransition> resourceNumbers = [];
    private readonly List<ResourceBrushTransition> resourceBrushes = [];
    private readonly List<LegacyColorTransition> legacyColors = [];

    private ThemeTransitionPlan(
        Dictionary<object, object?> resources,
        Dictionary<string, object> legacyValues)
    {
        Resources = resources;
        LegacyValues = legacyValues;
    }

    public Dictionary<object, object?> Resources { get; }
    public Dictionary<string, object> LegacyValues { get; }
    public bool HasAnimations => resourceColors.Count > 0 || resourceNumbers.Count > 0 || resourceBrushes.Count > 0 || legacyColors.Count > 0;

    public static ThemeTransitionPlan Create(
        Dictionary<object, object?> currentResources,
        Dictionary<object, object?> targetResources,
        Dictionary<string, object> currentLegacy,
        Dictionary<string, object> targetLegacy)
    {
        var plan = new ThemeTransitionPlan(targetResources, targetLegacy);
        foreach ((object key, object? targetValue) in targetResources.ToArray())
        {
            if (!currentResources.TryGetValue(key, out object? currentValue))
                continue;

            switch (currentValue, targetValue)
            {
                case (Color from, Color to) when from.ToArgb() != to.ToArgb():
                    targetResources[key] = from;
                    plan.resourceColors.Add(new ResourceColorTransition(key, from, to));
                    break;
                case (double from, double to) when !from.Equals(to) &&
                    key is string resourceKey && resourceKey.StartsWith("Theme.Resource.", StringComparison.Ordinal):
                    targetResources[key] = from;
                    plan.resourceNumbers.Add(new ResourceNumberTransition(key, from, to));
                    break;
                case (MfnBrush from, MfnBrush to):
                    TryAddBrush(plan, key, from, to, targetResources);
                    break;
            }
        }

        foreach ((string key, object targetValue) in targetLegacy.ToArray())
        {
            if (targetValue is SKColor to && currentLegacy.TryGetValue(key, out object? currentValue) &&
                currentValue is SKColor from && from != to)
            {
                targetLegacy[key] = from;
                plan.legacyColors.Add(new LegacyColorTransition(key, from, to));
            }
        }
        return plan;
    }

    public void Apply(
        float progress,
        ResourceDictionary resourceDictionary,
        IThemeLegacyStore legacyStore)
    {
        using Application.ThemeTransitionFrameScope transitionFrame = Application.BeginThemeTransitionFrame();
        using Application.VisualInvalidationBatchScope batch = Application.BeginVisualInvalidationBatch();

        foreach (ResourceBrushTransition transition in resourceBrushes)
            Resources[transition.Key] = transition.Plan.Interpolate(progress);

        foreach (ResourceColorTransition transition in resourceColors)
            Resources[transition.Key] = AnimationInterpolators.Color.Interpolate(transition.From, transition.To, progress);
        foreach (ResourceNumberTransition transition in resourceNumbers)
            Resources[transition.Key] = AnimationInterpolators.Double.Interpolate(transition.From, transition.To, progress);

        foreach (LegacyColorTransition transition in legacyColors)
            LegacyValues[transition.Key] = Interpolate(transition.From, transition.To, progress);

        // Make both compatibility surfaces visible before invoking either observer set. A failing
        // resource setter can fault the scheduler entry, but it cannot leave the resource and
        // legacy projections at different progress values.
        ResourceDictionaryChange[] changes = resourceDictionary.ReplaceSnapshot(Resources);
        legacyStore.Replace(LegacyValues);
        resourceDictionary.PublishChanges(changes);
        legacyStore.NotifyChanged();
    }

    private static void TryAddBrush(
        ThemeTransitionPlan plan,
        object key,
        MfnBrush from,
        MfnBrush to,
        Dictionary<object, object?> targetResources)
    {
        if (!BrushAnimationPlan.TryCreateLocal(from, to, out BrushAnimationPlan? brushPlan))
            return;

        // The exact source reference remains visible at progress zero. Intermediate frames reuse
        // the plan's private working brush, and completion replaces it with the exact resolved
        // target reference so resource identity and notifications remain deterministic.
        targetResources[key] = from;
        plan.resourceBrushes.Add(new ResourceBrushTransition(key, brushPlan!));
    }

    private static SKColor Interpolate(SKColor from, SKColor to, float progress)
    {
        Color result = AnimationInterpolators.Color.Interpolate(
            Color.FromArgb(from.Alpha, from.Red, from.Green, from.Blue),
            Color.FromArgb(to.Alpha, to.Red, to.Green, to.Blue),
            progress);
        return new SKColor(result.R, result.G, result.B, result.A);
    }

    private sealed record ResourceColorTransition(object Key, Color From, Color To);
    private sealed record ResourceNumberTransition(object Key, double From, double To);
    private sealed record ResourceBrushTransition(object Key, BrushAnimationPlan Plan);
    private sealed record LegacyColorTransition(string Key, SKColor From, SKColor To);
}
