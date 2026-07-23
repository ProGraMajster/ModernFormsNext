using System.Drawing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext;

/// <summary>
/// Provides isolated authoring copies of the built-in ModernFormsNext themes.
/// </summary>
public static class BuiltInThemes
{
    /// <summary>The identifier of the shared built-in base theme.</summary>
    public const string BaseThemeId = "modernformsnext.base";
    /// <summary>The identifier of the built-in light theme.</summary>
    public const string LightThemeId = "modernformsnext.light";
    /// <summary>The identifier of the built-in dark theme.</summary>
    public const string DarkThemeId = "modernformsnext.dark";

    private static readonly ThemeDefinition BaseDefinition = CreateBase();
    private static readonly ThemeDefinition LightDefinition = CreateLight();
    private static readonly ThemeDefinition DarkDefinition = CreateDark();

    /// <summary>Gets a mutable, isolated copy of the shared base theme.</summary>
    public static ThemeDefinition Base => BaseDefinition.Clone();

    /// <summary>Gets a mutable, isolated copy of ModernFormsNext Light.</summary>
    public static ThemeDefinition Light => LightDefinition.Clone();

    /// <summary>Gets a mutable, isolated copy of ModernFormsNext Dark.</summary>
    public static ThemeDefinition Dark => DarkDefinition.Clone();

    /// <summary>Gets a built-in light or dark definition.</summary>
    /// <param name="variant">The requested effective variant.</param>
    /// <returns>An isolated built-in definition.</returns>
    public static ThemeDefinition Get(ThemeVariant variant) => variant switch
    {
        ThemeVariant.Dark => Dark,
        ThemeVariant.Light or ThemeVariant.System => Light,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "A built-in theme is available only for Light, Dark, or System.")
    };

    private static ThemeDefinition CreateBase()
    {
        var theme = new ThemeDefinition(BaseThemeId, "ModernFormsNext Base")
        {
            Description = "Shared typography, metrics, and motion defaults for built-in themes.",
            Author = "ModernFormsNext",
            Variant = ThemeVariant.Custom
        };
        theme.Typography[ThemeTokens.Typography.Body.Name] = new ThemeTypography("Segoe UI", 14f);
        theme.Typography[ThemeTokens.Typography.Caption.Name] = new ThemeTypography("Segoe UI", 12f);
        theme.Typography[ThemeTokens.Typography.Heading.Name] = new ThemeTypography("Segoe UI", 18f, FontStyle.Bold);
        theme.Typography[ThemeTokens.Typography.Title.Name] = new ThemeTypography("Segoe UI", 24f, FontStyle.Bold);
        theme.Typography[ThemeTokens.Typography.Button.Name] = new ThemeTypography("Segoe UI", 14f, FontStyle.Bold);
        theme.Typography[ThemeTokens.Typography.Input.Name] = new ThemeTypography("Segoe UI", 14f);
        theme.Spacing.Add("Small", 4d);
        theme.Spacing.Add("Medium", 8d);
        theme.Spacing.Add("Large", 16d);
        theme.Padding.Add("Control", new Padding(8, 4, 8, 4));
        theme.Padding.Add("Card", new Padding(16));
        theme.Sizing.Add("ControlHeight", 32d);
        theme.Sizing.Add("IconSmall", 16d);
        theme.Sizing.Add("IconMedium", 24d);
        theme.Corners.Add("Small", 3d);
        theme.Corners.Add("Medium", 6d);
        theme.BorderThickness.Add("Default", 1d);
        theme.Animations.Add("ThemeTransition", new ThemeAnimationSettings(TimeSpan.FromMilliseconds(250), ThemeEasing.EaseInOut));
        return theme;
    }

    private static ThemeDefinition CreateLight()
    {
        var theme = new ThemeDefinition(LightThemeId, "ModernFormsNext Light")
        {
            Description = "The compatibility-focused built-in light theme.",
            Author = "ModernFormsNext",
            BaseTheme = BaseThemeId,
            Variant = ThemeVariant.Light
        };
        AddSemanticColors(
            theme,
            background: C("#F0F0F0"),
            surface: C("#FBFBFB"),
            surfaceVariant: C("#F3F3F3"),
            text: C("#000000"),
            textSecondary: C("#686868"),
            textDisabled: C("#AAAAAA"),
            border: C("#ABABAB"),
            divider: C("#808080"),
            primary: C("#2A8AD0"),
            secondary: C("#0078D4"));
        AddLegacyColors(theme,
            C("#F0F0F0"), C("#ABABAB"), C("#808080"), C("#333333"),
            C("#FBFBFB"), C("#F3F3F3"), C("#E1E1E1"), C("#C2C3C9"), C("#686868"),
            C("#C6C6C6"), C("#B0B0B0"), C("#808080"), C("#000000"), C("#AAAAAA"),
            C("#FFFFFF"), C("#2A8AD0"), C("#0078D4"), C("#99C9EF"), C("#E81123"));
        AddBrushes(theme, C("#FBFBFB"), C("#E7F3FB"), C("#2A8AD0"), C("#0078D4"));
        return theme;
    }

    private static ThemeDefinition CreateDark()
    {
        var theme = new ThemeDefinition(DarkThemeId, "ModernFormsNext Dark")
        {
            Description = "The compatibility-focused built-in dark theme.",
            Author = "ModernFormsNext",
            BaseTheme = BaseThemeId,
            Variant = ThemeVariant.Dark
        };
        AddSemanticColors(
            theme,
            background: C("#282828"),
            surface: C("#323232"),
            surfaceVariant: C("#505050"),
            text: C("#DEDEDE"),
            textSecondary: C("#A0A0A0"),
            textDisabled: C("#969696"),
            border: C("#505050"),
            divider: C("#808080"),
            primary: C("#096085"),
            secondary: C("#0078D4"));
        AddLegacyColors(theme,
            C("#282828"), C("#505050"), C("#808080"), C("#A0A0A0"),
            C("#282828"), C("#505050"), C("#686868"), C("#808080"), C("#EFEBEF"),
            C("#A8A8A8"), C("#828282"), C("#505050"), C("#DEDEDE"), C("#969696"),
            C("#FFFFFF"), C("#096085"), C("#0078D4"), C("#99C9EF"), C("#E81123"));
        AddBrushes(theme, C("#323232"), C("#505050"), C("#096085"), C("#0078D4"));
        return theme;
    }

    private static void AddSemanticColors(
        ThemeDefinition theme,
        Color background,
        Color surface,
        Color surfaceVariant,
        Color text,
        Color textSecondary,
        Color textDisabled,
        Color border,
        Color divider,
        Color primary,
        Color secondary)
    {
        theme.Colors.Add(ThemeTokens.Colors.Background.Name, background);
        theme.Colors.Add(ThemeTokens.Colors.Surface.Name, surface);
        theme.Colors.Add(ThemeTokens.Colors.SurfaceVariant.Name, surfaceVariant);
        theme.Colors.Add(ThemeTokens.Colors.TextPrimary.Name, text);
        theme.Colors.Add(ThemeTokens.Colors.TextSecondary.Name, textSecondary);
        theme.Colors.Add(ThemeTokens.Colors.TextDisabled.Name, textDisabled);
        theme.Colors.Add(ThemeTokens.Colors.Border.Name, border);
        theme.Colors.Add(ThemeTokens.Colors.Divider.Name, divider);
        theme.Colors.Add(ThemeTokens.Colors.Primary.Name, primary);
        theme.Colors.Add(ThemeTokens.Colors.PrimaryHover.Name, Blend(primary, Color.White, 0.14f));
        theme.Colors.Add(ThemeTokens.Colors.PrimaryPressed.Name, Blend(primary, Color.Black, 0.18f));
        theme.Colors.Add(ThemeTokens.Colors.PrimaryText.Name, Color.White);
        theme.Colors.Add(ThemeTokens.Colors.Secondary.Name, secondary);
        theme.Colors.Add(ThemeTokens.Colors.Accent.Name, secondary);
        theme.Colors.Add(ThemeTokens.Colors.Success.Name, C("#16823B"));
        theme.Colors.Add(ThemeTokens.Colors.Warning.Name, C("#C27C0E"));
        theme.Colors.Add(ThemeTokens.Colors.Error.Name, C("#E81123"));
        theme.Colors.Add(ThemeTokens.Colors.Info.Name, C("#0078D4"));
        theme.Colors.Add(ThemeTokens.Colors.Focus.Name, secondary);
        theme.Colors.Add(ThemeTokens.Colors.Selection.Name, C("#99C9EF"));
    }

    private static void AddLegacyColors(
        ThemeDefinition theme,
        Color background,
        Color borderLow,
        Color borderMid,
        Color borderHigh,
        Color controlLow,
        Color controlMid,
        Color controlMidHigh,
        Color controlHigh,
        Color controlVeryHigh,
        Color highlightLow,
        Color highlightMid,
        Color highlightHigh,
        Color foreground,
        Color foregroundDisabled,
        Color foregroundOnAccent,
        Color accent,
        Color accent2,
        Color selection,
        Color warning)
    {
        var values = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            [nameof(Theme.BackgroundColor)] = background,
            [nameof(Theme.BorderLowColor)] = borderLow,
            [nameof(Theme.BorderMidColor)] = borderMid,
            [nameof(Theme.BorderHighColor)] = borderHigh,
            [nameof(Theme.ControlLowColor)] = controlLow,
            [nameof(Theme.ControlMidColor)] = controlMid,
            [nameof(Theme.ControlMidHighColor)] = controlMidHigh,
            [nameof(Theme.ControlHighColor)] = controlHigh,
            [nameof(Theme.ControlVeryHighColor)] = controlVeryHigh,
            [nameof(Theme.ControlHighlightLowColor)] = highlightLow,
            [nameof(Theme.ControlHighlightMidColor)] = highlightMid,
            [nameof(Theme.ControlHighlightHighColor)] = highlightHigh,
            [nameof(Theme.ForegroundColor)] = foreground,
            [nameof(Theme.ForegroundDisabledColor)] = foregroundDisabled,
            [nameof(Theme.ForegroundColorOnAccent)] = foregroundOnAccent,
            [nameof(Theme.AccentColor)] = accent,
            [nameof(Theme.AccentColor2)] = accent2,
            [nameof(Theme.TextSelectionBackgroundColor)] = selection,
            [nameof(Theme.WarningHighlightColor)] = warning
        };
        foreach ((string name, Color color) in values)
            theme.Colors.Add("Legacy." + name, color);
    }

    private static void AddBrushes(ThemeDefinition theme, Color surface, Color surfaceVariant, Color primary, Color secondary)
    {
        theme.Brushes.Add("SurfaceBrush", new SolidColorBrush(surface));
        var primaryGradient = new LinearGradientBrush();
        primaryGradient.GradientStops.AddRange([
            new GradientStop(primary, 0f),
            new GradientStop(secondary, 1f)
        ]);
        theme.Brushes.Add("PrimaryGradient", primaryGradient);

        var surfaceGradient = new RadialGradientBrush { Radius = 0.9f };
        surfaceGradient.GradientStops.AddRange([
            new GradientStop(surfaceVariant, 0f),
            new GradientStop(surface, 1f)
        ]);
        theme.Brushes.Add("SurfaceGlow", surfaceGradient);
    }

    private static Color C(string value)
    {
        string hex = value.AsSpan(1).ToString();
        int argb = hex.Length == 6
            ? unchecked((int)(0xFF000000u | Convert.ToUInt32(hex, 16)))
            : unchecked((int)Convert.ToUInt32(hex, 16));
        return Color.FromArgb(argb);
    }

    private static Color Blend(Color from, Color to, float amount)
        => Color.FromArgb(
            from.A + (int)MathF.Round((to.A - from.A) * amount),
            from.R + (int)MathF.Round((to.R - from.R) * amount),
            from.G + (int)MathF.Round((to.G - from.G) * amount),
            from.B + (int)MathF.Round((to.B - from.B) * amount));
}
