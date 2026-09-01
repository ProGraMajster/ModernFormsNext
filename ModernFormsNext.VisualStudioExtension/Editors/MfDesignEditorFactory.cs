using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Creates Visual Studio editor panes for ModernFormsNext <c>.mfdesign</c> files.
/// </summary>
[Guid(ModernFormsDesignerPackage.EditorFactoryGuidString)]
public sealed class MfDesignEditorFactory : IVsEditorFactory
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfDesignEditorFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The package service provider.</param>
    public MfDesignEditorFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        => VSConstants.S_OK;

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
        => serviceProvider.GetService(serviceType);

    /// <inheritdoc/>
    public int Close()
        => VSConstants.S_OK;

    /// <inheritdoc/>
    public int MapLogicalView(ref Guid rguidLogicalView, out string? pbstrPhysicalView)
    {
        pbstrPhysicalView = null;

        return rguidLogicalView == VSConstants.LOGVIEWID_Primary
            || rguidLogicalView == VSConstants.LOGVIEWID_Designer
            ? VSConstants.S_OK
            : VSConstants.E_NOTIMPL;
    }

    /// <inheritdoc/>
    public int CreateEditorInstance(
        uint grfCreateDoc,
        string pszMkDocument,
        string pszPhysicalView,
        IVsHierarchy pvHier,
        uint itemid,
        IntPtr punkDocDataExisting,
        out IntPtr ppunkDocView,
        out IntPtr ppunkDocData,
        out string pbstrEditorCaption,
        out Guid pguidCmdUI,
        out int pgrfCDW)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        ppunkDocView = IntPtr.Zero;
        ppunkDocData = IntPtr.Zero;
        pbstrEditorCaption = " [Design]";
        pguidCmdUI = ModernFormsDesignerPackage.CommandSetGuid;
        pgrfCDW = 0;

        if (punkDocDataExisting != IntPtr.Zero)
            return VSConstants.VS_E_INCOMPATIBLEDOCDATA;

        try
        {
            var pane = new MfDesignEditorPane(serviceProvider, pszMkDocument);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);
            ppunkDocData = Marshal.GetIUnknownForObject(pane);
            return VSConstants.S_OK;
        }
        catch (Exception ex)
        {
            // Initialization failures must not leave partially attached HWND state behind. The
            // host lifecycle has already rolled back native state; report the actionable reason
            // through Visual Studio and return its HRESULT to the editor infrastructure.
            VsShellUtilities.ShowMessageBox(
                serviceProvider,
                $"ModernFormsNext Designer could not initialize.{Environment.NewLine}{ex.Message}",
                ModernFormsDesignerPackage.ExtensionDisplayName,
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return Marshal.GetHRForException(ex);
        }
    }
}
