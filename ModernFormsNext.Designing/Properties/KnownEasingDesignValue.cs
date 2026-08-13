namespace ModernFormsNext.Designing;

/// <summary>
/// Defines stable design-document identifiers for easing functions supplied by ModernFormsNext.
/// </summary>
/// <remarks>
/// Designer documents persist one of these identifiers instead of attempting to serialize the
/// runtime <c>Func&lt;float, float&gt;</c> delegate. Code generation maps the identifier back to the
/// corresponding member of <c>ModernFormsNext.Animations.Easings</c>.
/// </remarks>
public static class KnownEasingDesignValue
{
    /// <summary>Gets all easing identifiers accepted by the Designer, in display order.</summary>
    public static IReadOnlyList<string> Identifiers { get; } =
    [
        "Linear",
        "EaseIn",
        "EaseOut",
        "EaseInOut",
        "EaseOutCubic",
        "CubicIn",
        "CubicOut",
        "CubicInOut",
        "BounceOut",
        "EaseInOutCubic"
    ];

    /// <summary>Determines whether a value is a supported stable easing identifier.</summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <returns><see langword="true"/> when the identifier is known.</returns>
    public static bool IsKnown(string? identifier)
        => identifier is not null && Identifiers.Contains(identifier, StringComparer.Ordinal);
}
