namespace ModernFormsNext.Designer.History;

/// <summary>
/// Represents a scoped, atomic Designer model transaction.
/// </summary>
/// <remarks>
/// Transactions must be completed in last-in-first-out order on the thread that created the
/// owning Designer session. Disposing a transaction without calling <see cref="Commit"/>
/// deterministically rolls back every change recorded by that transaction. A nested commit joins
/// its changes to the outer transaction; a nested rollback reverts only its own changes.
/// </remarks>
public sealed class DesignerTransaction : IDisposable
{
    private DesignerTransactionManager? manager;
    private readonly long id;

    internal DesignerTransaction(DesignerTransactionManager manager, long id, string description)
    {
        this.manager = manager;
        this.id = id;
        Description = description;
    }

    /// <summary>
    /// Gets the user-visible description used by undo and redo UI.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Commits this transaction.
    /// </summary>
    /// <remarks>
    /// A nested commit does not create a separate history entry. The outermost commit creates one
    /// undo unit when at least one effective model change was recorded.
    /// </remarks>
    public void Commit()
    {
        var owner = manager ?? throw new InvalidOperationException("The Designer transaction has already completed.");
        if (!owner.IsTransactionActive(id))
        {
            manager = null;
            throw new InvalidOperationException("The Designer transaction has already completed.");
        }

        try
        {
            owner.Commit(id);
            manager = null;
        }
        catch
        {
            // Notification observers run after the manager has already completed the atomic
            // commit. Do not leave this scope looking active when only an observer failed.
            if (!owner.IsTransactionActive(id))
                manager = null;

            throw;
        }
    }

    /// <summary>
    /// Rolls this transaction back immediately.
    /// </summary>
    public void Rollback()
    {
        var owner = manager ?? throw new InvalidOperationException("The Designer transaction has already completed.");
        if (!owner.IsTransactionActive(id))
        {
            manager = null;
            throw new InvalidOperationException("The Designer transaction has already completed.");
        }

        try
        {
            owner.Rollback(id);
            manager = null;
        }
        catch
        {
            // A failed model revert deliberately leaves the manager frame active so callers can
            // diagnose or retry it. Observer failures happen after completion and must not do so.
            if (!owner.IsTransactionActive(id))
                manager = null;

            throw;
        }
    }

    /// <summary>
    /// Rolls back the transaction when it has not already been committed or rolled back.
    /// </summary>
    public void Dispose()
    {
        if (manager is not { } owner)
            return;
        if (!owner.IsTransactionActive(id))
        {
            manager = null;
            return;
        }

        try
        {
            owner.Rollback(id);
            manager = null;
        }
        catch
        {
            if (!owner.IsTransactionActive(id))
                manager = null;

            throw;
        }
    }
}
