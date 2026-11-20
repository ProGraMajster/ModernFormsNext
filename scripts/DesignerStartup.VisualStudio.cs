#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualStudio.Shell.Interop;

/// <summary>Opens a controlled startup document in one exact running Visual Studio instance.</summary>
public static class DesignerStartupVisualStudio
{
    /// <summary>The registered ModernFormsNext editor factory identity.</summary>
    public static readonly Guid EditorFactory = new Guid("C61567C8-F5AC-4F9E-9C6E-B4EC99C7AB31");

    /// <summary>The Visual Studio Designer logical view, matching the VSIX registration.</summary>
    public static readonly Guid DesignerView = new Guid("7651A702-06E5-11D1-8EBD-00A0C90F26EA");

    /// <summary>The installed trusted package that registers the editor factory.</summary>
    public static readonly Guid DesignerPackage = new Guid("0E6A57C2-A0C1-456E-BFE1-5C8F6EB0AA11");

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    /// <summary>Requests the registered factory and verifies the returned frame before showing it.</summary>
    /// <param name="processId">The owned experimental IDE process identifier.</param>
    /// <param name="majorVersion">The IDE major version used by its automation moniker.</param>
    /// <param name="document">The absolute path of the controlled copied document.</param>
    /// <returns>The verified editor factory identity.</returns>
    /// <remarks>Call on an STA thread. The caller bounds this operation in a separate helper process.</remarks>
    public static Guid Open(int processId, int majorVersion, string document)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        if (string.IsNullOrWhiteSpace(document)) throw new ArgumentException("A document is required.", nameof(document));
        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var table));
        IBindCtx? context = null;
        IEnumMoniker? monikers = null;
        try
        {
            Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
            table.EnumRunning(out monikers);
            var batch = new IMoniker[1];
            string target = "!VisualStudio.DTE." + majorVersion + ".0:" + processId;
            while (monikers.Next(1, batch, IntPtr.Zero) == 0)
            {
                try
                {
                    string name;
                    try { batch[0].GetDisplayName(context, null, out name); }
                    // The ROT also contains unrelated applications at other integrity levels.
                    // An unreadable moniker must not prevent finding our exact owned IDE.
                    catch (COMException error) when (error.HResult == unchecked((int)0x80070005)) { continue; }
                    if (name != target) continue;
                    Marshal.ThrowExceptionForHR(table.GetObject(batch[0], out object automation));
                    try { return OpenDocument(automation, document); }
                    finally { Release(automation); }
                }
                finally { Release(batch[0]); }
            }
            throw new COMException("The exact IDE automation moniker is not available yet.", unchecked((int)0x800401E3));
        }
        finally { Release(monikers); Release(context); Release(table); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010",
        Justification = "Runs in a separate STA process. COM marshals these proxy calls to the IDE UI thread.")]
    private static Guid OpenDocument(object automation, string document)
    {
        var provider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automation;
        // SolutionExists can schedule background package loading. Explicitly finish loading
        // our installed trusted package before asking it to create the editor at cold start.
        Guid shellService = typeof(SVsShell).GUID;
        Guid shellIid = typeof(IVsShell).GUID;
        Marshal.ThrowExceptionForHR(provider.QueryService(ref shellService, ref shellIid, out var shellPointer));
        if (shellPointer == IntPtr.Zero) throw new InvalidOperationException("The IDE did not provide its shell service.");
        IVsShell? packageShell = null;
        IVsPackage? package = null;
        try
        {
            packageShell = (IVsShell)Marshal.GetObjectForIUnknown(shellPointer);
            Guid packageId = DesignerPackage;
            Marshal.ThrowExceptionForHR(packageShell.LoadPackage(ref packageId, out package));
        }
        finally { Release(package); Release(packageShell); Marshal.Release(shellPointer); }
        Guid service = typeof(SVsUIShellOpenDocument).GUID;
        Guid iid = typeof(IVsUIShellOpenDocument).GUID;
        Marshal.ThrowExceptionForHR(provider.QueryService(ref service, ref iid, out var pointer));
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("The IDE did not provide its document service.");
        IVsUIShellOpenDocument? shell = null;
        IVsWindowFrame? frame = null;
        IVsUIHierarchy? hierarchy = null;
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider? hierarchyProvider = null;
        try
        {
            shell = (IVsUIShellOpenDocument)Marshal.GetObjectForIUnknown(pointer);
            Guid editor = EditorFactory;
            Guid logicalView = DesignerView;
            // Query context without letting the project open a deferred frame first.
            // Skip deferral in the subsequent explicit factory request: a tagged frame
            // can otherwise exist long before VS calls CreateEditorInstance.
            Marshal.ThrowExceptionForHR(shell.IsDocumentInAProject(document,
                out hierarchy, out var item, out hierarchyProvider, out _));
            int openResult = shell.OpenSpecificEditor((uint)__VSOSPEFLAGS3.OSPE_SKIPDEFERRAL,
                document, ref editor, string.Empty, ref logicalView, string.Empty,
                hierarchy, item, IntPtr.Zero, hierarchyProvider, out frame);
            Marshal.ThrowExceptionForHR(openResult);
            if (frame == null) throw new InvalidOperationException("The IDE returned no editor frame. HRESULT=0x" +
                openResult.ToString("X8") + "; ProjectContext=" + (hierarchy != null) + "; Item=" + item + ".");
            Marshal.ThrowExceptionForHR(frame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_guidEditorType, out var actual));
            Marshal.ThrowExceptionForHR(frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var moniker));
            ValidateFrame(actual, moniker as string, document);
            Marshal.ThrowExceptionForHR(frame.Show());
            return actual;
        }
        finally
        {
            Release(frame); Release(hierarchy); Release(hierarchyProvider); Release(shell);
            Marshal.Release(pointer);
        }
    }

    internal static void ValidateFrame(Guid actualFactory, string? actualDocument, string expectedDocument)
    {
        if (actualFactory != EditorFactory)
            throw new InvalidOperationException("The returned frame uses a different editor factory (" + actualFactory + ").");
        if (!string.Equals(actualDocument, expectedDocument, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The returned editor frame belongs to a different document.");
    }

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
}
