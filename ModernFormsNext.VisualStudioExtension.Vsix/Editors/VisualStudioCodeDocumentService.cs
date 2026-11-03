using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace ModernFormsNext.VisualStudioExtension.Editors;

// The VS editor owns live text, checkout, dirty state and undo. Do not replace this with disk
// writes or auto-save. The small buffer adapter also allows conflict/reentrancy tests without VS.
internal sealed class VisualStudioCodeDocumentService(IServiceProvider services)
{
    internal string Execute(DesignerCodeRequest request, Func<string> currentDesignPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return Execute(request, currentDesignPath, path =>
        { ThreadHelper.ThrowIfNotOnUIThread(); return new VisualStudioCodeBuffer(services, path); });
    }

    internal static string Execute(DesignerCodeRequest request, Func<string> currentDesignPath,
        Func<string, IDesignerCodeBuffer> openBuffer)
    {
        string Result(bool success, string? text = null) => DesignerCodeRequest.Result(request.Id, success, text);
        bool Current() => request.IsAuthorized(currentDesignPath());
        try
        {
            if (!Current()) return Result(false);
            var buffer = openBuffer(request.SourcePath);
            var text = buffer.ReadText();
            if (text.Length > DesignerCodeRequest.MaximumTextCharacters || !Current()) return Result(false);
            if (request.Operation == "READ") return Result(true, text);
            if (text != request.ExpectedText || request.Offset < 0 || request.Offset > text.Length) return Result(false);
            if (request.Operation == "INSERT")
            {
                if (request.Insertion.Length > DesignerCodeRequest.MaximumInsertionCharacters
                    || text.Length + request.Insertion.Length > DesignerCodeRequest.MaximumTextCharacters
                    || !buffer.QueryEdit() || !Current()) return Result(false);
                // QueryEdit can show UI and reenter VS. Reopen the current RDT buffer and re-read
                // after that callback, rather than applying offsets to an old/closed document.
                buffer = openBuffer(request.SourcePath);
                if (buffer.ReadText() != request.ExpectedText || !Current()) return Result(false);
                return Result(buffer.Insert(request.Offset, request.Insertion));
            }
            if (request.Operation == "NAVIGATE")
                return Result(buffer.Navigate(request.ExpectedText, request.Offset, Current));
        }
        catch (Exception ex)
        {
            // Never include transported source text in diagnostics.
            DesignerEditorDiagnosticLog.WriteException($"CODE_{request.Operation}_FAILED", ex);
        }
        return Result(false);
    }
}

internal interface IDesignerCodeBuffer
{
    string ReadText();
    bool QueryEdit();
    bool Insert(int offset, string text);
    bool Navigate(string expectedText, int offset, Func<bool> isCurrent);
}

internal sealed class VisualStudioCodeBuffer : IDesignerCodeBuffer
{
    private readonly IServiceProvider services;
    private readonly string path;
    private readonly IVsTextLines buffer;
    private readonly IVsWindowFrame frame;
    private readonly IVsTextView? view;

    internal VisualStudioCodeBuffer(IServiceProvider services, string path)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.services = services;
        this.path = path;
        VsShellUtilities.OpenDocument(services, path, VSConstants.LOGVIEWID_Code, out _, out _, out frame, out var textView);
        view = textView;
        ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out var data));
        if (data is IVsTextLines lines) buffer = lines;
        else if (data is IVsTextBufferProvider provider)
        { ErrorHandler.ThrowOnFailure(provider.GetTextBuffer(out lines)); buffer = lines; }
        else throw new InvalidOperationException("The paired C# document has no supported live text buffer.");
    }

    public string ReadText()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorHandler.ThrowOnFailure(buffer.GetSize(out var length));
        if (length < 0 || length > DesignerCodeRequest.MaximumTextCharacters)
            throw new InvalidOperationException("The C# buffer exceeds the event source safety budget.");
        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var lastLine, out var lastColumn));
        ErrorHandler.ThrowOnFailure(buffer.GetLineText(0, 0, lastLine, lastColumn, out var text));
        if (text.Length > DesignerCodeRequest.MaximumTextCharacters)
            throw new InvalidOperationException("The C# buffer changed beyond the event source safety budget.");
        return text;
    }

    public bool QueryEdit()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (services.GetService(typeof(SVsQueryEditQuerySave)) is not IVsQueryEditQuerySave2 queryEdit) return false;
        ErrorHandler.ThrowOnFailure(queryEdit.QueryEditFiles(0, 1, new[] { path }, null, null, out var result, out _));
        return result == (uint)tagVSQueryEditResult.QER_EditOK;
    }

    public bool Insert(int offset, string text)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorHandler.ThrowOnFailure(buffer.GetLineIndexOfPosition(offset, out var line, out var column));
        var insertion = Marshal.StringToCoTaskMemUni(text);
        try
        {
            // One insertion is one native text-buffer edit/undo operation. Existing source is
            // never replaced and saving remains the user's normal VS action.
            ErrorHandler.ThrowOnFailure(buffer.ReplaceLines(line, column, line, column, insertion, text.Length, new TextSpan[1]));
        }
        finally { Marshal.FreeCoTaskMem(insertion); }
        return true;
    }

    public bool Navigate(string expectedText, int offset, Func<bool> isCurrent)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorHandler.ThrowOnFailure(frame.Show());
        if (view is null || !isCurrent() || ReadText() != expectedText) return false;
        ErrorHandler.ThrowOnFailure(buffer.GetLineIndexOfPosition(offset, out var line, out var column));
        ErrorHandler.ThrowOnFailure(view.SetCaretPos(line, column));
        ErrorHandler.ThrowOnFailure(view.EnsureSpanVisible(new TextSpan { iStartLine = line, iEndLine = line,
            iStartIndex = column, iEndIndex = column }));
        return true;
    }
}
