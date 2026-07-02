using System.Collections.Generic;

namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a control instance in a designer document.
/// </summary>
/// <remarks>
/// The node is intentionally independent of runtime control instances and Visual Studio
/// designer services. It stores the type name to instantiate, the generated field name,
/// bounds relative to the parent container, primitive property values, and child nodes.
/// </remarks>
public sealed class DesignControlNode
{
    /// <summary>
    /// Gets or sets the control type name, such as <c>Button</c>, <c>Label</c>, or a custom control type.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated field name for the control.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bounds of the control relative to its parent container.
    /// </summary>
    public DesignBounds Bounds { get; set; }

    /// <summary>
    /// Gets or sets the visibility of the field generated for this control in C# designer code.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="DesignerMemberVisibility.None"/>, generators should avoid
    /// emitting a field and prefer a local variable inside <c>InitializeComponent</c> when
    /// that can be done without changing initialization semantics.
    /// </remarks>
    public DesignerMemberVisibility MemberVisibility { get; set; } = DesignerMemberVisibility.Private;

    /// <summary>
    /// Gets or sets the primitive property values assigned to this control.
    /// </summary>
    /// <remarks>
    /// The dictionary is sorted by property name so JSON and generated code remain
    /// deterministic even when values are added in different orders.
    /// </remarks>
    public SortedDictionary<string, DesignPropertyValue> Properties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets designer event handler bindings assigned to this control.
    /// </summary>
    /// <remarks>
    /// Keys are runtime event names, such as <c>Click</c> or <c>TextChanged</c>.
    /// Values are optional handler method names. The MVP stores these bindings for
    /// future code generation without emitting handler methods yet.
    /// </remarks>
    public SortedDictionary<string, string?> Events { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets child controls owned by this control.
    /// </summary>
    public DesignControlCollection Children { get; set; } = [];
}
