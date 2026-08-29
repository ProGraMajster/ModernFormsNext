using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ModernFormsNext.VisualStudioExtension.Editors;

internal interface IVisualStudioDocumentServices
{
    int QuerySaveFile(string documentPath, out uint result);

    int SaveDocDataToFile(
        VSSAVEFLAGS saveFlags,
        object persistFile,
        string documentPath,
        out string newDocumentPath,
        out int saveCanceled);

    void UpdateDirtyState(uint documentCookie);

    RunningDocumentState GetRunningDocumentState(uint documentCookie, string documentPath);

    void ReportSaveCanceled(string message);
}

internal sealed class VisualStudioDocumentServices : IVisualStudioDocumentServices
{
    private readonly IServiceProvider serviceProvider;

    public VisualStudioDocumentServices(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public int QuerySaveFile(string documentPath, out uint result)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (serviceProvider.GetService(typeof(SVsQueryEditQuerySave)) is not IVsQueryEditQuerySave2 querySave)
        {
            result = (uint)tagVSQuerySaveResult.QSR_NoSave_Cancel;
            return VSConstants.E_NOINTERFACE;
        }

        return querySave.QuerySaveFile(documentPath, 0, null, out result);
    }

    public int SaveDocDataToFile(
        VSSAVEFLAGS saveFlags,
        object persistFile,
        string documentPath,
        out string newDocumentPath,
        out int saveCanceled)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (serviceProvider.GetService(typeof(SVsUIShell)) is not IVsUIShell uiShell)
        {
            newDocumentPath = null!;
            saveCanceled = 0;
            return VSConstants.E_NOINTERFACE;
        }

        return uiShell.SaveDocDataToFile(
            saveFlags,
            persistFile,
            documentPath,
            out newDocumentPath,
            out saveCanceled);
    }

    public void UpdateDirtyState(uint documentCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (documentCookie == VSConstants.VSCOOKIE_NIL)
            return;

        if (serviceProvider.GetService(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable4 runningDocuments
            && runningDocuments.IsCookieValid(documentCookie))
        {
            // UpdateDirtyState asks the docdata for IsDocDataDirty and refreshes the RDT-backed
            // caption and standard Save/Save All command state. It must run on Visual Studio's
            // UI thread; the host raises this notification from its WinForms polling timer.
            runningDocuments.UpdateDirtyState(documentCookie);
        }
    }

    public RunningDocumentState GetRunningDocumentState(uint documentCookie, string documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (serviceProvider.GetService(typeof(SVsRunningDocumentTable)) is not IVsRunningDocumentTable4 runningDocuments)
            return new RunningDocumentState(documentCookie, VSConstants.VSCOOKIE_NIL, isCookieValid: false, isDirty: false);

        var canonicalCookie = runningDocuments.GetDocumentCookie(documentPath);
        var isCookieValid = documentCookie != VSConstants.VSCOOKIE_NIL
            && runningDocuments.IsCookieValid(documentCookie);
        var isDirty = isCookieValid && runningDocuments.IsDocumentDirty(documentCookie);
        return new RunningDocumentState(documentCookie, canonicalCookie, isCookieValid, isDirty);
    }

    public void ReportSaveCanceled(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.ShowMessageBox(
            serviceProvider,
            message,
            ModernFormsDesignerPackage.ExtensionDisplayName,
            OLEMSGICON.OLEMSGICON_WARNING,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}

internal readonly struct RunningDocumentState
{
    public RunningDocumentState(uint cookie, uint canonicalCookie, bool isCookieValid, bool isDirty)
    {
        Cookie = cookie;
        CanonicalCookie = canonicalCookie;
        IsCookieValid = isCookieValid;
        IsDirty = isDirty;
    }

    public uint Cookie { get; }

    public uint CanonicalCookie { get; }

    public bool IsCookieValid { get; }

    public bool IsDirty { get; }
}
