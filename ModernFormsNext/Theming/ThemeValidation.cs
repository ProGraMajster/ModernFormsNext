using System.Collections.ObjectModel;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>Identifies the severity of a theme validation diagnostic.</summary>
public enum ThemeDiagnosticSeverity
{
    /// <summary>Informational context that does not block application.</summary>
    Information,
    /// <summary>A recoverable concern that does not block application.</summary>
    Warning,
    /// <summary>An error that prevents the theme from being applied.</summary>
    Error
}

/// <summary>Describes one safe theme validation or resolution diagnostic.</summary>
/// <param name="Code">A stable machine-readable code.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Message">A user-facing message without environment paths.</param>
/// <param name="Path">The logical definition or JSON path, when available.</param>
public sealed record ThemeDiagnostic(
    string Code,
    ThemeDiagnosticSeverity Severity,
    string Message,
    string? Path = null);

/// <summary>Contains diagnostics produced while validating a theme.</summary>
public sealed class ThemeValidationResult
{
    internal ThemeValidationResult(IEnumerable<ThemeDiagnostic> diagnostics)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Gets whether no error diagnostics were produced.</summary>
    public bool IsValid => Diagnostics.All(static item => item.Severity != ThemeDiagnosticSeverity.Error);

    /// <summary>Gets the stable validation diagnostics.</summary>
    public IReadOnlyList<ThemeDiagnostic> Diagnostics { get; }
}

internal sealed class ThemeResolver
{
    private readonly Func<string, ThemeDefinition?> findTheme;
    private readonly Func<ThemeVariant> resolveSystemVariant;
    private readonly ThemeSecurityLimits limits;

    public ThemeResolver(
        Func<string, ThemeDefinition?> findTheme,
        Func<ThemeVariant> resolveSystemVariant,
        ThemeSecurityLimits limits)
    {
        this.findTheme = findTheme;
        this.resolveSystemVariant = resolveSystemVariant;
        this.limits = limits;
    }

    public ThemeResolutionResult Resolve(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var diagnostics = new List<ThemeDiagnostic>();
        var definitions = new List<ThemeDefinition>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        Collect(definition, definitions, visiting, diagnostics, 0);

        foreach (ThemeDefinition item in definitions)
            ValidateDefinition(item, diagnostics);

        if (HasErrors(diagnostics))
            return new ThemeResolutionResult(null, diagnostics);

        var colors = new Dictionary<string, System.Drawing.Color>(StringComparer.Ordinal);
        var brushes = new Dictionary<string, MfnBrush>(StringComparer.Ordinal);
        var typography = new Dictionary<string, ThemeTypography>(StringComparer.Ordinal);
        var spacing = new Dictionary<string, double>(StringComparer.Ordinal);
        var padding = new Dictionary<string, Padding>(StringComparer.Ordinal);
        var sizing = new Dictionary<string, double>(StringComparer.Ordinal);
        var corners = new Dictionary<string, double>(StringComparer.Ordinal);
        var borders = new Dictionary<string, double>(StringComparer.Ordinal);
        var animations = new Dictionary<string, ThemeAnimationSettings>(StringComparer.Ordinal);
        var resources = new Dictionary<string, ThemeResourceValue>(StringComparer.Ordinal);
        foreach (ThemeDefinition item in definitions)
        {
            Merge(item.Colors, colors, static value => value);
            Merge(item.Brushes, brushes, ThemeValueCloner.CloneBrush);
            Merge(item.Typography, typography, static value => value);
            Merge(item.Spacing, spacing, static value => value);
            Merge(item.Padding, padding, static value => value);
            Merge(item.Sizing, sizing, static value => value);
            Merge(item.Corners, corners, static value => value);
            Merge(item.BorderThickness, borders, static value => value);
            Merge(item.Animations, animations, static value => value);

            foreach ((string key, ThemeResourceValue value) in item.Resources)
            {
                if (resources.TryGetValue(key, out ThemeResourceValue? inherited) && inherited.Kind != value.Kind)
                {
                    diagnostics.Add(Error(
                        "THEME_INCOMPATIBLE_RESOURCE_OVERRIDE",
                        $"Resource '{key}' changes kind from '{inherited.Kind}' to '{value.Kind}'.",
                        $"resources.{key}"));
                    continue;
                }
                resources[key] = value.Clone();
            }
        }

        if (HasErrors(diagnostics))
            return new ThemeResolutionResult(null, diagnostics);

        ThemeVariant variant = definition.Variant == ThemeVariant.System
            ? resolveSystemVariant()
            : definition.Variant;
        if ((variant is ThemeVariant.System or ThemeVariant.Custom) && definition.Variant == ThemeVariant.System)
            variant = ThemeVariant.Light;

        string[] baseChain = definitions
            .Take(Math.Max(0, definitions.Count - 1))
            .Select(static item => item.Id)
            .ToArray();
        var snapshot = new ThemeResolvedSnapshot(
            definition.Clone(),
            variant,
            baseChain,
            colors,
            brushes,
            typography,
            spacing,
            padding,
            sizing,
            corners,
            borders,
            animations,
            resources);
        return new ThemeResolutionResult(snapshot, diagnostics);
    }

    public ThemeValidationResult ValidateWithoutBases(ThemeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var diagnostics = new List<ThemeDiagnostic>();
        ValidateDefinition(definition, diagnostics);
        return new ThemeValidationResult(diagnostics);
    }

    private void Collect(
        ThemeDefinition definition,
        List<ThemeDefinition> result,
        HashSet<string> visiting,
        List<ThemeDiagnostic> diagnostics,
        int depth)
    {
        if (depth >= limits.MaximumInheritanceDepth)
        {
            diagnostics.Add(Error(
                "THEME_INHERITANCE_DEPTH",
                $"Theme inheritance exceeds the configured depth limit of {limits.MaximumInheritanceDepth}.",
                "baseTheme"));
            return;
        }

        string identity = definition.Id;
        if (!visiting.Add(identity))
        {
            diagnostics.Add(Error("THEME_INHERITANCE_CYCLE", $"Theme inheritance contains a cycle at '{identity}'.", "baseTheme"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(definition.BaseTheme))
        {
            ThemeDefinition? baseTheme = findTheme(definition.BaseTheme);
            if (baseTheme is null)
            {
                diagnostics.Add(Error(
                    "THEME_BASE_MISSING",
                    $"Base theme '{definition.BaseTheme}' is not registered.",
                    "baseTheme"));
            }
            else
            {
                Collect(baseTheme, result, visiting, diagnostics, depth + 1);
            }
        }

        visiting.Remove(identity);
        result.Add(definition);
    }

    private void ValidateDefinition(ThemeDefinition definition, List<ThemeDiagnostic> diagnostics)
    {
        ValidateText(definition.Id, "id", required: true, diagnostics);
        ValidateText(definition.Name, "name", required: true, diagnostics);
        ValidateText(definition.Description, "description", required: false, diagnostics);
        ValidateText(definition.Author, "author", required: false, diagnostics);

        if (definition.SchemaVersion != ThemeJsonSerializer.CurrentSchemaVersion)
        {
            diagnostics.Add(Error(
                "THEME_SCHEMA_UNSUPPORTED",
                $"Schema version {definition.SchemaVersion} is not supported; version {ThemeJsonSerializer.CurrentSchemaVersion} is required.",
                "schemaVersion"));
        }
        if (!Enum.IsDefined(definition.Variant))
            diagnostics.Add(Error("THEME_VARIANT_INVALID", "The theme variant is not defined.", "variant"));
        if (!ThemeKeyValidator.IsValid(definition.Id))
            diagnostics.Add(Error("THEME_ID_INVALID", "The theme ID is not a valid stable key.", "id"));
        if (definition.BaseTheme is { } baseTheme && !ThemeKeyValidator.IsValid(baseTheme))
            diagnostics.Add(Error("THEME_BASE_INVALID", "The base theme ID is not a valid stable key.", "baseTheme"));

        int count = CountTokens(definition);
        if (count > limits.MaximumTokenCount)
        {
            diagnostics.Add(Error(
                "THEME_TOKEN_LIMIT",
                $"The definition contains {count} tokens; the configured maximum is {limits.MaximumTokenCount}.",
                null));
        }

        ValidateKeys(definition.Colors, ThemeTokenCategory.Color, diagnostics);
        ValidateKeys(definition.Brushes, ThemeTokenCategory.Brush, diagnostics);
        ValidateKeys(definition.Typography, ThemeTokenCategory.Typography, diagnostics);
        ValidateNumbers(definition.Spacing, ThemeTokenCategory.Spacing, diagnostics);
        ValidatePadding(definition.Padding, diagnostics);
        ValidateNumbers(definition.Sizing, ThemeTokenCategory.Sizing, diagnostics);
        ValidateNumbers(definition.Corners, ThemeTokenCategory.Corner, diagnostics);
        ValidateNumbers(definition.BorderThickness, ThemeTokenCategory.BorderThickness, diagnostics);
        ValidateKeys(definition.Animations, ThemeTokenCategory.Animation, diagnostics);
        ValidateKeys(definition.Resources, ThemeTokenCategory.Resource, diagnostics);

        foreach ((string key, MfnBrush? brush) in definition.Brushes)
            ValidateBrush(brush, $"brushes.{key}", diagnostics);
        foreach ((string key, ThemeTypography? typography) in definition.Typography)
        {
            if (typography is null)
                diagnostics.Add(Error("THEME_TYPOGRAPHY_NULL", "A typography token cannot be null.", $"typography.{key}"));
            else
                ValidateText(typography.FontFamily, $"typography.{key}.fontFamily", required: true, diagnostics);
        }
        foreach ((string key, ThemeAnimationSettings? animation) in definition.Animations)
        {
            if (animation is null)
                diagnostics.Add(Error("THEME_ANIMATION_NULL", "An animation token cannot be null.", $"animations.{key}"));
        }
        foreach ((string key, ThemeResourceValue? resource) in definition.Resources)
        {
            if (resource is null)
            {
                diagnostics.Add(Error("THEME_RESOURCE_NULL", "A custom resource cannot be null.", $"resources.{key}"));
                continue;
            }

            object value = resource.GetRawValue();
            switch (resource.Kind)
            {
                case ThemeResourceKind.String:
                    ValidateText((string)value, $"resources.{key}.value", required: false, diagnostics);
                    break;
                case ThemeResourceKind.Brush:
                    ValidateBrush((MfnBrush)value, $"resources.{key}.value", diagnostics);
                    break;
                case ThemeResourceKind.Typography:
                    ValidateText(
                        ((ThemeTypography)value).FontFamily,
                        $"resources.{key}.value.fontFamily",
                        required: true,
                        diagnostics);
                    break;
            }
        }

        foreach ((string key, string? value) in definition.Metadata)
        {
            if (!ThemeKeyValidator.IsValid(key))
                diagnostics.Add(Error("THEME_METADATA_KEY_INVALID", $"Metadata key '{key}' is invalid.", $"metadata.{key}"));
            if (value is null)
                diagnostics.Add(Error("THEME_METADATA_VALUE_NULL", "A metadata value cannot be null.", $"metadata.{key}"));
            else
                ValidateText(value, $"metadata.{key}", required: false, diagnostics);
        }
        for (int index = 0; index < definition.Tags.Count; index++)
            ValidateText(definition.Tags[index], $"tags[{index}]", required: true, diagnostics);
    }

    private void ValidateBrush(MfnBrush? brush, string path, List<ThemeDiagnostic> diagnostics)
    {
        if (brush is null)
        {
            diagnostics.Add(Error("THEME_BRUSH_NULL", "A theme brush cannot be null.", path));
            return;
        }

        try
        {
            _ = ThemeValueCloner.CloneBrush(brush);
        }
        catch (NotSupportedException)
        {
            diagnostics.Add(Error(
                "THEME_BRUSH_UNSUPPORTED",
                $"Brush type '{brush.GetType().Name}' is not in the theme allow-list.",
                path));
            return;
        }

        if (brush is GradientBrush gradient)
        {
            if (gradient.GradientStops.Count < 2)
                diagnostics.Add(Error("THEME_GRADIENT_STOPS_MIN", "A gradient theme brush requires at least two stops.", path + ".gradientStops"));
            if (gradient.GradientStops.Count > limits.MaximumGradientStops)
            {
                diagnostics.Add(Error(
                    "THEME_GRADIENT_STOPS_LIMIT",
                    $"The gradient contains {gradient.GradientStops.Count} stops; the maximum is {limits.MaximumGradientStops}.",
                    path + ".gradientStops"));
            }
        }
    }

    private void ValidateText(string? value, string path, bool required, List<ThemeDiagnostic> diagnostics)
    {
        if (required && string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error("THEME_STRING_REQUIRED", "A required theme string is empty.", path));
            return;
        }
        if (value?.Length > limits.MaximumStringLength)
        {
            diagnostics.Add(Error(
                "THEME_STRING_LIMIT",
                $"The string exceeds the configured limit of {limits.MaximumStringLength} characters.",
                path));
        }
    }

    private static void ValidateKeys<T>(
        IDictionary<string, T> values,
        ThemeTokenCategory category,
        List<ThemeDiagnostic> diagnostics)
    {
        foreach (string key in values.Keys)
        {
            if (!ThemeKeyValidator.IsValid(key))
                diagnostics.Add(Error("THEME_TOKEN_KEY_INVALID", $"Token key '{key}' is invalid.", $"{category}.{key}"));
        }
    }

    private static void ValidateNumbers(
        IDictionary<string, double> values,
        ThemeTokenCategory category,
        List<ThemeDiagnostic> diagnostics)
    {
        ValidateKeys(values, category, diagnostics);
        foreach ((string key, double value) in values)
        {
            if (!double.IsFinite(value) || value < 0d)
            {
                diagnostics.Add(Error(
                    "THEME_NUMBER_INVALID",
                    $"Token '{key}' must be finite and non-negative.",
                    $"{category}.{key}"));
            }
        }
    }

    private static void ValidatePadding(
        IDictionary<string, Padding> values,
        List<ThemeDiagnostic> diagnostics)
    {
        ValidateKeys(values, ThemeTokenCategory.Padding, diagnostics);
        foreach ((string key, Padding value) in values)
        {
            if (value.Left < 0 || value.Top < 0 || value.Right < 0 || value.Bottom < 0)
            {
                diagnostics.Add(Error(
                    "THEME_PADDING_INVALID",
                    $"Padding token '{key}' must have non-negative sides.",
                    $"Padding.{key}"));
            }
        }
    }

    private static void Merge<T>(
        IDictionary<string, T> source,
        Dictionary<string, T> destination,
        Func<T, T> clone)
    {
        foreach ((string key, T value) in source)
            destination[key] = clone(value);
    }

    private static int CountTokens(ThemeDefinition definition)
        => definition.Colors.Count + definition.Brushes.Count + definition.Typography.Count +
           definition.Spacing.Count + definition.Padding.Count + definition.Sizing.Count + definition.Corners.Count +
           definition.BorderThickness.Count + definition.Animations.Count + definition.Resources.Count;

    private static bool HasErrors(IEnumerable<ThemeDiagnostic> diagnostics)
        => diagnostics.Any(static item => item.Severity == ThemeDiagnosticSeverity.Error);

    private static ThemeDiagnostic Error(string code, string message, string? path)
        => new(code, ThemeDiagnosticSeverity.Error, message, path);
}

internal sealed record ThemeResolutionResult(
    ThemeResolvedSnapshot? Snapshot,
    IReadOnlyList<ThemeDiagnostic> Diagnostics)
{
    public bool Success => Snapshot is not null && Diagnostics.All(static item => item.Severity != ThemeDiagnosticSeverity.Error);
}
