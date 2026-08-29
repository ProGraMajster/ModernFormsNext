using System;
using System.Windows.Forms;
using ModernFormsNext.VisualStudioExtension.Commands;

namespace ModernFormsNext.VisualStudioExtension.Editors;

internal interface IDesignerDocumentHost : IDisposable
{
    event EventHandler<DesignerDocumentDirtyChangedEventArgs>? DocumentDirtyChanged;

    IWin32Window Window { get; }

    bool TryOpenDocument(string path);

    DesignerHostSaveResult SaveDocument();

    bool TryDiscardDocumentRecovery();

    bool TryGetDocumentDirty(out bool isDirty);
}

internal sealed class DesignerDocumentDirtyChangedEventArgs : EventArgs
{
    public DesignerDocumentDirtyChangedEventArgs(bool isDirty)
    {
        IsDirty = isDirty;
    }

    public bool IsDirty { get; }
}
