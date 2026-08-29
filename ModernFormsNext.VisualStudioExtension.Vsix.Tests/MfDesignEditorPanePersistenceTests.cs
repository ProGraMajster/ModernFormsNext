using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension.Editors;
using Xunit;

// The fake shell invokes every COM callback synchronously on the test thread, which is the
// contract-level equivalent of Visual Studio's UI thread for these tests.
#pragma warning disable VSTHRD010

namespace ModernFormsNext.VisualStudioExtension.Vsix.Tests;

public sealed class MfDesignEditorPanePersistenceTests
{
    [Fact]
    public void DirtyNotificationEnablesStandardSaveAndSuccessfulSaveReturnsRdtToClean()
    {
        using var context = new EditorContext();
        context.Pane.OnRegisterDocData(41, null!, VSConstants.VSITEMID_NIL);

        context.Host.SetDirty(true);

        Assert.Equal(new uint[] { 41, 41 }, context.Services.DirtyUpdates);
        Assert.Equal(VSConstants.S_OK, context.Pane.IsDirty(out var dirtyBeforeSave));
        Assert.Equal(1, dirtyBeforeSave);

        var result = ExecuteStandardSave(context.Pane, out var canceled);

        Assert.Equal(VSConstants.S_OK, result);
        Assert.Equal(0, canceled);
        Assert.Equal(1, context.Host.SaveCount);
        Assert.Equal(VSConstants.S_OK, context.Pane.IsDirty(out var dirtyAfterSave));
        Assert.Equal(0, dirtyAfterSave);
        Assert.Equal(new uint[] { 41, 41, 41 }, context.Services.DirtyUpdates);
    }

    [Fact]
    public void CleanCloseDoesNotSaveAndShutsHostDownOnlyWhenThePaneIsDisposed()
    {
        using var context = new EditorContext();

        var closeResult = SimulateClose(context, CloseChoice.Save);

        Assert.Equal(VSConstants.S_OK, closeResult);
        Assert.Equal(0, context.Host.SaveCount);
        Assert.Equal(0, context.Host.DisposeCount);

        context.Pane.Dispose();

        Assert.Equal(1, context.Host.DisposeCount);
    }

    [Fact]
    public void DirtyCloseWithSavePersistsThenClosesAndDisposesExactlyOnce()
    {
        using var context = new EditorContext();
        context.Host.SetDirty(true);

        var closeResult = SimulateClose(context, CloseChoice.Save);
        context.Pane.Dispose();
        context.Pane.Dispose();

        Assert.Equal(VSConstants.S_OK, closeResult);
        Assert.Equal(1, context.Host.SaveCount);
        Assert.False(context.Host.IsDirty);
        Assert.Equal(1, context.Host.DisposeCount);
    }

    [Fact]
    public void DirtyCloseWithDontSaveDoesNotInvokePersistence()
    {
        using var context = new EditorContext();
        context.Host.SetDirty(true);

        var closeResult = SimulateClose(context, CloseChoice.DontSave);
        context.Pane.Dispose();

        Assert.Equal(VSConstants.S_OK, closeResult);
        Assert.Equal(0, context.Host.SaveCount);
        Assert.Equal(1, context.Host.DiscardCount);
        Assert.Equal(1, context.Host.DisposeCount);
    }

    [Fact]
    public void DirtyCloseWithCancelKeepsPaneHostAndDirtyStateAlive()
    {
        using var context = new EditorContext();
        context.Host.SetDirty(true);

        var closeResult = SimulateClose(context, CloseChoice.Cancel);

        Assert.Equal(VSConstants.OLE_E_PROMPTSAVECANCELLED, closeResult);
        Assert.Equal(0, context.Host.SaveCount);
        Assert.Equal(0, context.Host.DiscardCount);
        Assert.Equal(0, context.Host.DisposeCount);
        Assert.Equal(VSConstants.S_OK, context.Pane.IsDirty(out var dirty));
        Assert.Equal(1, dirty);
    }

    [Fact]
    public void SaveRefusalIsReportedAsCanceledWithoutEFailAndPaneRemainsDirtyOpen()
    {
        using var context = new EditorContext();
        context.Host.SetDirty(true);
        context.Host.NextSaveResult = DesignerHostSaveResult.Canceled(
            "Save is unavailable until the active Designer transaction completes.");

        var result = ExecuteStandardSave(context.Pane, out var canceled);

        Assert.Equal(VSConstants.S_OK, result);
        Assert.NotEqual(0, canceled);
        Assert.Equal(1, context.Host.SaveCount);
        Assert.Equal(0, context.Host.DisposeCount);
        Assert.True(context.Host.IsDirty);
        Assert.Single(context.Services.SaveCanceledMessages);
        Assert.Contains("transaction completes", context.Services.SaveCanceledMessages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySaveCancelDoesNotCallHostOrReturnFailure()
    {
        using var context = new EditorContext();
        context.Host.SetDirty(true);
        context.Services.QuerySaveResult = tagVSQuerySaveResult.QSR_NoSave_Cancel;

        var result = ExecuteStandardSave(context.Pane, out var canceled);

        Assert.Equal(VSConstants.S_OK, result);
        Assert.NotEqual(0, canceled);
        Assert.Equal(0, context.Host.SaveCount);
        Assert.True(context.Host.IsDirty);
    }

    [Fact]
    public void CloseIsIdempotentAndAStaleDirtyCallbackAfterDisposeCannotReachRdt()
    {
        using var context = new EditorContext();
        context.Pane.OnRegisterDocData(73, null!, VSConstants.VSITEMID_NIL);
        context.Host.SetDirty(true);
        Assert.Equal(VSConstants.S_OK, context.Pane.Close());
        Assert.Equal(VSConstants.S_OK, context.Pane.Close());

        var updatesAfterClose = context.Services.DirtyUpdates.Count;
        context.Host.SetDirty(false);
        Assert.Equal(updatesAfterClose, context.Services.DirtyUpdates.Count);

        context.Pane.Dispose();
        context.Pane.Dispose();
        var updatesBeforeStaleCallback = context.Services.DirtyUpdates.Count;
        context.Host.SetDirty(true);

        Assert.Equal(1, context.Host.DisposeCount);
        Assert.Equal(1, context.Host.DiscardCount);
        Assert.Equal(updatesBeforeStaleCallback, context.Services.DirtyUpdates.Count);
    }

    [Fact]
    public void SaveAllPersistsEveryDirtyDocDataIndependently()
    {
        using var first = new EditorContext("First.mfdesign");
        using var second = new EditorContext("Second.mfdesign");
        first.Host.SetDirty(true);
        second.Host.SetDirty(true);

        var firstResult = ExecuteStandardSave(first.Pane, out var firstCanceled);
        var secondResult = ExecuteStandardSave(second.Pane, out var secondCanceled);

        Assert.Equal(VSConstants.S_OK, firstResult);
        Assert.Equal(VSConstants.S_OK, secondResult);
        Assert.Equal(0, firstCanceled);
        Assert.Equal(0, secondCanceled);
        Assert.Equal(1, first.Host.SaveCount);
        Assert.Equal(1, second.Host.SaveCount);
        Assert.False(first.Host.IsDirty);
        Assert.False(second.Host.IsDirty);
    }

    [Fact]
    public void OrdinarySaveKeepsMonikerNullWhileSaveAsReturnsTheNewMoniker()
    {
        using var context = new EditorContext();

        Assert.Equal(
            VSConstants.S_OK,
            context.Pane.SaveDocData(VSSAVEFLAGS.VSSAVE_Save, out var ordinaryMoniker, out var ordinaryCanceled));
        Assert.Null(ordinaryMoniker);
        Assert.Equal(0, ordinaryCanceled);

        context.Services.SaveAsTargetPath = Path.Combine(context.DirectoryPath, "Renamed.mfdesign");
        Assert.Equal(
            VSConstants.S_OK,
            context.Pane.SaveDocData(VSSAVEFLAGS.VSSAVE_SaveAs, out var renamedMoniker, out var saveAsCanceled));
        Assert.Equal(context.Services.SaveAsTargetPath, renamedMoniker);
        Assert.Equal(0, saveAsCanceled);
    }

    private static int ExecuteStandardSave(MfDesignEditorPane pane, out int canceled)
        => pane.SaveDocData(VSSAVEFLAGS.VSSAVE_Save, out _, out canceled);

    private static int SimulateClose(EditorContext context, CloseChoice choice)
    {
        Assert.Equal(VSConstants.S_OK, context.Pane.IsDirty(out var dirty));
        if (dirty == 0)
            return context.Pane.Close();
        if (choice == CloseChoice.Cancel)
            return VSConstants.OLE_E_PROMPTSAVECANCELLED;
        if (choice == CloseChoice.Save)
        {
            var saveResult = ExecuteStandardSave(context.Pane, out var canceled);
            if (ErrorHandler.Failed(saveResult) || canceled != 0)
                return canceled != 0 ? VSConstants.OLE_E_PROMPTSAVECANCELLED : saveResult;
        }

        return context.Pane.Close();
    }

    private enum CloseChoice
    {
        Save,
        DontSave,
        Cancel
    }

    private sealed class EditorContext : IDisposable
    {
        public EditorContext(string fileName = "Form1.mfdesign")
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "ModernFormsNext-VsixContract-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(DirectoryPath);
            var documentPath = Path.Combine(DirectoryPath, fileName);
            Host = new FakeDesignerDocumentHost();
            Services = new FakeVisualStudioDocumentServices();
            Pane = new MfDesignEditorPane(new EmptyServiceProvider(), documentPath, Host, Services);
        }

        public string DirectoryPath { get; }

        public FakeDesignerDocumentHost Host { get; }

        public FakeVisualStudioDocumentServices Services { get; }

        public MfDesignEditorPane Pane { get; }

        public void Dispose()
        {
            Pane.Dispose();
            try
            {
                System.IO.Directory.Delete(DirectoryPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeDesignerDocumentHost : IDesignerDocumentHost, IWin32Window
    {
        public event EventHandler<DesignerDocumentDirtyChangedEventArgs>? DocumentDirtyChanged;

        public IWin32Window Window => this;

        public IntPtr Handle => IntPtr.Zero;

        public bool IsDirty { get; private set; }

        public int SaveCount { get; private set; }

        public int DisposeCount { get; private set; }

        public int DiscardCount { get; private set; }

        public DesignerHostSaveResult NextSaveResult { get; set; } = DesignerHostSaveResult.Saved;

        public bool TryOpenDocument(string path)
        {
            SetDirty(false);
            return true;
        }

        public DesignerHostSaveResult SaveDocument()
        {
            SaveCount++;
            var result = NextSaveResult;
            NextSaveResult = DesignerHostSaveResult.Saved;
            if (result.Outcome == DesignerHostSaveOutcome.Saved)
                SetDirty(false);
            return result;
        }

        public bool TryGetDocumentDirty(out bool isDirty)
        {
            isDirty = IsDirty;
            return true;
        }

        public bool TryDiscardDocumentRecovery()
        {
            DiscardCount++;
            return true;
        }

        public void SetDirty(bool isDirty)
        {
            IsDirty = isDirty;
            DocumentDirtyChanged?.Invoke(this, new DesignerDocumentDirtyChangedEventArgs(isDirty));
        }

        public void Dispose()
        {
            DisposeCount++;
            DocumentDirtyChanged = null;
        }
    }

    private sealed class FakeVisualStudioDocumentServices : IVisualStudioDocumentServices
    {
        public tagVSQuerySaveResult QuerySaveResult { get; set; } = tagVSQuerySaveResult.QSR_SaveOK;

        public string? SaveAsTargetPath { get; set; }

        public List<uint> DirtyUpdates { get; } = new();

        public List<string> SaveCanceledMessages { get; } = new();

        public int QuerySaveFile(string documentPath, out uint result)
        {
            result = (uint)QuerySaveResult;
            return VSConstants.S_OK;
        }

        public int SaveDocDataToFile(
            VSSAVEFLAGS saveFlags,
            object persistFile,
            string documentPath,
            out string newDocumentPath,
            out int saveCanceled)
        {
            var persistence = Assert.IsAssignableFrom<IPersistFileFormat>(persistFile);
            var target = saveFlags == VSSAVEFLAGS.VSSAVE_SaveAs
                ? SaveAsTargetPath ?? throw new InvalidOperationException("The fake Save As target was not configured.")
                : documentPath;
            var result = persistence.Save(target, 1, 0);
            if (result == VSConstants.OLE_E_PROMPTSAVECANCELLED)
            {
                newDocumentPath = null!;
                saveCanceled = -1;
                return result;
            }

            newDocumentPath = saveFlags == VSSAVEFLAGS.VSSAVE_SaveAs ? target : null!;
            saveCanceled = 0;
            return result;
        }

        public void UpdateDirtyState(uint documentCookie)
            => DirtyUpdates.Add(documentCookie);

        public void ReportSaveCanceled(string message)
            => SaveCanceledMessages.Add(message);
    }
}
