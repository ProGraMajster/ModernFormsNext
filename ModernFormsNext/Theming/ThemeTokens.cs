using System.Drawing;
using ModernFormsNext.Drawing;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext;

/// <summary>
/// Identifies the strongly typed token collection that owns a theme value.
/// </summary>
public enum ThemeTokenCategory
{
    /// <summary>A semantic or custom color.</summary>
    Color,

    /// <summary>A solid, gradient, glass, or no-fill brush.</summary>
    Brush,

    /// <summary>A named typography role.</summary>
    Typography,

    /// <summary>A logical spacing value.</summary>
    Spacing,

    /// <summary>A logical four-sided padding value.</summary>
    Padding,

    /// <summary>A logical size, control height, or icon size.</summary>
    Sizing,

    /// <summary>A logical corner-radius value.</summary>
    Corner,

    /// <summary>A logical border-thickness value.</summary>
    BorderThickness,

    /// <summary>A named animation setting.</summary>
    Animation,

    /// <summary>A custom allow-listed resource value.</summary>
    Resource
}

/// <summary>
/// Represents a typed theme-token identifier.
/// </summary>
/// <typeparam name="T">The value type associated with the token category.</typeparam>
/// <remarks>
/// A token is only an identifier. Values are stored by <see cref="ThemeDefinition"/> while
/// resolved, immutable values are read from <see cref="ThemeResolvedSnapshot"/>.
/// </remarks>
public readonly struct ThemeToken<T> : IEquatable<ThemeToken<T>>
{
    /// <summary>
    /// Creates a typed token in the specified category.
    /// </summary>
    /// <param name="category">The token category.</param>
    /// <param name="name">The stable, case-sensitive token name.</param>
    /// <exception cref="ArgumentException">The name is not a valid theme key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The category is not defined.</exception>
    public ThemeToken(ThemeTokenCategory category, string name)
    {
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category), category, "The theme token category is not defined.");

        ThemeKeyValidator.Validate(name, nameof(name));
        Category = category;
        Name = name;
    }

    /// <summary>Gets the category containing this token.</summary>
    public ThemeTokenCategory Category { get; }

    /// <summary>Gets the stable, case-sensitive token name.</summary>
    public string Name { get; }

    /// <summary>Gets the key used by dynamic resource lookup.</summary>
    public string ResourceKey => ThemeResourceKeys.Create(Category, Name);

    /// <inheritdoc />
    public bool Equals(ThemeToken<T> other)
        => Category == other.Category && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ThemeToken<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Category, Name is null ? 0 : StringComparer.Ordinal.GetHashCode(Name));

    /// <inheritdoc />
    public override string ToString() => ResourceKey;

    /// <summary>Tests two token identifiers for equality.</summary>
    public static bool operator ==(ThemeToken<T> left, ThemeToken<T> right) => left.Equals(right);

    /// <summary>Tests two token identifiers for inequality.</summary>
    public static bool operator !=(ThemeToken<T> left, ThemeToken<T> right) => !left.Equals(right);
}

/// <summary>
/// Provides stable dynamic-resource keys for theme tokens.
/// </summary>
public static class ThemeResourceKeys
{
    /// <summary>Creates the resource key for a token category and name.</summary>
    /// <param name="category">The token category.</param>
    /// <param name="name">The validated token name.</param>
    /// <returns>A key in the form <c>Theme.Category.Name</c>.</returns>
    public static string Create(ThemeTokenCategory category, string name)
    {
        ThemeKeyValidator.Validate(name, nameof(name));
        return $"Theme.{category}.{name}";
    }
}

/// <summary>
/// Exposes the standard ModernFormsNext semantic tokens while allowing applications to create
/// additional <see cref="ThemeToken{T}"/> values.
/// </summary>
public static class ThemeTokens
{
    /// <summary>Contains standard semantic color tokens.</summary>
    public static class Colors
    {
        /// <summary>The application or window background.</summary>
        public static ThemeToken<Color> Background { get; } = new(ThemeTokenCategory.Color, nameof(Background));
        /// <summary>A control or card surface.</summary>
        public static ThemeToken<Color> Surface { get; } = new(ThemeTokenCategory.Color, nameof(Surface));
        /// <summary>An alternate surface.</summary>
        public static ThemeToken<Color> SurfaceVariant { get; } = new(ThemeTokenCategory.Color, nameof(SurfaceVariant));
        /// <summary>Primary text.</summary>
        public static ThemeToken<Color> TextPrimary { get; } = new(ThemeTokenCategory.Color, nameof(TextPrimary));
        /// <summary>Secondary text.</summary>
        public static ThemeToken<Color> TextSecondary { get; } = new(ThemeTokenCategory.Color, nameof(TextSecondary));
        /// <summary>Disabled text.</summary>
        public static ThemeToken<Color> TextDisabled { get; } = new(ThemeTokenCategory.Color, nameof(TextDisabled));
        /// <summary>Control borders.</summary>
        public static ThemeToken<Color> Border { get; } = new(ThemeTokenCategory.Color, nameof(Border));
        /// <summary>Dividers and subtle separators.</summary>
        public static ThemeToken<Color> Divider { get; } = new(ThemeTokenCategory.Color, nameof(Divider));
        /// <summary>The primary action color.</summary>
        public static ThemeToken<Color> Primary { get; } = new(ThemeTokenCategory.Color, nameof(Primary));
        /// <summary>The hovered primary action color.</summary>
        public static ThemeToken<Color> PrimaryHover { get; } = new(ThemeTokenCategory.Color, nameof(PrimaryHover));
        /// <summary>The pressed primary action color.</summary>
        public static ThemeToken<Color> PrimaryPressed { get; } = new(ThemeTokenCategory.Color, nameof(PrimaryPressed));
        /// <summary>Text rendered over the primary color.</summary>
        public static ThemeToken<Color> PrimaryText { get; } = new(ThemeTokenCategory.Color, nameof(PrimaryText));
        /// <summary>The secondary action color.</summary>
        public static ThemeToken<Color> Secondary { get; } = new(ThemeTokenCategory.Color, nameof(Secondary));
        /// <summary>The general accent color.</summary>
        public static ThemeToken<Color> Accent { get; } = new(ThemeTokenCategory.Color, nameof(Accent));
        /// <summary>A successful operation.</summary>
        public static ThemeToken<Color> Success { get; } = new(ThemeTokenCategory.Color, nameof(Success));
        /// <summary>A warning condition.</summary>
        public static ThemeToken<Color> Warning { get; } = new(ThemeTokenCategory.Color, nameof(Warning));
        /// <summary>An error or destructive operation.</summary>
        public static ThemeToken<Color> Error { get; } = new(ThemeTokenCategory.Color, nameof(Error));
        /// <summary>Informational emphasis.</summary>
        public static ThemeToken<Color> Info { get; } = new(ThemeTokenCategory.Color, nameof(Info));
        /// <summary>The keyboard focus indicator.</summary>
        public static ThemeToken<Color> Focus { get; } = new(ThemeTokenCategory.Color, nameof(Focus));
        /// <summary>The selection background.</summary>
        public static ThemeToken<Color> Selection { get; } = new(ThemeTokenCategory.Color, nameof(Selection));
    }

    /// <summary>Contains standard typography roles.</summary>
    public static class Typography
    {
        /// <summary>Normal body content.</summary>
        public static ThemeToken<ThemeTypography> Body { get; } = new(ThemeTokenCategory.Typography, nameof(Body));
        /// <summary>Small supporting content.</summary>
        public static ThemeToken<ThemeTypography> Caption { get; } = new(ThemeTokenCategory.Typography, nameof(Caption));
        /// <summary>Section headings.</summary>
        public static ThemeToken<ThemeTypography> Heading { get; } = new(ThemeTokenCategory.Typography, nameof(Heading));
        /// <summary>Page or window titles.</summary>
        public static ThemeToken<ThemeTypography> Title { get; } = new(ThemeTokenCategory.Typography, nameof(Title));
        /// <summary>Button labels.</summary>
        public static ThemeToken<ThemeTypography> Button { get; } = new(ThemeTokenCategory.Typography, nameof(Button));
        /// <summary>Editable input text.</summary>
        public static ThemeToken<ThemeTypography> Input { get; } = new(ThemeTokenCategory.Typography, nameof(Input));
    }
}

internal static class ThemeKeyValidator
{
    public const int MaximumKeyLength = 128;

    public static void Validate(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0 || value.Length > MaximumKeyLength || !IsStart(value[0]))
            throw new ArgumentException("A theme key must start with a letter and contain 1 through 128 characters.", parameterName);

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
                throw new ArgumentException("A theme key can contain only ASCII letters, digits, '.', '_' and '-'.", parameterName);
        }
    }

    public static bool IsValid(string? value)
    {
        try
        {
            Validate(value!, nameof(value));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsStart(char value) => char.IsAsciiLetter(value);
}
