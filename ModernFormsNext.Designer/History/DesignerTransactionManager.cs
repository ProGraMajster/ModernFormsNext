using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext.Designer.History;

/// <summary>
/// Coordinates atomic Designer transactions and the active document's bounded undo/redo history.
/// </summary>
/// <remarks>
/// The manager belongs to one <see cref="DesignerSession"/> and must be used on the session's
/// creating UI thread. Model mutation helpers record explicit property, tree, and collection
/// changes; history never serializes an entire document for an ordinary edit. Undo and redo replay
/// those stored forward and previous values while recording is suppressed.
/// </remarks>
public sealed class DesignerTransactionManager
{
    private readonly DesignerSession session;
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly List<TransactionFrame> frames = [];
    private readonly List<IDesignerChange> pendingChanges = [];
    private long nextTransactionId = 1;
    private DesignControlNode? outerSelectionBefore;
    private DesignerHistoryReplayMode mode;

    internal DesignerTransactionManager(DesignerSession session)
    {
        this.session = session;
    }

    /// <summary>
    /// Occurs after the outermost transaction commits an effective model change.
    /// </summary>
    public event EventHandler<DesignerHistoryEventArgs>? TransactionCommitted;

    /// <summary>
    /// Occurs after a transaction is rolled back.
    /// </summary>
    public event EventHandler<DesignerHistoryEventArgs>? TransactionRolledBack;

    /// <summary>
    /// Occurs after an undo operation completes.
    /// </summary>
    public event EventHandler<DesignerHistoryEventArgs>? UndoPerformed;

    /// <summary>
    /// Occurs after a redo operation completes.
    /// </summary>
    public event EventHandler<DesignerHistoryEventArgs>? RedoPerformed;

    /// <summary>
    /// Occurs when undo/redo availability, descriptions, save state, or the active history changes.
    /// </summary>
    public event EventHandler? HistoryChanged;

    /// <summary>
    /// Gets a value indicating whether the active document has an undo unit.
    /// </summary>
    public bool CanUndo => !HasActiveTransaction && session.CurrentHistory.CanUndo;

    /// <summary>
    /// Gets a value indicating whether the active document has a redo unit.
    /// </summary>
    public bool CanRedo => !HasActiveTransaction && session.CurrentHistory.CanRedo;

    /// <summary>
    /// Gets the description of the next undo operation, or <see langword="null"/>.
    /// </summary>
    public string? UndoDescription => session.CurrentHistory.UndoDescription;

    /// <summary>
    /// Gets the description of the next redo operation, or <see langword="null"/>.
    /// </summary>
    public string? RedoDescription => session.CurrentHistory.RedoDescription;

    /// <summary>
    /// Gets a value indicating whether at least one transaction scope is active.
    /// </summary>
    public bool HasActiveTransaction => frames.Count > 0;

    /// <summary>
    /// Gets the current recording or replay mode.
    /// </summary>
    public DesignerHistoryReplayMode ReplayMode => mode;

    /// <summary>
    /// Gets a value indicating whether undo, redo, or rollback replay is active.
    /// </summary>
    public bool IsReplaying => mode is DesignerHistoryReplayMode.Undoing or DesignerHistoryReplayMode.Redoing or DesignerHistoryReplayMode.RollingBack;

    /// <summary>
    /// Gets or sets the maximum number of retained history entries per open Designer document.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than one.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when set from a thread other than the session UI thread or during an active transaction.
    /// </exception>
    public int HistoryLimit
    {
        get => session.HistoryLimit;
        set
        {
            EnsureAccess();
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Designer history limit must be at least one entry.");
            if (HasActiveTransaction)
                throw new InvalidOperationException("Designer history limit cannot change during an active transaction.");

            session.SetHistoryLimit(value);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Begins an atomic Designer transaction.
    /// </summary>
    /// <param name="description">A concise user-visible operation description.</param>
    /// <returns>A transaction that must be committed or rolled back.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="description"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the session UI thread or during history replay.
    /// </exception>
    public DesignerTransaction Begin(string description)
    {
        EnsureAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (IsReplaying)
            throw new InvalidOperationException("A Designer transaction cannot begin during history replay.");

        var isOutermost = frames.Count == 0;
        if (isOutermost)
        {
            pendingChanges.Clear();
            outerSelectionBefore = session.SelectedNode;
            mode = DesignerHistoryReplayMode.Recording;
        }

        var id = nextTransactionId++;
        frames.Add(new TransactionFrame(id, description.Trim(), pendingChanges.Count, session.SelectedNode));
        if (isOutermost)
        {
            try
            {
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // No mutation has happened yet. If command-state notification prevents Begin from
                // returning its scope, restore the manager to its exact pre-transaction state.
                frames.RemoveAt(frames.Count - 1);
                pendingChanges.Clear();
                outerSelectionBefore = null;
                mode = DesignerHistoryReplayMode.Idle;
                throw;
            }
        }
        return new DesignerTransaction(this, id, description.Trim());
    }

    /// <summary>
    /// Undoes the latest committed transaction for the active document.
    /// </summary>
    /// <returns><see langword="true"/> when an undo unit was replayed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the session UI thread, during another replay,
    /// or while a transaction is active.
    /// </exception>
    public bool Undo()
    {
        EnsureReplayCanStart();
        var history = session.CurrentHistory;
        if (!history.CanUndo)
            return false;

        var unit = history.PeekUndo();
        mode = DesignerHistoryReplayMode.Undoing;
        try
        {
            unit.Revert();
            history.CompleteUndo(unit);
            session.SynchronizeCommittedModelState();
            session.Host.Selection.Select(unit.SelectionBefore);

            // Keep replay suppression active while observers refresh derived Designer state.
            session.NotifyCommittedModelState($"Undo {unit.Description}");
            UndoPerformed?.Invoke(this, new DesignerHistoryEventArgs(unit.Description, DesignerHistoryReplayMode.Undoing));
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            mode = DesignerHistoryReplayMode.Idle;
        }
        return true;
    }

    /// <summary>
    /// Redoes the latest undone transaction for the active document.
    /// </summary>
    /// <returns><see langword="true"/> when a redo unit was replayed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the session UI thread, during another replay,
    /// or while a transaction is active.
    /// </exception>
    public bool Redo()
    {
        EnsureReplayCanStart();
        var history = session.CurrentHistory;
        if (!history.CanRedo)
            return false;

        var unit = history.PeekRedo();
        mode = DesignerHistoryReplayMode.Redoing;
        try
        {
            unit.Apply();
            history.CompleteRedo(unit);
            session.SynchronizeCommittedModelState();
            session.Host.Selection.Select(unit.SelectionAfter);

            session.NotifyCommittedModelState($"Redo {unit.Description}");
            RedoPerformed?.Invoke(this, new DesignerHistoryEventArgs(unit.Description, DesignerHistoryReplayMode.Redoing));
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            mode = DesignerHistoryReplayMode.Idle;
        }
        return true;
    }

    /// <summary>
    /// Marks the active document's current history revision as saved.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the session UI thread or while a transaction is active.
    /// </exception>
    public void MarkSavedState()
    {
        EnsureAccess();
        if (HasActiveTransaction)
            throw new InvalidOperationException("A Designer document cannot be marked saved during an active transaction.");

        session.CurrentHistory.MarkSaved();
        session.RefreshDirtyState();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears undo and redo units for the active document while preserving whether it is dirty.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the session UI thread or while a transaction is active.
    /// </exception>
    public void ClearHistory()
    {
        EnsureAccess();
        if (HasActiveTransaction)
            throw new InvalidOperationException("Designer history cannot be cleared during an active transaction.");

        session.CurrentHistory.Clear(preserveDirtyState: true);
        session.RefreshDirtyState();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ExecuteChange(IDesignerChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        EnsureAccess();

        if (IsReplaying)
        {
            change.Apply();
            return;
        }

        EnsureRecording();
        // Every concrete typed change keeps its own Apply atomic. Earlier changes in the outer
        // transaction are rolled back by the transaction scope if this call throws.
        change.Apply();
        AddPendingChange(change);
    }

    internal void RecordAppliedChange(IDesignerChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        EnsureAccess();
        if (IsReplaying)
            return;

        EnsureRecording();
        AddPendingChange(change);
    }

    internal void Commit(long transactionId)
    {
        EnsureAccess();
        var frame = GetCurrentFrame(transactionId);
        frames.RemoveAt(frames.Count - 1);

        if (frames.Count > 0)
            return;

        mode = DesignerHistoryReplayMode.Idle;
        RemoveEmptyChanges();
        if (pendingChanges.Count == 0)
        {
            pendingChanges.Clear();
            outerSelectionBefore = null;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var committedChanges = pendingChanges.ToArray();
        pendingChanges.Clear();
        session.CurrentHistory.Commit(
            frame.Description,
            committedChanges,
            outerSelectionBefore,
            session.SelectedNode);
        outerSelectionBefore = null;

        session.NotifyCommittedModelState(frame.Description);
        TransactionCommitted?.Invoke(this, new DesignerHistoryEventArgs(frame.Description, DesignerHistoryReplayMode.Recording));
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void Rollback(long transactionId)
    {
        EnsureAccess();
        var frame = GetCurrentFrame(transactionId);
        var hadChanges = pendingChanges.Count > frame.StartChangeIndex;
        var selectionChanged = !ReferenceEquals(session.SelectedNode, frame.SelectionBefore);
        mode = DesignerHistoryReplayMode.RollingBack;

        try
        {
            var reverted = new List<IDesignerChange>();
            try
            {
                for (var index = pendingChanges.Count - 1; index >= frame.StartChangeIndex; index--)
                {
                    pendingChanges[index].Revert();
                    reverted.Add(pendingChanges[index]);
                }
            }
            catch
            {
                // Restore the still-active transaction if rollback itself fails. This prevents a
                // half-rolled-back model and leaves the frame available for diagnostics/retry.
                for (var index = reverted.Count - 1; index >= 0; index--)
                    reverted[index].Apply();

                throw;
            }

            if (pendingChanges.Count > frame.StartChangeIndex)
                pendingChanges.RemoveRange(frame.StartChangeIndex, pendingChanges.Count - frame.StartChangeIndex);
        }
        catch
        {
            mode = DesignerHistoryReplayMode.Recording;
            throw;
        }

        frames.RemoveAt(frames.Count - 1);
        var nextMode = frames.Count == 0 ? DesignerHistoryReplayMode.Idle : DesignerHistoryReplayMode.Recording;
        if (frames.Count == 0)
        {
            pendingChanges.Clear();
            outerSelectionBefore = null;
        }

        var historyChangedRaised = false;
        try
        {
            // The transaction is already fully reverted and removed before selection observers
            // run. An observer exception can therefore propagate without leaving a stale frame or
            // making a subsequent rollback apply the same changes twice.
            session.Host.Selection.Select(frame.SelectionBefore);

            if (hadChanges || selectionChanged)
            {
                session.NotifyRolledBackModelState(frame.Description);
                TransactionRolledBack?.Invoke(this, new DesignerHistoryEventArgs(frame.Description, DesignerHistoryReplayMode.RollingBack));
                HistoryChanged?.Invoke(this, EventArgs.Empty);
                historyChangedRaised = true;
            }
        }
        finally
        {
            // The transaction has already been reverted and removed. Observer exceptions must not
            // leave replay suppression enabled for later, unrelated Designer edits.
            mode = nextMode;
            if (frames.Count == 0 && !historyChangedRaised)
                HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void MarkActiveDocumentUnsaved()
    {
        session.CurrentHistory.MarkUnsaved();
        session.RefreshDirtyState();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void InvalidateForExternalMutation()
    {
        EnsureAccess();
        if (HasActiveTransaction)
            throw new InvalidOperationException("An untracked Designer mutation cannot be reported during an active transaction.");

        session.CurrentHistory.Clear(preserveDirtyState: false);
        session.CurrentHistory.MarkUnsaved();
        session.NotifyCommittedModelState("External model change");
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void NotifyActiveHistoryChanged()
        => HistoryChanged?.Invoke(this, EventArgs.Empty);

    internal void RollbackActiveTransactionForDisposal()
    {
        Exception? notificationException = null;

        while (frames.Count > 0)
        {
            var id = frames[^1].Id;
            try
            {
                Rollback(id);
            }
            catch (Exception ex) when (!IsTransactionActive(id))
            {
                // The frame was successfully reverted and only a post-rollback observer failed.
                // Continue unwinding outer scopes so disposal never commits an unfinished edit.
                notificationException ??= ex;
            }
        }

        if (notificationException is not null)
            ExceptionDispatchInfo.Capture(notificationException).Throw();
    }

    internal bool IsTransactionActive(long transactionId)
        => frames.Any(frame => frame.Id == transactionId);

    internal void ReleaseObservers()
    {
        TransactionCommitted = null;
        TransactionRolledBack = null;
        UndoPerformed = null;
        RedoPerformed = null;
        HistoryChanged = null;
    }

    private void AddPendingChange(IDesignerChange change)
    {
        if (change.IsEmpty)
            return;

        var mergeFloor = frames[^1].StartChangeIndex;
        if (pendingChanges.Count > mergeFloor && pendingChanges[^1].TryMerge(change))
        {
            if (pendingChanges[^1].IsEmpty)
                pendingChanges.RemoveAt(pendingChanges.Count - 1);
            return;
        }

        pendingChanges.Add(change);
    }

    private void RemoveEmptyChanges()
        => pendingChanges.RemoveAll(change => change.IsEmpty);

    private TransactionFrame GetCurrentFrame(long transactionId)
    {
        if (frames.Count == 0 || frames[^1].Id != transactionId)
            throw new InvalidOperationException("Designer transactions must complete in last-in-first-out order.");

        return frames[^1];
    }

    private void EnsureRecording()
    {
        if (frames.Count == 0 || mode != DesignerHistoryReplayMode.Recording)
            throw new InvalidOperationException("Designer model changes must be recorded inside an active transaction.");
    }

    private void EnsureReplayCanStart()
    {
        EnsureAccess();
        if (HasActiveTransaction)
            throw new InvalidOperationException("Undo and redo are not allowed during an active Designer transaction.");
        if (IsReplaying)
            throw new InvalidOperationException("Designer history replay is already active.");
    }

    private void EnsureAccess()
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Designer transactions must run on the session's creating UI thread.");
    }

    private sealed record TransactionFrame(
        long Id,
        string Description,
        int StartChangeIndex,
        DesignControlNode? SelectionBefore);
}
