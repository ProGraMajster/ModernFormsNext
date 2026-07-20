using System.ComponentModel;
using System.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Represents the mutable authoring model for one ModernFormsNext theme.
/// </summary>
/// <remarks>
/// <para>
/// Definitions are safe to edit before validation. Applying a definition never stores these
/// mutable collections directly: inheritance is resolved into an isolated
/// <see cref="ThemeResolvedSnapshot"/> and every brush is cloned.
/// </para>
/// <para>
/// Complex collections are hidden from generic designer property grids because they require a
/// dedicated editor. Define them in code or load them with <see cref="ThemeJsonSerializer"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var theme = new ThemeDefinition("sample.dark", "Sample Dark")
/// {
///     Variant = ThemeVariant.Dark
/// };
/// theme.Set(ThemeTokens.Colors.Background, Color.FromArgb(32, 32, 32));
/// ThemeManager.Current.Apply(theme);
/// </code>
/// </example>
public sealed class ThemeDefinition
{
    private string id;
    private string name;

    /// <summary>Creates a theme definition with the required identity fields.</summary>
    /// <param name="id">The stable theme identifier.</param>
    /// <param name="name">The user-facing name.</param>
    public ThemeDefinition(string id, string name)
    {
        this.id = id ?? throw new ArgumentNullException(nameof(id));
        this.name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Gets or sets the schema major version. Version 1 is currently supported.</summary>
    public int SchemaVersion { get; set; } = ThemeJsonSerializer.CurrentSchemaVersion;

    /// <summary>Gets or sets the stable theme identifier.</summary>
    public string Id
    {
        get => id;
        set => id = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the user-facing theme name.</summary>
    public string Name
    {
        get => name;
        set => name = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the optional theme description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the optional author name.</summary>
    public string? Author { get; set; }

    /// <summary>Gets or sets the optional identifier of one registered base theme.</summary>
    public string? BaseTheme { get; set; }

    /// <summary>Gets or sets the theme color-scheme intent.</summary>
    public ThemeVariant Variant { get; set; } = ThemeVariant.Custom;

    /// <summary>Gets optional stable metadata values.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets optional classification tags.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList<string> Tags { get; } = new List<string>();

    /// <summary>Gets semantic and custom color tokens.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, Color> Colors { get; } = new Dictionary<string, Color>(StringComparer.Ordinal);

    /// <summary>Gets semantic and custom brush tokens.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, MfnBrush> Brushes { get; } = new Dictionary<string, MfnBrush>(StringComparer.Ordinal);

    /// <summary>Gets named typography tokens.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, ThemeTypography> Typography { get; } = new Dictionary<string, ThemeTypography>(StringComparer.Ordinal);

    /// <summary>Gets spacing tokens in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, double> Spacing { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets four-sided padding tokens in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, Padding> Padding { get; } = new Dictionary<string, Padding>(StringComparer.Ordinal);

    /// <summary>Gets sizing, control-height, and icon-size tokens in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, double> Sizing { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets corner-radius tokens in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, double> Corners { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets border-thickness tokens in logical pixels.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, double> BorderThickness { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Gets named animation settings.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, ThemeAnimationSettings> Animations { get; } = new Dictionary<string, ThemeAnimationSettings>(StringComparer.Ordinal);

    /// <summary>Gets custom resources from the closed, serialization-safe type allow-list.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDictionary<string, ThemeResourceValue> Resources { get; } = new Dictionary<string, ThemeResourceValue>(StringComparer.Ordinal);

    /// <summary>Assigns a value through a typed token.</summary>
    /// <typeparam name="T">The token value type.</typeparam>
    /// <param name="token">The typed token identifier.</param>
    /// <param name="value">The non-null value.</param>
    /// <exception cref="ArgumentException">The token category and value type are incompatible.</exception>
    public void Set<T>(ThemeToken<T> token, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        object boxed = value;

        switch (token.Category, boxed)
        {
            case (ThemeTokenCategory.Color, Color color): Colors[token.Name] = color; break;
            case (ThemeTokenCategory.Brush, MfnBrush brush): Brushes[token.Name] = brush; break;
            case (ThemeTokenCategory.Typography, ThemeTypography typography): Typography[token.Name] = typography; break;
            case (ThemeTokenCategory.Spacing, double spacing): Spacing[token.Name] = spacing; break;
            case (ThemeTokenCategory.Padding, Padding padding): Padding[token.Name] = padding; break;
            case (ThemeTokenCategory.Sizing, double sizing): Sizing[token.Name] = sizing; break;
            case (ThemeTokenCategory.Corner, double corner): Corners[token.Name] = corner; break;
            case (ThemeTokenCategory.BorderThickness, double border): BorderThickness[token.Name] = border; break;
            case (ThemeTokenCategory.Animation, ThemeAnimationSettings animation): Animations[token.Name] = animation; break;
            case (ThemeTokenCategory.Resource, ThemeResourceValue resource): Resources[token.Name] = resource; break;
            default:
                throw new ArgumentException(
                    $"Token category '{token.Category}' cannot store values of type '{typeof(T).FullName}'.",
                    nameof(value));
        }
    }

    /// <summary>Creates a deep authoring copy, including isolated brush instances.</summary>
    /// <returns>A mutable copy that can be edited without affecting this definition.</returns>
    public ThemeDefinition Clone()
    {
        var clone = new ThemeDefinition(Id, Name)
        {
            SchemaVersion = SchemaVersion,
            Description = Description,
            Author = Author,
            BaseTheme = BaseTheme,
            Variant = Variant
        };

        Copy(Metadata, clone.Metadata, static value => value);
        foreach (string tag in Tags)
            clone.Tags.Add(tag);
        Copy(Colors, clone.Colors, static value => value);
        Copy(Brushes, clone.Brushes, static value => value is null ? null! : ThemeValueCloner.CloneBrush(value));
        Copy(Typography, clone.Typography, static value => value);
        Copy(Spacing, clone.Spacing, static value => value);
        Copy(Padding, clone.Padding, static value => value);
        Copy(Sizing, clone.Sizing, static value => value);
        Copy(Corners, clone.Corners, static value => value);
        Copy(BorderThickness, clone.BorderThickness, static value => value);
        Copy(Animations, clone.Animations, static value => value);
        Copy(Resources, clone.Resources, static value => value is null ? null! : value.Clone());
        return clone;
    }

    private static void Copy<T>(IDictionary<string, T> source, IDictionary<string, T> destination, Func<T, T> clone)
    {
        foreach ((string key, T value) in source)
            destination.Add(key, clone(value));
    }
}
