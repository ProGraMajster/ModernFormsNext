namespace ModernFormsNext;

/// <summary>
/// Identifies the color-scheme intent of a <see cref="ThemeDefinition"/>.
/// </summary>
public enum ThemeVariant
{
    /// <summary>Uses a light color scheme.</summary>
    Light,

    /// <summary>Uses a dark color scheme.</summary>
    Dark,

    /// <summary>Uses the platform preference, with an explicit fallback when unavailable.</summary>
    System,

    /// <summary>Uses a color scheme that is neither a standard light nor dark variant.</summary>
    Custom
}
