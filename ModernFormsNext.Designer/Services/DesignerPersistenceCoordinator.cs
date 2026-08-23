using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

[Flags]
internal enum DesignerPersistenceActions
{
    None = 0,
    Restore = 1,
    Discard = 2,
    Keep = 4,
    Reload = 8,
    SaveAs = 16,
    OpenDisk = 32,
    Compare = 64,
    Dismiss = 128
}

internal enum DesignerPersistenceNoticeKind
{
    RecoveryAvailable,
    RecoveryConflict,
    CorruptRecovery,
    UnsupportedRecovery,
    ExternalDesignConflict,
    ExternalGeneratedCodeConflict,
    ExternalGeneratedCodeInvalid,
    FileMissing,
    AutosaveFailed
}

internal sealed record DesignerPersistenceNotification(
    Guid Id,
    DesignerPersistenceNoticeKind Kind,
    string Title,
    string Message,
    string DocumentName,
    DesignerPersistenceActions Actions,
    DateTimeOffset? RecoveryTimestampUtc = null,
    DateTimeOffset? DiskTimestampUtc = null);

internal readonly record struct DesignerSaveResult(bool Succeeded, string? Path, string? Error)
{
    public static DesignerSaveResult Failure(string error)
        => new(false, Path: null, error);
}

/// <summary>
/// Coordinates revision-aware recovery snapshots, canonical saves, and external file changes for
/// every document in one Designer session.
/// </summary>
/// <remarks>
/// Model snapshots and notifications are captured on the Designer UI thread. Recovery writes and
/// file observation run in the background, then marshal immutable results through the configured
/// dispatcher. Each document owns a separate scheduler, write gate, watcher, and conflict state.
/// </remarks>
internal sealed class DesignerPersistenceCoordinator : IDisposable
{
    private static readonly TimeSpan ExternalChangeDebounce = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ExternalChangeRetry = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan AutosaveFailureRetry = TimeSpan.FromSeconds(30);
    private const int ImmediateArtifactDeleteAttemptLimit = 2;

    private readonly DesignerSession session;
    private readonly DesignerFileService files;
    private readonly ModernFormsDesignerOptions options;
    private readonly IDesignerRecoveryStore recoveryStore;
    private readonly IDesignerOneShotScheduler scheduler;
    private readonly IDesignerUiDispatcher dispatcher;
    private readonly IDesignerFileChangeSourceFactory fileChangeSourceFactory;
    private readonly IDesignerBackgroundWorkQueue backgroundWorkQueue;
    private readonly DesignerRecoverySessionIdentity recoverySession;
    private readonly Dictionary<DesignerOpenDocument, DocumentState> documentStates = [];
    private readonly List<RecoveryEntry> recoveryEntries = [];
    private readonly Dictionary<string, HashSet<string>> recoveryArtifactsByIdentity = new(StringComparer.Ordinal);
    private readonly List<InformationNotice> informationNotices = [];
    private readonly HashSet<Task> backgroundTasks = [];
    private readonly object backgroundTasksGate = new();
    private int disposeState;

    public DesignerPersistenceCoordinator(
        DesignerSession session,
        DesignerFileService files,
        ModernFormsDesignerOptions options,
        IDesignerRecoveryStore? recoveryStore = null,
        IDesignerOneShotScheduler? scheduler = null,
        IDesignerUiDispatcher? dispatcher = null,
        IDesignerFileChangeSourceFactory? fileChangeSourceFactory = null,
        DesignerRecoverySessionIdentity? recoverySession = null,
        IDesignerBackgroundWorkQueue? backgroundWorkQueue = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.files = files ?? throw new ArgumentNullException(nameof(files));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.recoveryStore = recoveryStore ?? new DesignerRecoveryStore();
        this.scheduler = scheduler ?? SystemDesignerOneShotScheduler.Instance;
        this.dispatcher = dispatcher ?? DesignerUiDispatcher.Instance;
        this.fileChangeSourceFactory = fileChangeSourceFactory ?? FileSystemDesignerFileChangeSourceFactory.Instance;
        this.recoverySession = recoverySession ?? DesignerRecoverySessionIdentity.Create();
        this.backgroundWorkQueue = backgroundWorkQueue ?? DesignerBackgroundWorkQueue.Instance;

        ValidateOptions();
        Subscribe();
        foreach (var document in session.OpenDocuments)
            TrackDocument(document);
        DiscoverRecovery();
    }

    public event EventHandler? StateChanged;

    public DesignerPersistenceNotification? CurrentNotification
    {
        get
        {
            if (session.ActiveOpenDocument is { } active
                && documentStates.TryGetValue(active, out var state))
            {
                if (state.Recovery is { } documentRecovery)
                    return CreateRecoveryNotification(documentRecovery);
                if (state.Notice is { } documentNotice)
                    return documentNotice;
            }

            if (recoveryEntries.Count > 0)
                return CreateRecoveryNotification(recoveryEntries[0]);

            return informationNotices.Count == 0 ? null : informationNotices[0].Notification;
        }
    }

    public string ActiveStatusText
    {
        get
        {
            if (session.ActiveOpenDocument is not { } document
                || !documentStates.TryGetValue(document, out var state))
            {
                return string.Empty;
            }

            if (state.IsRecoveryWriteInFlight)
                return "Saving recovery copy";
            if (state.AutosavePending)
                return "Recovery copy pending";
            if (!string.IsNullOrWhiteSpace(state.LastAutosaveError))
                return "Recovery save failed";
            if (state.LastAutosavedGeneration == document.RevisionGeneration
                && state.LastAutosavedRevision == document.History.CurrentRevision)
            {
                return $"Recovery copy saved {state.LastAutosaveUtc?.ToLocalTime():t}";
            }
            if (state.Notice is not null)
                return "External change needs attention";

            return string.Empty;
        }
    }

    internal int TrackedDocumentCount => documentStates.Count;

    internal string RecoveryRootPath => recoveryStore.RootPath;

    internal bool ActiveDocumentHasUnresolvedRecovery
        => session.ActiveOpenDocument is { } document
            && documentStates.TryGetValue(document, out var state)
            && state.Recovery is not null;

    internal DesignerPersistenceDiagnostics GetActiveDiagnostics()
    {
        if (session.ActiveOpenDocument is not { } document
            || !documentStates.TryGetValue(document, out var state))
        {
            return DesignerPersistenceDiagnostics.Empty;
        }

        return new DesignerPersistenceDiagnostics(
            state.LastAutosaveUtc,
            state.LastAutosaveError,
            state.CurrentArtifactPath,
            state.AutosavePending,
            state.IsRecoveryWriteInFlight,
            state.LastAutosavedGeneration,
            state.LastAutosavedRevision,
            state.PendingDesignChange || state.PendingGeneratedChange,
            state.Notice?.Kind);
    }

    public DesignerSaveResult SaveActiveDocument(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ThrowIfDisposed();

        var document = session.ActiveOpenDocument;
        if (document is null || !documentStates.TryGetValue(document, out var state))
            return DesignerSaveResult.Failure("There is no open Designer document to save.");
        if (session.Transactions.HasActiveTransaction)
            return DesignerSaveResult.Failure("Save is unavailable until the active Designer transaction completes.");

        string normalizedPath;
        try
        {
            normalizedPath = DesignerDocumentPath.NormalizeDesignPath(path)
                ?? throw new InvalidOperationException("The save path could not be normalized.");
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return DesignerSaveResult.Failure(exception.Message);
        }

        if (session.OpenDocuments.Any(candidate =>
            !ReferenceEquals(candidate, document) && PathsEqual(candidate.Path, normalizedPath)))
        {
            return DesignerSaveResult.Failure(
                $"The Designer document '{normalizedPath}' is already open in another tab.");
        }

        var savingCanonicalPath = !string.IsNullOrWhiteSpace(document.Path)
            && PathsEqual(document.Path, normalizedPath);
        if (savingCanonicalPath && state.Recovery is not null)
        {
            return DesignerSaveResult.Failure(
                "Recovery for this document is still unresolved. Choose Restore, Open Disk, Keep, Discard, or Save As before overwriting the canonical file.");
        }
        if (savingCanonicalPath
            && state.Notice?.Kind is DesignerPersistenceNoticeKind.ExternalDesignConflict
                or DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict
                or DesignerPersistenceNoticeKind.ExternalGeneratedCodeInvalid)
        {
            return DesignerSaveResult.Failure(
                "The file changed outside the Designer. Choose Keep before overwriting it, or use Save As.");
        }

        var originalIdentity = GetIdentity(document, state).Value;
        var preserveUnresolvedRecoveryOnSaveAs = !savingCanonicalPath && state.Recovery is not null;
        CancelAutosaveSchedules(state);
        state.IsNormalSaveInProgress = true;
        var revisionGeneration = document.RevisionGeneration;
        var revision = document.History.CurrentRevision;
        DesignerGenerationFileResult? preparedCode;
        try
        {
            preparedCode = options.AutoGenerateDesignerCodeOnSave
                ? files.PrepareDesignerCode(document.Document, normalizedPath)
                : null;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            state.IsNormalSaveInProgress = false;
            ScheduleAutosave(document, state);
            return DesignerSaveResult.Failure("Generation failed: " + exception.Message);
        }

        if (preparedCode is { Succeeded: false })
        {
            state.IsNormalSaveInProgress = false;
            ScheduleAutosave(document, state);
            return DesignerSaveResult.Failure("Generation failed: " + string.Join("; ", preparedCode.Errors));
        }

        state.WriteGate.Wait();
        try
        {
            CancelExternalSchedule(state);
            if (savingCanonicalPath
                && !VerifyCanonicalTargetsBeforeWrite(
                    document,
                    state,
                    normalizedPath,
                    "Save",
                    out var verificationError))
            {
                return DesignerSaveResult.Failure(verificationError!);
            }

            files.SaveDesignDocument(document.Document, normalizedPath);
            var designHash = DesignerFileHash.ComputeFileSha256(normalizedPath);
            state.KnownDesignHash = designHash;
            state.ExpectedDesignHash = designHash;
            state.SourceLastWriteUtc = TryGetLastWriteUtc(normalizedPath);

            if (preparedCode is not null)
                DesignerAtomicFileWriter.WriteUtf8(preparedCode.Path, preparedCode.Code);

            var generatedPath = files.GetGeneratedCodePath(document.Document, normalizedPath);
            var generatedHash = File.Exists(generatedPath)
                ? DesignerFileHash.ComputeFileSha256(generatedPath)
                : null;
            if (!PathsEqual(document.Path, normalizedPath))
                session.UpdateDocumentPath(document, normalizedPath);

            state.KnownGeneratedHash = generatedHash;
            state.ExpectedGeneratedHash = generatedHash;
            state.FileMissing = false;
            CancelExternalSchedule(state);
            ClearPendingExternalState(state);
            state.SuccessfullySavedGeneration = revisionGeneration;
            state.SuccessfullySavedRevision = revision;
            state.LastAutosaveError = null;
            state.ConsecutiveAutosaveFailures = 0;

            session.MarkSaved(document, revisionGeneration, revision, "Document saved.");
            RecreateWatcher(document, state);
            DeleteObsoleteArtifacts(state);
            if (state.Recovery is { } attachedRecovery)
            {
                // Save As preserves both versions when an attached recovery decision is still
                // unresolved. The candidate returns to the startup queue instead of being deleted
                // as though the user had explicitly discarded or restored it.
                if (!savingCanonicalPath && !recoveryEntries.Contains(attachedRecovery))
                    recoveryEntries.Add(attachedRecovery);
                else
                    recoveryStore.Delete(attachedRecovery.Candidate.ArtifactPath);
                state.Recovery = null;
            }
            var savedIdentity = GetIdentity(document, state).Value;
            var identitiesToClean = new HashSet<string>(StringComparer.Ordinal)
            {
                savedIdentity
            };
            if (!preserveUnresolvedRecoveryOnSaveAs)
            {
                identitiesToClean.Add(originalIdentity);
                if (!string.IsNullOrWhiteSpace(state.OriginRecoveryIdentity))
                    identitiesToClean.Add(state.OriginRecoveryIdentity);
            }

            foreach (var identity in identitiesToClean)
            {
                var cleanupErrors = DeleteRecoveryArtifactsForIdentity(identity);
                foreach (var cleanupError in cleanupErrors)
                    session.LogDiagnostic("Saved document but could not remove an obsolete recovery artifact: " + cleanupError);
                RemoveRecoveryEntriesForIdentity(identity);
                if (cleanupErrors.Count == 0
                    && string.Equals(state.OriginRecoveryIdentity, identity, StringComparison.Ordinal))
                {
                    state.OriginRecoveryIdentity = null;
                }
            }
            session.Log($"Saved {document.DisplayName} to {normalizedPath}.");
            RaiseStateChanged();
            return new DesignerSaveResult(true, normalizedPath, Error: null);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            state.LastAutosaveError = exception.Message;
            session.Log($"Save failed: {exception.Message}");
            RaiseStateChanged();
            return DesignerSaveResult.Failure(exception.Message);
        }
        finally
        {
            state.WriteGate.Release();
            state.IsNormalSaveInProgress = false;
            if (document.IsDirty)
                ScheduleAutosave(document, state);
        }
    }

    private bool VerifyCanonicalTargetsBeforeWrite(
        DesignerOpenDocument document,
        DocumentState state,
        string designPath,
        string operationName,
        out string? error)
    {
        var generatedPath = files.GetGeneratedCodePath(document.Document, designPath);
        var observation = ObserveExternalFiles(designPath, generatedPath, document.Document.RootKind);
        if (!string.IsNullOrWhiteSpace(observation.Error))
        {
            state.PendingDesignChange = true;
            state.PendingGeneratedChange = true;
            ScheduleExternalObservation(document, state, ExternalChangeRetry);
            error = $"{operationName} was canceled because the current disk files could not be verified: " + observation.Error;
            return false;
        }

        var designChanged = IsUnexpectedDiskState(
            observation.Design,
            state.KnownDesignHash,
            state.ExpectedDesignHash);
        var generatedChanged = IsUnexpectedDiskState(
            observation.Generated,
            state.KnownGeneratedHash,
            state.ExpectedGeneratedHash);
        if (!designChanged && !generatedChanged)
        {
            error = null;
            return true;
        }

        // FileSystemWatcher delivery is intentionally debounced and can also be lost after a
        // native buffer overflow. Invalidate any older observation and process the stable hashes
        // synchronously so Save can never silently replace an unobserved external edit.
        CancelExternalSchedule(state);
        var designDisposition = designChanged && observation.Design is { } design
            ? ProcessExternalDesign(document, state, design)
            : ExternalDesignDisposition.NoChange;

        if (generatedChanged && observation.Generated is { } generated)
        {
            if (designDisposition == ExternalDesignDisposition.Accepted)
            {
                state.KnownGeneratedHash = generated.Exists ? generated.Hash : null;
                state.ExpectedGeneratedHash = null;
            }
            else if (designDisposition == ExternalDesignDisposition.Conflict)
            {
                // Keep the design conflict visible. The generated observation is processed only
                // after the user decides what to do with the authoritative .mfdesign version.
                state.DeferredGeneratedObservation = generated;
            }
            else
            {
                ProcessExternalGeneratedCode(document, state, generated);
            }
        }

        RaiseStateChanged();
        error = $"{operationName} was canceled because the canonical Designer files changed outside the Designer. Review the external-change notice, or use Save As.";
        return false;
    }

    private static bool IsUnexpectedDiskState(
        ObservedFile? observed,
        string? knownHash,
        string? expectedHash)
    {
        if (observed is null)
            return false;
        if (!observed.Exists)
            return knownHash is not null || expectedHash is not null;

        return !string.Equals(observed.Hash, knownHash, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(observed.Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public DesignerGenerationFileResult GenerateActiveDocumentCode()
    {
        ThrowIfDisposed();
        if (session.Transactions.HasActiveTransaction)
        {
            return new DesignerGenerationFileResult(
                false,
                string.Empty,
                string.Empty,
                ["Code generation is unavailable until the active Designer transaction completes."]);
        }

        var document = session.ActiveOpenDocument;
        if (document is null || string.IsNullOrWhiteSpace(document.Path))
        {
            return new DesignerGenerationFileResult(
                false,
                string.Empty,
                string.Empty,
                ["Save the Designer document before generating code."]);
        }

        if (documentStates.TryGetValue(document, out var documentState)
            && documentState.Recovery is not null)
        {
            return new DesignerGenerationFileResult(
                false,
                string.Empty,
                string.Empty,
                ["Resolve the attached recovery copy before regenerating canonical Designer code."]);
        }

        if (documentStates.TryGetValue(document, out documentState)
            && documentState.Notice?.Kind is DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict
                or DesignerPersistenceNoticeKind.ExternalGeneratedCodeInvalid)
        {
            return new DesignerGenerationFileResult(
                false,
                string.Empty,
                string.Empty,
                ["Resolve the external generated-code change before overwriting it."]);
        }

        try
        {
            var result = files.PrepareDesignerCode(document.Document, document.Path);
            if (!result.Succeeded)
                return result;

            if (!documentStates.TryGetValue(document, out var state))
                return new DesignerGenerationFileResult(false, string.Empty, string.Empty, ["The Designer document is no longer open."]);
            state.WriteGate.Wait();
            try
            {
                // Invalidate any observation captured before this write. Otherwise its stale hash
                // could complete afterward and be mistaken for a new external modification.
                CancelExternalSchedule(state);
                if (!VerifyCanonicalTargetsBeforeWrite(
                        document,
                        state,
                        document.Path,
                        "Code generation",
                        out var verificationError))
                {
                    return new DesignerGenerationFileResult(false, string.Empty, string.Empty, [verificationError!]);
                }

                DesignerAtomicFileWriter.WriteUtf8(result.Path, result.Code);
                var hash = DesignerFileHash.ComputeFileSha256(result.Path);
                state.KnownGeneratedHash = hash;
                state.ExpectedGeneratedHash = hash;
            }
            finally
            {
                state.WriteGate.Release();
            }

            return result;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            return new DesignerGenerationFileResult(false, string.Empty, string.Empty, [exception.Message]);
        }
    }

    public bool ApplyCurrentAction(
        Guid notificationId,
        DesignerPersistenceActions action,
        string? saveAsPath,
        out string? error)
    {
        ThrowIfDisposed();
        error = null;
        var current = CurrentNotification;
        if (current is null || current.Id != notificationId || (current.Actions & action) == 0)
        {
            error = "The Designer notification is no longer current.";
            return false;
        }

        if (informationNotices.Count > 0 && informationNotices[0].Notification.Id == notificationId)
        {
            informationNotices.RemoveAt(0);
            RaiseStateChanged();
            return true;
        }

        var recovery = FindRecoveryEntry(notificationId);
        if (recovery is not null)
            return ApplyRecoveryAction(recovery, action, saveAsPath, out error);

        var documentState = documentStates.Values.FirstOrDefault(candidate => candidate.Notice?.Id == notificationId);
        if (documentState is null)
        {
            error = "The document associated with this notification is no longer open.";
            return false;
        }

        return ApplyExternalAction(documentState, action, saveAsPath, out error);
    }

    public string GetCurrentComparisonText(Guid notificationId)
    {
        var current = CurrentNotification;
        if (current is null || current.Id != notificationId)
            return "The comparison is no longer available.";

        var recovery = FindRecoveryEntry(notificationId);
        if (recovery is not null)
        {
            var metadata = recovery.Candidate.Envelope!.Metadata!;
            return string.Join(
                Environment.NewLine,
                $"Document: {metadata.SuggestedName}",
                $"Recovery: {metadata.TimestampUtc.LocalDateTime:G}",
                $"Disk: {(recovery.DiskLastWriteUtc?.LocalDateTime.ToString("G") ?? "missing")}",
                $"Recovery payload SHA-256: {recovery.Candidate.Envelope.PayloadSha256}",
                $"Disk SHA-256: {recovery.DiskHash ?? "missing"}",
                string.Empty,
                "This comparison reports trusted fingerprints and timestamps. ModernFormsNext does not merge recovery JSON automatically.");
        }

        var state = documentStates.Values.FirstOrDefault(candidate => candidate.Notice?.Id == notificationId);
        if (state is null)
            return "The comparison is no longer available.";

        return string.Join(
            Environment.NewLine,
            $"Document: {state.Document.DisplayName}",
            $"Designer baseline SHA-256: {state.KnownDesignHash ?? "missing"}",
            $"Current disk SHA-256: {state.PendingDiskDesignHash ?? "missing"}",
            $"Generated-code baseline SHA-256: {state.KnownGeneratedHash ?? "missing"}",
            $"Current generated-code SHA-256: {state.PendingDiskGeneratedHash ?? "missing"}",
            string.Empty,
            "Choose Reload to use the disk model, Keep to retain the Designer model, or Save As to preserve both versions.");
    }

    public void CheckForExternalChanges()
    {
        ThrowIfDisposed();
        if (session.ActiveOpenDocument is not { } document
            || !documentStates.TryGetValue(document, out var state)
            || string.IsNullOrWhiteSpace(document.Path))
        {
            session.Log("External-change check requires a saved Designer document.");
            return;
        }

        state.PendingDesignChange = true;
        state.PendingGeneratedChange = true;
        ScheduleExternalObservation(document, state, TimeSpan.Zero);
    }

    public bool PrepareDocumentForDiscard(DesignerOpenDocument document, out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        error = null;
        if (!documentStates.TryGetValue(document, out var state))
            return true;

        var identities = new HashSet<string>(StringComparer.Ordinal)
        {
            GetIdentity(document, state).Value
        };
        if (!string.IsNullOrWhiteSpace(state.OriginRecoveryIdentity))
            identities.Add(state.OriginRecoveryIdentity);
        CancelAutosaveSchedules(state);
        state.WriteGate.Wait();
        try
        {
            string[] artifactPaths;
            lock (state.ArtifactGate)
            {
                artifactPaths = state.CompletedArtifactPaths
                    .Append(state.CurrentArtifactPath)
                    .Append(state.Recovery?.Candidate.ArtifactPath)
                    .Concat(identities.SelectMany(GetRecoveryArtifactPaths))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            foreach (var artifactPath in artifactPaths)
            {
                var deletion = recoveryStore.Delete(artifactPath);
                if (!deletion.Succeeded)
                {
                    error = deletion.Error ?? $"Recovery artifact '{artifactPath}' could not be deleted.";
                    SetDiscardArtifacts(state, discard: false);
                    if (document.IsDirty)
                        ScheduleAutosave(document, state);
                    return false;
                }

                UnregisterRecoveryArtifact(artifactPath);
            }

            lock (state.ArtifactGate)
                state.CompletedArtifactPaths.Clear();
            state.CurrentArtifactPath = null;
            foreach (var identity in identities)
                RemoveRecoveryEntriesForIdentity(identity);
            state.OriginRecoveryIdentity = null;
            SetDiscardArtifacts(state, discard: true);
            return true;
        }
        finally
        {
            state.WriteGate.Release();
        }
    }

    public void ResumeDocumentProtection(DesignerOpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!documentStates.TryGetValue(document, out var state))
            return;

        SetDiscardArtifacts(state, discard: false);
        state.LastKnownDirty = document.IsDirty;
        if (document.IsDirty)
            ScheduleAutosave(document, state);
    }

    public bool EnsureRecoveryNow(DesignerOpenDocument document, out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        error = null;
        if (!documentStates.TryGetValue(document, out var state))
            return true;
        if (session.Transactions.HasActiveTransaction || state.IsNormalSaveInProgress)
        {
            error = "Recovery cannot be captured during an active transaction or normal save.";
            return false;
        }
        if (!document.IsDirty)
            return true;

        DesignerRecoverySnapshot snapshot;
        try
        {
            snapshot = CaptureSnapshot(document, state);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            error = exception.Message;
            return false;
        }

        state.WriteGate.Wait();
        try
        {
            var autosaveSequence = Interlocked.Increment(ref state.NextAutosaveSequence);
            var result = recoveryStore.Write(snapshot);
            if (!result.Succeeded)
            {
                error = result.Error ?? "Recovery write failed.";
                RecordAutosaveFailure(state, error);
                return false;
            }

            RecordSuccessfulAutosave(state, snapshot, result.ArtifactPath, autosaveSequence);
            return true;
        }
        finally
        {
            state.WriteGate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
            return;

        Unsubscribe();
        foreach (var state in documentStates.Values.ToArray())
            StopTracking(state, preserveDirtyRecovery: state.LastKnownDirty);
        documentStates.Clear();
        StateChanged = null;
        GC.SuppressFinalize(this);
    }

    internal async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task[] tasks;
            lock (backgroundTasksGate)
                tasks = backgroundTasks.ToArray();

            if (tasks.Length == 0)
                return;

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private void Subscribe()
    {
        session.DocumentOpened += Session_DocumentOpened;
        session.DocumentClosed += Session_DocumentClosed;
        session.DocumentPathChanged += Session_DocumentPathChanged;
        session.DocumentBaselineChanged += Session_DocumentBaselineChanged;
        session.DocumentTabsChanged += Session_DocumentTabsChanged;
        session.SettingsChanged += Session_SettingsChanged;
        session.Transactions.TransactionCommitted += Transactions_StableStateChanged;
        session.Transactions.UndoPerformed += Transactions_StableStateChanged;
        session.Transactions.RedoPerformed += Transactions_StableStateChanged;
        session.Transactions.TransactionRolledBack += Transactions_StableStateChanged;
        session.Transactions.HistoryChanged += Transactions_HistoryChanged;
    }

    private void Unsubscribe()
    {
        session.DocumentOpened -= Session_DocumentOpened;
        session.DocumentClosed -= Session_DocumentClosed;
        session.DocumentPathChanged -= Session_DocumentPathChanged;
        session.DocumentBaselineChanged -= Session_DocumentBaselineChanged;
        session.DocumentTabsChanged -= Session_DocumentTabsChanged;
        session.SettingsChanged -= Session_SettingsChanged;
        session.Transactions.TransactionCommitted -= Transactions_StableStateChanged;
        session.Transactions.UndoPerformed -= Transactions_StableStateChanged;
        session.Transactions.RedoPerformed -= Transactions_StableStateChanged;
        session.Transactions.TransactionRolledBack -= Transactions_StableStateChanged;
        session.Transactions.HistoryChanged -= Transactions_HistoryChanged;
    }

    private void Session_DocumentOpened(object? sender, DesignerOpenDocumentEventArgs e)
        => TrackDocument(e.Document);

    private void Session_DocumentClosed(object? sender, DesignerOpenDocumentEventArgs e)
    {
        if (!documentStates.Remove(e.Document, out var state))
            return;

        StopTracking(state, preserveDirtyRecovery: state.LastKnownDirty && !ShouldDiscardArtifacts(state));
        RaiseStateChanged();
    }

    private void Session_DocumentPathChanged(object? sender, DesignerDocumentPathChangedEventArgs e)
    {
        if (!documentStates.TryGetValue(e.Document, out var state))
            return;

        state.KnownDesignHash = TryComputeFileHash(e.NewPath) ?? state.KnownDesignHash;
        state.SourceLastWriteUtc = TryGetLastWriteUtc(e.NewPath) ?? state.SourceLastWriteUtc;
        state.KnownGeneratedHash = TryComputeGeneratedHash(e.Document) ?? state.KnownGeneratedHash;
        RecreateWatcher(e.Document, state);
        RaiseStateChanged();
    }

    private void Session_DocumentBaselineChanged(object? sender, DesignerOpenDocumentEventArgs e)
    {
        if (!documentStates.TryGetValue(e.Document, out var state))
            return;

        CancelAutosaveSchedules(state);
        if (e.Document.IsDirty)
            ScheduleAutosave(e.Document, state);
        RaiseStateChanged();
    }

    private void Session_DocumentTabsChanged(object? sender, EventArgs e)
        => RaiseStateChanged();

    private void Session_SettingsChanged(object? sender, EventArgs e)
    {
        foreach (var (document, state) in documentStates)
        {
            CancelAutosaveSchedules(state);
            if (options.AutoSaveEnabled && document.IsDirty)
                ScheduleAutosave(document, state);
        }
    }

    private void Transactions_StableStateChanged(object? sender, DesignerHistoryEventArgs e)
        => ScheduleAllDocumentsIfNeeded();

    private void Transactions_HistoryChanged(object? sender, EventArgs e)
    {
        if (!session.Transactions.HasActiveTransaction)
            ScheduleAllDocumentsIfNeeded();
    }

    private void ScheduleAllDocumentsIfNeeded()
    {
        if (IsDisposed)
            return;

        foreach (var (document, state) in documentStates)
        {
            if (!document.IsDirty)
            {
                state.LastKnownDirty = false;
                if (state.IsRecoveryWriteInFlight)
                    Interlocked.Increment(ref state.NextAutosaveSequence);
                CancelAutosaveSchedules(state);
                continue;
            }

            state.LastKnownDirty = true;
            ScheduleAutosave(document, state);
        }
    }

    private void TrackDocument(DesignerOpenDocument document)
    {
        if (IsDisposed || documentStates.ContainsKey(document))
            return;

        var state = new DocumentState(document)
        {
            TemporaryRecoveryId = document.Id,
            LastKnownDirty = document.IsDirty,
            KnownDesignHash = TryComputeFileHash(document.Path),
            SourceLastWriteUtc = TryGetLastWriteUtc(document.Path),
            KnownGeneratedHash = TryComputeGeneratedHash(document)
        };
        documentStates.Add(document, state);
        AttachMatchingRecovery(document, state);
        RecreateWatcher(document, state);

        if (document.IsDirty)
            ScheduleAutosave(document, state);
        RaiseStateChanged();
    }

    private void StopTracking(DocumentState state, bool preserveDirtyRecovery)
    {
        CancelAutosaveSchedules(state);
        CancelExternalSchedule(state);
        state.Closed = true;
        state.ChangeSource?.Dispose();
        state.ChangeSource = null;

        if (!preserveDirtyRecovery)
        {
            // Publish the discard decision through the same gate used by a completing recovery
            // worker. Whichever side wins the gate is then responsible for deleting the artifact.
            SetDiscardArtifacts(state, discard: true);
            DeleteAllKnownArtifacts(state);
        }
    }

    private void ScheduleAutosave(DesignerOpenDocument document, DocumentState state)
    {
        if (IsDisposed || state.Closed || !options.AutoSaveEnabled || !document.IsDirty)
        {
            CancelAutosaveSchedules(state);
            return;
        }

        if (state.IsNormalSaveInProgress || session.Transactions.HasActiveTransaction)
            return;

        if (state.IsRecoveryWriteInFlight)
        {
            state.NeedsAnotherAutosave = true;
            state.AutosavePending = true;
            RaiseStateChanged();
            return;
        }

        state.DebounceHandle?.Dispose();
        var debounce = options.AutoSaveDebounceDelay < TimeSpan.Zero
            ? TimeSpan.Zero
            : options.AutoSaveDebounceDelay;
        state.DebounceHandle = scheduler.Schedule(
            debounce,
            () => PostToUi(() => StartAutosave(document, state)));

        if (state.MaximumIntervalHandle is null && options.AutoSaveMaximumInterval > TimeSpan.Zero)
        {
            state.MaximumIntervalHandle = scheduler.Schedule(
                options.AutoSaveMaximumInterval,
                () => PostToUi(() => StartAutosave(document, state)));
        }

        state.AutosavePending = true;
        RaiseStateChanged();
    }

    private void StartAutosave(DesignerOpenDocument document, DocumentState state)
    {
        state.DebounceHandle?.Dispose();
        state.DebounceHandle = null;
        state.MaximumIntervalHandle?.Dispose();
        state.MaximumIntervalHandle = null;

        if (IsDisposed || state.Closed || !documentStates.TryGetValue(document, out var current)
            || !ReferenceEquals(current, state) || !options.AutoSaveEnabled || !document.IsDirty)
        {
            state.AutosavePending = false;
            return;
        }

        if (session.Transactions.HasActiveTransaction || state.IsNormalSaveInProgress)
        {
            state.AutosavePending = true;
            return;
        }

        if (state.IsRecoveryWriteInFlight)
        {
            state.NeedsAnotherAutosave = true;
            state.AutosavePending = true;
            return;
        }

        DesignerRecoverySnapshot snapshot;
        try
        {
            snapshot = CaptureSnapshot(document, state);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            RecordAutosaveFailure(state, exception.Message);
            ScheduleAutosaveRetry(document, state);
            return;
        }

        var autosaveSequence = Interlocked.Increment(ref state.NextAutosaveSequence);
        state.IsRecoveryWriteInFlight = true;
        state.AutosavePending = false;
        state.NeedsAnotherAutosave = false;
        RaiseStateChanged();

        var task = backgroundWorkQueue.Run(() =>
        {
            state.WriteGate.Wait();
            try
            {
                if (autosaveSequence < Volatile.Read(ref state.NextAutosaveSequence)
                    || ShouldDiscardArtifacts(state)
                    || state.SuccessfullySavedGeneration == snapshot.Metadata.RevisionGeneration
                        && state.SuccessfullySavedRevision >= snapshot.Metadata.DirtyRevision)
                {
                    return new AutosaveWriteOutcome(Result: null, Skipped: true, DeletedAfterWrite: false);
                }

                var result = recoveryStore.Write(snapshot);
                var discardArtifact = false;
                var deletedAfterWrite = false;
                if (result.Succeeded)
                {
                    lock (state.ArtifactGate)
                    {
                        discardArtifact = state.DiscardArtifactsOnCompletion;
                        if (!discardArtifact)
                            state.CompletedArtifactPaths.Add(result.ArtifactPath);
                    }

                    // Disposal can suppress the UI completion callback. Delete here so a clean
                    // close still wins even when the write completed after tracking stopped.
                    if (discardArtifact)
                        deletedAfterWrite = TryDeleteArtifactImmediately(result.ArtifactPath);
                }
                return new AutosaveWriteOutcome(result, Skipped: false, DeletedAfterWrite: deletedAfterWrite);
            }
            finally
            {
                state.WriteGate.Release();
            }
        });
        TrackBackgroundTask(task);
        _ = task.ContinueWith(
            completed => PostToUi(() => CompleteAutosave(document, state, snapshot, autosaveSequence, completed)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CompleteAutosave(
        DesignerOpenDocument document,
        DocumentState state,
        DesignerRecoverySnapshot snapshot,
        long autosaveSequence,
        Task<AutosaveWriteOutcome> task)
    {
        state.IsRecoveryWriteInFlight = false;
        AutosaveWriteOutcome outcome;
        try
        {
            outcome = task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            outcome = new AutosaveWriteOutcome(
                new DesignerRecoveryWriteResult(false, string.Empty, exception.GetBaseException().Message),
                Skipped: false,
                DeletedAfterWrite: false);
        }

        var result = outcome.Result;
        if (outcome.Skipped)
        {
            // A newer synchronous snapshot, Save, Undo-to-clean, or explicit discard won the
            // write gate. Nothing reached disk and no failure should be reported.
        }
        else if (ShouldDiscardArtifacts(state) || IsSnapshotObsolete(state, snapshot))
        {
            if (result!.Succeeded && !outcome.DeletedAfterWrite)
                recoveryStore.Delete(result.ArtifactPath);
        }
        else if (result!.Succeeded)
        {
            RecordSuccessfulAutosave(state, snapshot, result.ArtifactPath, autosaveSequence);
        }
        else
        {
            RecordAutosaveFailure(state, result.Error ?? "Recovery write failed.");
        }

        if (IsDisposed || state.Closed || !documentStates.TryGetValue(document, out var current)
            || !ReferenceEquals(current, state))
        {
            return;
        }

        var currentIsProtected = state.LastAutosavedGeneration == document.RevisionGeneration
            && state.LastAutosavedRevision == document.History.CurrentRevision;
        if (document.IsDirty && (state.NeedsAnotherAutosave || !currentIsProtected))
        {
            if (outcome.Skipped || result!.Succeeded)
                ScheduleAutosave(document, state);
            else
                ScheduleAutosaveRetry(document, state);
        }
        else
        {
            state.AutosavePending = false;
        }

        RaiseStateChanged();
    }

    private DesignerRecoverySnapshot CaptureSnapshot(DesignerOpenDocument document, DocumentState state)
    {
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return DesignerRecoverySnapshot.CaptureUnsaved(
                document.Document,
                state.TemporaryRecoveryId,
                document.DisplayName,
                document.ProjectPath,
                document.History.CurrentRevision,
                document.RevisionGeneration,
                recoverySession,
                scheduler.UtcNow,
                state.KnownGeneratedHash);
        }

        return DesignerRecoverySnapshot.CaptureSaved(
            document.Document,
            document.Path,
            document.ProjectPath,
            document.History.CurrentRevision,
            document.RevisionGeneration,
            recoverySession,
            scheduler.UtcNow,
            state.SourceLastWriteUtc,
            state.KnownDesignHash,
            state.KnownGeneratedHash);
    }

    private void RecordSuccessfulAutosave(
        DocumentState state,
        DesignerRecoverySnapshot snapshot,
        string artifactPath,
        long autosaveSequence)
    {
        lock (state.ArtifactGate)
            state.CompletedArtifactPaths.Add(artifactPath);
        RegisterRecoveryArtifact(snapshot.Metadata.DocumentIdentity, artifactPath);
        if (autosaveSequence < state.LastCompletedAutosaveSequence)
            return;

        state.LastCompletedAutosaveSequence = autosaveSequence;
        state.LastAutosavedGeneration = snapshot.Metadata.RevisionGeneration;
        state.LastAutosavedRevision = snapshot.Metadata.DirtyRevision;
        state.LastAutosaveUtc = snapshot.Metadata.TimestampUtc;
        state.LastAutosaveError = null;
        state.ConsecutiveAutosaveFailures = 0;
        state.CurrentArtifactPath = artifactPath;
        state.AutosavePending = false;
        if (state.Notice?.Kind == DesignerPersistenceNoticeKind.AutosaveFailed)
            state.Notice = null;
        session.LogDiagnostic(
            $"Recovery snapshot saved for {state.Document.DisplayName} at generation " +
            $"{snapshot.Metadata.RevisionGeneration}, revision {snapshot.Metadata.DirtyRevision}: {artifactPath}");
    }

    private void RecordAutosaveFailure(DocumentState state, string error)
    {
        var shouldReport = state.ConsecutiveAutosaveFailures == 0
            || !string.Equals(state.LastAutosaveError, error, StringComparison.Ordinal);
        state.ConsecutiveAutosaveFailures++;
        state.LastAutosaveError = error;
        state.AutosavePending = false;
        state.Notice ??= new DesignerPersistenceNotification(
            Guid.NewGuid(),
            DesignerPersistenceNoticeKind.AutosaveFailed,
            "Recovery copy could not be saved",
            "Editing can continue, but the latest changes are not protected by a recovery copy. " + error,
            state.Document.DisplayName,
            DesignerPersistenceActions.Dismiss);

        if (shouldReport)
            session.Log($"Recovery autosave failed for {state.Document.DisplayName}: {error}");
        else
            session.LogDiagnostic($"Repeated recovery autosave failure for {state.Document.DisplayName}: {error}");
        RaiseStateChanged();
    }

    private void ScheduleAutosaveRetry(DesignerOpenDocument document, DocumentState state)
    {
        if (IsDisposed || state.Closed || !document.IsDirty)
            return;

        state.DebounceHandle?.Dispose();
        var delay = options.AutoSaveDebounceDelay > AutosaveFailureRetry
            ? options.AutoSaveDebounceDelay
            : AutosaveFailureRetry;
        state.DebounceHandle = scheduler.Schedule(
            delay,
            () => PostToUi(() => StartAutosave(document, state)));
        state.AutosavePending = true;
    }

    private void CancelAutosaveSchedules(DocumentState state)
    {
        state.DebounceHandle?.Dispose();
        state.DebounceHandle = null;
        state.MaximumIntervalHandle?.Dispose();
        state.MaximumIntervalHandle = null;
        state.AutosavePending = false;
        state.NeedsAnotherAutosave = false;
    }

    private void RecreateWatcher(DesignerOpenDocument document, DocumentState state)
    {
        if (state.ChangeSource is not null)
        {
            state.ChangeSource.Changed -= ChangeSource_Changed;
            state.ChangeSource.Dispose();
            state.ChangeSource = null;
        }

        CancelExternalSchedule(state);
        if (string.IsNullOrWhiteSpace(document.Path))
            return;

        try
        {
            state.ChangeSource = fileChangeSourceFactory.Create(document.Path);
            state.ChangeSource.Changed += ChangeSource_Changed;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            session.Log($"External-change monitoring could not start for {document.DisplayName}: {exception.Message}");
        }
    }

    private void ChangeSource_Changed(object? sender, DesignerFileChangeEventArgs e)
    {
        // FileSystemWatcher callbacks can run on arbitrary threads. Do not inspect or mutate the
        // Designer model here; post only the immutable raw event to the session dispatcher.
        PostToUi(() => HandleRawFileChange(sender, e));
    }

    private void HandleRawFileChange(object? sender, DesignerFileChangeEventArgs e)
    {
        if (IsDisposed)
            return;

        var state = documentStates.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.ChangeSource, sender));
        if (state is null || state.Closed || state.ChangeSource is null)
            return;

        if (PathsEqual(e.Path, state.ChangeSource.DesignDocumentPath)
            || (!string.IsNullOrWhiteSpace(e.OldPath) && PathsEqual(e.OldPath, state.ChangeSource.DesignDocumentPath)))
        {
            state.PendingDesignChange = true;
        }
        if (PathsEqual(e.Path, state.ChangeSource.GeneratedCodePath)
            || (!string.IsNullOrWhiteSpace(e.OldPath) && PathsEqual(e.OldPath, state.ChangeSource.GeneratedCodePath)))
        {
            state.PendingGeneratedChange = true;
        }

        ScheduleExternalObservation(state.Document, state, ExternalChangeDebounce);
    }

    private void ScheduleExternalObservation(
        DesignerOpenDocument document,
        DocumentState state,
        TimeSpan delay)
    {
        state.ExternalHandle?.Dispose();
        state.ExternalHandle = scheduler.Schedule(
            delay,
            () => PostToUi(() => StartExternalObservation(document, state)));
        RaiseStateChanged();
    }

    private void StartExternalObservation(DesignerOpenDocument document, DocumentState state)
    {
        state.ExternalHandle?.Dispose();
        state.ExternalHandle = null;
        if (IsDisposed || state.Closed || string.IsNullOrWhiteSpace(document.Path)
            || !documentStates.TryGetValue(document, out var current) || !ReferenceEquals(current, state))
        {
            return;
        }

        if (session.Transactions.HasActiveTransaction || state.IsNormalSaveInProgress || state.IsExternalObservationInFlight)
        {
            ScheduleExternalObservation(document, state, ExternalChangeRetry);
            return;
        }

        var inspectDesign = state.PendingDesignChange;
        var inspectGenerated = state.PendingGeneratedChange;
        state.PendingDesignChange = false;
        state.PendingGeneratedChange = false;
        if (!inspectDesign && !inspectGenerated)
            return;

        var generation = ++state.ExternalObservationGeneration;
        state.IsExternalObservationInFlight = true;
        var designPath = document.Path;
        var generatedPath = files.GetGeneratedCodePath(document.Document, designPath);
        var rootKind = document.Document.RootKind;
        var task = backgroundWorkQueue.Run(() => ObserveExternalFiles(
            inspectDesign ? designPath : null,
            inspectGenerated ? generatedPath : null,
            rootKind));
        TrackBackgroundTask(task);
        _ = task.ContinueWith(
            completed => PostToUi(() => CompleteExternalObservation(
                document,
                state,
                generation,
                inspectDesign,
                inspectGenerated,
                completed)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CompleteExternalObservation(
        DesignerOpenDocument document,
        DocumentState state,
        long generation,
        bool inspectDesign,
        bool inspectGenerated,
        Task<ExternalObservation> task)
    {
        state.IsExternalObservationInFlight = false;
        if (IsDisposed || state.Closed || generation != state.ExternalObservationGeneration
            || !documentStates.TryGetValue(document, out var current) || !ReferenceEquals(current, state))
        {
            return;
        }

        ExternalObservation observation;
        try
        {
            observation = task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            observation = new ExternalObservation(null, null, exception.GetBaseException().Message);
        }

        if (session.Transactions.HasActiveTransaction || state.IsNormalSaveInProgress)
        {
            // The model may have changed after the worker captured its files. Do not apply that
            // stale result inside a transaction/save; preserve the exact request and read again
            // once the session returns to a stable state.
            state.ExternalObservationGeneration++;
            state.PendingDesignChange |= inspectDesign || observation.Design is not null;
            state.PendingGeneratedChange |= inspectGenerated || observation.Generated is not null;
            ScheduleExternalObservation(document, state, ExternalChangeRetry);
            return;
        }

        if (!string.IsNullOrWhiteSpace(observation.Error))
        {
            session.LogDiagnostic($"External-change verification deferred for {document.DisplayName}: {observation.Error}");
            // A sharing violation can happen before either requested file is opened. Rechecking
            // both exact targets prevents a transient replace sequence from losing its only event.
            state.PendingDesignChange = true;
            state.PendingGeneratedChange = true;
            ScheduleExternalObservation(document, state, ExternalChangeRetry);
            return;
        }

        var designDisposition = observation.Design is { } design
            ? ProcessExternalDesign(document, state, design)
            : ExternalDesignDisposition.NoChange;

        // A changed .mfdesign is authoritative for the model. Coalesced generated-code events are
        // recorded as the new observed baseline and are not reverse-imported over that reload.
        if (observation.Generated is { } generated)
        {
            if (designDisposition == ExternalDesignDisposition.NoChange)
                ProcessExternalGeneratedCode(document, state, generated);
            else if (designDisposition == ExternalDesignDisposition.Accepted)
            {
                state.KnownGeneratedHash = generated.Exists ? generated.Hash : null;
                state.ExpectedGeneratedHash = null;
            }
            else
            {
                state.DeferredGeneratedObservation = generated;
            }
        }

        if (state.PendingDesignChange || state.PendingGeneratedChange)
            ScheduleExternalObservation(document, state, ExternalChangeDebounce);
        RaiseStateChanged();
    }

    private ExternalDesignDisposition ProcessExternalDesign(
        DesignerOpenDocument document,
        DocumentState state,
        ObservedFile observed)
    {
        if (!observed.Exists)
        {
            if (state.KnownDesignHash is null && state.FileMissing)
                return ExternalDesignDisposition.NoChange;

            state.ExpectedDesignHash = null;
            state.FileMissing = true;
            state.PendingDiskDesignHash = null;
            state.PendingDiskDocument = null;
            state.PendingDesignMissing = true;
            state.Notice = new DesignerPersistenceNotification(
                Guid.NewGuid(),
                DesignerPersistenceNoticeKind.FileMissing,
                "Designer file is missing",
                "The .mfdesign file was deleted or renamed outside the Designer. The in-memory document is still available.",
                document.DisplayName,
                DesignerPersistenceActions.Keep | DesignerPersistenceActions.SaveAs);
            return ExternalDesignDisposition.Conflict;
        }

        var wasMissing = state.FileMissing || state.PendingDesignMissing;
        state.FileMissing = false;
        state.PendingDesignMissing = false;
        if (wasMissing && state.Notice?.Kind == DesignerPersistenceNoticeKind.FileMissing)
            state.Notice = null;
        if (string.Equals(observed.Hash, state.ExpectedDesignHash, StringComparison.OrdinalIgnoreCase))
        {
            state.KnownDesignHash = observed.Hash;
            state.ExpectedDesignHash = null;
            state.SourceLastWriteUtc = observed.LastWriteUtc;
            return ExternalDesignDisposition.NoChange;
        }
        state.ExpectedDesignHash = null;
        if (string.Equals(observed.Hash, state.KnownDesignHash, StringComparison.OrdinalIgnoreCase))
            return ExternalDesignDisposition.NoChange;

        if (observed.DesignDocument is null)
        {
            state.Notice = new DesignerPersistenceNotification(
                Guid.NewGuid(),
                DesignerPersistenceNoticeKind.ExternalDesignConflict,
                "External design file is invalid",
                "The changed .mfdesign file could not be parsed. The current Designer document was preserved. " + observed.Error,
                document.DisplayName,
                DesignerPersistenceActions.Keep | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare);
            state.PendingDiskDesignHash = observed.Hash;
            return ExternalDesignDisposition.Conflict;
        }

        if (!document.IsDirty)
        {
            try
            {
                session.ReloadDocumentBaseline(document, observed.DesignDocument, markDirty: false, "Reloaded external .mfdesign changes.");
            }
            catch (Exception exception)
            {
                state.PendingDiskDesignHash = observed.Hash;
                state.PendingDiskDocument = observed.DesignDocument;
                state.PendingDiskLastWriteUtc = observed.LastWriteUtc;
                state.Notice = new DesignerPersistenceNotification(
                    Guid.NewGuid(),
                    DesignerPersistenceNoticeKind.ExternalDesignConflict,
                    "External design reload failed",
                    "The changed .mfdesign file was verified, but the Designer could not apply it. The current model and history were preserved. " + exception.Message,
                    document.DisplayName,
                    DesignerPersistenceActions.Keep | DesignerPersistenceActions.Reload | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare,
                    DiskTimestampUtc: observed.LastWriteUtc);
                session.LogDiagnostic($"Could not reload external .mfdesign changes for {document.DisplayName}: {exception.Message}");
                return ExternalDesignDisposition.Conflict;
            }
            state.KnownDesignHash = observed.Hash;
            state.ExpectedDesignHash = null;
            state.SourceLastWriteUtc = observed.LastWriteUtc;
            state.PendingDiskDesignHash = null;
            state.PendingDiskDocument = null;
            state.Notice = null;
            session.Log($"Reloaded externally changed {document.DisplayName}.");
            return ExternalDesignDisposition.Accepted;
        }

        state.PendingDiskDesignHash = observed.Hash;
        state.PendingDiskDocument = observed.DesignDocument;
        state.PendingDiskLastWriteUtc = observed.LastWriteUtc;
        state.Notice = new DesignerPersistenceNotification(
            Guid.NewGuid(),
            DesignerPersistenceNoticeKind.ExternalDesignConflict,
            "Design file changed outside the Designer",
            "The Designer also has unsaved changes. Choose Keep, Reload, or Save As; no version was overwritten.",
            document.DisplayName,
            DesignerPersistenceActions.Keep | DesignerPersistenceActions.Reload | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare,
            DiskTimestampUtc: observed.LastWriteUtc);
        return ExternalDesignDisposition.Conflict;
    }

    private void ProcessExternalGeneratedCode(
        DesignerOpenDocument document,
        DocumentState state,
        ObservedFile observed)
    {
        if (!observed.Exists)
        {
            if (state.KnownGeneratedHash is null)
                return;

            state.ExpectedGeneratedHash = null;
            state.PendingDiskGeneratedHash = null;
            state.PendingGeneratedMissing = true;
            state.Notice = new DesignerPersistenceNotification(
                Guid.NewGuid(),
                DesignerPersistenceNoticeKind.FileMissing,
                "Generated Designer file is missing",
                "The tracked .Designer.cs file was deleted or renamed outside the Designer. The design model was preserved.",
                document.DisplayName,
                DesignerPersistenceActions.Keep | DesignerPersistenceActions.SaveAs);
            return;
        }

        var generatedWasMissing = state.PendingGeneratedMissing;
        state.PendingGeneratedMissing = false;
        if (generatedWasMissing && state.Notice?.Kind == DesignerPersistenceNoticeKind.FileMissing)
            state.Notice = null;

        if (string.Equals(observed.Hash, state.ExpectedGeneratedHash, StringComparison.OrdinalIgnoreCase))
        {
            state.KnownGeneratedHash = observed.Hash;
            state.ExpectedGeneratedHash = null;
            return;
        }
        state.ExpectedGeneratedHash = null;
        if (string.Equals(observed.Hash, state.KnownGeneratedHash, StringComparison.OrdinalIgnoreCase))
            return;

        state.PendingDiskGeneratedHash = observed.Hash;
        state.PendingGeneratedDocument = observed.ParsedGeneratedDocument;
        state.PendingDiskLastWriteUtc = observed.LastWriteUtc;
        if (observed.ParsedGeneratedDocument is null)
        {
            state.Notice = new DesignerPersistenceNotification(
                Guid.NewGuid(),
                DesignerPersistenceNoticeKind.ExternalGeneratedCodeInvalid,
                "Generated code could not be imported",
                "The external .Designer.cs edit was not applied, and the current Designer model was preserved. " + observed.Error,
                document.DisplayName,
                DesignerPersistenceActions.Keep | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare,
                DiskTimestampUtc: observed.LastWriteUtc);
            return;
        }

        if (!document.IsDirty && ReferenceEquals(session.ActiveOpenDocument, document))
        {
            try
            {
                session.ReplaceDocument(observed.ParsedGeneratedDocument, "Import external Designer code");
            }
            catch (Exception exception)
            {
                state.Notice = new DesignerPersistenceNotification(
                    Guid.NewGuid(),
                    DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict,
                    "Generated code import failed",
                    "The changed .Designer.cs file was verified, but its model could not be applied. The current Designer model and history were preserved. " + exception.Message,
                    document.DisplayName,
                    DesignerPersistenceActions.Keep | DesignerPersistenceActions.Reload | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare,
                    DiskTimestampUtc: observed.LastWriteUtc);
                session.LogDiagnostic($"Could not import external generated code for {document.DisplayName}: {exception.Message}");
                return;
            }
            state.KnownGeneratedHash = observed.Hash;
            state.PendingDiskGeneratedHash = null;
            state.PendingGeneratedDocument = null;
            state.Notice = null;
            session.Log($"Imported externally changed {IOPath.GetFileName(observed.Path)} as one Designer transaction.");
            return;
        }

        state.Notice = new DesignerPersistenceNotification(
            Guid.NewGuid(),
            DesignerPersistenceNoticeKind.ExternalGeneratedCodeConflict,
            "Generated code changed outside the Designer",
            document.IsDirty
                ? "The Designer also has unsaved changes. Choose Keep, Reload, or Save As; no reverse import was performed."
                : "Activate this document, then choose Reload to import the generated code as one transaction.",
            document.DisplayName,
            DesignerPersistenceActions.Keep | DesignerPersistenceActions.Reload | DesignerPersistenceActions.SaveAs | DesignerPersistenceActions.Compare,
            DiskTimestampUtc: observed.LastWriteUtc);
    }

    private bool ApplyExternalAction(
        DocumentState state,
        DesignerPersistenceActions action,
        string? saveAsPath,
        out string? error)
    {
        error = null;
        var document = state.Document;
        switch (action)
        {
            case DesignerPersistenceActions.Keep:
                var deferredGenerated = state.DeferredGeneratedObservation;
                state.ExpectedDesignHash = null;
                state.ExpectedGeneratedHash = null;
                state.KnownDesignHash = state.PendingDesignMissing
                    ? null
                    : state.PendingDiskDesignHash ?? state.KnownDesignHash;
                state.KnownGeneratedHash = state.PendingGeneratedMissing
                    ? null
                    : state.PendingDiskGeneratedHash ?? state.KnownGeneratedHash;
                ClearPendingExternalState(state);
                if (deferredGenerated is not null)
                    ProcessExternalGeneratedCode(document, state, deferredGenerated);
                session.Log($"Kept the in-memory Designer version for {document.DisplayName}.");
                RaiseStateChanged();
                return true;

            case DesignerPersistenceActions.Reload:
                if (!EnsureRecoveryNow(document, out error))
                    return false;
                var deferredAfterReload = state.DeferredGeneratedObservation;
                var designWasReloaded = false;
                try
                {
                    if (state.PendingDiskDocument is { } diskDocument)
                    {
                        session.ReloadDocumentBaseline(document, diskDocument, markDirty: false, "Reloaded the disk version.");
                        state.KnownDesignHash = state.PendingDiskDesignHash;
                        state.SourceLastWriteUtc = state.PendingDiskLastWriteUtc;
                        designWasReloaded = true;
                    }
                    else if (state.PendingGeneratedDocument is { } generatedDocument
                        && ReferenceEquals(session.ActiveOpenDocument, document))
                    {
                        session.ReplaceDocument(generatedDocument, "Import external Designer code");
                        state.KnownGeneratedHash = state.PendingDiskGeneratedHash;
                    }
                    else
                    {
                        error = "The external file no longer contains a reloadable document, or its tab is not active.";
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    error = "The external version could not be applied; the current Designer model and recovery copy were preserved. " + exception.Message;
                    return false;
                }

                state.ExpectedDesignHash = null;
                state.ExpectedGeneratedHash = null;
                ClearPendingExternalState(state);
                if (deferredAfterReload is not null)
                {
                    if (designWasReloaded)
                    {
                        state.KnownGeneratedHash = deferredAfterReload.Exists ? deferredAfterReload.Hash : null;
                        state.ExpectedGeneratedHash = null;
                    }
                    else
                    {
                        ProcessExternalGeneratedCode(document, state, deferredAfterReload);
                    }
                }
                RaiseStateChanged();
                return true;

            case DesignerPersistenceActions.SaveAs:
                if (string.IsNullOrWhiteSpace(saveAsPath))
                {
                    error = "A Save As path is required.";
                    return false;
                }

                var result = SaveActiveDocument(saveAsPath);
                error = result.Error;
                return result.Succeeded;

            case DesignerPersistenceActions.Dismiss:
                state.Notice = null;
                RaiseStateChanged();
                return true;

            default:
                error = "The selected action is not valid for this external-change notification.";
                return false;
        }
    }

    private void DiscoverRecovery()
    {
        var discovery = recoveryStore.Discover();
        if (!string.IsNullOrWhiteSpace(discovery.Error))
            session.Log($"Recovery discovery failed: {discovery.Error}");
        if (discovery.WasTruncated)
            session.Log("Recovery discovery reached its bounded entry, file, or byte limit; remaining entries were not scanned.");

        foreach (var candidate in discovery.Candidates)
        {
            if (candidate.Status is DesignerRecoveryCandidateStatus.Corrupt or DesignerRecoveryCandidateStatus.Unsupported)
            {
                var quarantine = recoveryStore.Quarantine(candidate.ArtifactPath, scheduler.UtcNow);
                var kind = candidate.Status == DesignerRecoveryCandidateStatus.Corrupt
                    ? DesignerPersistenceNoticeKind.CorruptRecovery
                    : DesignerPersistenceNoticeKind.UnsupportedRecovery;
                var title = candidate.Status == DesignerRecoveryCandidateStatus.Corrupt
                    ? "A corrupt recovery copy was isolated"
                    : "An unsupported recovery copy was isolated";
                informationNotices.Add(new InformationNotice(new DesignerPersistenceNotification(
                    Guid.NewGuid(),
                    kind,
                    title,
                    (candidate.Error ?? "The recovery artifact is not usable.") +
                    (quarantine.Succeeded ? " It was moved to quarantine." : " It could not be moved to quarantine."),
                    IOPath.GetFileName(candidate.ArtifactPath),
                    DesignerPersistenceActions.Dismiss)));
                continue;
            }

            if (candidate.Envelope?.Metadata is not { } metadata || candidate.Document is null)
                continue;

            var entry = EvaluateRecovery(candidate);
            if (entry.IsObsolete)
            {
                recoveryStore.Delete(candidate.ArtifactPath);
                continue;
            }

            RegisterRecoveryArtifact(metadata.DocumentIdentity, candidate.ArtifactPath);
            recoveryEntries.Add(entry);
        }

        // Present only the newest candidate for each stable document identity. Older valid copies
        // remain tracked for identity-scoped resolution and bounded retention cleanup, but do not
        // produce duplicate prompts.
        var newest = recoveryEntries
            .GroupBy(entry => entry.Candidate.Envelope!.Metadata!.DocumentIdentity, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(entry => entry.Candidate.Envelope!.Metadata!.TimestampUtc).First())
            .ToHashSet();
        recoveryEntries.RemoveAll(entry => !newest.Contains(entry));
        recoveryEntries.Sort((left, right) => right.Candidate.Envelope!.Metadata!.TimestampUtc.CompareTo(
            left.Candidate.Envelope!.Metadata!.TimestampUtc));

        var retention = new DesignerRecoveryRetentionPolicy(
            options.RecoveryRetention,
            Math.Max(1, options.MaximumRecoveryFiles),
            temporaryFileMaxAge: TimeSpan.FromDays(1),
            quarantineMaxAge: options.RecoveryRetention);
        // Attach recovery for documents already open in the host before applying retention. Also
        // protect the one host-supplied startup target while the shell is being constructed, since
        // production hosts load that document immediately after the coordinator is created.
        // All other inactive entries remain subject to the configured age and count limits.
        foreach (var state in documentStates.Values)
            AttachMatchingRecovery(state.Document, state);
        var protectedArtifacts = documentStates.Values
            .Select(state => state.Recovery?.Candidate.ArtifactPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
        if (FindStartupRecoveryArtifactPath() is { } startupArtifactPath)
            protectedArtifacts.Add(startupArtifactPath);
        var cleanup = recoveryStore.Cleanup(retention, protectedArtifacts, scheduler.UtcNow);
        if (cleanup.DeletedPaths.Count > 0)
        {
            var deleted = cleanup.DeletedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            recoveryEntries.RemoveAll(entry => deleted.Contains(entry.Candidate.ArtifactPath));
            foreach (var deletedPath in cleanup.DeletedPaths)
                UnregisterRecoveryArtifact(deletedPath);
        }
        foreach (var cleanupError in cleanup.Errors)
            session.LogDiagnostic("Recovery cleanup: " + cleanupError);

        RaiseStateChanged();
    }

    private string? FindStartupRecoveryArtifactPath()
    {
        var documentPath = session.CurrentDocumentPath;
        if (session.ActiveOpenDocument is not null || string.IsNullOrWhiteSpace(documentPath))
        {
            return null;
        }

        try
        {
            var identity = DesignerRecoveryDocumentIdentity.ForSavedDocument(
                documentPath,
                session.CurrentProjectPath);
            return recoveryEntries.FirstOrDefault(entry => string.Equals(
                entry.Candidate.Envelope!.Metadata!.DocumentIdentity,
                identity.Value,
                StringComparison.Ordinal))?.Candidate.ArtifactPath;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            session.LogDiagnostic($"Could not protect startup recovery from retention cleanup: {exception.Message}");
            return null;
        }
    }

    private RecoveryEntry EvaluateRecovery(DesignerRecoveryCandidate candidate)
    {
        var metadata = candidate.Envelope!.Metadata!;
        if (metadata.IsUnsaved || string.IsNullOrWhiteSpace(metadata.DocumentPath))
            return new RecoveryEntry(candidate, Conflict: false, IsObsolete: false, DiskHash: null, DiskLastWriteUtc: null);

        string? diskHash = null;
        DateTimeOffset? diskWriteUtc = null;
        if (File.Exists(metadata.DocumentPath))
        {
            try
            {
                diskHash = DesignerFileHash.ComputeFileSha256(metadata.DocumentPath);
                diskWriteUtc = TryGetLastWriteUtc(metadata.DocumentPath);
            }
            catch (Exception exception) when (IsPersistenceException(exception))
            {
                session.LogDiagnostic($"Could not fingerprint recovery source '{metadata.DocumentPath}': {exception.Message}");
            }
        }

        var payloadHash = DesignerFileHash.ComputeUtf8Sha256(candidate.Envelope.SerializedDesignDocument);
        var obsolete = string.Equals(diskHash, payloadHash, StringComparison.OrdinalIgnoreCase);
        var conflict = diskHash is null
            || metadata.SourceFileHashSha256 is null
            || !string.Equals(diskHash, metadata.SourceFileHashSha256, StringComparison.OrdinalIgnoreCase);
        return new RecoveryEntry(candidate, conflict, obsolete, diskHash, diskWriteUtc);
    }

    private void AttachMatchingRecovery(DesignerOpenDocument document, DocumentState state)
    {
        if (string.IsNullOrWhiteSpace(document.Path))
            return;

        DesignerRecoveryDocumentIdentity identity;
        try
        {
            identity = DesignerRecoveryDocumentIdentity.ForSavedDocument(document.Path, document.ProjectPath);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            session.LogDiagnostic($"Could not identify recovery for {document.DisplayName}: {exception.Message}");
            return;
        }

        var entry = recoveryEntries.FirstOrDefault(candidate => string.Equals(
            candidate.Candidate.Envelope!.Metadata!.DocumentIdentity,
            identity.Value,
            StringComparison.Ordinal));
        if (entry is null)
            return;

        recoveryEntries.Remove(entry);
        state.Recovery = entry;
    }

    private bool ApplyRecoveryAction(
        RecoveryEntry recovery,
        DesignerPersistenceActions action,
        string? saveAsPath,
        out string? error)
    {
        error = null;
        var candidate = recovery.Candidate;
        var metadata = candidate.Envelope!.Metadata!;
        switch (action)
        {
            case DesignerPersistenceActions.Restore:
            {
                var targetState = documentStates.Values.FirstOrDefault(state => ReferenceEquals(state.Recovery, recovery));
                if (targetState is not null
                    && targetState.Document.IsDirty
                    && !EnsureRecoveryNow(targetState.Document, out error))
                {
                    return false;
                }

                try
                {
                    if (targetState is not null)
                    {
                        session.ReloadDocumentBaseline(
                            targetState.Document,
                            CloneDocument(candidate.Document!),
                            markDirty: true,
                            "Restored Designer recovery copy.");
                        targetState.TemporaryRecoveryId = metadata.TemporaryDocumentId ?? targetState.TemporaryRecoveryId;
                        targetState.CurrentArtifactPath = candidate.ArtifactPath;
                        lock (targetState.ArtifactGate)
                            targetState.CompletedArtifactPaths.Add(candidate.ArtifactPath);
                        targetState.Recovery = null;
                        ScheduleAutosave(targetState.Document, targetState);
                    }
                    else
                    {
                        // A path from an artifact is never adopted as an automatic save destination.
                        // Startup restore opens a dirty, unsaved copy and therefore requires Save As.
                        session.OpenDocument(CloneDocument(candidate.Document!), path: null, markDirty: true);
                        var opened = session.ActiveOpenDocument!;
                        var openedState = documentStates[opened];
                        openedState.TemporaryRecoveryId = metadata.TemporaryDocumentId ?? openedState.TemporaryRecoveryId;
                        if (!metadata.IsUnsaved)
                            openedState.OriginRecoveryIdentity = metadata.DocumentIdentity;
                        openedState.CurrentArtifactPath = candidate.ArtifactPath;
                        lock (openedState.ArtifactGate)
                            openedState.CompletedArtifactPaths.Add(candidate.ArtifactPath);
                    }
                }
                catch (Exception exception)
                {
                    error = "The recovery copy could not be applied; the current document and artifact were preserved. " + exception.Message;
                    return false;
                }

                RemoveRecoveryEntry(recovery);
                session.Log($"Restored recovery copy for {metadata.SuggestedName}; the document remains modified.");
                RaiseStateChanged();
                return true;
            }

            case DesignerPersistenceActions.Discard:
            {
                var cleanupErrors = DeleteRecoveryArtifactsForIdentity(metadata.DocumentIdentity);
                if (cleanupErrors.Count > 0)
                {
                    error = "The recovery copies could not all be deleted: " + string.Join("; ", cleanupErrors);
                    return false;
                }

                RemoveRecoveryEntriesForIdentity(metadata.DocumentIdentity);
                RaiseStateChanged();
                return true;
            }

            case DesignerPersistenceActions.Keep:
                foreach (var state in documentStates.Values.Where(state => ReferenceEquals(state.Recovery, recovery)))
                    state.Recovery = null;
                RemoveRecoveryEntry(recovery);
                RaiseStateChanged();
                return true;

            case DesignerPersistenceActions.OpenDisk:
            {
                if (string.IsNullOrWhiteSpace(metadata.DocumentPath) || !File.Exists(metadata.DocumentPath))
                {
                    error = "The canonical disk document is missing.";
                    return false;
                }

                try
                {
                    var diskDocument = files.LoadDesignDocument(metadata.DocumentPath);
                    var targetState = documentStates.Values.FirstOrDefault(state => ReferenceEquals(state.Recovery, recovery));
                    if (targetState is not null
                        && targetState.Document.IsDirty
                        && !EnsureRecoveryNow(targetState.Document, out error))
                    {
                        return false;
                    }
                    if (targetState is not null)
                    {
                        session.ReloadDocumentBaseline(targetState.Document, diskDocument, markDirty: false, "Opened disk version.");
                        targetState.Recovery = null;
                    }
                    else
                    {
                        session.OpenDocument(diskDocument, metadata.DocumentPath, markDirty: false);
                    }
                    RemoveRecoveryEntry(recovery);
                    RaiseStateChanged();
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            case DesignerPersistenceActions.SaveAs:
                if (string.IsNullOrWhiteSpace(saveAsPath))
                {
                    error = "A Save As path is required.";
                    return false;
                }
                return SaveRecoveryAs(recovery, saveAsPath, out error);

            default:
                error = "The selected action is not valid for this recovery copy.";
                return false;
        }
    }

    private bool SaveRecoveryAs(RecoveryEntry recovery, string path, out string? error)
    {
        error = null;
        try
        {
            var document = CloneDocument(recovery.Candidate.Document!);
            var normalizedPath = DesignerDocumentPath.NormalizeDesignPath(path)
                ?? throw new InvalidOperationException("The Save As path could not be normalized.");
            if (session.OpenDocuments.Any(open => PathsEqual(open.Path, normalizedPath)))
            {
                error = $"The Designer document '{normalizedPath}' is already open.";
                return false;
            }
            DesignerGenerationFileResult? preparedCode = options.AutoGenerateDesignerCodeOnSave
                ? files.PrepareDesignerCode(document, normalizedPath)
                : null;
            if (preparedCode is { Succeeded: false })
            {
                error = "Generation failed: " + string.Join("; ", preparedCode.Errors);
                return false;
            }

            files.SaveDesignDocument(document, normalizedPath);
            if (preparedCode is not null)
                DesignerAtomicFileWriter.WriteUtf8(preparedCode.Path, preparedCode.Code);

            var identity = recovery.Candidate.Envelope!.Metadata!.DocumentIdentity;
            foreach (var cleanupError in DeleteRecoveryArtifactsForIdentity(identity))
                session.LogDiagnostic("Saved recovery copy but could not remove an obsolete artifact: " + cleanupError);
            RemoveRecoveryEntriesForIdentity(identity);
            session.OpenDocument(document, normalizedPath, markDirty: false);
            session.Log($"Saved recovered document as {normalizedPath}.");
            RaiseStateChanged();
            return true;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            error = exception.Message;
            return false;
        }
    }

    private DesignerPersistenceNotification CreateRecoveryNotification(RecoveryEntry recovery)
    {
        var metadata = recovery.Candidate.Envelope!.Metadata!;
        var sourceMissing = !metadata.IsUnsaved
            && !string.IsNullOrWhiteSpace(metadata.DocumentPath)
            && recovery.DiskHash is null
            && !File.Exists(metadata.DocumentPath);
        var actions = DesignerPersistenceActions.Restore
            | DesignerPersistenceActions.Discard
            | DesignerPersistenceActions.Keep
            | DesignerPersistenceActions.SaveAs
            | DesignerPersistenceActions.Compare;
        if (!metadata.IsUnsaved && File.Exists(metadata.DocumentPath))
            actions |= DesignerPersistenceActions.OpenDisk;

        return new DesignerPersistenceNotification(
            recovery.NotificationId,
            recovery.Conflict ? DesignerPersistenceNoticeKind.RecoveryConflict : DesignerPersistenceNoticeKind.RecoveryAvailable,
            sourceMissing
                ? "Recovery source is missing"
                : recovery.Conflict ? "Recovery copy conflicts with disk" : "Unsaved Designer work is available",
            sourceMissing
                ? "The original .mfdesign path no longer exists. Restore the recovery as an unsaved document or use Save As; no renamed path is guessed automatically."
                : recovery.Conflict
                ? "Both the recovery copy and the canonical file changed. Choose which version to open, or save the recovery copy elsewhere."
                : "A newer recovery copy contains unsaved Designer work. Restore it, preserve it for later, or discard it.",
            metadata.SuggestedName,
            actions,
            metadata.TimestampUtc,
            recovery.DiskLastWriteUtc);
    }

    private RecoveryEntry? FindRecoveryEntry(Guid notificationId)
        => documentStates.Values.Select(state => state.Recovery)
            .Concat(recoveryEntries)
            .FirstOrDefault(entry => entry?.NotificationId == notificationId);

    private void RemoveRecoveryEntry(RecoveryEntry recovery)
        => recoveryEntries.Remove(recovery);

    private void RegisterRecoveryArtifact(string identity, string artifactPath)
    {
        if (!recoveryArtifactsByIdentity.TryGetValue(identity, out var paths))
        {
            paths = new HashSet<string>(OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            recoveryArtifactsByIdentity.Add(identity, paths);
        }

        paths.Add(artifactPath);
    }

    private void UnregisterRecoveryArtifact(string artifactPath)
    {
        foreach (var identity in recoveryArtifactsByIdentity.Keys.ToArray())
        {
            var paths = recoveryArtifactsByIdentity[identity];
            paths.Remove(artifactPath);
            if (paths.Count == 0)
                recoveryArtifactsByIdentity.Remove(identity);
        }
    }

    private string[] GetRecoveryArtifactPaths(string identity)
    {
        var paths = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        if (recoveryArtifactsByIdentity.TryGetValue(identity, out var registered))
            paths.UnionWith(registered);
        paths.UnionWith(recoveryEntries
            .Where(entry => HasRecoveryIdentity(entry, identity))
            .Select(entry => entry.Candidate.ArtifactPath));
        paths.UnionWith(documentStates.Values
            .Select(state => state.Recovery)
            .Where(entry => HasRecoveryIdentity(entry, identity))
            .Select(entry => entry!.Candidate.ArtifactPath));
        return paths.ToArray();
    }

    private IReadOnlyList<string> DeleteRecoveryArtifactsForIdentity(string identity)
    {
        var errors = new List<string>();
        foreach (var artifactPath in GetRecoveryArtifactPaths(identity))
        {
            var deletion = recoveryStore.Delete(artifactPath);
            if (deletion.Succeeded)
                UnregisterRecoveryArtifact(artifactPath);
            else
                errors.Add(deletion.Error ?? $"Recovery artifact '{artifactPath}' could not be deleted.");
        }

        return errors;
    }

    private void RemoveRecoveryEntriesForIdentity(string identity)
    {
        recoveryEntries.RemoveAll(entry => HasRecoveryIdentity(entry, identity));
        foreach (var state in documentStates.Values.Where(state => HasRecoveryIdentity(state.Recovery, identity)))
            state.Recovery = null;
    }

    private static bool HasRecoveryIdentity(RecoveryEntry? entry, string identity)
        => entry is not null && string.Equals(
            entry.Candidate.Envelope!.Metadata!.DocumentIdentity,
            identity,
            StringComparison.Ordinal);

    private DesignerRecoveryDocumentIdentity GetIdentity(DesignerOpenDocument document, DocumentState state)
        => string.IsNullOrWhiteSpace(document.Path)
            ? DesignerRecoveryDocumentIdentity.ForUnsavedDocument(state.TemporaryRecoveryId)
            : DesignerRecoveryDocumentIdentity.ForSavedDocument(document.Path, document.ProjectPath);

    private void DeleteObsoleteArtifacts(DocumentState state)
    {
        string[] paths;
        lock (state.ArtifactGate)
        {
            paths = state.CompletedArtifactPaths.ToArray();
            state.CompletedArtifactPaths.Clear();
        }
        foreach (var path in paths)
        {
            if (recoveryStore.Delete(path).Succeeded)
                UnregisterRecoveryArtifact(path);
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentArtifactPath))
        {
            if (recoveryStore.Delete(state.CurrentArtifactPath).Succeeded)
                UnregisterRecoveryArtifact(state.CurrentArtifactPath);
        }
        state.CurrentArtifactPath = null;
    }

    private void DeleteAllKnownArtifacts(DocumentState state)
        => DeleteObsoleteArtifacts(state);

    private static bool ShouldDiscardArtifacts(DocumentState state)
    {
        lock (state.ArtifactGate)
            return state.DiscardArtifactsOnCompletion;
    }

    private static void SetDiscardArtifacts(DocumentState state, bool discard)
    {
        lock (state.ArtifactGate)
            state.DiscardArtifactsOnCompletion = discard;
    }

    private bool TryDeleteArtifactImmediately(string artifactPath)
    {
        for (var attempt = 0; attempt < ImmediateArtifactDeleteAttemptLimit; attempt++)
        {
            if (recoveryStore.Delete(artifactPath).Succeeded)
                return true;
        }

        return false;
    }

    private bool IsSnapshotObsolete(DocumentState state, DesignerRecoverySnapshot snapshot)
    {
        if (state.SuccessfullySavedGeneration == snapshot.Metadata.RevisionGeneration
            && state.SuccessfullySavedRevision >= snapshot.Metadata.DirtyRevision)
        {
            return true;
        }

        // Undo can return to the saved revision while an older dirty snapshot is still being
        // written. That snapshot represents work the user explicitly reversed and must not be
        // offered after restart. A baseline reload increments RevisionGeneration, so this check
        // intentionally does not discard the pre-reload recovery copy.
        var document = state.Document;
        return !document.IsDirty
            && document.RevisionGeneration == snapshot.Metadata.RevisionGeneration
            && document.History.CurrentRevision == document.History.SavedRevision
            && snapshot.Metadata.DirtyRevision != document.History.SavedRevision;
    }

    private void CancelExternalSchedule(DocumentState state)
    {
        state.ExternalHandle?.Dispose();
        state.ExternalHandle = null;
        state.PendingDesignChange = false;
        state.PendingGeneratedChange = false;
        state.ExternalObservationGeneration++;
    }

    private static void ClearPendingExternalState(DocumentState state)
    {
        state.PendingDiskDesignHash = null;
        state.PendingDiskGeneratedHash = null;
        state.PendingDiskLastWriteUtc = null;
        state.PendingDiskDocument = null;
        state.PendingGeneratedDocument = null;
        state.DeferredGeneratedObservation = null;
        state.PendingDesignMissing = false;
        state.PendingGeneratedMissing = false;
        state.Notice = null;
    }

    private ExternalObservation ObserveExternalFiles(
        string? designPath,
        string? generatedPath,
        DesignRootKind rootKind)
    {
        ObservedFile? design = null;
        ObservedFile? generated = null;
        try
        {
            if (designPath is not null)
            {
                design = ObserveFile(designPath, parseDesign: true, rootKind);
                if (design.Error == UnstableFileMarker)
                    return new ExternalObservation(design, null, "The .mfdesign file is still changing.");
            }
            if (generatedPath is not null)
            {
                generated = ObserveFile(generatedPath, parseDesign: false, rootKind);
                if (generated.Error == UnstableFileMarker)
                    return new ExternalObservation(design, generated, "The generated-code file is still changing.");
            }
            return new ExternalObservation(design, generated, Error: null);
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            return new ExternalObservation(design, generated, exception.Message);
        }
    }

    private const string UnstableFileMarker = "__MFN_UNSTABLE_FILE__";

    private ObservedFile ObserveFile(string path, bool parseDesign, DesignRootKind rootKind)
    {
        if (!File.Exists(path))
            return new ObservedFile(path, Exists: false, Hash: null, LastWriteUtc: null, null, null, Error: null);

        var beforeHash = DesignerFileHash.ComputeFileSha256(path);
        var text = File.ReadAllText(path);
        var afterHash = DesignerFileHash.ComputeFileSha256(path);
        if (!string.Equals(beforeHash, afterHash, StringComparison.OrdinalIgnoreCase))
            return new ObservedFile(path, true, afterHash, TryGetLastWriteUtc(path), null, null, UnstableFileMarker);

        if (parseDesign)
        {
            try
            {
                var document = DesignDocumentSerializer.Default.Deserialize(text);
                return new ObservedFile(path, true, afterHash, TryGetLastWriteUtc(path), document, null, Error: null);
            }
            catch (Exception exception) when (IsPersistenceException(exception))
            {
                return new ObservedFile(path, true, afterHash, TryGetLastWriteUtc(path), null, null, exception.Message);
            }
        }

        var parse = files.ImportDesignerCodeText(text, new CSharpDesignerParseOptions { RootKind = rootKind });
        var parseError = parse.Success && parse.Document is not null
            ? null
            : string.Join("; ", parse.Diagnostics.Select(diagnostic => diagnostic.Message));
        return new ObservedFile(
            path,
            true,
            afterHash,
            TryGetLastWriteUtc(path),
            null,
            parse.Success ? parse.Document : null,
            parseError);
    }

    private string? TryComputeGeneratedHash(DesignerOpenDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Path))
            return null;
        try
        {
            return TryComputeFileHash(files.GetGeneratedCodePath(document.Document, document.Path));
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            session.LogDiagnostic($"Could not fingerprint generated code for {document.DisplayName}: {exception.Message}");
            return null;
        }
    }

    private static string? TryComputeFileHash(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? DesignerFileHash.ComputeFileSha256(path)
                : null;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            return null;
        }
    }

    private static DateTimeOffset? TryGetLastWriteUtc(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            return null;
        }
    }

    private static DesignDocument CloneDocument(DesignDocument document)
        => DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));

    private void ValidateOptions()
    {
        if (options.AutoSaveDebounceDelay < TimeSpan.Zero)
            session.Log("Negative Designer autosave debounce was treated as zero.");
        if (options.RecoveryRetention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.RecoveryRetention), "Recovery retention must be positive.");
    }

    private void TrackBackgroundTask(Task task)
    {
        lock (backgroundTasksGate)
            backgroundTasks.Add(task);
        _ = task.ContinueWith(
            completed =>
            {
                lock (backgroundTasksGate)
                    backgroundTasks.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void PostToUi(Action callback)
    {
        if (IsDisposed)
            return;

        try
        {
            dispatcher.Post(() =>
            {
                if (!IsDisposed)
                    callback();
            });
        }
        catch (Exception exception) when (IsDisposed
            && exception is ObjectDisposedException or InvalidOperationException)
        {
            // A timer or filesystem callback can win the final race with host disposal. Once the
            // coordinator is disposed there is no UI state left to update, so abandon the callback
            // instead of allowing an exception to escape on a ThreadPool thread.
        }
    }

    private void RaiseStateChanged()
    {
        if (!IsDisposed)
            StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsDisposed => Volatile.Read(ref disposeState) != 0;

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(IsDisposed, this);

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        try
        {
            return string.Equals(
                IOPath.GetFullPath(left),
                IOPath.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return false;
        }
    }

    private static bool IsPathException(Exception exception)
        => exception is ArgumentException or NotSupportedException or PathTooLongException;

    private static bool IsPersistenceException(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or System.Text.Json.JsonException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or System.Security.SecurityException;

    private enum ExternalDesignDisposition
    {
        NoChange,
        Accepted,
        Conflict
    }

    private sealed class DocumentState(DesignerOpenDocument document)
    {
        public DesignerOpenDocument Document { get; } = document;
        public Guid TemporaryRecoveryId { get; set; }
        public SemaphoreSlim WriteGate { get; } = new(1, 1);
        public object ArtifactGate { get; } = new();
        public HashSet<string> CompletedArtifactPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IDesignerScheduledHandle? DebounceHandle { get; set; }
        public IDesignerScheduledHandle? MaximumIntervalHandle { get; set; }
        public IDesignerScheduledHandle? ExternalHandle { get; set; }
        public IDesignerFileChangeSource? ChangeSource { get; set; }
        public bool AutosavePending { get; set; }
        public bool NeedsAnotherAutosave { get; set; }
        public bool IsRecoveryWriteInFlight { get; set; }
        public bool IsNormalSaveInProgress { get; set; }
        public bool IsExternalObservationInFlight { get; set; }
        public bool PendingDesignChange { get; set; }
        public bool PendingGeneratedChange { get; set; }
        public bool FileMissing { get; set; }
        public bool PendingDesignMissing { get; set; }
        public bool PendingGeneratedMissing { get; set; }
        public bool Closed { get; set; }
        public bool DiscardArtifactsOnCompletion { get; set; }
        public bool LastKnownDirty { get; set; }
        public long ExternalObservationGeneration { get; set; }
        public long LastAutosavedGeneration { get; set; } = -1;
        public long LastAutosavedRevision { get; set; } = -1;
        public long SuccessfullySavedGeneration { get; set; } = -1;
        public long SuccessfullySavedRevision { get; set; } = -1;
        public long NextAutosaveSequence;
        public long LastCompletedAutosaveSequence { get; set; } = -1;
        public int ConsecutiveAutosaveFailures { get; set; }
        public DateTimeOffset? LastAutosaveUtc { get; set; }
        public string? LastAutosaveError { get; set; }
        public string? CurrentArtifactPath { get; set; }
        public string? OriginRecoveryIdentity { get; set; }
        public string? KnownDesignHash { get; set; }
        public string? ExpectedDesignHash { get; set; }
        public string? KnownGeneratedHash { get; set; }
        public string? ExpectedGeneratedHash { get; set; }
        public DateTimeOffset? SourceLastWriteUtc { get; set; }
        public string? PendingDiskDesignHash { get; set; }
        public string? PendingDiskGeneratedHash { get; set; }
        public DateTimeOffset? PendingDiskLastWriteUtc { get; set; }
        public DesignDocument? PendingDiskDocument { get; set; }
        public DesignDocument? PendingGeneratedDocument { get; set; }
        public ObservedFile? DeferredGeneratedObservation { get; set; }
        public DesignerPersistenceNotification? Notice { get; set; }
        public RecoveryEntry? Recovery { get; set; }
    }

    private sealed record RecoveryEntry(
        DesignerRecoveryCandidate Candidate,
        bool Conflict,
        bool IsObsolete,
        string? DiskHash,
        DateTimeOffset? DiskLastWriteUtc)
    {
        public Guid NotificationId { get; } = Guid.NewGuid();
    }

    private sealed record InformationNotice(DesignerPersistenceNotification Notification);

    private sealed record AutosaveWriteOutcome(
        DesignerRecoveryWriteResult? Result,
        bool Skipped,
        bool DeletedAfterWrite);

    private sealed record ExternalObservation(
        ObservedFile? Design,
        ObservedFile? Generated,
        string? Error);

    private sealed record ObservedFile(
        string Path,
        bool Exists,
        string? Hash,
        DateTimeOffset? LastWriteUtc,
        DesignDocument? DesignDocument,
        DesignDocument? ParsedGeneratedDocument,
        string? Error);
}

internal readonly record struct DesignerPersistenceDiagnostics(
    DateTimeOffset? LastAutosaveUtc,
    string? LastAutosaveError,
    string? RecoveryArtifactPath,
    bool AutosavePending,
    bool AutosaveInProgress,
    long LastAutosavedGeneration,
    long LastAutosavedRevision,
    bool ExternalChangePending,
    DesignerPersistenceNoticeKind? NoticeKind)
{
    public static DesignerPersistenceDiagnostics Empty { get; } = new(
        null,
        null,
        null,
        false,
        false,
        -1,
        -1,
        false,
        null);
}
