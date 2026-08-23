using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.History;

internal interface IDesignerChange
{
    bool IsEmpty { get; }

    void Apply();

    void Revert();

    bool TryMerge(IDesignerChange subsequentChange);
}

internal sealed class DesignerUndoUnit
{
    private readonly List<IDesignerChange> changes = [];

    public DesignerUndoUnit(
        string description,
        IReadOnlyList<IDesignerChange> sourceChanges,
        DesignControlNode? selectionBefore,
        DesignControlNode? selectionAfter,
        long beforeRevision,
        long afterRevision)
    {
        Description = description;
        changes.AddRange(sourceChanges);
        SelectionBefore = selectionBefore;
        SelectionAfter = selectionAfter;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
    }

    public string Description { get; }

    public DesignControlNode? SelectionBefore { get; }

    public DesignControlNode? SelectionAfter { get; }

    public long BeforeRevision { get; }

    public long AfterRevision { get; }

    public void Apply()
    {
        var appliedCount = 0;

        try
        {
            foreach (var change in changes)
            {
                change.Apply();
                appliedCount++;
            }
        }
        catch
        {
            for (var index = appliedCount - 1; index >= 0; index--)
                changes[index].Revert();

            throw;
        }
    }

    public void Revert()
    {
        var reverted = new List<IDesignerChange>(changes.Count);

        try
        {
            for (var index = changes.Count - 1; index >= 0; index--)
            {
                changes[index].Revert();
                reverted.Add(changes[index]);
            }
        }
        catch
        {
            for (var index = reverted.Count - 1; index >= 0; index--)
                reverted[index].Apply();

            throw;
        }
    }

    public void Release()
        => changes.Clear();
}

internal sealed class DesignerHistory
{
    private readonly List<DesignerUndoUnit> undoUnits = [];
    private readonly List<DesignerUndoUnit> redoUnits = [];
    private long currentRevision;
    private long nextRevision = 1;
    private long? savedRevision;

    public DesignerHistory(int limit, bool initiallyDirty)
    {
        Limit = limit;
        savedRevision = initiallyDirty ? null : currentRevision;
    }

    public int Limit { get; private set; }

    public bool CanUndo => undoUnits.Count > 0;

    public bool CanRedo => redoUnits.Count > 0;

    public string? UndoDescription => CanUndo ? undoUnits[^1].Description : null;

    public string? RedoDescription => CanRedo ? redoUnits[^1].Description : null;

    public bool IsDirty => savedRevision != currentRevision;

    public long CurrentRevision => currentRevision;

    public long? SavedRevision => savedRevision;

    public DesignerUndoUnit PeekUndo()
        => CanUndo ? undoUnits[^1] : throw new InvalidOperationException("Designer undo history is empty.");

    public DesignerUndoUnit PeekRedo()
        => CanRedo ? redoUnits[^1] : throw new InvalidOperationException("Designer redo history is empty.");

    public void Commit(
        string description,
        IReadOnlyList<IDesignerChange> changes,
        DesignControlNode? selectionBefore,
        DesignControlNode? selectionAfter)
    {
        ReleaseAll(redoUnits);
        redoUnits.Clear();

        var afterRevision = nextRevision++;
        undoUnits.Add(new DesignerUndoUnit(
            description,
            changes,
            selectionBefore,
            selectionAfter,
            currentRevision,
            afterRevision));
        currentRevision = afterRevision;
        TrimToLimit();
    }

    public void CompleteUndo(DesignerUndoUnit unit)
    {
        if (!ReferenceEquals(undoUnits[^1], unit))
            throw new InvalidOperationException("Designer undo history changed during replay.");

        undoUnits.RemoveAt(undoUnits.Count - 1);
        redoUnits.Add(unit);
        currentRevision = unit.BeforeRevision;
    }

    public void CompleteRedo(DesignerUndoUnit unit)
    {
        if (!ReferenceEquals(redoUnits[^1], unit))
            throw new InvalidOperationException("Designer redo history changed during replay.");

        redoUnits.RemoveAt(redoUnits.Count - 1);
        undoUnits.Add(unit);
        currentRevision = unit.AfterRevision;
    }

    public void MarkSaved()
        => savedRevision = currentRevision;

    public void MarkSaved(long revision)
    {
        if (revision < 0 || revision >= nextRevision)
            throw new ArgumentOutOfRangeException(nameof(revision), "The saved Designer revision is not part of this history generation.");

        savedRevision = revision;
    }

    public void MarkUnsaved()
        => savedRevision = null;

    public void SetLimit(int limit)
    {
        Limit = limit;
        TrimToLimit();
    }

    public void Clear(bool preserveDirtyState)
    {
        var wasDirty = IsDirty;
        ReleaseAll(undoUnits);
        ReleaseAll(redoUnits);
        undoUnits.Clear();
        redoUnits.Clear();
        currentRevision = 0;
        nextRevision = 1;
        savedRevision = preserveDirtyState && wasDirty ? null : currentRevision;
    }

    private void TrimToLimit()
    {
        while (undoUnits.Count + redoUnits.Count > Limit)
        {
            var collection = undoUnits.Count > 0 ? undoUnits : redoUnits;
            collection[0].Release();
            collection.RemoveAt(0);
        }
    }

    private static void ReleaseAll(IEnumerable<DesignerUndoUnit> units)
    {
        foreach (var unit in units)
            unit.Release();
    }
}
