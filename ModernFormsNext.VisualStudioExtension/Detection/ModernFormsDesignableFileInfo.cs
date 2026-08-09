using ModernFormsNext.Designing;

namespace ModernFormsNext.VisualStudioExtension.Detection;

/// <summary>
/// Describes a C# file that may be opened with the ModernFormsNext designer.
/// </summary>
/// <param name="CodeFilePath">The primary user-authored <c>.cs</c> file.</param>
/// <param name="DesignerCodePath">The sibling generated <c>.Designer.cs</c> file.</param>
/// <param name="DesignFilePath">The sibling <c>.mfdesign</c> metadata file.</param>
/// <param name="Namespace">The namespace declared by the primary class, when available.</param>
/// <param name="ClassName">The primary class name, when available.</param>
/// <param name="BaseTypeName">The first declared base type, when available.</param>
/// <param name="IsPartial">A value indicating whether the class is declared <c>partial</c>.</param>
/// <param name="InheritsKnownModernFormsType">A value indicating whether the class inherits from a known ModernFormsNext form/control base type.</param>
/// <param name="HasInitializeComponent">A value indicating whether the source declares or calls <c>InitializeComponent()</c>.</param>
/// <param name="HasDesignFile">A value indicating whether the companion <c>.mfdesign</c> file exists and looks like a ModernFormsNext design document.</param>
/// <param name="HasDesignerCodeFile">A value indicating whether the companion <c>.Designer.cs</c> file exists.</param>
/// <param name="HasProjectDesignMetadata">A value indicating whether the project metadata marks the file as designable.</param>
/// <param name="IsDesignable">A value indicating whether the file is safe to expose through the ModernFormsNext designer command.</param>
public sealed record ModernFormsDesignableFileInfo(
    string CodeFilePath,
    string DesignerCodePath,
    string DesignFilePath,
    string? Namespace,
    string? ClassName,
    string? BaseTypeName,
    bool IsPartial,
    bool InheritsKnownModernFormsType,
    bool HasInitializeComponent,
    bool HasDesignFile,
    bool HasDesignerCodeFile,
    bool HasProjectDesignMetadata,
    bool IsDesignable)
{
    /// <summary>
    /// Gets the design root kind inferred from the primary class base type.
    /// </summary>
    /// <remarks>
    /// This additive property preserves the original positional record constructor and deconstruction
    /// contract for callers compiled against earlier extension versions.
    /// </remarks>
    public DesignRootKind RootKind { get; init; } = DesignRootKind.Form;
}
