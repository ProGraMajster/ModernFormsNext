using System.Collections.Generic;

namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a complete ModernFormsNext designer document.
/// </summary>
/// <remarks>
/// A document describes one form class and its child control tree. It is neutral model
/// state and does not depend on Visual Studio, runtime control instances, or a specific
/// platform backend.
/// </remarks>
/// <example>
/// <code>
/// var document = new DesignDocument
/// {
///     Namespace = "MyApp",
///     ClassName = "MainForm",
///     FormName = "MainForm",
///     Size = new DesignSize(900, 600)
/// };
///
/// document.Controls.Add(new DesignControlNode
/// {
///     TypeName = "Button",
///     Name = "buttonLogin",
///     Bounds = new DesignBounds(40, 50, 120, 36)
/// });
/// </code>
/// </example>
public sealed class DesignDocument
{
    /// <summary>
    /// Gets or sets optional metadata about the document format and writer.
    /// </summary>
    public DesignDocumentMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the namespace used for the generated form partial class.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated form class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime form name and title text used by the MVP generator.
    /// </summary>
    public string FormName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the form size in logical pixels.
    /// </summary>
    public DesignSize Size { get; set; }

    /// <summary>
    /// Gets or sets additional form-level property values.
    /// </summary>
    /// <remarks>
    /// This dictionary mirrors <see cref="DesignControlNode.Properties"/> for the root
    /// form. Keys are runtime property names and values are stable designer values that
    /// can be serialized to <c>.mfdesign</c> and emitted by code generation when supported.
    /// </remarks>
    public SortedDictionary<string, DesignPropertyValue> Properties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets designer event handler bindings assigned to the form itself.
    /// </summary>
    /// <remarks>
    /// Keys are runtime event names and values are optional handler method names.
    /// The current MVP persists these values so a later generator can emit event
    /// hookup code without changing the document format again.
    /// </remarks>
    public SortedDictionary<string, string?> Events { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets root controls placed directly on the form.
    /// </summary>
    public DesignControlCollection Controls { get; set; } = [];
}
