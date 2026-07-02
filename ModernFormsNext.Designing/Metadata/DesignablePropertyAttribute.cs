namespace ModernFormsNext.Designing;

/// <summary>
/// Describes how a property should appear and serialize in ModernFormsNext designer tooling.
/// </summary>
/// <remarks>
/// When this attribute is present it takes precedence over standard
/// <see cref="System.ComponentModel"/> metadata for designer visibility, naming,
/// read-only state, and serialization intent.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class DesignablePropertyAttribute : Attribute
{
    private DesignPropertyVisibility visibility = DesignPropertyVisibility.Visible;

    /// <summary>
    /// Gets or initializes the property display name shown in designer UI.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or initializes the designer category for the property.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or initializes a short description shown by designer tooling.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the designer property visibility.
    /// </summary>
    public DesignPropertyVisibility Visibility
    {
        get => visibility;
        init => visibility = value;
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the property is visible in normal designer UI.
    /// </summary>
    /// <remarks>
    /// This convenience property is equivalent to setting <see cref="Visibility"/> to
    /// <see cref="DesignPropertyVisibility.Visible"/> or <see cref="DesignPropertyVisibility.Hidden"/>.
    /// Use <see cref="Visibility"/> directly when a property should be marked as advanced.
    /// </remarks>
    public bool Visible
    {
        get => visibility != DesignPropertyVisibility.Hidden;
        init => visibility = value ? DesignPropertyVisibility.Visible : DesignPropertyVisibility.Hidden;
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the property should be edited as read-only.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the property should be serialized by designer code generation.
    /// </summary>
    public bool Serialize { get; init; } = true;
}
