namespace ModernFormsNext.Designing;

/// <summary>
/// Defines well-known designer-only property names and role values used to model
/// container parts that are not independent generated fields.
/// </summary>
/// <remarks>
/// These values keep the neutral <see cref="DesignControlNode"/> model explicit
/// while allowing generators and designer hosts to recognize structural nodes
/// such as <c>SplitContainer.Panel1</c> without relying on invalid C# identifiers.
/// </remarks>
public static class DesignNodeRoleNames
{
    /// <summary>
    /// Gets the property name that stores the designer-only structural role.
    /// </summary>
    public const string RolePropertyName = "DesignerRole";

    /// <summary>
    /// Gets the property name that stores an outline-friendly display name.
    /// </summary>
    public const string DisplayNamePropertyName = "DesignerDisplayName";

    /// <summary>
    /// Gets the property name that stores an outline-friendly display type.
    /// </summary>
    public const string DisplayTypePropertyName = "DesignerDisplayType";

    /// <summary>
    /// Gets the role value used for the first panel of a split container.
    /// </summary>
    public const string SplitContainerPanel1 = "SplitContainer.Panel1";

    /// <summary>
    /// Gets the role value used for the second panel of a split container.
    /// </summary>
    public const string SplitContainerPanel2 = "SplitContainer.Panel2";
}
