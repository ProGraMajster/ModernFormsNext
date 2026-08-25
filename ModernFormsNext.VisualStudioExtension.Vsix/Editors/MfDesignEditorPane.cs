using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Visual Studio document pane for an out-of-process ModernFormsNext Designer surface.
/// </summary>
/// <remarks>
/// One pane owns exactly one child process and one <c>.mfdesign</c> document. The process is never
/// discovered or terminated by name, which prevents one Visual Studio instance from affecting
/// another instance's Designer host.
/// </remarks>
public sealed class MfDesignEditorPane : WindowPane, IVsPersistDocData, IPersistFileFormat
{
    private const uint CurrentFileFormat = 0;
    private readonly OutOfProcessDesignerHostControl hostControl;
    private string documentPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfDesignEditorPane"/> class.
    /// </summary>
    /// <param name="serviceProvider">The Visual Studio service provider.</param>
    /// <param name="documentPath">The canonical Designer document path.</param>
    public MfDesignEditorPane(IServiceProvider serviceProvider, string documentPath)
        : base(serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
            throw new ArgumentException("A Designer document path is required.", nameof(documentPath));

        this.documentPath = System.IO.Path.GetFullPath(documentPath);
        hostControl = new OutOfProcessDesignerHostControl(this.documentPath);
    }

    /// <inheritdoc/>
    public override System.Windows.Forms.IWin32Window Window => hostControl;

    /// <inheritdoc/>
    public int GetClassID(out Guid pClassID)
    {
        pClassID = ModernFormsDesignerPackage.EditorFactoryGuid;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int IsDirty(out int pfIsDirty)
    {
        if (hostControl.TryGetDocumentDirty(out var isDirty))
        {
            pfIsDirty = isDirty ? 1 : 0;
            return VSConstants.S_OK;
        }

        // Protect unsaved work when the child is alive but temporarily unresponsive. Visual
        // Studio can still offer Save/Don't Save, while issue #41 recovery remains available.
        pfIsDirty = 1;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int InitNew(uint nFormatIndex) => VSConstants.S_OK;

    /// <inheritdoc/>
    public int Load(string pszFilename, uint grfMode, int fReadOnly)
        => ReloadDocument(pszFilename);

    /// <inheritdoc/>
    public int Save(string pszFilename, int fRemember, uint nFormatIndex)
        => hostControl.TrySaveDocument() ? VSConstants.S_OK : VSConstants.E_FAIL;

    /// <inheritdoc/>
    public int SaveCompleted(string pszFilename) => VSConstants.S_OK;

    /// <inheritdoc/>
    public int GetCurFile(out string ppszFilename, out uint pnFormatIndex)
    {
        ppszFilename = documentPath;
        pnFormatIndex = CurrentFileFormat;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int GetFormatList(out string ppszFormatList)
    {
        ppszFormatList = "ModernFormsNext design metadata (*.mfdesign)\n*.mfdesign";
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int Close()
    {
        hostControl.Dispose();
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int GetGuidEditorType(out Guid pClassID)
    {
        pClassID = ModernFormsDesignerPackage.EditorFactoryGuid;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int IsDocDataDirty(out int pfDirty)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return IsDirty(out pfDirty);
    }

    /// <inheritdoc/>
    public int IsDocDataReloadable(out int pfReloadable)
    {
        pfReloadable = 1;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int LoadDocData(string pszMkDocument) => ReloadDocument(pszMkDocument);

    /// <inheritdoc/>
    public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew) => VSConstants.S_OK;

    /// <inheritdoc/>
    public int ReloadDocData(uint grfFlags) => ReloadDocument(documentPath);

    /// <inheritdoc/>
    public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        => ReloadDocument(pszMkDocumentNew);

    /// <inheritdoc/>
    public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        pbstrMkDocumentNew = documentPath;
        pfSaveCanceled = 0;
        return Save(documentPath, 1, CurrentFileFormat);
    }

    /// <inheritdoc/>
    public int SetUntitledDocPath(string pszDocDataPath) => ReloadDocument(pszDocDataPath);

    private int ReloadDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return VSConstants.E_INVALIDARG;

        var candidateDocumentPath = System.IO.Path.GetFullPath(path);
        if (!hostControl.TryOpenDocument(candidateDocumentPath))
            return VSConstants.E_FAIL;

        documentPath = candidateDocumentPath;
        return VSConstants.S_OK;
    }
}
