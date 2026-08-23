using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.Designer;
using ModernFormsNext.VisualStudioExtension.Hosting;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Visual Studio editor pane for ModernFormsNext design metadata files.
/// </summary>
/// <remarks>
/// The pane owns the shared <see cref="ModernFormsDesignerShell"/> and embeds it through a
/// small HWND host control. The Visual Studio extension does not duplicate designer UI.
/// </remarks>
public sealed class MfDesignEditorPane : WindowPane, IVsPersistDocData, IPersistFileFormat
{
    private const uint CurrentFileFormat = 0;

    private readonly VisualStudioDesignerDocumentAdapter documentAdapter = new();
    private readonly VisualStudioModernFormsHostControl hostControl;
    private string documentPath;
    private bool isDirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfDesignEditorPane"/> class.
    /// </summary>
    /// <param name="serviceProvider">The Visual Studio service provider.</param>
    /// <param name="documentPath">The <c>.mfdesign</c> file path.</param>
    public MfDesignEditorPane(IServiceProvider serviceProvider, string documentPath)
        : base(serviceProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        this.documentPath = documentPath;
        hostControl = CreateHostControl();
        LoadDocument(documentPath);
    }

    /// <summary>
    /// Gets the shared reusable designer shell owned by this editor pane.
    /// </summary>
    public ModernFormsDesignerShell? Shell { get; private set; }

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
        pfIsDirty = isDirty ? 1 : 0;
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int InitNew(uint nFormatIndex)
        => VSConstants.S_OK;

    /// <inheritdoc/>
    public int Load(string pszFilename, uint grfMode, int fReadOnly)
    {
        LoadDocument(pszFilename);
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int Save(string pszFilename, int fRemember, uint nFormatIndex)
        => SaveDocument(string.IsNullOrWhiteSpace(pszFilename) ? documentPath : pszFilename);

    /// <inheritdoc/>
    public int SaveCompleted(string pszFilename)
        => VSConstants.S_OK;

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
        var currentShell = Shell;
        Shell = null;

        if (currentShell is null)
            return VSConstants.S_OK;

        currentShell.Session.DocumentChanged -= HandleDocumentChanged;

        Exception? cleanupFailure = null;
        try
        {
            // A clean disk model can still have unresolved pre-crash work attached to its safety
            // banner. Visual Studio does not report a distinct Don't Save reason to Close(), so
            // preserve that artifact until the user resolves it explicitly in the Designer.
            if (!currentShell.HasUnresolvedRecovery)
                currentShell.DiscardActiveDocumentRecovery();
        }
        catch (Exception ex)
        {
            cleanupFailure = ex;
        }

        try
        {
            hostControl.Dispose();
        }
        catch (Exception ex)
        {
            cleanupFailure ??= ex;
        }

        try
        {
            currentShell.Dispose();
        }
        catch (Exception ex)
        {
            cleanupFailure ??= ex;
        }

        return cleanupFailure is null
            ? VSConstants.S_OK
            : Marshal.GetHRForException(cleanupFailure);
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
    public int LoadDocData(string pszMkDocument)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return Load(pszMkDocument, 0, 0);
    }

    /// <inheritdoc/>
    public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew)
        => VSConstants.S_OK;

    /// <inheritdoc/>
    public int ReloadDocData(uint grfFlags)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return Load(documentPath, 0, 0);
    }

    /// <inheritdoc/>
    public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
    {
        return UpdateDocumentPath(pszMkDocumentNew);
    }

    /// <inheritdoc/>
    public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
    {
        pbstrMkDocumentNew = documentPath;
        pfSaveCanceled = 0;

        return SaveDocument(documentPath);
    }

    /// <inheritdoc/>
    public int SetUntitledDocPath(string pszDocDataPath)
        => UpdateDocumentPath(pszDocDataPath);

    private static VisualStudioModernFormsHostControl CreateHostControl()
        => new();

    private void LoadDocument(string path)
    {
        var documentData = documentAdapter.Load(path);
        var host = new VisualStudioDesignerHost(path, documentData.CodeFilePath, FindNearestProjectPath(path));
        var replacementShell = new ModernFormsDesignerShell(new ModernFormsDesignerOptions(), host);
        try
        {
            replacementShell.LoadDocument(documentData.Document);
            replacementShell.Session.DocumentChanged += HandleDocumentChanged;
        }
        catch
        {
            replacementShell.Dispose();
            throw;
        }

        var previousShell = Shell;
        try
        {
            hostControl.AttachShell(replacementShell);
            Shell = replacementShell;
            documentPath = path;
        }
        catch
        {
            replacementShell.Session.DocumentChanged -= HandleDocumentChanged;
            replacementShell.Dispose();
            if (previousShell is not null)
                hostControl.AttachShell(previousShell);
            throw;
        }

        if (previousShell is not null)
        {
            previousShell.Session.DocumentChanged -= HandleDocumentChanged;
            previousShell.Dispose();
        }
        isDirty = false;
    }

    private int SaveDocument(string path)
    {
        if (Shell is null)
            return VSConstants.E_UNEXPECTED;

        try
        {
            if (!Shell.SaveDocument(path))
                return VSConstants.E_FAIL;

            documentPath = path;
            isDirty = Shell.Session.IsDirty;
            return VSConstants.S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    private int UpdateDocumentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return VSConstants.E_INVALIDARG;
        if (Shell is null)
            return VSConstants.E_UNEXPECTED;

        try
        {
            Shell.NotifyDocumentRenamed(path);
            documentPath = path;
            return VSConstants.S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    private void HandleDocumentChanged(object? sender, EventArgs e)
        => isDirty = Shell?.Session.IsDirty ?? true;

    private static string? FindNearestProjectPath(string path)
    {
        var directory = File.Exists(path)
            ? IOPath.GetDirectoryName(path)
            : Directory.Exists(path)
                ? path
                : null;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();

            if (project is not null)
                return project;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
