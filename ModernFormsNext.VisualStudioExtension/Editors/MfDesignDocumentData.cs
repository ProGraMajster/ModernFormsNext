using ModernFormsNext.Designing;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Represents the document data owned by a Visual Studio <c>.mfdesign</c> editor.
/// </summary>
/// <param name="Path">The design metadata path on disk.</param>
/// <param name="CodeFilePath">The primary user-authored C# file path.</param>
/// <param name="DesignerCodePath">The generated sibling C# designer file path.</param>
/// <param name="Document">The parsed ModernFormsNext design document.</param>
public sealed record MfDesignDocumentData(
    string Path,
    string CodeFilePath,
    string DesignerCodePath,
    DesignDocument Document);
