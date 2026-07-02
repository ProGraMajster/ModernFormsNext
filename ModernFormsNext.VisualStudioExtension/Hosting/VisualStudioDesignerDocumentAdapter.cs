using ModernFormsNext.Designing;
using ModernFormsNext.VisualStudioExtension.Detection;
using ModernFormsNext.VisualStudioExtension.Editors;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

/// <summary>
/// Loads and saves ModernFormsNext design documents for Visual Studio editor panes.
/// </summary>
public sealed class VisualStudioDesignerDocumentAdapter
{
    private readonly ModernFormsDesignFileLocator fileLocator = new();

    /// <summary>
    /// Loads a design document from disk.
    /// </summary>
    /// <param name="path">The <c>.mfdesign</c> file path.</param>
    /// <returns>The loaded document data.</returns>
    public MfDesignDocumentData Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var document = DesignDocumentSerializer.Default.Load(path);
        var codeFilePath = fileLocator.GetCodeFilePath(path);
        var designerCodePath = fileLocator.GetDesignerCodePath(codeFilePath);
        return new MfDesignDocumentData(path, codeFilePath, designerCodePath, document);
    }

    /// <summary>
    /// Saves a design document to disk.
    /// </summary>
    /// <param name="path">The target <c>.mfdesign</c> path.</param>
    /// <param name="document">The document to save.</param>
    public void Save(string path, DesignDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        DesignDocumentSerializer.Default.Save(path, document);
    }
}
