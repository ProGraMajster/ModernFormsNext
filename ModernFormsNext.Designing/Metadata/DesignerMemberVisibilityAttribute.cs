namespace ModernFormsNext.Designing;

/// <summary>
/// Specifies the generated member visibility for designer-owned fields or properties.
/// </summary>
/// <remarks>
/// Designer document nodes store the effective value in
/// <see cref="DesignControlNode.MemberVisibility"/> so code generation can remain
/// deterministic without reflecting over runtime controls.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class DesignerMemberVisibilityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerMemberVisibilityAttribute"/> class.
    /// </summary>
    /// <param name="visibility">The generated member visibility.</param>
    public DesignerMemberVisibilityAttribute(DesignerMemberVisibility visibility)
    {
        Visibility = visibility;
    }

    /// <summary>
    /// Gets the generated member visibility.
    /// </summary>
    public DesignerMemberVisibility Visibility { get; }
}
