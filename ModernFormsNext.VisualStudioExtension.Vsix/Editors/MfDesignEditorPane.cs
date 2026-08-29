using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.VisualStudioExtension.Commands;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Visual Studio document pane for an out-of-process ModernFormsNext Designer surface.
/// </summary>
/// <remarks>
/// One pane owns exactly one child process and one <c>.mfdesign</c> document. The process is never
/// discovered or terminated by name, which prevents one Visual Studio instance from affecting
/// another instance's Designer host. Visual Studio owns save prompts and the RDT document state;
/// the out-of-process Designer remains the sole source of the actual dirty value.
/// </remarks>
public sealed class MfDesignEditorPane : WindowPane, IVsPersistDocData, IPersistFileFormat
{
    private const uint CurrentFileFormat = 0;
    private readonly IDesignerDocumentHost hostControl;
    private readonly IVisualStudioDocumentServices documentServices;
    private string documentPath;
    private uint documentCookie = VSConstants.VSCOOKIE_NIL;
    private bool lastKnownDirty;
    private int closeState;
    private int disposeState;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfDesignEditorPane"/> class.
    /// </summary>
    /// <param name="serviceProvider">The Visual Studio service provider.</param>
    /// <param name="documentPath">The canonical Designer document path.</param>
    public MfDesignEditorPane(IServiceProvider serviceProvider, string documentPath)
        : this(
            serviceProvider,
            documentPath,
            new OutOfProcessDesignerHostControl(documentPath),
            new VisualStudioDocumentServices(serviceProvider))
    {
    }

    internal MfDesignEditorPane(
        IServiceProvider serviceProvider,
        string documentPath,
        IDesignerDocumentHost hostControl,
        IVisualStudioDocumentServices documentServices)
        : base(serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
            throw new ArgumentException("A Designer document path is required.", nameof(documentPath));

        this.hostControl = hostControl ?? throw new ArgumentNullException(nameof(hostControl));
        this.documentServices = documentServices ?? throw new ArgumentNullException(nameof(documentServices));
        this.documentPath = System.IO.Path.GetFullPath(documentPath);
        this.hostControl.DocumentDirtyChanged += HandleDocumentDirtyChanged;
        DesignerEditorDiagnosticLog.Write($"PANE_CREATED Moniker={this.documentPath}");
    }

    /// <inheritdoc/>
    public override System.Windows.Forms.IWin32Window Window => hostControl.Window;

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
            lastKnownDirty = isDirty;

        pfIsDirty = lastKnownDirty ? 1 : 0;
        DesignerEditorDiagnosticLog.Write($"IS_DIRTY Dirty={lastKnownDirty} Cookie={documentCookie}");
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int InitNew(uint nFormatIndex)
        => nFormatIndex == CurrentFileFormat ? VSConstants.S_OK : VSConstants.E_INVALIDARG;

    /// <inheritdoc/>
    public int Load(string pszFilename, uint grfMode, int fReadOnly)
        => ReloadDocument(pszFilename);

    /// <inheritdoc/>
    public int Save(string pszFilename, int fRemember, uint nFormatIndex)
    {
        if (nFormatIndex != CurrentFileFormat && nFormatIndex != uint.MaxValue)
            return VSConstants.E_INVALIDARG;

        var targetPath = string.IsNullOrWhiteSpace(pszFilename)
            ? documentPath
            : System.IO.Path.GetFullPath(pszFilename);
        var rememberingTarget = fRemember != 0 || PathsEqual(targetPath, documentPath);
        if (!rememberingTarget)
        {
            const string copyMessage =
                "Save a Copy As is not supported by the ModernFormsNext Designer. Use Save As to move the active .mfdesign document.";
            documentServices.ReportSaveCanceled(copyMessage);
            DesignerEditorDiagnosticLog.Write(
                $"PERSIST_SAVE_CANCELED HResult=0x{VSConstants.OLE_E_PROMPTSAVECANCELLED:X8} Reason={copyMessage}");
            return VSConstants.OLE_E_PROMPTSAVECANCELLED;
        }

        DesignerEditorDiagnosticLog.Write(
            $"PERSIST_SAVE_BEGIN Target={targetPath} Remember={fRemember != 0} Format={nFormatIndex}");
        var result = hostControl.SaveDocument();
        if (result.Outcome == DesignerHostSaveOutcome.Saved)
        {
            if (fRemember != 0)
                documentPath = targetPath;
            PublishDirtyState(isDirty: false);
            DesignerEditorDiagnosticLog.Write("PERSIST_SAVE_OK HResult=0x00000000");
            return VSConstants.S_OK;
        }

        var message = result.Error ?? "The Designer did not save the document.";
        documentServices.ReportSaveCanceled(message);
        DesignerEditorDiagnosticLog.Write(
            $"PERSIST_SAVE_CANCELED Outcome={result.Outcome} " +
            $"HResult=0x{VSConstants.OLE_E_PROMPTSAVECANCELLED:X8} Reason={message}");
        return VSConstants.OLE_E_PROMPTSAVECANCELLED;
    }

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
        ppszFormatList = "ModernFormsNext design metadata (*.mfdesign)\n*.mfdesign\n";
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int Close()
    {
        // IVsPersistDocData.Close releases the docdata contract. The doc view can still be inside
        // its ClosePane callback, so terminating the child here races Visual Studio's frame
        // teardown. WindowPane.Dispose owns the single, later host shutdown instead.
        if (Interlocked.Exchange(ref closeState, 1) != 0)
        {
            DesignerEditorDiagnosticLog.Write("DOC_DATA_CLOSE_ALREADY_COMPLETED HResult=0x00000000");
            return VSConstants.S_OK;
        }

        if (lastKnownDirty)
        {
            var discarded = hostControl.TryDiscardDocumentRecovery();
            DesignerEditorDiagnosticLog.Write($"DOC_DATA_DISCARD Dirty=True Succeeded={discarded}");
        }

        hostControl.DocumentDirtyChanged -= HandleDocumentDirtyChanged;
        documentCookie = VSConstants.VSCOOKIE_NIL;
        DesignerEditorDiagnosticLog.Write("DOC_DATA_CLOSE HResult=0x00000000");
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
    public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew)
    {
        documentCookie = docCookie;
        DesignerEditorDiagnosticLog.Write($"DOC_DATA_REGISTERED Cookie={docCookie} Moniker={documentPath}");
        NotifyVisualStudioDirtyState();
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int ReloadDocData(uint grfFlags) => ReloadDocument(documentPath);

    /// <inheritdoc/>
    public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        => ReloadDocument(pszMkDocumentNew);

    /// <inheritdoc/>
    public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
    {
        pbstrMkDocumentNew = null!;
        pfSaveCanceled = 0;
        DesignerEditorDiagnosticLog.Write($"SAVE_DOC_DATA_BEGIN Flags={dwSave} Moniker={documentPath}");

        try
        {
            int result;
            switch (dwSave)
            {
                case VSSAVEFLAGS.VSSAVE_Save:
                case VSSAVEFLAGS.VSSAVE_SilentSave:
                    result = SaveCurrentDocument(dwSave, out pbstrMkDocumentNew, out pfSaveCanceled);
                    break;
                case VSSAVEFLAGS.VSSAVE_SaveAs:
                    result = SaveThroughVisualStudio(
                        VSSAVEFLAGS.VSSAVE_SaveAs,
                        out pbstrMkDocumentNew,
                        out pfSaveCanceled);
                    break;
                case VSSAVEFLAGS.VSSAVE_SaveCopyAs:
                    pfSaveCanceled = -1;
                    result = VSConstants.S_OK;
                    break;
                default:
                    result = VSConstants.E_INVALIDARG;
                    break;
            }

            DesignerEditorDiagnosticLog.Write(
                $"SAVE_DOC_DATA_END Flags={dwSave} HResult=0x{result:X8} " +
                $"Canceled={pfSaveCanceled != 0} NewMoniker={pbstrMkDocumentNew ?? "<null>"}");
            return result;
        }
        catch (Exception exception)
        {
            DesignerEditorDiagnosticLog.WriteException("SAVE_DOC_DATA_EXCEPTION", exception);
            return Marshal.GetHRForException(exception);
        }
    }

    /// <inheritdoc/>
    public int SetUntitledDocPath(string pszDocDataPath) => ReloadDocument(pszDocDataPath);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        if (Interlocked.Exchange(ref disposeState, 1) != 0)
            return;

        DesignerEditorDiagnosticLog.Write("PANE_DISPOSE_BEGIN");
        hostControl.DocumentDirtyChanged -= HandleDocumentDirtyChanged;
        documentCookie = VSConstants.VSCOOKIE_NIL;

        // WindowPane owns and disposes the IWin32Window returned by Window. Calling Dispose on
        // hostControl here as well would issue two SHUTDOWN paths for the same child process.
        base.Dispose(disposing);
        DesignerEditorDiagnosticLog.Write("PANE_DISPOSE_END");
    }

    private int SaveCurrentDocument(
        VSSAVEFLAGS saveFlags,
        out string newDocumentPath,
        out int saveCanceled)
    {
        newDocumentPath = null!;
        saveCanceled = 0;
        var queryHResult = documentServices.QuerySaveFile(documentPath, out var queryResult);
        DesignerEditorDiagnosticLog.Write(
            $"QUERY_SAVE_RESULT HResult=0x{queryHResult:X8} Result={(tagVSQuerySaveResult)queryResult}");
        if (ErrorHandler.Failed(queryHResult))
            return queryHResult;

        switch ((tagVSQuerySaveResult)queryResult)
        {
            case tagVSQuerySaveResult.QSR_NoSave_Cancel:
                saveCanceled = -1;
                return VSConstants.S_OK;
            case tagVSQuerySaveResult.QSR_NoSave_Continue:
                return VSConstants.S_OK;
            case tagVSQuerySaveResult.QSR_ForceSaveAs:
                return SaveThroughVisualStudio(
                    VSSAVEFLAGS.VSSAVE_SaveAs,
                    out newDocumentPath,
                    out saveCanceled);
            case tagVSQuerySaveResult.QSR_SaveOK:
                return SaveThroughVisualStudio(saveFlags, out newDocumentPath, out saveCanceled);
            default:
                return VSConstants.E_UNEXPECTED;
        }
    }

    private int SaveThroughVisualStudio(
        VSSAVEFLAGS saveFlags,
        out string newDocumentPath,
        out int saveCanceled)
    {
        var result = documentServices.SaveDocDataToFile(
            saveFlags,
            this,
            documentPath,
            out newDocumentPath,
            out saveCanceled);
        if (result == VSConstants.OLE_E_PROMPTSAVECANCELLED)
        {
            newDocumentPath = null!;
            saveCanceled = -1;
            return VSConstants.S_OK;
        }

        return result;
    }

    private int ReloadDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return VSConstants.E_INVALIDARG;

        var candidateDocumentPath = System.IO.Path.GetFullPath(path);
        if (!hostControl.TryOpenDocument(candidateDocumentPath))
            return VSConstants.E_FAIL;

        documentPath = candidateDocumentPath;
        PublishDirtyState(isDirty: false);
        DesignerEditorDiagnosticLog.Write($"DOCUMENT_RELOADED Moniker={documentPath}");
        return VSConstants.S_OK;
    }

    private void HandleDocumentDirtyChanged(object? sender, DesignerDocumentDirtyChangedEventArgs e)
        => PublishDirtyState(e.IsDirty);

    private void PublishDirtyState(bool isDirty)
    {
        if (lastKnownDirty == isDirty)
            return;

        lastKnownDirty = isDirty;
        DesignerEditorDiagnosticLog.Write($"DIRTY_CHANGED Dirty={isDirty} Cookie={documentCookie}");
        NotifyVisualStudioDirtyState();
    }

    private void NotifyVisualStudioDirtyState()
    {
        try
        {
            documentServices.UpdateDirtyState(documentCookie);
        }
        catch (Exception exception)
        {
            // The RDT may invalidate the cookie concurrently with frame teardown. Dirty state is
            // still retained in the Designer host and the next explicit IsDocDataDirty query.
            DesignerEditorDiagnosticLog.WriteException("RDT_DIRTY_UPDATE_EXCEPTION", exception);
        }
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            System.IO.Path.GetFullPath(left),
            System.IO.Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
