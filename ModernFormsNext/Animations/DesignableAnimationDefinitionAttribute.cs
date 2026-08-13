namespace ModernFormsNext.Animations;

/// <summary>
/// Marks a project-owned animation definition or interaction effect for safe source-based Designer discovery.
/// </summary>
/// <remarks>
/// The Designer reads this attribute from C# syntax and does not load the project assembly or invoke
/// the attributed type. Custom <see cref="AnimationDefinition"/> types remain code-first because
/// controls currently expose no general animation-definition attachment collection. Custom
/// <see cref="InteractionEffect"/> types can be configured when every exposed property also carries
/// <see cref="DesignableAnimationPropertyAttribute"/>.
/// </remarks>
/// <example>
/// <code>
/// [DesignableAnimationDefinition("Glow")]
/// public sealed class GlowEffect : InteractionEffect
/// {
///     [DesignableAnimationProperty(DesignableAnimationPropertyKind.Number, DefaultValue = "0.5")]
///     public float Opacity { get; set; } = 0.5f;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DesignableAnimationDefinitionAttribute : Attribute
{
    /// <summary>Initializes the marker with a user-facing display name.</summary>
    /// <param name="displayName">The non-empty name shown by Designer editors.</param>
    public DesignableAnimationDefinitionAttribute(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
    }

    /// <summary>Gets the name shown by Designer editors.</summary>
    public string DisplayName { get; }
}

/// <summary>Identifies the source-serializable shape of a custom animation property.</summary>
public enum DesignableAnimationPropertyKind
{
    /// <summary>A Boolean value.</summary>
    Boolean,
    /// <summary>A signed 32-bit integer.</summary>
    Int32,
    /// <summary>A finite <see cref="float"/> or <see cref="double"/> value.</summary>
    Number,
    /// <summary>A non-negative <see cref="TimeSpan"/> persisted as milliseconds.</summary>
    TimeSpan,
    /// <summary>A built-in easing identifier mapped to an <see cref="Easings"/> member.</summary>
    Easing,
    /// <summary>An enum whose allowed members are declared by the attribute.</summary>
    Enum,
    /// <summary>A <see cref="System.Drawing.Color"/> persisted as ARGB.</summary>
    ColorArgb,
    /// <summary>A string value.</summary>
    String
}

/// <summary>Marks a public settable custom animation property as safe for detached Designer editing.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DesignableAnimationPropertyAttribute : Attribute
{
    /// <summary>Initializes property metadata.</summary>
    /// <param name="kind">The explicitly supported serialized property shape.</param>
    public DesignableAnimationPropertyAttribute(DesignableAnimationPropertyKind kind)
        => Kind = kind;

    /// <summary>Gets the serialized property shape.</summary>
    public DesignableAnimationPropertyKind Kind { get; }
    /// <summary>Gets or sets the invariant default text used by the Designer.</summary>
    public string? DefaultValue { get; set; }
    /// <summary>Gets or sets the inclusive numeric minimum.</summary>
    public double Minimum { get; set; } = double.MinValue;
    /// <summary>Gets or sets the inclusive numeric maximum.</summary>
    public double Maximum { get; set; } = double.MaxValue;
    /// <summary>Gets or sets the namespace-qualified enum type name for <see cref="DesignableAnimationPropertyKind.Enum"/>.</summary>
    public string? EnumTypeName { get; set; }
    /// <summary>Gets or sets the allowed enum member names.</summary>
    public string[] EnumMembers { get; set; } = [];
}
