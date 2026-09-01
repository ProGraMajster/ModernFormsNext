using System;
using Microsoft.VisualStudio;
using IOleCommandTarget = Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget;
using OLECMD = Microsoft.VisualStudio.OLE.Interop.OLECMD;

namespace ModernFormsNext.VisualStudioExtension.Editors;

/// <summary>
/// Routes the standard Visual Studio Save command to the active ModernFormsNext Designer pane.
/// </summary>
/// <remarks>
/// Visual Studio's global Save implementation can call <c>SaveDocData</c> directly instead of
/// offering the command to an out-of-process child view. Registering this narrow priority target
/// ensures Ctrl+S reaches the pane first, where the cross-process save is deferred until the
/// synchronous accelerator dispatch has returned to the Designer host.
/// </remarks>
internal sealed class DesignerSaveCommandTarget : IOleCommandTarget
{
    private readonly Func<MfDesignEditorPane?> getActivePane;

    public DesignerSaveCommandTarget(Func<MfDesignEditorPane?> getActivePane)
    {
        this.getActivePane = getActivePane ?? throw new ArgumentNullException(nameof(getActivePane));
    }

    public int QueryStatus(
        ref Guid pguidCmdGroup,
        uint cCmds,
        OLECMD[] prgCmds,
        IntPtr pCmdText)
    {
        if (!IsStandardSaveQuery(pguidCmdGroup, cCmds, prgCmds))
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

        var pane = getActivePane();
        return pane is null
            ? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED
#pragma warning disable VSTHRD010 // Visual Studio invokes registered priority command targets on its UI thread.
            : ((IOleCommandTarget)pane).QueryStatus(
                ref pguidCmdGroup,
                cCmds,
                prgCmds,
                pCmdText);
#pragma warning restore VSTHRD010
    }

    public int Exec(
        ref Guid pguidCmdGroup,
        uint nCmdID,
        uint nCmdexecopt,
        IntPtr pvaIn,
        IntPtr pvaOut)
    {
        if (pguidCmdGroup != VSConstants.GUID_VSStandardCommandSet97
            || nCmdID != (uint)VSConstants.VSStd97CmdID.Save)
        {
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        var pane = getActivePane();
        return pane is null
            ? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED
#pragma warning disable VSTHRD010 // Visual Studio invokes registered priority command targets on its UI thread.
            : ((IOleCommandTarget)pane).Exec(
                ref pguidCmdGroup,
                nCmdID,
                nCmdexecopt,
                pvaIn,
                pvaOut);
#pragma warning restore VSTHRD010
    }

    private static bool IsStandardSaveQuery(Guid commandGroup, uint commandCount, OLECMD[]? commands)
    {
        if (commandGroup != VSConstants.GUID_VSStandardCommandSet97 || commands is null)
            return false;

        var count = Math.Min((int)commandCount, commands.Length);
        for (var index = 0; index < count; index++)
        {
            if (commands[index].cmdID == (uint)VSConstants.VSStd97CmdID.Save)
                return true;
        }

        return false;
    }
}
