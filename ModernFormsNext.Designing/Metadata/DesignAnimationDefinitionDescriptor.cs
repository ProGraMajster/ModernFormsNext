namespace ModernFormsNext.Designing;

/// <summary>Identifies the runtime animation concept described by design-time metadata.</summary>
public enum DesignAnimationDefinitionKind
{
    /// <summary>A definition derived from <c>InteractionEffect</c> and attachable to a control.</summary>
    InteractionEffect,

    /// <summary>A code-first definition derived from <c>AnimationDefinition</c>.</summary>
    AnimationDefinition
}

/// <summary>Identifies a safely serializable property shape exposed by an animation descriptor.</summary>
public enum DesignAnimationPropertyKind
{
    /// <summary>A Boolean value.</summary>
    Boolean,
    /// <summary>A signed 32-bit integer.</summary>
    Int32,
    /// <summary>A finite floating-point number.</summary>
    Number,
    /// <summary>A non-negative <c>TimeSpan</c>, persisted as milliseconds.</summary>
    TimeSpan,
    /// <summary>A stable identifier from <see cref="KnownEasingDesignValue"/>.</summary>
    Easing,
    /// <summary>An enum member with an explicitly declared member set.</summary>
    Enum,
    /// <summary>An ARGB color persisted as a signed 32-bit value.</summary>
    ColorArgb,
    /// <summary>A string value.</summary>
    String
}

/// <summary>Describes one designer-safe property of an animation or interaction effect.</summary>
public sealed class DesignAnimationPropertyDescriptor
{
    /// <summary>Initializes a property descriptor.</summary>
    public DesignAnimationPropertyDescriptor(
        string name,
        string runtimePropertyName,
        DesignAnimationPropertyKind kind,
        DesignPropertyValue defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePropertyName);
        Name = name;
        RuntimePropertyName = runtimePropertyName;
        Kind = kind;
        DefaultValue = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));
    }

    /// <summary>Gets the stable design-document property name.</summary>
    public string Name { get; }
    /// <summary>Gets the corresponding runtime CLR property name.</summary>
    public string RuntimePropertyName { get; }
    /// <summary>Gets the serialized property shape.</summary>
    public DesignAnimationPropertyKind Kind { get; }
    /// <summary>Gets the runtime-compatible default represented as a detached value.</summary>
    public DesignPropertyValue DefaultValue { get; }
    /// <summary>Gets or initializes the inclusive numeric minimum.</summary>
    public double Minimum { get; init; } = double.MinValue;
    /// <summary>Gets or initializes the inclusive numeric maximum.</summary>
    public double Maximum { get; init; } = double.MaxValue;
    /// <summary>Gets or initializes the runtime enum type name.</summary>
    public string? EnumTypeName { get; init; }
    /// <summary>Gets or initializes the explicitly allowed enum members.</summary>
    public IReadOnlyList<string> EnumMembers { get; init; } = [];
    /// <summary>Gets or initializes the source runtime type used to choose numeric literal syntax.</summary>
    public string? RuntimeTypeName { get; init; }
}

/// <summary>
/// Provides detached, non-executable metadata for a runtime animation or interaction-effect type.
/// </summary>
/// <remarks>
/// A descriptor contains no delegates, constructors, instances, or assembly references. Designer
/// hosts may obtain custom descriptors from source analysis, then pass the same descriptors to the
/// editor, generator, and conservative reverse parser.
/// </remarks>
public sealed class DesignAnimationDefinitionDescriptor
{
    /// <summary>Initializes a definition descriptor.</summary>
    public DesignAnimationDefinitionDescriptor(
        string typeName,
        string displayName,
        DesignAnimationDefinitionKind kind,
        IEnumerable<DesignAnimationPropertyDescriptor> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(properties);
        TypeName = typeName;
        DisplayName = displayName;
        Kind = kind;
        Properties = properties.ToArray();
    }

    /// <summary>Gets the namespace-qualified, assembly-independent runtime type name.</summary>
    public string TypeName { get; }
    /// <summary>Gets the user-facing display name.</summary>
    public string DisplayName { get; }
    /// <summary>Gets the described runtime concept.</summary>
    public DesignAnimationDefinitionKind Kind { get; }
    /// <summary>Gets properties in deterministic editor display order.</summary>
    public IReadOnlyList<DesignAnimationPropertyDescriptor> Properties { get; }
    /// <summary>Gets or initializes whether the type is supplied by ModernFormsNext itself.</summary>
    public bool IsBuiltIn { get; init; }
}
