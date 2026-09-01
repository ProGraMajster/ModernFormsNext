using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Creates the lightweight Visual Studio pane that owns an out-of-process Designer host.
/// </summary>
[Guid(ModernFormsDesignerPackage.EditorFactoryGuidString)]
public sealed class MfDesignEditorFactory : IVsEditorFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly Func<DesignerHostingMode> hostingModeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfDesignEditorFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The owning Visual Studio package.</param>
    public MfDesignEditorFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        hostingModeProvider = serviceProvider is ModernFormsDesignerPackage package
            ? package.GetDesignerHostingMode
            : static () => DesignerHostingMode.Integrated;
    }

    internal MfDesignEditorFactory(
        IServiceProvider serviceProvider,
        Func<DesignerHostingMode> hostingModeProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.hostingModeProvider = hostingModeProvider
            ?? throw new ArgumentNullException(nameof(hostingModeProvider));
    }

    /// <inheritdoc/>
    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp) => VSConstants.S_OK;

    /// <inheritdoc/>
    public object? GetService(Type serviceType) => serviceProvider.GetService(serviceType);

    /// <inheritdoc/>
    public int Close() => VSConstants.S_OK;

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
            var pane = CreateEditorPane(pszMkDocument);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);
            ppunkDocData = Marshal.GetIUnknownForObject(pane);
            return VSConstants.S_OK;
        }
        catch (Exception ex)
        {
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

    internal MfDesignEditorPane CreateEditorPane(string documentPath)
        => new(serviceProvider, documentPath, hostingModeProvider());
}
