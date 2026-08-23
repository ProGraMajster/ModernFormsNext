using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerPersistenceCoordinatorTests
{
    private static readonly DateTimeOffset TestStartUtc = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
    private static readonly DesignerRecoverySessionIdentity TestRecoverySession = new(
        Guid.Parse("4ce28bc8-ad65-45a1-93e6-bb65437660c7"),
        4101);

    [Fact]
    public async Task DirtyCommitSchedulesRecoveryButCleanDocumentDoesNot()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);

        Assert.False(test.Coordinator.GetActiveDiagnostics().AutosavePending);
        test.Scheduler.AdvanceBy(TimeSpan.FromMinutes(10));
        await test.DrainAsync();
        Assert.Empty(test.Store.Writes);

        test.Edit("first");

        Assert.True(test.Coordinator.GetActiveDiagnostics().AutosavePending);
        await test.FireDebounceAsync();
        Assert.Single(test.Store.Writes);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task RapidCommittedEditsCoalesceIntoOneLatestRecovery(int editCount)
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);

        for (var index = 0; index < editCount; index++)
            test.Edit($"edit-{index}");

        await test.FireDebounceAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.Equal(editCount, snapshot.Metadata.DirtyRevision);
        Assert.Contains($"edit-{editCount - 1}", snapshot.SerializedDesignDocument, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClipboardUndoRedoAutosaveRestoresExactProtectedTreeAndRevisionPayload()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        var original = test.Session.Document.Controls[0];
        test.Session.SelectNode(original);
        Assert.True(test.Session.CopySelectedNode());
        test.Session.SelectNode(null);
        Assert.True(test.Session.PasteCopiedNode());
        Assert.Equal(2, test.Session.Document.Controls.Count);
        Assert.True(test.Session.DuplicateSelectedNode());
        Assert.Equal(3, test.Session.Document.Controls.Count);
        Assert.True(test.Session.Transactions.Undo());
        Assert.Equal(2, test.Session.Document.Controls.Count);
        Assert.True(test.Session.Transactions.Redo());
        Assert.Equal(3, test.Session.Document.Controls.Count);
        var protectedTree = DesignDocumentSerializer.Default.Serialize(test.Session.Document);
        var protectedRevision = test.Session.ActiveOpenDocument!.History.CurrentRevision;

        await test.FireDebounceAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.Equal(protectedRevision, snapshot.Metadata.DirtyRevision);
        Assert.Equal(protectedTree, snapshot.SerializedDesignDocument);
        Assert.Equal(
            DesignerFileHash.ComputeUtf8Sha256(protectedTree),
            DesignerFileHash.ComputeUtf8Sha256(snapshot.SerializedDesignDocument));

        using var restarted = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) => store.DiscoveryCandidates.Add(CreateCandidate(store, snapshot)));
        var notice = Assert.IsType<DesignerPersistenceNotification>(restarted.Coordinator.CurrentNotification);
        Assert.True(restarted.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var error), error);

        Assert.Equal(protectedTree, DesignDocumentSerializer.Default.Serialize(restarted.Session.Document));
        Assert.Equal(["button1", "button2", "button3"], restarted.Session.Document.Controls.Select(node => node.Name));
        Assert.True(restarted.Session.IsDirty);
        Assert.False(restarted.Session.Transactions.CanUndo);
        Assert.Equal(protectedRevision, snapshot.Metadata.DirtyRevision);
    }

    [Fact]
    public async Task BoundedEditSaveWatcherStressRestoresFinalRecoveryHashExactly()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        for (var index = 0; index < 1000; index++)
            test.Edit($"initial stress {index}");
        await test.FireDebounceAsync();
        Assert.True(test.Coordinator.SaveActiveDocument(test.DocumentPath!).Succeeded);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            for (var edit = 0; edit < 10; edit++)
                test.Edit($"save cycle {cycle} edit {edit}");
            var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
            Assert.True(save.Succeeded, save.Error);
            var watcher = test.Watchers.Latest(test.DocumentPath!);
            for (var burst = 0; burst < 25; burst++)
                watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
            await test.FireExternalDebounceAsync();
            Assert.Null(test.Coordinator.CurrentNotification);
        }

        for (var index = 0; index < 50; index++)
            test.Edit($"final protected edit {index}");
        var finalTree = DesignDocumentSerializer.Default.Serialize(test.Session.Document);
        var finalRevision = test.Session.ActiveOpenDocument!.History.CurrentRevision;
        await test.FireDebounceAsync();
        var finalSnapshot = test.Store.Writes.Last();

        Assert.Equal(finalRevision, finalSnapshot.Metadata.DirtyRevision);
        Assert.Equal(finalTree, finalSnapshot.SerializedDesignDocument);
        var finalHash = DesignerFileHash.ComputeUtf8Sha256(finalSnapshot.SerializedDesignDocument);
        using var restarted = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) => store.DiscoveryCandidates.Add(CreateCandidate(store, finalSnapshot)));
        var notice = Assert.IsType<DesignerPersistenceNotification>(restarted.Coordinator.CurrentNotification);
        Assert.True(restarted.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var error), error);

        var restoredTree = DesignDocumentSerializer.Default.Serialize(restarted.Session.Document);
        Assert.Equal(finalTree, restoredTree);
        Assert.Equal(finalHash, DesignerFileHash.ComputeUtf8Sha256(restoredTree));
        Assert.Equal("final protected edit 49", restarted.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(restarted.Session.IsDirty);
    }

    [Fact]
    public async Task MaximumIntervalProtectsContinuouslyEditedDocument()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureOptions: options =>
            {
                options.AutoSaveDebounceDelay = TimeSpan.FromSeconds(10);
                options.AutoSaveMaximumInterval = TimeSpan.FromSeconds(3);
            });

        test.Edit("one");
        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        test.Edit("two");
        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        test.Edit("three");
        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        await test.DrainAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.Equal(3, snapshot.Metadata.DirtyRevision);
        Assert.Contains("three", snapshot.SerializedDesignDocument, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveAndNestedTransactionsBlockRecoveryUntilOutermostCommit()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        using var outer = test.Session.Transactions.Begin("outer");
        test.Edit("outer edit");
        using (var nested = test.Session.Transactions.Begin("nested"))
        {
            test.Edit("nested edit");
            nested.Commit();
        }

        test.Scheduler.AdvanceBy(TimeSpan.FromMinutes(10));
        await test.DrainAsync();
        Assert.Empty(test.Store.Writes);

        outer.Commit();
        await test.FireDebounceAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.Equal(1, snapshot.Metadata.DirtyRevision);
        Assert.Contains("nested edit", snapshot.SerializedDesignDocument, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RolledBackTransactionDoesNotCreateRecovery()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        using (var transaction = test.Session.Transactions.Begin("cancelled edit"))
        {
            test.Edit("temporary");
            transaction.Rollback();
        }

        test.Scheduler.AdvanceBy(TimeSpan.FromMinutes(10));
        await test.DrainAsync();

        Assert.False(test.Session.IsDirty);
        Assert.Empty(test.Store.Writes);
    }

    [Fact]
    public async Task UndoBackToSavedRevisionCancelsPendingRecovery()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("will be undone");
        Assert.True(test.Coordinator.GetActiveDiagnostics().AutosavePending);

        Assert.True(test.Session.Transactions.Undo());
        test.Scheduler.AdvanceBy(TimeSpan.FromMinutes(10));
        await test.DrainAsync();

        Assert.False(test.Session.IsDirty);
        Assert.Empty(test.Store.Writes);
        Assert.False(test.Coordinator.GetActiveDiagnostics().AutosavePending);
    }

    [Fact]
    public async Task RecoveryDoesNotChangeDirtyStateHistorySavedRevisionOrCanonicalFile()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var originalCanonicalText = File.ReadAllText(test.DocumentPath!);
        var document = test.Session.ActiveOpenDocument!;

        test.Edit("protected local edit");
        var revision = document.History.CurrentRevision;
        var savedRevision = document.History.SavedRevision;
        await test.FireDebounceAsync();

        Assert.True(document.IsDirty);
        Assert.True(test.Session.Transactions.CanUndo);
        Assert.Equal(revision, document.History.CurrentRevision);
        Assert.Equal(savedRevision, document.History.SavedRevision);
        Assert.Equal(originalCanonicalText, File.ReadAllText(test.DocumentPath!));
    }

    [Fact]
    public async Task UnsavedDocumentRecoveryHasTemporaryIdentityAndNoCanonicalPath()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        var openDocument = test.Session.ActiveOpenDocument!;

        test.Edit("scratch edit");
        await test.FireDebounceAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.True(snapshot.Metadata.IsUnsaved);
        Assert.Null(snapshot.Metadata.DocumentPath);
        Assert.Equal(openDocument.Id, snapshot.Metadata.TemporaryDocumentId);
        Assert.StartsWith("unsaved:", snapshot.Metadata.DocumentIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewerRevisionDuringBlockedWriteSchedulesExactlyOneFollowUp()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        using var firstWriteEntered = new ManualResetEventSlim();
        using var releaseFirstWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            if (test.Store.WriteCallCount == 1)
            {
                firstWriteEntered.Set();
                Assert.True(releaseFirstWrite.Wait(TimeSpan.FromSeconds(10)));
            }

            return test.Store.Success(snapshot);
        };

        test.Edit("first");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(firstWriteEntered.Wait(TimeSpan.FromSeconds(10)));

        test.Edit("second");
        Assert.True(test.Coordinator.GetActiveDiagnostics().AutosavePending);
        releaseFirstWrite.Set();
        await test.DrainAsync();
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        await test.DrainAsync();

        Assert.Equal(2, test.Store.WriteCallCount);
        Assert.Equal(1, test.Store.MaxConcurrentWriteCount);
        Assert.Contains("first", test.Store.Writes.ElementAt(0).SerializedDesignDocument, StringComparison.Ordinal);
        Assert.Contains("second", test.Store.Writes.ElementAt(1).SerializedDesignDocument, StringComparison.Ordinal);
        Assert.False(test.Coordinator.GetActiveDiagnostics().AutosaveInProgress);
    }

    [Fact]
    public void NewerSynchronousRecoverySupersedesQueuedAutosaveBeforeItEntersWriteGate()
    {
        var backgroundWork = new ManualDesignerBackgroundWorkQueue();
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            backgroundWorkQueue: backgroundWork);
        test.Edit("queued revision one");
        var firstRevision = test.Session.ActiveOpenDocument!.History.CurrentRevision;
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);

        Assert.Equal(1, backgroundWork.PendingCount);
        Assert.Equal(0, test.Store.WriteCallCount);
        Assert.True(test.Coordinator.GetActiveDiagnostics().AutosaveInProgress);

        test.Edit("synchronous revision two");
        var winningRevision = test.Session.ActiveOpenDocument.History.CurrentRevision;
        Assert.True(winningRevision > firstRevision);
        Assert.True(test.Coordinator.EnsureRecoveryNow(
            test.Session.ActiveOpenDocument,
            out var error), error);

        var winningSnapshot = Assert.Single(test.Store.Writes);
        var winningArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Equal(winningRevision, winningSnapshot.Metadata.DirtyRevision);
        Assert.Contains("synchronous revision two", winningSnapshot.SerializedDesignDocument, StringComparison.Ordinal);
        Assert.Equal(winningArtifact, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);

        backgroundWork.RunNext();
        test.Dispatcher.DrainAll();
        test.WaitForBackgroundOnly();

        var diagnostics = test.Coordinator.GetActiveDiagnostics();
        Assert.Equal(0, backgroundWork.PendingCount);
        Assert.Equal(1, test.Store.WriteCallCount);
        Assert.Single(test.Store.Writes);
        Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Equal(winningRevision, diagnostics.LastAutosavedRevision);
        Assert.Equal(winningSnapshot.Metadata.RevisionGeneration, diagnostics.LastAutosavedGeneration);
        Assert.Equal(winningArtifact, diagnostics.RecoveryArtifactPath);
        Assert.DoesNotContain(winningArtifact, test.Store.DeletedPaths);
    }

    [Fact]
    public void NewerNormalSaveSupersedesQueuedOlderAutosaveBeforeItEntersWriteGate()
    {
        var backgroundWork = new ManualDesignerBackgroundWorkQueue();
        using var test = CoordinatorTestContext.CreateSaved(backgroundWorkQueue: backgroundWork);
        test.Edit("queued dirty revision");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.Equal(1, backgroundWork.PendingCount);

        test.Edit("newer canonical save");
        var savedRevision = test.Session.ActiveOpenDocument!.History.CurrentRevision;
        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.True(save.Succeeded, save.Error);
        Assert.False(test.Session.IsDirty);
        Assert.Equal(savedRevision, test.Session.ActiveOpenDocument.History.SavedRevision);
        backgroundWork.RunNext();
        test.Dispatcher.DrainAll();
        test.WaitForBackgroundOnly();

        Assert.Equal(0, backgroundWork.PendingCount);
        Assert.Equal(0, test.Store.WriteCallCount);
        Assert.Empty(test.Store.SuccessfulArtifactPaths);
        Assert.Null(test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        Assert.Contains("newer canonical save", File.ReadAllText(test.DocumentPath!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndoToSavedRevisionWhileRecoveryWriteIsBlockedDeletesLateArtifact()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };
        test.Edit("snapshot that becomes obsolete");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));

        Assert.True(test.Session.Transactions.Undo());
        Assert.False(test.Session.IsDirty);
        releaseWrite.Set();
        await test.DrainAsync();

        var lateArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Contains(lateArtifact, test.Store.DeletedPaths);
        Assert.Null(test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        Assert.False(test.Coordinator.GetActiveDiagnostics().AutosavePending);
        Assert.DoesNotContain(test.Store.Writes, snapshot => snapshot.Metadata.DirtyRevision == 0);
    }

    [Fact]
    public async Task RepeatedRecoveryFailureRetriesAtBoundedRateAndUsesOneNotice()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        test.Store.OnWrite = snapshot => new DesignerRecoveryWriteResult(false, test.Store.GetPath(snapshot), "disk full");

        test.Edit("unprotected");
        await test.FireDebounceAsync();
        var firstNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.AutosaveFailed, firstNotice.Kind);
        Assert.Equal(1, test.Store.WriteCallCount);

        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(29));
        await test.DrainAsync();
        Assert.Equal(1, test.Store.WriteCallCount);

        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
        await test.DrainAsync();
        Assert.Equal(2, test.Store.WriteCallCount);
        Assert.Equal(firstNotice.Id, test.Coordinator.CurrentNotification?.Id);
        Assert.True(test.Session.IsDirty);
        Assert.Equal("disk full", test.Coordinator.GetActiveDiagnostics().LastAutosaveError);
    }

    [Fact]
    public async Task SuccessfulRetryClearsAutosaveFailureNotice()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        test.Store.OnWrite = snapshot => test.Store.WriteCallCount == 1
            ? new DesignerRecoveryWriteResult(false, test.Store.GetPath(snapshot), "temporarily unavailable")
            : test.Store.Success(snapshot);

        test.Edit("retry me");
        await test.FireDebounceAsync();
        Assert.Equal(DesignerPersistenceNoticeKind.AutosaveFailed, test.Coordinator.CurrentNotification?.Kind);

        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(30));
        await test.DrainAsync();

        Assert.Equal(2, test.Store.WriteCallCount);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.Null(test.Coordinator.GetActiveDiagnostics().LastAutosaveError);
    }

    [Fact]
    public async Task NormalSaveMarksCapturedRevisionAndDeletesRecoveryArtifact()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("save me");
        await test.FireDebounceAsync();
        var artifactPath = Assert.Single(test.Store.SuccessfulArtifactPaths);
        var revision = test.Session.ActiveOpenDocument!.History.CurrentRevision;

        var result = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.True(result.Succeeded, result.Error);
        Assert.False(test.Session.IsDirty);
        Assert.Equal(revision, test.Session.ActiveOpenDocument!.History.SavedRevision);
        Assert.Contains(artifactPath, test.Store.DeletedPaths);
        Assert.Contains("save me", File.ReadAllText(test.DocumentPath!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanShutdownAndRestartDoesNotPromptForDeletedRecoveryArtifact()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("saved before clean shutdown");
        await test.FireDebounceAsync();
        var snapshot = Assert.Single(test.Store.Writes);
        var artifactPath = Assert.Single(test.Store.SuccessfulArtifactPaths);
        test.Store.DiscoveryCandidates.Add(CreateCandidate(test.Store, snapshot, artifactPath));
        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
        Assert.True(save.Succeeded, save.Error);
        Assert.Contains(artifactPath, test.Store.DeletedPaths);
        test.Coordinator.Dispose();
        test.Session.Dispose();

        using var restartedSession = new DesignerSession(null, DesignerControlRenderMode.Runtime, 100);
        restartedSession.OpenDocument(DesignDocumentSerializer.Default.Load(test.DocumentPath!), test.DocumentPath!, markDirty: false);
        using var restartedCoordinator = new DesignerPersistenceCoordinator(
            restartedSession,
            new DesignerFileService(currentDocumentPathProvider: () => restartedSession.CurrentDocumentPath),
            CreateOptions(),
            test.Store,
            new ManualDesignerOneShotScheduler(TestStartUtc.AddMinutes(1)),
            new TestUiDispatcher(),
            new FakeFileChangeSourceFactory(),
            TestRecoverySession);

        Assert.Null(restartedCoordinator.CurrentNotification);
        Assert.False(restartedSession.IsDirty);
    }

    [Fact]
    public async Task FailedNormalSaveLeavesDocumentDirtyAndRecoveryArtifactAvailable()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        test.Edit("keep protected");
        await test.FireDebounceAsync();
        var artifactPath = Assert.Single(test.Store.SuccessfulArtifactPaths);
        var invalidPath = IOPath.Combine(test.Directory.Path, "missing", "MainForm.mfdesign");

        var result = test.Coordinator.SaveActiveDocument(invalidPath);

        Assert.False(result.Succeeded);
        Assert.True(test.Session.IsDirty);
        Assert.DoesNotContain(artifactPath, test.Store.DeletedPaths);
        Assert.Equal(artifactPath, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
    }

    [Fact]
    public void CanonicalSaveDetectsUnobservedExternalDesignEditAndPreservesDisk()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("local unsaved version");
        test.WriteExternalDesign("unobserved disk version");
        var externalDiskText = File.ReadAllText(test.DocumentPath!);

        var result = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.False(result.Succeeded);
        Assert.Contains("changed outside", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(externalDiskText, File.ReadAllText(test.DocumentPath!));
        Assert.Equal("local unsaved version", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.Equal(
            DesignerPersistenceNoticeKind.ExternalDesignConflict,
            test.Coordinator.CurrentNotification?.Kind);
    }

    [Fact]
    public void CanonicalSaveDetectsUnobservedGeneratedCodeEditAndPreservesBothVersions()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var baseline = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(baseline.Succeeded, string.Join("; ", baseline.Errors));
        test.Edit("local model version");
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("unobserved generated version");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);
        var externalGeneratedText = File.ReadAllText(generated.Path);
        var canonicalDesignText = File.ReadAllText(test.DocumentPath!);

        var result = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.False(result.Succeeded);
        Assert.Contains("changed outside", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(externalGeneratedText, File.ReadAllText(generated.Path));
        Assert.Equal(canonicalDesignText, File.ReadAllText(test.DocumentPath!));
        Assert.Equal("local model version", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.Equal(
            DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict,
            test.Coordinator.CurrentNotification?.Kind);
    }

    [Fact]
    public void CodeGenerationDetectsUnobservedGeneratedEditAndDoesNotOverwriteIt()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var baseline = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(baseline.Succeeded, string.Join("; ", baseline.Errors));
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("external generated edit");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);
        var externalGeneratedText = File.ReadAllText(generated.Path);

        var result = test.Coordinator.GenerateActiveDocumentCode();

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("changed outside", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(externalGeneratedText, File.ReadAllText(generated.Path));
        Assert.Equal("external generated edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.True(test.Session.Transactions.CanUndo);
    }

    [Fact]
    public async Task PreflightWithBothExternalTargetsChangedKeepsDesignDocumentAuthoritative()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var baseline = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(baseline.Succeeded, string.Join("; ", baseline.Errors));
        test.WriteExternalDesign("mfdesign authoritative");
        var generatedDocument = CreateDocument();
        generatedDocument.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("generated must not reverse import");
        var generated = test.Files.PrepareDesignerCode(generatedDocument, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);

        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.False(save.Succeeded);
        Assert.Equal("mfdesign authoritative", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(400));
        await test.DrainAsync();
        Assert.Equal("mfdesign authoritative", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.NotEqual(
            DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict,
            test.Coordinator.CurrentNotification?.Kind);
    }

    [Fact]
    public async Task AutosaveDisabledNeverWritesRecovery()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureOptions: options => options.AutoSaveEnabled = false);

        test.Edit("not autosaved");
        test.Scheduler.AdvanceBy(TimeSpan.FromHours(1));
        await test.DrainAsync();

        Assert.True(test.Session.IsDirty);
        Assert.Empty(test.Store.Writes);
        Assert.False(test.Coordinator.GetActiveDiagnostics().AutosavePending);
    }

    [Fact]
    public async Task DisposeCancelsPendingAutosaveAndWatcherCallbacks()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("pending");
        var watcher = test.Watchers.Latest(test.DocumentPath!);

        test.Coordinator.Dispose();
        test.Scheduler.AdvanceBy(TimeSpan.FromHours(1));
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        await test.Coordinator.WaitForIdleAsync();

        Assert.Empty(test.Store.Writes);
        Assert.True(watcher.IsDisposed);
        Assert.Equal(0, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public void QueuedWatcherCallbackAfterDisposeIsIgnoredWithoutModelMutation()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var serializedBefore = DesignDocumentSerializer.Default.Serialize(test.Session.Document);
        var generationBefore = test.Session.ActiveOpenDocument!.RevisionGeneration;
        test.WriteExternalDesign("must not load after disposal");
        var watcher = test.Watchers.Latest(test.DocumentPath!);
        test.Dispatcher.QueueAllCallbacks = true;
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        Assert.Equal(1, test.Dispatcher.PendingCount);

        test.Coordinator.Dispose();
        var callbackCount = test.Dispatcher.DrainAll();

        Assert.Equal(1, callbackCount);
        Assert.Equal(serializedBefore, DesignDocumentSerializer.Default.Serialize(test.Session.Document));
        Assert.Equal(generationBefore, test.Session.ActiveOpenDocument!.RevisionGeneration);
        Assert.True(watcher.IsDisposed);
        Assert.Equal(0, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public async Task BackgroundWatcherCallbackQueuedBeforeDisposeIsIgnoredAfterCrossThreadPublication()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var serializedBefore = DesignDocumentSerializer.Default.Serialize(test.Session.Document);
        test.WriteExternalDesign("background callback must not load");
        var watcher = test.Watchers.Latest(test.DocumentPath!);
        test.Dispatcher.QueueAllCallbacks = true;

        await Task.Run(() => watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath));
        Assert.Equal(1, test.Dispatcher.PendingCount);

        test.Coordinator.Dispose();
        test.Dispatcher.DrainAll();

        Assert.Equal(serializedBefore, DesignDocumentSerializer.Default.Serialize(test.Session.Document));
        Assert.True(watcher.IsDisposed);
        Assert.Equal(0, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public void DisposeAfterUndoWhileRecoveryWriteIsInFlightDeletesLateSuccessfulArtifact()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };

        test.Edit("late clean-close snapshot");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(test.Session.Transactions.Undo());
        Assert.False(test.Session.IsDirty);

        test.Coordinator.Dispose();
        releaseWrite.Set();
        test.WaitForBackgroundOnly();

        var artifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Contains(artifact, test.Store.DeletedPaths);
        Assert.Equal(0, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public void CleanDisposeRetriesLateArtifactDeleteWithoutUiCompletion()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };
        test.Store.OnDelete = artifactPath => test.Store.DeleteAttempts.Count == 1
            ? new DesignerRecoveryFileOperationResult(false, ResultPath: null, "first cleanup failed")
            : new DesignerRecoveryFileOperationResult(true, artifactPath, Error: null);

        test.Edit("late clean-close retry");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(test.Session.Transactions.Undo());
        test.Dispatcher.QueueAllCallbacks = true;

        test.Coordinator.Dispose();
        releaseWrite.Set();
        test.WaitForBackgroundOnly();

        var artifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Equal(2, test.Store.DeleteAttempts.Count(path => string.Equals(path, artifact, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(artifact, test.Store.DeletedPaths);
        Assert.Equal(0, test.Dispatcher.PendingCount);
    }

    [Fact]
    public void DisposeWhileDirtyRecoveryWriteIsInFlightPreservesLateSuccessfulArtifact()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };

        test.Edit("late dirty-close snapshot");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(test.Session.IsDirty);

        test.Coordinator.Dispose();
        releaseWrite.Set();
        test.WaitForBackgroundOnly();

        var artifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.DoesNotContain(artifact, test.Store.DeletedPaths);
        Assert.Equal(0, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public async Task ExhaustedWorkerSideDeleteRetriesAreRetriedByUiCompletion()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };
        test.Store.OnDelete = artifactPath => test.Store.DeleteAttempts.Count <= 2
            ? new DesignerRecoveryFileOperationResult(false, ResultPath: null, "worker cleanup failed")
            : new DesignerRecoveryFileOperationResult(true, artifactPath, Error: null);

        test.Edit("late delete retry");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(test.Session.Transactions.Undo());
        test.Session.OpenDocument(CreateDocument("SecondForm", "secondForm", "secondButton"), path: null, markDirty: false);
        test.Session.CloseDocument(0);

        releaseWrite.Set();
        await test.DrainAsync();

        var artifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Equal(3, test.Store.DeleteAttempts.Count(path => string.Equals(path, artifact, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(artifact, test.Store.DeletedPaths);
    }

    [Fact]
    public async Task SelfWriteWatcherEventIsAcknowledgedWithoutReloadOrConflict()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("canonical save");
        var result = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
        Assert.True(result.Succeeded, result.Error);
        var generation = test.Session.ActiveOpenDocument!.RevisionGeneration;
        var watcher = test.Watchers.Latest(test.DocumentPath!);

        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        await test.FireExternalDebounceAsync();

        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Session.IsDirty);
        Assert.Equal(generation, test.Session.ActiveOpenDocument!.RevisionGeneration);
        Assert.Equal("canonical save", test.Session.Document.Controls[0].Properties["Text"].GetString());
    }

    [Fact]
    public async Task CleanExternalDesignChangeReloadsAsCleanBaselineWithoutUndo()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var generation = test.Session.ActiveOpenDocument!.RevisionGeneration;
        test.WriteExternalDesign("external clean edit");

        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();

        Assert.Equal("external clean edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Equal(generation + 1, test.Session.ActiveOpenDocument!.RevisionGeneration);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task DuplicateRapidExternalEventsCauseOneCleanReload()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var generation = test.Session.ActiveOpenDocument!.RevisionGeneration;
        test.WriteExternalDesign("coalesced external edit");

        for (var index = 0; index < 25; index++)
            test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();

        Assert.Equal(generation + 1, test.Session.ActiveOpenDocument!.RevisionGeneration);
        Assert.Equal("coalesced external edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task WatchersReloadOnlyTheirOwnDocumentAcrossTabs()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var pathA = test.DocumentPath!;
        var pathB = IOPath.Combine(test.Directory.Path, "SecondForm.mfdesign");
        var documentB = CreateDocument("SecondForm", "secondForm", "secondButton");
        DesignDocumentSerializer.Default.Save(pathB, documentB);
        test.Session.OpenDocument(documentB, pathB, markDirty: false);
        var watcherA = test.Watchers.Latest(pathA);
        var watcherB = test.Watchers.Latest(pathB);
        Assert.NotSame(watcherA, watcherB);
        var externalA = CreateDocument();
        externalA.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("external A only");
        DesignDocumentSerializer.Default.Save(pathA, externalA);

        watcherA.Raise(DesignerFileChangeKind.Changed, watcherA.DesignDocumentPath);
        await test.FireExternalDebounceAsync();

        Assert.Equal("external A only", test.Session.OpenDocuments[0].Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.OpenDocuments[1].Document.Controls[0].Properties.ContainsKey("Text"));
        Assert.Same(test.Session.OpenDocuments[1], test.Session.ActiveOpenDocument);

        var externalB = CreateDocument("SecondForm", "secondForm", "secondButton");
        externalB.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("external B only");
        DesignDocumentSerializer.Default.Save(pathB, externalB);
        watcherB.Raise(DesignerFileChangeKind.Changed, watcherB.DesignDocumentPath);
        await test.FireExternalDebounceAsync();

        Assert.Equal("external A only", test.Session.OpenDocuments[0].Document.Controls[0].Properties["Text"].GetString());
        Assert.Equal("external B only", test.Session.OpenDocuments[1].Document.Controls[0].Properties["Text"].GetString());
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task RapidDifferentExternalDesignWritesReloadOnlyLatestStableContent()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var watcher = test.Watchers.Latest(test.DocumentPath!);
        foreach (var value in new[] { "external v1", "external v2", "external final" })
        {
            test.WriteExternalDesign(value);
            watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        }

        await test.FireExternalDebounceAsync();

        Assert.Equal("external final", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task DirtyExternalDesignConflictKeepPreservesLocalAndAcknowledgesDiskVersion()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("local wins for now");
        test.WriteExternalDesign("disk changed");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalDesignConflict, notice.Kind);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var error), error);

        Assert.True(test.Session.IsDirty);
        Assert.Equal("local wins for now", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.Null(test.Coordinator.CurrentNotification);

        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.Equal("local wins for now", test.Session.Document.Controls[0].Properties["Text"].GetString());
    }

    [Fact]
    public async Task KeepClearsExpectedSelfWriteTokenSoEarlierExternalContentIsDetectedAgain()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("content A");
        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
        Assert.True(save.Succeeded, save.Error);
        var serializedContentA = File.ReadAllText(test.DocumentPath!);
        test.Edit("local version kept in memory");
        test.WriteExternalDesign("content B");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        var firstNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalDesignConflict, firstNotice.Kind);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            firstNotice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var keepError), keepError);
        Assert.Null(test.Coordinator.CurrentNotification);

        File.WriteAllText(test.DocumentPath!, serializedContentA);
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();

        var secondNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalDesignConflict, secondNotice.Kind);
        Assert.NotEqual(firstNotice.Id, secondNotice.Id);
        Assert.Equal("local version kept in memory", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
    }

    [Fact]
    public async Task DirtyExternalDesignReloadFirstCapturesRecoveryThenUsesCleanDiskBaseline()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("local protected before reload");
        test.WriteExternalDesign("disk selected");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Reload,
            saveAsPath: null,
            out var error), error);

        Assert.Single(test.Store.Writes);
        Assert.Contains("local protected before reload", test.Store.Writes.Single().SerializedDesignDocument, StringComparison.Ordinal);
        Assert.Equal("disk selected", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task DirtyExternalDesignSaveAsPreservesOriginalDiskAndSavesLocalVersionElsewhere()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("local saved elsewhere");
        test.WriteExternalDesign("original disk remains");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        var saveAsPath = IOPath.Combine(test.Directory.Path, "PreservedLocal.mfdesign");

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.SaveAs,
            saveAsPath,
            out var error), error);

        Assert.False(test.Session.IsDirty);
        Assert.Equal(IOPath.GetFullPath(saveAsPath), test.Session.ActiveOpenDocument!.Path);
        Assert.Contains("local saved elsewhere", File.ReadAllText(saveAsPath), StringComparison.Ordinal);
        Assert.Contains("original disk remains", File.ReadAllText(test.DocumentPath!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictSaveAsClearsOldPathPendingStateAndIgnoresDisposedWatcher()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var oldPath = test.DocumentPath!;
        var oldWatcher = test.Watchers.Latest(oldPath);
        test.Edit("local moved by Save As");
        test.WriteExternalDesign("old path external version");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        var saveAsPath = IOPath.Combine(test.Directory.Path, "ConflictPreserved.mfdesign");

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.SaveAs,
            saveAsPath,
            out var error), error);

        Assert.True(oldWatcher.IsDisposed);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Coordinator.GetActiveDiagnostics().ExternalChangePending);
        Assert.Equal(IOPath.GetFullPath(saveAsPath), test.Session.ActiveOpenDocument!.Path);
        oldWatcher.Raise(DesignerFileChangeKind.Changed, oldWatcher.DesignDocumentPath);
        test.Scheduler.AdvanceBy(TimeSpan.FromSeconds(2));
        await test.DrainAsync();
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Coordinator.GetActiveDiagnostics().ExternalChangePending);
        var secondSave = test.Coordinator.SaveActiveDocument(saveAsPath);
        Assert.True(secondSave.Succeeded, secondSave.Error);
        Assert.Contains("old path external version", File.ReadAllText(oldPath), StringComparison.Ordinal);
        Assert.Contains("local moved by Save As", File.ReadAllText(saveAsPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)DesignerFileChangeKind.Deleted)]
    [InlineData((int)DesignerFileChangeKind.Renamed)]
    public async Task DeletedOrRenamedDesignFileRaisesMissingNoticeAndPreservesMemory(
        int rawKind)
    {
        var kind = (DesignerFileChangeKind)rawKind;
        using var test = CoordinatorTestContext.CreateSaved();
        var watcher = test.Watchers.Latest(test.DocumentPath!);
        var movedPath = IOPath.Combine(test.Directory.Path, "Moved.mfdesign");
        if (kind == DesignerFileChangeKind.Renamed)
            File.Move(test.DocumentPath!, movedPath);
        else
            File.Delete(test.DocumentPath!);

        watcher.Raise(
            kind,
            kind == DesignerFileChangeKind.Renamed ? movedPath : watcher.DesignDocumentPath,
            kind == DesignerFileChangeKind.Renamed ? watcher.DesignDocumentPath : null);
        await test.FireExternalDebounceAsync();

        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.FileMissing, notice.Kind);
        Assert.Equal("MainForm", test.Session.Document.ClassName);
        Assert.False(test.Session.IsDirty);
    }

    [Fact]
    public async Task ExternalEventDuringTransactionIsDeferredUntilStableState()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.WriteExternalDesign("deferred disk edit");
        using var transaction = test.Session.Transactions.Begin("hold observation");
        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();

        Assert.Equal("MainForm", test.Session.Document.ClassName);
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));
        Assert.Null(test.Coordinator.CurrentNotification);

        transaction.Commit();
        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(750));
        await test.DrainAsync();

        Assert.Equal("deferred disk edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
    }

    [Fact]
    public void ExternalObservationCapturedBeforeTransactionCompletesWithoutMutatingUntilStableRetry()
    {
        var backgroundWork = new ManualDesignerBackgroundWorkQueue();
        using var test = CoordinatorTestContext.CreateSaved(backgroundWorkQueue: backgroundWork);
        var initialGeneration = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(initialGeneration.Succeeded, string.Join("; ", initialGeneration.Errors));
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("captured external generated edit");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);

        test.RaiseGeneratedChange();
        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(400));
        Assert.Equal(1, backgroundWork.PendingCount);

        // Complete the disk read before the transaction, but delay its UI continuation until the
        // model is unstable. This is the exact worker/UI race exercised by a real dispatcher.
        test.Dispatcher.QueueAllCallbacks = true;
        backgroundWork.RunNext();
        Assert.Equal(1, test.Dispatcher.PendingCount);
        using var transaction = test.Session.Transactions.Begin("edit while external result is queued");
        test.Edit("local transaction edit");
        test.Dispatcher.DrainAll();

        Assert.Equal("local transaction edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Equal(0, backgroundWork.PendingCount);

        test.Dispatcher.QueueAllCallbacks = false;
        transaction.Rollback();
        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(750));
        Assert.Equal(1, backgroundWork.PendingCount);
        backgroundWork.RunNext();
        test.WaitForBackgroundOnly();

        Assert.Equal(
            "captured external generated edit",
            test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.True(test.Session.Transactions.CanUndo);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task ValidGeneratedCodeExternalEditImportsAsOneUndoableTransaction()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var initialGeneration = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(initialGeneration.Succeeded, string.Join("; ", initialGeneration.Errors));
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("from generated code");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);

        test.RaiseGeneratedChange();
        await test.FireExternalDebounceAsync();

        Assert.Equal("from generated code", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.True(test.Session.Transactions.CanUndo);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.True(test.Session.Transactions.Undo());
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));
    }

    [Fact]
    public async Task InvalidGeneratedCodeExternalEditPreservesModelAndRaisesDiagnosticNotice()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var initialGeneration = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(initialGeneration.Succeeded, string.Join("; ", initialGeneration.Errors));
        File.WriteAllText(initialGeneration.Path, "this is not valid Designer code");

        test.RaiseGeneratedChange();
        await test.FireExternalObservationThroughRetryLimitAsync();

        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalGeneratedCodeInvalid, notice.Kind);
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));
    }

    [Fact]
    public async Task InvalidExternalDesignFilePreservesCleanModelAndRaisesConflictNotice()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        File.WriteAllText(test.DocumentPath!, "{ definitely not a design document }");

        test.RaiseDesignChange();
        await test.FireExternalObservationThroughRetryLimitAsync();

        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalDesignConflict, notice.Kind);
        Assert.False(test.Session.IsDirty);
        Assert.Equal("MainForm", test.Session.Document.ClassName);
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));
    }

    [Fact]
    public async Task StablePartialExternalDesignIsRetriedBeforeFinalPayloadReloadsCleanModel()
    {
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("complete external payload");
        var finalText = DesignDocumentSerializer.Default.Serialize(external);
        var reader = new TestStableFileReader((attempt, path) => attempt == 1
            ? new DesignerStableFileReadResult(
                path,
                Exists: true,
                Text: "{",
                DesignerFileHash.ComputeUtf8Sha256("{"),
                TestStartUtc,
                Retryable: false,
                Error: null)
            : new DesignerStableFileReadResult(
                path,
                Exists: true,
                finalText,
                DesignerFileHash.ComputeUtf8Sha256(finalText),
                TestStartUtc.AddSeconds(1),
                Retryable: false,
                Error: null));
        using var test = CoordinatorTestContext.CreateSaved(stableFileReader: reader);

        test.RaiseDesignChange();
        await test.FireExternalDebounceAsync();

        Assert.Equal(1, reader.ReadCallCount);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));

        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(750));
        await test.DrainAsync();

        Assert.Equal(2, reader.ReadCallCount);
        Assert.Equal("complete external payload", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task StableExternalReadFailureStopsAfterBoundedAttemptsAndPreservesModel()
    {
        var reader = new TestStableFileReader((_, _) => throw new IOException("sharing violation"));
        using var test = CoordinatorTestContext.CreateSaved(stableFileReader: reader);

        test.RaiseDesignChange();
        await test.FireExternalObservationThroughRetryLimitAsync();

        Assert.Equal(3, reader.ReadCallCount);
        Assert.Equal(
            DesignerPersistenceNoticeKind.ExternalObservationFailed,
            test.Coordinator.CurrentNotification?.Kind);
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Document.Controls[0].Properties.ContainsKey("Text"));
        Assert.False(test.Coordinator.GetActiveDiagnostics().ExternalChangePending);
    }

    [Fact]
    public async Task TrustedRenameMovesRecoveryIdentityOnlyAfterNewPathSnapshotSucceeds()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var oldPath = test.DocumentPath!;
        var oldWatcher = test.Watchers.Latest(oldPath);
        test.Edit("protected across trusted rename");
        await test.FireDebounceAsync();
        var oldSnapshot = Assert.Single(test.Store.Writes);
        var oldArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        var newPath = IOPath.Combine(test.Directory.Path, "RenamedForm.mfdesign");

        File.Move(oldPath, newPath);
        test.Session.UpdateDocumentPath(test.Session.ActiveOpenDocument!, newPath);

        Assert.True(oldWatcher.IsDisposed);
        Assert.DoesNotContain(oldArtifact, test.Store.DeletedPaths);
        var newWatcher = test.Watchers.Latest(newPath);
        Assert.False(newWatcher.IsDisposed);
        await test.FireDebounceAsync();

        Assert.Equal(2, test.Store.WriteCallCount);
        var newSnapshot = test.Store.Writes.Last();
        var newArtifact = test.Store.SuccessfulArtifactPaths.Last();
        Assert.NotEqual(oldSnapshot.Metadata.DocumentIdentity, newSnapshot.Metadata.DocumentIdentity);
        Assert.Equal(IOPath.GetFullPath(newPath), newSnapshot.Metadata.DocumentPath);
        Assert.Contains(oldArtifact, test.Store.DeletedPaths);
        Assert.DoesNotContain(newArtifact, test.Store.DeletedPaths);
        Assert.Equal(newArtifact, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public async Task RepeatedTrustedRenameRetainsFailedOldCleanupButRemovesEveryNewlySupersededIdentity()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var firstPath = test.DocumentPath!;
        test.Edit("protected across repeated rename");
        await test.FireDebounceAsync();
        var firstArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        test.Store.OnDelete = path => string.Equals(path, firstArtifact, StringComparison.OrdinalIgnoreCase)
            ? new DesignerRecoveryFileOperationResult(false, null, "locked old artifact")
            : new DesignerRecoveryFileOperationResult(true, path, Error: null);

        var secondPath = IOPath.Combine(test.Directory.Path, "SecondName.mfdesign");
        File.Move(firstPath, secondPath);
        test.Session.UpdateDocumentPath(test.Session.ActiveOpenDocument!, secondPath);
        await test.FireDebounceAsync();
        var secondArtifact = test.Store.SuccessfulArtifactPaths.Last();

        Assert.Contains(firstArtifact, test.Store.DeleteAttempts);
        Assert.DoesNotContain(firstArtifact, test.Store.DeletedPaths);
        Assert.DoesNotContain(secondArtifact, test.Store.DeletedPaths);

        var thirdPath = IOPath.Combine(test.Directory.Path, "ThirdName.mfdesign");
        File.Move(secondPath, thirdPath);
        test.Session.UpdateDocumentPath(test.Session.ActiveOpenDocument!, thirdPath);
        await test.FireDebounceAsync();
        var thirdArtifact = test.Store.SuccessfulArtifactPaths.Last();

        Assert.True(test.Store.DeleteAttempts.Count(path =>
            string.Equals(path, firstArtifact, StringComparison.OrdinalIgnoreCase)) >= 2);
        Assert.Contains(secondArtifact, test.Store.DeletedPaths);
        Assert.DoesNotContain(firstArtifact, test.Store.DeletedPaths);
        Assert.DoesNotContain(thirdArtifact, test.Store.DeletedPaths);
        Assert.Equal(thirdArtifact, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
    }

    [Fact]
    public async Task CoalescedDesignAndGeneratedChangesUseDesignDocumentAsAuthority()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var initialGeneration = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(initialGeneration.Succeeded, string.Join("; ", initialGeneration.Errors));
        test.WriteExternalDesign("authoritative mfdesign");
        var generatedDocument = CreateDocument();
        generatedDocument.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("different generated code");
        var generated = test.Files.PrepareDesignerCode(generatedDocument, test.DocumentPath);
        File.WriteAllText(generated.Path, generated.Code);
        var watcher = test.Watchers.Latest(test.DocumentPath!);

        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.GeneratedCodePath);
        await test.FireExternalDebounceAsync();

        Assert.Equal("authoritative mfdesign", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateSelfDesignEventsDoNotHideRealGeneratedCodeChange(bool makeDirty)
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("saved design baseline");
        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
        Assert.True(save.Succeeded, save.Error);
        var baseline = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(baseline.Succeeded, string.Join("; ", baseline.Errors));
        if (makeDirty)
            test.Edit("local dirty model");

        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("real generated edit");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        File.WriteAllText(generated.Path, generated.Code);
        var watcher = test.Watchers.Latest(test.DocumentPath!);

        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.GeneratedCodePath);
        await test.FireExternalDebounceAsync();

        if (makeDirty)
        {
            Assert.Equal("local dirty model", test.Session.Document.Controls[0].Properties["Text"].GetString());
            Assert.Equal(
                DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict,
                test.Coordinator.CurrentNotification?.Kind);
        }
        else
        {
            Assert.Equal("real generated edit", test.Session.Document.Controls[0].Properties["Text"].GetString());
            Assert.True(test.Session.IsDirty);
            Assert.True(test.Session.Transactions.CanUndo);
            Assert.Null(test.Coordinator.CurrentNotification);
        }
    }

    [Fact]
    public async Task ExplicitExternalCheckDetectsChangeWithoutWatcherEvent()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.WriteExternalDesign("found by explicit check");

        test.Coordinator.CheckForExternalChanges();
        test.Scheduler.AdvanceBy(TimeSpan.Zero);
        await test.DrainAsync();

        Assert.Equal("found by explicit check", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.False(test.Session.IsDirty);
    }

    [Fact]
    public async Task DirtyGeneratedCodeConflictRequiresExplicitReloadAndPreservesRecoveryFirst()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var initialGeneration = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(initialGeneration.Succeeded, string.Join("; ", initialGeneration.Errors));
        test.Edit("local generated conflict");
        var external = CreateDocument();
        external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("generated disk version");
        var generated = test.Files.PrepareDesignerCode(external, test.DocumentPath);
        File.WriteAllText(generated.Path, generated.Code);

        test.RaiseGeneratedChange();
        await test.FireExternalDebounceAsync();
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict, notice.Kind);
        Assert.Equal("local generated conflict", test.Session.Document.Controls[0].Properties["Text"].GetString());

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Reload,
            saveAsPath: null,
            out var error), error);

        Assert.Contains(test.Store.Writes, snapshot =>
            snapshot.SerializedDesignDocument.Contains("local generated conflict", StringComparison.Ordinal));
        Assert.Equal("generated disk version", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.True(test.Session.Transactions.CanUndo);
    }

    [Fact]
    public void LateExternalObservationCapturedBeforeGenerationCannotReportStaleConflict()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var baseline = test.Coordinator.GenerateActiveDocumentCode();
        Assert.True(baseline.Succeeded, string.Join("; ", baseline.Errors));
        var baselineGeneratedText = File.ReadAllText(baseline.Path);
        test.Edit("new model generated while observation is in flight");
        var watcher = test.Watchers.Latest(test.DocumentPath!);
        watcher.Raise(DesignerFileChangeKind.Changed, watcher.GeneratedCodePath);
        test.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(400));
        test.WaitForBackgroundOnly();
        Assert.True(SpinWait.SpinUntil(
            () => test.Dispatcher.PendingCount > 0,
            TimeSpan.FromSeconds(5)));

        var generated = test.Coordinator.GenerateActiveDocumentCode();

        Assert.True(generated.Succeeded, string.Join("; ", generated.Errors));
        Assert.NotEqual(baselineGeneratedText, File.ReadAllText(generated.Path));
        Assert.True(test.Dispatcher.DrainAll() > 0);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.Equal(
            "new model generated while observation is in flight",
            test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
    }

    [Fact]
    public async Task SeparateDocumentsHaveIndependentDebounceAndRecoveryIdentity()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("document A");
        var secondPath = IOPath.Combine(test.Directory.Path, "SecondForm.mfdesign");
        var second = CreateDocument("SecondForm", "secondForm", "secondButton");
        DesignDocumentSerializer.Default.Save(secondPath, second);
        test.Session.OpenDocument(second, secondPath, markDirty: false);
        test.Edit("document C");

        await test.FireDebounceAsync();

        Assert.Equal(2, test.Store.Writes.Count);
        var identities = test.Store.Writes.Select(snapshot => snapshot.Metadata.DocumentIdentity).ToHashSet();
        Assert.Equal(2, identities.Count);
        Assert.Contains(test.Store.Writes, snapshot => snapshot.SerializedDesignDocument.Contains("document A", StringComparison.Ordinal));
        Assert.Contains(test.Store.Writes, snapshot => snapshot.SerializedDesignDocument.Contains("document C", StringComparison.Ordinal));
        Assert.Equal(2, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public async Task RestartDiscoversRecoveryOnlyForDirtyDocumentsAAndC()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        var pathA = test.DocumentPath!;
        test.Edit("dirty A");
        var pathB = IOPath.Combine(test.Directory.Path, "CleanB.mfdesign");
        var documentB = CreateDocument("CleanB", "cleanB", "buttonB");
        DesignDocumentSerializer.Default.Save(pathB, documentB);
        test.Session.OpenDocument(documentB, pathB, markDirty: false);
        var pathC = IOPath.Combine(test.Directory.Path, "DirtyC.mfdesign");
        var documentC = CreateDocument("DirtyC", "dirtyC", "buttonC");
        DesignDocumentSerializer.Default.Save(pathC, documentC);
        test.Session.OpenDocument(documentC, pathC, markDirty: false);
        test.Edit("dirty C");

        await test.FireDebounceAsync();

        Assert.Equal(2, test.Store.Writes.Count);
        Assert.Contains(test.Store.Writes, snapshot => snapshot.SerializedDesignDocument.Contains("dirty A", StringComparison.Ordinal));
        Assert.Contains(test.Store.Writes, snapshot => snapshot.SerializedDesignDocument.Contains("dirty C", StringComparison.Ordinal));
        Assert.DoesNotContain(test.Store.Writes, snapshot => snapshot.Metadata.SuggestedName.Contains("CleanB", StringComparison.Ordinal));
        foreach (var snapshot in test.Store.Writes)
            test.Store.DiscoveryCandidates.Add(CreateCandidate(test.Store, snapshot));
        test.Coordinator.Dispose();
        test.Session.Dispose();

        using var restartedSession = new DesignerSession(null, DesignerControlRenderMode.Runtime, 100);
        restartedSession.OpenDocument(DesignDocumentSerializer.Default.Load(pathA), pathA, markDirty: false);
        restartedSession.OpenDocument(DesignDocumentSerializer.Default.Load(pathB), pathB, markDirty: false);
        restartedSession.OpenDocument(DesignDocumentSerializer.Default.Load(pathC), pathC, markDirty: false);
        using var restartedCoordinator = new DesignerPersistenceCoordinator(
            restartedSession,
            new DesignerFileService(currentDocumentPathProvider: () => restartedSession.CurrentDocumentPath),
            CreateOptions(),
            test.Store,
            new ManualDesignerOneShotScheduler(TestStartUtc.AddMinutes(2)),
            new TestUiDispatcher(),
            new FakeFileChangeSourceFactory(),
            TestRecoverySession);

        var cNotice = Assert.IsType<DesignerPersistenceNotification>(restartedCoordinator.CurrentNotification);
        Assert.Equal("DirtyC.mfdesign", cNotice.DocumentName);
        Assert.True(restartedCoordinator.ApplyCurrentAction(
            cNotice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var cError), cError);
        restartedSession.SwitchDocument(1);
        Assert.Null(restartedCoordinator.CurrentNotification);
        restartedSession.SwitchDocument(0);
        var aNotice = Assert.IsType<DesignerPersistenceNotification>(restartedCoordinator.CurrentNotification);
        Assert.Equal("MainForm.mfdesign", aNotice.DocumentName);
        Assert.True(restartedCoordinator.ApplyCurrentAction(
            aNotice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var aError), aError);
        Assert.Null(restartedCoordinator.CurrentNotification);
    }

    [Fact]
    public async Task DirtyInactiveDocumentRecoversSchedulingAfterOtherDocumentTransactionEnds()
    {
        using var test = CoordinatorTestContext.CreateSaved();
        test.Edit("dirty document A");
        var secondPath = IOPath.Combine(test.Directory.Path, "SecondForm.mfdesign");
        var second = CreateDocument("SecondForm", "secondForm", "secondButton");
        DesignDocumentSerializer.Default.Save(secondPath, second);
        test.Session.OpenDocument(second, secondPath, markDirty: false);
        using var transaction = test.Session.Transactions.Begin("transaction in document B");
        test.Edit("temporary document B edit");

        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        await test.DrainAsync();
        Assert.Empty(test.Store.Writes);

        transaction.Rollback();
        Assert.True(test.Session.OpenDocuments[0].IsDirty);
        Assert.False(test.Session.ActiveOpenDocument!.IsDirty);
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        await test.DrainAsync();

        var snapshot = Assert.Single(test.Store.Writes);
        Assert.Contains("dirty document A", snapshot.SerializedDesignDocument, StringComparison.Ordinal);
        Assert.Equal(test.Session.OpenDocuments[0].History.CurrentRevision, snapshot.Metadata.DirtyRevision);
        Assert.Single(test.Store.SuccessfulArtifactPaths);
    }

    [Fact]
    public void ValidSavedRecoveryIsAttachedAndRestoreCreatesDirtyBaselineWithoutUndo()
    {
        var recovered = CreateDocument();
        recovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("recovered payload");
        string? artifactPath = null;
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
            {
                var snapshot = DesignerRecoverySnapshot.CaptureSaved(
                    recovered,
                    documentPath!,
                    projectPath: null,
                    dirtyRevision: 7,
                    revisionGeneration: 2,
                    TestRecoverySession,
                    TestStartUtc.AddMinutes(-2),
                    sourceFileHashSha256: DesignerFileHash.ComputeFileSha256(documentPath!));
                var candidate = CreateCandidate(store, snapshot);
                artifactPath = candidate.ArtifactPath;
                store.DiscoveryCandidates.Add(candidate);
            });
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.RecoveryAvailable, notice.Kind);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var error), error);

        Assert.Equal("recovered payload", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.True(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
        Assert.Equal(test.DocumentPath, test.Session.ActiveOpenDocument!.Path);
        Assert.Equal(artifactPath, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public void UnresolvedAttachedRecoveryBlocksCanonicalWritesAndSurvivesCurrentVersionSaveAs()
    {
        string[] recoveryArtifacts = [];
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
                recoveryArtifacts = AddTwoSavedRecoveryCandidates(store, documentPath!));
        var recoveryNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        var canonicalSave = test.Coordinator.SaveActiveDocument(test.DocumentPath!);
        var generation = test.Coordinator.GenerateActiveDocumentCode();

        Assert.False(canonicalSave.Succeeded);
        Assert.Contains("Recovery", canonicalSave.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(generation.Succeeded);
        Assert.Contains(generation.Errors, error => error.Contains("recovery", StringComparison.OrdinalIgnoreCase));
        Assert.All(recoveryArtifacts, artifact => Assert.DoesNotContain(artifact, test.Store.DeletedPaths));
        Assert.Equal(recoveryNotice.Id, test.Coordinator.CurrentNotification?.Id);

        var saveAsPath = IOPath.Combine(test.Directory.Path, "CurrentVersion.mfdesign");
        var saveAs = test.Coordinator.SaveActiveDocument(saveAsPath);

        Assert.True(saveAs.Succeeded, saveAs.Error);
        Assert.Equal(IOPath.GetFullPath(saveAsPath), test.Session.ActiveOpenDocument!.Path);
        Assert.All(recoveryArtifacts, artifact => Assert.DoesNotContain(artifact, test.Store.DeletedPaths));
        Assert.Equal(2, test.Store.Discover().Candidates.Count);
        Assert.Equal(recoveryNotice.Id, test.Coordinator.CurrentNotification?.Id);
        Assert.True(File.Exists(saveAsPath));
    }

    [Theory]
    [InlineData((int)DesignerPersistenceActions.Restore)]
    [InlineData((int)DesignerPersistenceActions.OpenDisk)]
    public void DirtyCurrentVersionIsForcedToRecoveryBeforeAttachedCandidateReplacement(int rawAction)
    {
        var action = (DesignerPersistenceActions)rawAction;
        var canonical = CreateDocument();
        canonical.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("canonical disk");
        string? candidateArtifact = null;
        using var test = CoordinatorTestContext.CreateSaved(
            document: canonical,
            configureStore: (store, documentPath, _) =>
            {
                var recovered = CreateDocument();
                recovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("attached recovery candidate");
                var snapshot = DesignerRecoverySnapshot.CaptureSaved(
                    recovered,
                    documentPath!,
                    projectPath: null,
                    dirtyRevision: 8,
                    revisionGeneration: 1,
                    TestRecoverySession,
                    TestStartUtc,
                    sourceFileHashSha256: DesignerFileHash.ComputeFileSha256(documentPath!));
                var candidate = CreateCandidate(store, snapshot);
                candidateArtifact = candidate.ArtifactPath;
                store.DiscoveryCandidates.Add(candidate);
            });
        test.Edit("current edit before recovery decision");
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            action,
            saveAsPath: null,
            out var error), error);

        var forcedSnapshot = Assert.Single(test.Store.Writes);
        var forcedArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Contains("current edit before recovery decision", forcedSnapshot.SerializedDesignDocument, StringComparison.Ordinal);
        Assert.DoesNotContain(Assert.IsType<string>(candidateArtifact), test.Store.DeletedPaths);
        Assert.DoesNotContain(forcedArtifact, test.Store.DeletedPaths);
        Assert.Null(test.Coordinator.CurrentNotification);
        if (action == DesignerPersistenceActions.Restore)
        {
            Assert.Equal("attached recovery candidate", test.Session.Document.Controls[0].Properties["Text"].GetString());
            Assert.True(test.Session.IsDirty);
        }
        else
        {
            Assert.Equal("canonical disk", test.Session.Document.Controls[0].Properties["Text"].GetString());
            Assert.False(test.Session.IsDirty);
            Assert.Equal(forcedArtifact, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        }
    }

    [Fact]
    public void DirectDiscardDeletesRecoveryCandidateWithoutChangingOpenDocument()
    {
        string? artifactPath = null;
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                    CreateDocument("DiscardedForm", "discardedForm", "discardedButton"),
                    Guid.Parse("0eb766e2-426f-4e17-871d-dca553578e32"),
                    "DiscardedForm.mfdesign",
                    projectPath: null,
                    dirtyRevision: 2,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc);
                var candidate = CreateCandidate(store, snapshot);
                artifactPath = candidate.ArtifactPath;
                store.DiscoveryCandidates.Add(candidate);
            });
        var originalDocument = test.Session.Document;
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Discard,
            saveAsPath: null,
            out var error), error);

        Assert.Contains(artifactPath, test.Store.DeletedPaths);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.Same(originalDocument, test.Session.Document);
        Assert.Single(test.Session.OpenDocuments);
        Assert.False(test.Session.IsDirty);
    }

    [Fact]
    public void DiscardDeletesOlderValidCandidatesForSameIdentityAcrossRestart()
    {
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
                artifacts = AddTwoSavedRecoveryCandidates(store, documentPath!));
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Discard,
            saveAsPath: null,
            out var error), error);

        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        Assert.Null(test.Coordinator.CurrentNotification);
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, test.DocumentPath!);
    }

    [Fact]
    public void NormalSaveAfterKeepDeletesOlderValidCandidatesForSameIdentityAcrossRestart()
    {
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
                artifacts = AddTwoSavedRecoveryCandidates(store, documentPath!));
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var keepError), keepError);

        var save = test.Coordinator.SaveActiveDocument(test.DocumentPath!);

        Assert.True(save.Succeeded, save.Error);
        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, test.DocumentPath!);
    }

    [Fact]
    public void OrdinarySaveAsDeletesOlderCandidatesFromOriginalIdentityAcrossRestart()
    {
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
                artifacts = AddTwoSavedRecoveryCandidates(store, documentPath!));
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var keepError), keepError);
        var originalPath = test.DocumentPath!;
        var saveAsPath = IOPath.Combine(test.Directory.Path, "OrdinarySaveAs.mfdesign");

        var save = test.Coordinator.SaveActiveDocument(saveAsPath);

        Assert.True(save.Succeeded, save.Error);
        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, originalPath);
    }

    [Fact]
    public void SaveRecoveryAsDeletesOlderValidCandidatesForSameIdentityAcrossRestart()
    {
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
                artifacts = AddTwoSavedRecoveryCandidates(store, documentPath!));
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        var originalPath = test.DocumentPath!;
        var recoveredPath = IOPath.Combine(test.Directory.Path, "RecoveredCopy.mfdesign");

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.SaveAs,
            recoveredPath,
            out var error), error);

        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        Assert.True(File.Exists(recoveredPath));
        Assert.Null(test.Coordinator.CurrentNotification);
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, originalPath);
    }

    [Fact]
    public void RestoredDetachedSavedRecoverySaveAsDeletesAllOriginCandidatesAcrossRestart()
    {
        string? originPath = null;
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                originPath = IOPath.Combine(IOPath.GetDirectoryName(store.RootPath)!, "DetachedOrigin.mfdesign");
                DesignDocumentSerializer.Default.Save(originPath, CreateDocument());
                artifacts = AddTwoSavedRecoveryCandidates(store, originPath);
            });
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var restoreError), restoreError);
        Assert.Null(test.Session.ActiveOpenDocument!.Path);
        var saveAsPath = IOPath.Combine(test.Directory.Path, "RestoredDetachedCopy.mfdesign");

        var save = test.Coordinator.SaveActiveDocument(saveAsPath);

        Assert.True(save.Succeeded, save.Error);
        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, originPath!);
    }

    [Fact]
    public void RestoredDetachedSavedRecoveryDiscardDeletesAllOriginCandidatesAcrossRestart()
    {
        string? originPath = null;
        string[] artifacts = [];
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                originPath = IOPath.Combine(IOPath.GetDirectoryName(store.RootPath)!, "DiscardedDetachedOrigin.mfdesign");
                DesignDocumentSerializer.Default.Save(originPath, CreateDocument());
                artifacts = AddTwoSavedRecoveryCandidates(store, originPath);
            });
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var restoreError), restoreError);

        Assert.True(test.Coordinator.PrepareDocumentForDiscard(
            test.Session.ActiveOpenDocument!,
            out var discardError), discardError);

        Assert.All(artifacts, artifact => Assert.Contains(artifact, test.Store.DeletedPaths));
        test.Coordinator.Dispose();
        test.Session.Dispose();
        AssertRestartHasNoRecovery(test.Store, originPath!);
    }

    [Fact]
    public void SavedRecoveryWithMissingSourceOffersRestoreAndSaveAsButNotOpenDisk()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                var missingPath = IOPath.Combine(store.RootPath, "MissingForm.mfdesign");
                var snapshot = DesignerRecoverySnapshot.CaptureSaved(
                    CreateDocument("MissingForm", "missingForm", "missingButton"),
                    missingPath,
                    projectPath: null,
                    dirtyRevision: 4,
                    revisionGeneration: 1,
                    TestRecoverySession,
                    TestStartUtc,
                    sourceFileHashSha256: DesignerFileHash.ComputeUtf8Sha256("missing source baseline"));
                store.DiscoveryCandidates.Add(CreateCandidate(store, snapshot));
            });

        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.Equal(DesignerPersistenceNoticeKind.RecoveryConflict, notice.Kind);
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.Restore));
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.SaveAs));
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.Discard));
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.Compare));
        Assert.False(notice.Actions.HasFlag(DesignerPersistenceActions.OpenDisk));
        Assert.Null(notice.DiskTimestampUtc);
    }

    [Fact]
    public void RetentionProtectsOnlyOpenDocumentRecoveryAndDoesNotPromptDeletedInactiveEntry()
    {
        string? activeArtifact = null;
        string? deletedInactiveArtifact = null;
        string? survivingInactiveArtifact = null;
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
            {
                var activeRecovered = CreateDocument();
                activeRecovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("active recovery");
                var activeSnapshot = DesignerRecoverySnapshot.CaptureSaved(
                    activeRecovered,
                    documentPath!,
                    projectPath: null,
                    dirtyRevision: 3,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc,
                    sourceFileHashSha256: DesignerFileHash.ComputeFileSha256(documentPath!));
                var activeCandidate = CreateCandidate(store, activeSnapshot);
                activeArtifact = activeCandidate.ArtifactPath;

                var deletedSnapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                    CreateDocument("DeletedInactive", "deletedInactive", "deletedButton"),
                    Guid.Parse("33508850-f1bc-4b80-9185-e2bcc2d25088"),
                    "DeletedInactive.mfdesign",
                    projectPath: null,
                    dirtyRevision: 1,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc.AddHours(-3));
                var deletedCandidate = CreateCandidate(store, deletedSnapshot);
                deletedInactiveArtifact = deletedCandidate.ArtifactPath;

                var survivingSnapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                    CreateDocument("SurvivingInactive", "survivingInactive", "survivingButton"),
                    Guid.Parse("3f90c580-a930-4f21-b81d-4e4cdb0de23a"),
                    "SurvivingInactive.mfdesign",
                    projectPath: null,
                    dirtyRevision: 2,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc.AddHours(-1));
                var survivingCandidate = CreateCandidate(store, survivingSnapshot);
                survivingInactiveArtifact = survivingCandidate.ArtifactPath;
                store.DiscoveryCandidates.AddRange([activeCandidate, deletedCandidate, survivingCandidate]);
                store.OnCleanup = (_, _, _) => new DesignerRecoveryCleanupResult(
                    [deletedCandidate.ArtifactPath],
                    [],
                    InspectedEntryCount: 3,
                    WasTruncated: false);
            });

        Assert.Equal(Assert.IsType<string>(activeArtifact), Assert.Single(test.Store.LastCleanupProtectedPaths));
        Assert.DoesNotContain(Assert.IsType<string>(deletedInactiveArtifact), test.Store.LastCleanupProtectedPaths);
        Assert.DoesNotContain(Assert.IsType<string>(survivingInactiveArtifact), test.Store.LastCleanupProtectedPaths);
        var activeNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal("MainForm.mfdesign", activeNotice.DocumentName);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            activeNotice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var activeError), activeError);

        var survivingNotice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal("SurvivingInactive.mfdesign", survivingNotice.DocumentName);
        Assert.NotEqual("DeletedInactive.mfdesign", survivingNotice.DocumentName);
        Assert.True(test.Coordinator.ApplyCurrentAction(
            survivingNotice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var survivingError), survivingError);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public void StartupTargetRecoveryIsProtectedBeforeDocumentOpenedAndAttachesAfterOpen()
    {
        using var directory = new TemporaryDirectory();
        var documentPath = IOPath.Combine(directory.Path, "MainForm.mfdesign");
        var projectPath = IOPath.Combine(directory.Path, "Example.csproj");
        var diskDocument = CreateDocument();
        DesignDocumentSerializer.Default.Save(documentPath, diskDocument);

        var recoveredDocument = CreateDocument();
        recoveredDocument.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("startup target recovery");
        var store = new FakeRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var targetSnapshot = DesignerRecoverySnapshot.CaptureSaved(
            recoveredDocument,
            documentPath,
            projectPath,
            dirtyRevision: 3,
            revisionGeneration: 0,
            TestRecoverySession,
            TestStartUtc.AddHours(-2),
            sourceFileHashSha256: DesignerFileHash.ComputeFileSha256(documentPath));
        var targetCandidate = CreateCandidate(store, targetSnapshot);
        var newerInactiveSnapshot = DesignerRecoverySnapshot.CaptureUnsaved(
            CreateDocument("OtherForm", "otherForm", "otherButton"),
            Guid.Parse("8180caf0-98f8-4794-a3c4-83d1bd6c4f5d"),
            "OtherForm.mfdesign",
            projectPath: null,
            dirtyRevision: 1,
            revisionGeneration: 0,
            TestRecoverySession,
            TestStartUtc.AddHours(-1));
        var newerInactiveCandidate = CreateCandidate(store, newerInactiveSnapshot);
        store.DiscoveryCandidates.AddRange([targetCandidate, newerInactiveCandidate]);
        store.OnCleanup = (policy, protectedPaths, _) =>
        {
            var protectedSet = protectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var deletedPaths = store.DiscoveryCandidates
                .Where(candidate => !protectedSet.Contains(candidate.ArtifactPath))
                .OrderByDescending(candidate => candidate.Envelope!.Metadata!.TimestampUtc)
                .Skip(policy.MaxArtifacts)
                .Select(candidate => candidate.ArtifactPath)
                .ToArray();
            foreach (var deletedPath in deletedPaths)
                store.Delete(deletedPath);
            return new DesignerRecoveryCleanupResult(
                deletedPaths,
                [],
                store.DiscoveryCandidates.Count,
                WasTruncated: false);
        };

        var options = CreateOptions();
        options.MaximumRecoveryFiles = 1;
        var environment = new TestDesignerHostEnvironment(documentPath, projectPath);
        using var session = new DesignerSession(environment, DesignerControlRenderMode.Runtime, 2000);
        var files = new DesignerFileService(environment, () => session.CurrentDocumentPath);
        var scheduler = new ManualDesignerOneShotScheduler(TestStartUtc);
        var dispatcher = new TestUiDispatcher();
        var watchers = new FakeFileChangeSourceFactory();
        using var coordinator = new DesignerPersistenceCoordinator(
            session,
            files,
            options,
            store,
            scheduler,
            dispatcher,
            watchers,
            new DesignerRecoverySessionIdentity(Guid.Parse("8999751b-9254-435b-a37e-72694a410223"), 4200));

        Assert.Empty(session.OpenDocuments);
        Assert.Equal(targetCandidate.ArtifactPath, Assert.Single(store.LastCleanupProtectedPaths));
        Assert.DoesNotContain(targetCandidate.ArtifactPath, store.DeletedPaths);

        session.OpenDocument(DesignDocumentSerializer.Default.Load(documentPath), documentPath, markDirty: false);

        var notice = Assert.IsType<DesignerPersistenceNotification>(coordinator.CurrentNotification);
        Assert.Equal("MainForm.mfdesign", notice.DocumentName);
        Assert.Equal(DesignerPersistenceNoticeKind.RecoveryAvailable, notice.Kind);
        Assert.DoesNotContain(targetCandidate.ArtifactPath, store.DeletedPaths);
    }

    [Fact]
    public void UnsavedRecoveryRestoreOpensDirtyScratchDocumentWithoutAdoptingArtifactPath()
    {
        var recovered = CreateDocument("RecoveredForm", "recoveredForm", "recoveredButton");
        recovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("unsaved recovered payload");
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                    recovered,
                    Guid.Parse("cb59a337-8002-4127-96fc-70f421c68495"),
                    "RecoveredForm.mfdesign",
                    projectPath: null,
                    dirtyRevision: 3,
                    revisionGeneration: 1,
                    TestRecoverySession,
                    TestStartUtc.AddMinutes(-1));
                store.DiscoveryCandidates.Add(CreateCandidate(store, snapshot));
            });
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Restore,
            saveAsPath: null,
            out var error), error);

        Assert.Equal(2, test.Session.OpenDocuments.Count);
        Assert.Equal("RecoveredForm", test.Session.Document.ClassName);
        Assert.Equal("unsaved recovered payload", test.Session.Document.Controls[0].Properties["Text"].GetString());
        Assert.Null(test.Session.ActiveOpenDocument!.Path);
        Assert.True(test.Session.IsDirty);
        Assert.False(test.Session.Transactions.CanUndo);
    }

    [Fact]
    public void ObsoleteRecoveryMatchingCanonicalPayloadIsDeletedWithoutPrompt()
    {
        string? artifactPath = null;
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, canonicalDocument) =>
            {
                var hash = DesignerFileHash.ComputeFileSha256(documentPath!);
                var snapshot = DesignerRecoverySnapshot.CaptureSaved(
                    canonicalDocument,
                    documentPath!,
                    projectPath: null,
                    dirtyRevision: 1,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc.AddHours(-1),
                    sourceFileHashSha256: hash);
                var candidate = CreateCandidate(store, snapshot);
                artifactPath = candidate.ArtifactPath;
                store.DiscoveryCandidates.Add(candidate);
            });

        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.Contains(artifactPath, test.Store.DeletedPaths);
    }

    [Fact]
    public void RecoveryWhoseSourceAndDiskBothChangedRequiresExplicitConflictDecision()
    {
        var recovered = CreateDocument();
        recovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("recovery side");
        using var test = CoordinatorTestContext.CreateSaved(
            configureStore: (store, documentPath, _) =>
            {
                var snapshot = DesignerRecoverySnapshot.CaptureSaved(
                    recovered,
                    documentPath!,
                    projectPath: null,
                    dirtyRevision: 2,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc,
                    sourceFileHashSha256: new string('0', 64));
                store.DiscoveryCandidates.Add(CreateCandidate(store, snapshot));
            });

        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(DesignerPersistenceNoticeKind.RecoveryConflict, notice.Kind);
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.Restore));
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.OpenDisk));
        Assert.True(notice.Actions.HasFlag(DesignerPersistenceActions.SaveAs));
        Assert.Contains("Recovery payload SHA-256", test.Coordinator.GetCurrentComparisonText(notice.Id), StringComparison.Ordinal);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Keep,
            saveAsPath: null,
            out var error), error);
        Assert.Null(test.Coordinator.CurrentNotification);
        Assert.False(test.Session.IsDirty);
        Assert.DoesNotContain(test.Store.DiscoveryCandidates[0].ArtifactPath, test.Store.DeletedPaths);
    }

    [Theory]
    [InlineData((int)DesignerRecoveryCandidateStatus.Corrupt)]
    [InlineData((int)DesignerRecoveryCandidateStatus.Unsupported)]
    public void InvalidRecoveryIsQuarantinedAndReportedWithoutOpeningDocument(int rawStatus)
    {
        var status = (DesignerRecoveryCandidateStatus)rawStatus;
        string? artifactPath = null;
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                artifactPath = IOPath.Combine(store.RootPath, $"invalid-{status}{DesignerRecoveryFormat.ArtifactExtension}");
                store.DiscoveryCandidates.Add(new DesignerRecoveryCandidate(
                    artifactPath,
                    status,
                    TestStartUtc,
                    Envelope: null,
                    Document: null,
                    Error: $"{status} test artifact"));
            });

        Assert.Contains(artifactPath, test.Store.QuarantinedPaths);
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        Assert.Equal(
            status == DesignerRecoveryCandidateStatus.Corrupt
                ? DesignerPersistenceNoticeKind.CorruptRecovery
                : DesignerPersistenceNoticeKind.UnsupportedRecovery,
            notice.Kind);
        Assert.Single(test.Session.OpenDocuments);

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.Dismiss,
            saveAsPath: null,
            out var error), error);
        Assert.Null(test.Coordinator.CurrentNotification);
    }

    [Fact]
    public void RecoverySaveAsWritesIndependentCanonicalCopyAndRemovesArtifact()
    {
        var recovered = CreateDocument("RecoveredForm", "recoveredForm", "recoveredButton");
        recovered.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("save recovered elsewhere");
        string? artifactPath = null;
        using var test = CoordinatorTestContext.CreateUnsaved(
            markDirty: false,
            configureStore: (store, _, _) =>
            {
                var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                    recovered,
                    Guid.Parse("98b495fe-fec7-4656-87d2-941bba70f8ce"),
                    "RecoveredForm.mfdesign",
                    projectPath: null,
                    dirtyRevision: 5,
                    revisionGeneration: 0,
                    TestRecoverySession,
                    TestStartUtc);
                var candidate = CreateCandidate(store, snapshot);
                artifactPath = candidate.ArtifactPath;
                store.DiscoveryCandidates.Add(candidate);
            });
        var notice = Assert.IsType<DesignerPersistenceNotification>(test.Coordinator.CurrentNotification);
        var saveAsPath = IOPath.Combine(test.Directory.Path, "RecoveredCopy.mfdesign");

        Assert.True(test.Coordinator.ApplyCurrentAction(
            notice.Id,
            DesignerPersistenceActions.SaveAs,
            saveAsPath,
            out var error), error);

        Assert.True(File.Exists(saveAsPath));
        Assert.Contains("save recovered elsewhere", File.ReadAllText(saveAsPath), StringComparison.Ordinal);
        Assert.Contains(artifactPath, test.Store.DeletedPaths);
        Assert.Equal(saveAsPath, test.Session.ActiveOpenDocument!.Path);
        Assert.False(test.Session.IsDirty);
    }

    [Fact]
    public async Task CloseDuringBlockedWriteDeletesLateRecoveryAndDoesNotReschedule()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        test.Store.OnWrite = snapshot =>
        {
            writeEntered.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(10)));
            return test.Store.Success(snapshot);
        };

        test.Edit("closing");
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));
        test.Session.OpenDocument(CreateDocument("SecondForm", "secondForm", "secondButton"), path: null, markDirty: false);
        test.Session.CloseDocument(0);
        releaseWrite.Set();
        await test.DrainAsync();

        var artifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        Assert.Contains(artifact, test.Store.DeletedPaths);
        Assert.Equal(1, test.Coordinator.TrackedDocumentCount);
    }

    [Fact]
    public async Task PrepareDiscardDeletesRecoveryAndPreventsLateWriteFromSurviving()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        test.Edit("discard this");
        await test.FireDebounceAsync();
        var firstArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);

        Assert.True(test.Coordinator.PrepareDocumentForDiscard(
            test.Session.ActiveOpenDocument!,
            out var error), error);

        Assert.Contains(firstArtifact, test.Store.DeletedPaths);
        Assert.Null(test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
    }

    [Fact]
    public async Task FailedDiscardCleanupKeepsArtifactAndReschedulesDocumentProtection()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        test.Edit("must remain protected");
        await test.FireDebounceAsync();
        var protectedArtifact = Assert.Single(test.Store.SuccessfulArtifactPaths);
        test.Store.OnDelete = artifactPath => new DesignerRecoveryFileOperationResult(
            false,
            ResultPath: null,
            $"Cannot delete {IOPath.GetFileName(artifactPath)}");

        var prepared = test.Coordinator.PrepareDocumentForDiscard(
            test.Session.ActiveOpenDocument!,
            out var error);

        Assert.False(prepared);
        Assert.Contains("Cannot delete", error, StringComparison.Ordinal);
        Assert.Contains(protectedArtifact, test.Store.DeleteAttempts);
        Assert.DoesNotContain(protectedArtifact, test.Store.DeletedPaths);
        Assert.Equal(protectedArtifact, test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
        Assert.True(test.Coordinator.GetActiveDiagnostics().AutosavePending);

        test.Store.OnDelete = null;
        test.Scheduler.AdvanceBy(test.Options.AutoSaveDebounceDelay);
        await test.DrainAsync();

        Assert.Equal(2, test.Store.WriteCallCount);
        Assert.True(test.Session.IsDirty);
        Assert.NotNull(test.Coordinator.GetActiveDiagnostics().RecoveryArtifactPath);
    }

    [Fact]
    public void EnsureRecoveryNowRejectsActiveTransactionWithoutWritingPartialState()
    {
        using var test = CoordinatorTestContext.CreateUnsaved(markDirty: false);
        using var transaction = test.Session.Transactions.Begin("in progress");
        test.Edit("not committed yet");

        Assert.False(test.Coordinator.EnsureRecoveryNow(test.Session.ActiveOpenDocument!, out var error));

        Assert.Contains("active transaction", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(test.Store.Writes);
        transaction.Rollback();
    }

    [Fact]
    public void CoordinatorBecomesCollectibleAfterDisposeAndIdle()
    {
        var weakReference = CreateDisposedCoordinatorWeakReference();

        for (var attempt = 0; attempt < 5 && weakReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(weakReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedCoordinatorWeakReference()
    {
        using var directory = new TemporaryDirectory();
        var session = new DesignerSession(null, DesignerControlRenderMode.Runtime, 100);
        session.OpenDocument(CreateDocument(), path: null, markDirty: false);
        var coordinator = new DesignerPersistenceCoordinator(
            session,
            new DesignerFileService(currentDocumentPathProvider: () => session.CurrentDocumentPath),
            CreateOptions(),
            new FakeRecoveryStore(directory.Path),
            new ManualDesignerOneShotScheduler(TestStartUtc),
            new TestUiDispatcher(),
            new FakeFileChangeSourceFactory());
        coordinator.Dispose();
        var weakReference = new WeakReference(coordinator);
        coordinator = null!;
        session.Dispose();
        session = null!;
        return weakReference;
    }

    private static ModernFormsDesignerOptions CreateOptions()
        => new()
        {
            AutoSaveEnabled = true,
            AutoSaveDebounceDelay = TimeSpan.FromSeconds(1),
            AutoSaveMaximumInterval = TimeSpan.FromSeconds(10),
            AutoGenerateDesignerCodeOnSave = false,
            RecoveryRetention = TimeSpan.FromDays(14),
            MaximumRecoveryFiles = 100
        };

    private static DesignerRecoveryCandidate CreateCandidate(
        FakeRecoveryStore store,
        DesignerRecoverySnapshot snapshot,
        string? artifactPath = null)
    {
        artifactPath ??= IOPath.Combine(
            store.RootPath,
            $"discovered-{Guid.NewGuid():N}{DesignerRecoveryFormat.ArtifactExtension}");
        return new DesignerRecoveryCandidate(
            artifactPath,
            DesignerRecoveryCandidateStatus.Valid,
            snapshot.Metadata.TimestampUtc,
            DesignerRecoveryEnvelope.FromSnapshot(snapshot),
            DesignDocumentSerializer.Default.Deserialize(snapshot.SerializedDesignDocument),
            Error: null);
    }

    private static string[] AddTwoSavedRecoveryCandidates(FakeRecoveryStore store, string documentPath)
    {
        var sourceHash = DesignerFileHash.ComputeFileSha256(documentPath);
        var older = CreateDocument();
        older.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("older recovery");
        var newer = CreateDocument();
        newer.Controls[0].Properties["Text"] = DesignPropertyValue.FromString("newer recovery");
        var olderCandidate = CreateCandidate(store, DesignerRecoverySnapshot.CaptureSaved(
            older,
            documentPath,
            projectPath: null,
            dirtyRevision: 4,
            revisionGeneration: 1,
            TestRecoverySession,
            TestStartUtc.AddMinutes(-2),
            sourceFileHashSha256: sourceHash));
        var newerCandidate = CreateCandidate(store, DesignerRecoverySnapshot.CaptureSaved(
            newer,
            documentPath,
            projectPath: null,
            dirtyRevision: 5,
            revisionGeneration: 1,
            TestRecoverySession,
            TestStartUtc.AddMinutes(-1),
            sourceFileHashSha256: sourceHash));
        store.DiscoveryCandidates.AddRange([olderCandidate, newerCandidate]);
        return [olderCandidate.ArtifactPath, newerCandidate.ArtifactPath];
    }

    private static void AssertRestartHasNoRecovery(FakeRecoveryStore store, string documentPath)
    {
        using var session = new DesignerSession(null, DesignerControlRenderMode.Runtime, 100);
        session.OpenDocument(DesignDocumentSerializer.Default.Load(documentPath), documentPath, markDirty: false);
        using var coordinator = new DesignerPersistenceCoordinator(
            session,
            new DesignerFileService(currentDocumentPathProvider: () => session.CurrentDocumentPath),
            CreateOptions(),
            store,
            new ManualDesignerOneShotScheduler(TestStartUtc.AddMinutes(10)),
            new TestUiDispatcher(),
            new FakeFileChangeSourceFactory(),
            TestRecoverySession);

        Assert.Null(coordinator.CurrentNotification);
    }

    private static DesignDocument CreateDocument(
        string className = "MainForm",
        string formName = "mainForm",
        string buttonName = "button1")
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = className,
            FormName = formName,
            Size = new DesignSize(800, 600)
        };
        document.Controls.AddNode("Button", buttonName, new DesignBounds(10, 10, 100, 30));
        return document;
    }

    private sealed class CoordinatorTestContext : IDisposable
    {
        private CoordinatorTestContext(
            DesignDocument document,
            string? path,
            bool markDirty,
            Action<ModernFormsDesignerOptions>? configureOptions,
            Action<FakeRecoveryStore, string?, DesignDocument>? configureStore,
            IDesignerBackgroundWorkQueue? backgroundWorkQueue,
            IDesignerStableFileReader? stableFileReader)
        {
            Directory = new TemporaryDirectory();
            DocumentPath = path is null ? null : IOPath.Combine(Directory.Path, path);
            if (DocumentPath is not null)
                DesignDocumentSerializer.Default.Save(DocumentPath, document);

            Scheduler = new ManualDesignerOneShotScheduler(TestStartUtc);
            Dispatcher = new TestUiDispatcher();
            Store = new FakeRecoveryStore(IOPath.Combine(Directory.Path, "Recovery"));
            configureStore?.Invoke(Store, DocumentPath, document);
            Watchers = new FakeFileChangeSourceFactory();
            Options = CreateOptions();
            configureOptions?.Invoke(Options);
            Session = new DesignerSession(null, DesignerControlRenderMode.Runtime, 2000);
            Session.OpenDocument(document, DocumentPath, markDirty);
            Files = new DesignerFileService(currentDocumentPathProvider: () => Session.CurrentDocumentPath);
            Coordinator = new DesignerPersistenceCoordinator(
                Session,
                Files,
                Options,
                Store,
                Scheduler,
                Dispatcher,
                Watchers,
                new DesignerRecoverySessionIdentity(Guid.Parse("cc7780e4-c1aa-4e41-a153-b29141111901"), 4100),
                backgroundWorkQueue,
                stableFileReader);
        }

        public TemporaryDirectory Directory { get; }

        public string? DocumentPath { get; }

        public ManualDesignerOneShotScheduler Scheduler { get; }

        public TestUiDispatcher Dispatcher { get; }

        public FakeRecoveryStore Store { get; }

        public FakeFileChangeSourceFactory Watchers { get; }

        public ModernFormsDesignerOptions Options { get; }

        public DesignerSession Session { get; }

        public DesignerFileService Files { get; }

        public DesignerPersistenceCoordinator Coordinator { get; }

        public static CoordinatorTestContext CreateUnsaved(
            bool markDirty,
            Action<ModernFormsDesignerOptions>? configureOptions = null,
            Action<FakeRecoveryStore, string?, DesignDocument>? configureStore = null,
            DesignDocument? document = null,
            IDesignerBackgroundWorkQueue? backgroundWorkQueue = null,
            IDesignerStableFileReader? stableFileReader = null)
            => new(
                document ?? CreateDocument(),
                null,
                markDirty,
                configureOptions,
                configureStore,
                backgroundWorkQueue,
                stableFileReader);

        public static CoordinatorTestContext CreateSaved(
            Action<ModernFormsDesignerOptions>? configureOptions = null,
            Action<FakeRecoveryStore, string?, DesignDocument>? configureStore = null,
            DesignDocument? document = null,
            string fileName = "MainForm.mfdesign",
            IDesignerBackgroundWorkQueue? backgroundWorkQueue = null,
            IDesignerStableFileReader? stableFileReader = null)
            => new(
                document ?? CreateDocument(),
                fileName,
                markDirty: false,
                configureOptions,
                configureStore,
                backgroundWorkQueue,
                stableFileReader);

        public void Edit(string value)
            => Session.SetPropertyValue(
                Session.Document.Controls[0],
                "Text",
                DesignPropertyValue.FromString(value));

        public Task FireDebounceAsync()
        {
            Scheduler.AdvanceBy(Options.AutoSaveDebounceDelay);
            return DrainAsync();
        }

        public Task FireExternalDebounceAsync()
        {
            Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(400));
            return DrainAsync();
        }

        public async Task FireExternalObservationThroughRetryLimitAsync()
        {
            await FireExternalDebounceAsync();
            for (var retry = 1; retry < 3; retry++)
            {
                Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(750));
                await DrainAsync();
            }
        }

        public Task DrainAsync()
        {
            for (var pass = 0; pass < 20; pass++)
            {
                WaitForCompletion(Coordinator.WaitForIdleAsync());
                var drained = Dispatcher.DrainAll();
                if (drained == 0 && Dispatcher.PendingCount == 0)
                {
                    WaitForCompletion(Coordinator.WaitForIdleAsync());
                    if (Dispatcher.PendingCount == 0)
                        return Task.CompletedTask;
                }
            }

            throw new TimeoutException("The deterministic Designer persistence queue did not become idle.");
        }

        public void WaitForBackgroundOnly()
            => WaitForCompletion(Coordinator.WaitForIdleAsync());

        public void WriteExternalDesign(string text)
        {
            var external = CreateDocument();
            external.Controls[0].Properties["Text"] = DesignPropertyValue.FromString(text);
            DesignDocumentSerializer.Default.Save(DocumentPath!, external);
        }

        public void RaiseDesignChange()
        {
            var watcher = Watchers.Latest(DocumentPath!);
            watcher.Raise(DesignerFileChangeKind.Changed, watcher.DesignDocumentPath);
        }

        public void RaiseGeneratedChange()
        {
            var watcher = Watchers.Latest(DocumentPath!);
            watcher.Raise(DesignerFileChangeKind.Changed, watcher.GeneratedCodePath);
        }

        private static void WaitForCompletion(Task task)
        {
            if (!SpinWait.SpinUntil(() => task.IsCompleted, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("A Designer persistence background operation did not complete.");
            if (task.IsCanceled)
                throw new TaskCanceledException(task);
            if (task.Exception is { } exception)
                throw exception.GetBaseException();
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            Dispatcher.DrainAll();
            Session.Dispose();
            Directory.Dispose();
        }
    }

    private sealed class ManualDesignerBackgroundWorkQueue : IDesignerBackgroundWorkQueue
    {
        private readonly Queue<IWorkItem> pending = new();

        public int PendingCount => pending.Count;

        public Task<T> Run<T>(Func<T> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var item = new WorkItem<T>(callback);
            pending.Enqueue(item);
            return item.Task;
        }

        public void RunNext()
        {
            Assert.True(pending.TryDequeue(out var item), "No Designer background work is pending.");
            item.Run();
        }

        private interface IWorkItem
        {
            void Run();
        }

        private sealed class WorkItem<T>(Func<T> callback) : IWorkItem
        {
            private readonly TaskCompletionSource<T> completion = new();

            public Task<T> Task => completion.Task;

            public void Run()
            {
                try
                {
                    completion.SetResult(callback());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }
        }
    }

    private sealed class TestStableFileReader(
        Func<int, string, DesignerStableFileReadResult> read) : IDesignerStableFileReader
    {
        private int readCallCount;

        public int ReadCallCount => Volatile.Read(ref readCallCount);

        public DesignerStableFileReadResult Read(string path)
            => read(Interlocked.Increment(ref readCallCount), path);
    }

    private sealed class TestDesignerHostEnvironment(
        string? currentDocumentPath,
        string? currentProjectPath) : IDesignerHostEnvironment
    {
        public string? CurrentDocumentPath { get; } = currentDocumentPath;

        public string? CurrentProjectPath { get; } = currentProjectPath;

        public void ReportStatus(string message)
        {
        }

        public void ReportOutput(string message)
        {
        }
    }

    private sealed class FakeRecoveryStore(string rootPath) : IDesignerRecoveryStore
    {
        private int artifactSequence;
        private int activeWriteCount;
        private int maxConcurrentWriteCount;
        private int writeCallCount;

        public string RootPath { get; } = rootPath;

        public ConcurrentQueue<DesignerRecoverySnapshot> Writes { get; } = new();

        public ConcurrentQueue<string> SuccessfulArtifactPaths { get; } = new();

        public ConcurrentQueue<string> DeletedPaths { get; } = new();

        public ConcurrentQueue<string> DeleteAttempts { get; } = new();

        public ConcurrentQueue<string> QuarantinedPaths { get; } = new();

        public List<DesignerRecoveryCandidate> DiscoveryCandidates { get; } = [];

        public Func<DesignerRecoverySnapshot, DesignerRecoveryWriteResult>? OnWrite { get; set; }

        public Func<string, DesignerRecoveryFileOperationResult>? OnDelete { get; set; }

        public Func<
            DesignerRecoveryRetentionPolicy,
            IReadOnlyList<string>,
            DateTimeOffset?,
            DesignerRecoveryCleanupResult>? OnCleanup { get; set; }

        public IReadOnlyList<string> LastCleanupProtectedPaths { get; private set; } = [];

        public int WriteCallCount => Volatile.Read(ref writeCallCount);

        public int MaxConcurrentWriteCount => Volatile.Read(ref maxConcurrentWriteCount);

        public DesignerRecoveryWriteResult Write(DesignerRecoverySnapshot snapshot)
        {
            Interlocked.Increment(ref writeCallCount);
            Writes.Enqueue(snapshot);
            var active = Interlocked.Increment(ref activeWriteCount);
            UpdateMaximum(ref maxConcurrentWriteCount, active);
            try
            {
                var result = OnWrite?.Invoke(snapshot) ?? Success(snapshot);
                if (result.Succeeded)
                    SuccessfulArtifactPaths.Enqueue(result.ArtifactPath);
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref activeWriteCount);
            }
        }

        public DesignerRecoveryWriteResult Success(DesignerRecoverySnapshot snapshot)
            => new(true, GetPath(snapshot), Error: null);

        public string GetPath(DesignerRecoverySnapshot snapshot)
            => IOPath.Combine(
                RootPath,
                $"{snapshot.Identity.FileNameToken}-{Interlocked.Increment(ref artifactSequence)}{DesignerRecoveryFormat.ArtifactExtension}");

        public DesignerRecoveryCandidate Read(string artifactPath)
            => DiscoveryCandidates.Single(candidate => candidate.ArtifactPath == artifactPath);

        public DesignerRecoveryDiscoveryResult Discover()
        {
            var deleted = DeletedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new DesignerRecoveryDiscoveryResult(
                DiscoveryCandidates.Where(candidate => !deleted.Contains(candidate.ArtifactPath)).ToArray(),
                WasTruncated: false,
                Error: null);
        }

        public DesignerRecoveryFileOperationResult Delete(string artifactPath)
        {
            DeleteAttempts.Enqueue(artifactPath);
            var result = OnDelete?.Invoke(artifactPath)
                ?? new DesignerRecoveryFileOperationResult(true, artifactPath, Error: null);
            if (result.Succeeded)
                DeletedPaths.Enqueue(artifactPath);
            return result;
        }

        public DesignerRecoveryFileOperationResult Quarantine(string artifactPath, DateTimeOffset? timestampUtc = null)
        {
            QuarantinedPaths.Enqueue(artifactPath);
            return new DesignerRecoveryFileOperationResult(true, artifactPath + ".quarantined", Error: null);
        }

        public DesignerRecoveryCleanupResult Cleanup(
            DesignerRecoveryRetentionPolicy policy,
            IEnumerable<string>? protectedArtifactPaths = null,
            DateTimeOffset? nowUtc = null)
        {
            LastCleanupProtectedPaths = protectedArtifactPaths?.ToArray() ?? [];
            return OnCleanup?.Invoke(policy, LastCleanupProtectedPaths, nowUtc)
                ?? new DesignerRecoveryCleanupResult([], [], 0, WasTruncated: false);
        }

        private static void UpdateMaximum(ref int target, int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref target);
                if (candidate <= current || Interlocked.CompareExchange(ref target, candidate, current) == current)
                    return;
            }
        }
    }

    private sealed class TestUiDispatcher : IDesignerUiDispatcher
    {
        private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
        private readonly ConcurrentQueue<Action> callbacks = new();

        public int PendingCount => callbacks.Count;

        public bool QueueAllCallbacks { get; set; }

        public void Post(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (!QueueAllCallbacks && Environment.CurrentManagedThreadId == ownerThreadId)
                callback();
            else
                callbacks.Enqueue(callback);
        }

        public int DrainAll()
        {
            var count = 0;
            while (callbacks.TryDequeue(out var callback))
            {
                callback();
                count++;
            }
            return count;
        }
    }

    private sealed class FakeFileChangeSourceFactory : IDesignerFileChangeSourceFactory
    {
        private readonly List<FakeFileChangeSource> sources = [];

        public IDesignerFileChangeSource Create(string designDocumentPath)
        {
            var source = new FakeFileChangeSource(designDocumentPath);
            sources.Add(source);
            return source;
        }

        public FakeFileChangeSource Latest(string designDocumentPath)
            => sources.Last(source => PathsEqual(source.DesignDocumentPath, designDocumentPath));

        private static bool PathsEqual(string left, string right)
            => string.Equals(IOPath.GetFullPath(left), IOPath.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeFileChangeSource : IDesignerFileChangeSource
    {
        private EventHandler<DesignerFileChangeEventArgs>? changed;

        public FakeFileChangeSource(string designDocumentPath)
        {
            DesignDocumentPath = IOPath.GetFullPath(designDocumentPath);
            GeneratedCodePath = IOPath.Combine(
                IOPath.GetDirectoryName(DesignDocumentPath)!,
                $"{IOPath.GetFileNameWithoutExtension(DesignDocumentPath)}.Designer.cs");
        }

        public string DesignDocumentPath { get; }

        public string GeneratedCodePath { get; }

        public bool IsDisposed { get; private set; }

        public event EventHandler<DesignerFileChangeEventArgs>? Changed
        {
            add => changed += value;
            remove => changed -= value;
        }

        public void Raise(DesignerFileChangeKind kind, string path, string? oldPath = null)
            => changed?.Invoke(this, new DesignerFileChangeEventArgs(kind, path, oldPath));

        public void Dispose()
        {
            IsDisposed = true;
            changed = null;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = IOPath.Combine(
                IOPath.GetTempPath(),
                "ModernFormsNext.Designer.CoordinatorTests",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
                System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
