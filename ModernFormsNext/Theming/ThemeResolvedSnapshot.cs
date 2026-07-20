using System.Collections.ObjectModel;
using System.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Reports token totals in one resolved theme snapshot.
/// </summary>
/// <param name="Colors">The number of resolved color tokens.</param>
/// <param name="Brushes">The number of resolved brush tokens.</param>
/// <param name="Typography">The number of resolved typography tokens.</param>
/// <param name="Spacing">The number of resolved spacing tokens.</param>
/// <param name="Padding">The number of resolved padding tokens.</param>
/// <param name="Sizing">The number of resolved sizing tokens.</param>
/// <param name="Corners">The number of resolved corner-radius tokens.</param>
/// <param name="BorderThickness">The number of resolved border-thickness tokens.</param>
/// <param name="Animations">The number of resolved animation-setting tokens.</param>
/// <param name="Resources">The number of resolved custom resource tokens.</param>
public readonly record struct ThemeTokenCounts(
    int Colors,
    int Brushes,
    int Typography,
    int Spacing,
    int Padding,
    int Sizing,
    int Corners,
    int BorderThickness,
    int Animations,
    int Resources)
{
    /// <summary>Gets the combined number of resolved tokens.</summary>
    public int Total => Colors + Brushes + Typography + Spacing + Padding + Sizing + Corners +
        BorderThickness + Animations + Resources;
}

/// <summary>
/// Represents an isolated, immutable view of a validated and inherited theme.
/// </summary>
/// <remarks>
/// Scalar dictionaries are exposed read-only. Mutable brushes never escape from the snapshot:
/// <see cref="TryGet{T}(ThemeToken{T}, out T)"/> returns a fresh clone, and application resources
/// receive a separate working clone. A running transition mutates only those working brushes.
/// </remarks>
public sealed class ThemeResolvedSnapshot
{
    private readonly IReadOnlyDictionary<string, MfnBrush> brushes;

    internal ThemeResolvedSnapshot(
        ThemeDefinition source,
        ThemeVariant resolvedVariant,
        IReadOnlyList<string> baseChain,
        Dictionary<string, Color> colors,
        Dictionary<string, MfnBrush> brushes,
        Dictionary<string, ThemeTypography> typography,
        Dictionary<string, double> spacing,
        Dictionary<string, Padding> padding,
        Dictionary<string, double> sizing,
        Dictionary<string, double> corners,
        Dictionary<string, double> borderThickness,
        Dictionary<string, ThemeAnimationSettings> animations,
        Dictionary<string, ThemeResourceValue> resources)
    {
        Id = source.Id;
        Name = source.Name;
        Description = source.Description;
        Author = source.Author;
        SchemaVersion = source.SchemaVersion;
        DeclaredVariant = source.Variant;
        Variant = resolvedVariant;
        BaseChain = Array.AsReadOnly(baseChain.ToArray());
        Metadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal));
        Tags = Array.AsReadOnly(source.Tags.ToArray());
        Colors = new ReadOnlyDictionary<string, Color>(colors);
        this.brushes = new ReadOnlyDictionary<string, MfnBrush>(brushes);
        Typography = new ReadOnlyDictionary<string, ThemeTypography>(typography);
        Spacing = new ReadOnlyDictionary<string, double>(spacing);
        Padding = new ReadOnlyDictionary<string, Padding>(padding);
        Sizing = new ReadOnlyDictionary<string, double>(sizing);
        Corners = new ReadOnlyDictionary<string, double>(corners);
        BorderThickness = new ReadOnlyDictionary<string, double>(borderThickness);
        Animations = new ReadOnlyDictionary<string, ThemeAnimationSettings>(animations);
        Resources = new ReadOnlyDictionary<string, ThemeResourceValue>(resources);
        Counts = new ThemeTokenCounts(
            colors.Count,
            brushes.Count,
            typography.Count,
            spacing.Count,
            padding.Count,
            sizing.Count,
            corners.Count,
            borderThickness.Count,
            animations.Count,
            resources.Count);
    }

    /// <summary>Gets the stable theme identifier.</summary>
    public string Id { get; }
    /// <summary>Gets the user-facing theme name.</summary>
    public string Name { get; }
    /// <summary>Gets the optional description.</summary>
    public string? Description { get; }
    /// <summary>Gets the optional author.</summary>
    public string? Author { get; }
    /// <summary>Gets the schema major version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets the variant declared by the authoring definition.</summary>
    public ThemeVariant DeclaredVariant { get; }
    /// <summary>Gets the effective variant after resolving <see cref="ThemeVariant.System"/>.</summary>
    public ThemeVariant Variant { get; }
    /// <summary>Gets base-theme identifiers from the root base through the direct base.</summary>
    public IReadOnlyList<string> BaseChain { get; }
    /// <summary>Gets immutable metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
    /// <summary>Gets immutable tags.</summary>
    public IReadOnlyList<string> Tags { get; }
    /// <summary>Gets resolved color tokens.</summary>
    public IReadOnlyDictionary<string, Color> Colors { get; }
    /// <summary>Gets the names of resolved brush tokens. Brush values are returned through <see cref="TryGet{T}(ThemeToken{T}, out T)"/>.</summary>
    public IEnumerable<string> BrushTokenNames => brushes.Keys;
    /// <summary>Gets resolved typography tokens.</summary>
    public IReadOnlyDictionary<string, ThemeTypography> Typography { get; }
    /// <summary>Gets resolved spacing tokens.</summary>
    public IReadOnlyDictionary<string, double> Spacing { get; }
    /// <summary>Gets resolved four-sided padding tokens in logical pixels.</summary>
    public IReadOnlyDictionary<string, Padding> Padding { get; }
    /// <summary>Gets resolved sizing tokens.</summary>
    public IReadOnlyDictionary<string, double> Sizing { get; }
    /// <summary>Gets resolved corner-radius tokens.</summary>
    public IReadOnlyDictionary<string, double> Corners { get; }
    /// <summary>Gets resolved border-thickness tokens.</summary>
    public IReadOnlyDictionary<string, double> BorderThickness { get; }
    /// <summary>Gets resolved animation settings.</summary>
    public IReadOnlyDictionary<string, ThemeAnimationSettings> Animations { get; }
    /// <summary>Gets resolved custom resources. Their values remain isolated by their wrapper.</summary>
    public IReadOnlyDictionary<string, ThemeResourceValue> Resources { get; }
    /// <summary>Gets the resolved token totals.</summary>
    public ThemeTokenCounts Counts { get; }

    /// <summary>Attempts to retrieve a value through a typed token.</summary>
    /// <typeparam name="T">The expected token type.</typeparam>
    /// <param name="token">The token identifier.</param>
    /// <param name="value">Receives the value or an isolated brush clone.</param>
    /// <returns><see langword="true"/> when the token exists with the requested type.</returns>
    public bool TryGet<T>(ThemeToken<T> token, out T? value)
    {
        object? result = null;
        bool found = token.Category switch
        {
            ThemeTokenCategory.Color => Colors.TryGetValue(token.Name, out Color color) && Assign(color, out result),
            ThemeTokenCategory.Brush => brushes.TryGetValue(token.Name, out MfnBrush? brush) && Assign(ThemeValueCloner.CloneBrush(brush), out result),
            ThemeTokenCategory.Typography => Typography.TryGetValue(token.Name, out ThemeTypography? typography) && Assign(typography, out result),
            ThemeTokenCategory.Spacing => Spacing.TryGetValue(token.Name, out double spacing) && Assign(spacing, out result),
            ThemeTokenCategory.Padding => Padding.TryGetValue(token.Name, out Padding padding) && Assign(padding, out result),
            ThemeTokenCategory.Sizing => Sizing.TryGetValue(token.Name, out double sizing) && Assign(sizing, out result),
            ThemeTokenCategory.Corner => Corners.TryGetValue(token.Name, out double corner) && Assign(corner, out result),
            ThemeTokenCategory.BorderThickness => BorderThickness.TryGetValue(token.Name, out double border) && Assign(border, out result),
            ThemeTokenCategory.Animation => Animations.TryGetValue(token.Name, out ThemeAnimationSettings? animation) && Assign(animation, out result),
            ThemeTokenCategory.Resource => Resources.TryGetValue(token.Name, out ThemeResourceValue? resource) && Assign(resource, out result),
            _ => false
        };

        if (found && result is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Gets a required typed token.</summary>
    /// <typeparam name="T">The expected token type.</typeparam>
    /// <param name="token">The token identifier.</param>
    /// <returns>The isolated resolved value.</returns>
    /// <exception cref="KeyNotFoundException">The token is absent or has an incompatible type.</exception>
    public T Get<T>(ThemeToken<T> token)
        => TryGet(token, out T? value)
            ? value!
            : throw new KeyNotFoundException($"Theme token '{token.ResourceKey}' was not found with type '{typeof(T).FullName}'.");

    internal IReadOnlyDictionary<string, MfnBrush> GetBrushes() => brushes;

    internal Dictionary<object, object?> CreateResourceEntries()
    {
        var result = new Dictionary<object, object?>();
        Add(result, ThemeTokenCategory.Color, Colors, static value => value);
        Add(result, ThemeTokenCategory.Brush, brushes, ThemeValueCloner.CloneBrush);
        Add(result, ThemeTokenCategory.Typography, Typography, static value => value);
        Add(result, ThemeTokenCategory.Spacing, Spacing, static value => value);
        Add(result, ThemeTokenCategory.Padding, Padding, static value => value);
        Add(result, ThemeTokenCategory.Sizing, Sizing, static value => value);
        Add(result, ThemeTokenCategory.Corner, Corners, static value => value);
        Add(result, ThemeTokenCategory.BorderThickness, BorderThickness, static value => value);
        Add(result, ThemeTokenCategory.Animation, Animations, static value => value);
        Add(result, ThemeTokenCategory.Resource, Resources, static value => value.Value);
        return result;
    }

    private static void Add<T>(
        Dictionary<object, object?> destination,
        ThemeTokenCategory category,
        IReadOnlyDictionary<string, T> source,
        Func<T, object?> clone)
    {
        foreach ((string key, T value) in source)
            destination.Add(ThemeResourceKeys.Create(category, key), clone(value));
    }

    private static bool Assign(object value, out object result)
    {
        result = value;
        return true;
    }
}
