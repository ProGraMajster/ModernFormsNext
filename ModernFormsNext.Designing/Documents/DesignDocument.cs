using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModernFormsNext.Designing;

/// <summary>
/// Represents a complete ModernFormsNext designer document.
/// </summary>
/// <remarks>
/// A document describes one Form or UserControl class and its child control tree. It is neutral model
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
    /// Gets or sets the namespace used for the generated design-root partial class.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated design-root class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime base kind represented by the design root.
    /// </summary>
    /// <remarks>
    /// Form is the default for backward compatibility. The serializer omits that default, so
    /// existing Form documents retain their historical shape while UserControl documents write
    /// <c>"rootKind": "userControl"</c>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DesignRootKind RootKind { get; set; } = DesignRootKind.Form;

    /// <summary>
    /// Gets or sets the runtime root name used by generated initialization code.
    /// </summary>
    /// <remarks>
    /// The historical property name is retained for backward compatibility with existing
    /// <c>.mfdesign</c> documents. For a Form it is also the fallback title text. For a UserControl
    /// it is the value assigned to <c>Control.Name</c>.
    /// </remarks>
    public string FormName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the design root size in logical pixels.
    /// </summary>
    public DesignSize Size { get; set; }

    /// <summary>
    /// Gets or sets additional root-level property values.
    /// </summary>
    /// <remarks>
    /// This dictionary mirrors <see cref="DesignControlNode.Properties"/> for the root
    /// control. Keys are runtime property names and values are stable designer values that
    /// can be serialized to <c>.mfdesign</c> and emitted by code generation when supported.
    /// </remarks>
    public SortedDictionary<string, DesignPropertyValue> Properties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets designer event handler bindings assigned to the root itself.
    /// </summary>
    /// <remarks>
    /// Keys are runtime event names and values are optional handler method names.
    /// The current MVP persists these values so a later generator can emit event
    /// hookup code without changing the document format again.
    /// </remarks>
    public SortedDictionary<string, string?> Events { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets controls placed directly on the design root.
    /// </summary>
    public DesignControlCollection Controls { get; set; } = [];
}
