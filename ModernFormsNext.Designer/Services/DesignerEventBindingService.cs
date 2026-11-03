using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Properties;

namespace ModernFormsNext.Designer.Services;

// The model history remains the sole owner of bindings. User code has the editor's own undo
// history and is deliberately retained on unbind, model Undo, cancellation or a late conflict.
internal sealed class DesignerEventBindingService(DesignerSession session, DesignerFileService files)
{
    internal async Task<bool> BindAsync(DesignerEventDescriptor descriptor, string handlerName,
        bool create, bool navigate, CancellationToken cancellationToken)
    {
        if (!descriptor.IsCurrentTarget() || session.Transactions.HasActiveTransaction) return false;
        var document = session.Document;
        var selection = DesignerSelectionSnapshot.Capture(session.Host.Selection);
        var generation = session.Transactions.RecordedChangeGeneration;
        var documentPath = session.CurrentDocumentPath;
        bool Current() => descriptor.IsCurrentTarget() && ReferenceEquals(session.Document, document) && selection.Matches(session.Host.Selection)
            && generation == session.Transactions.RecordedChangeGeneration
            && !session.Transactions.HasActiveTransaction
            && string.Equals(documentPath, session.CurrentDocumentPath, StringComparison.OrdinalIgnoreCase);
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void Changed(object? sender, EventArgs args) { if (!Current()) pending.Cancel(); }
        session.DocumentChanged += Changed;
        session.SelectionChanged += Changed;
        DesignerPreparedEventHandler? prepared = null;
        var committedBinding = false;
        try
        {
            var name = handlerName.Trim();
            if (name.Length != 0)
            {
                if (!descriptor.CanBind)
                { session.Log("This event delegate signature is unsupported; edit the binding in code."); return false; }
                prepared = await files.PrepareEventHandlerAsync(document, name, descriptor.Parameters, create, pending.Token);
                if (!prepared.Plan.Succeeded) { session.Log(prepared.Plan.Message); return false; }
                if (!Current() || pending.IsCancellationRequested) return false;
                if (!await files.ApplyEventHandlerAsync(prepared, pending.Token))
                { session.Log("Event source changed or the host denied the edit; the binding was preserved."); return false; }
                if (!Current() || pending.IsCancellationRequested)
                { session.Log("Event edit became stale; no binding was changed. Any created user method was retained."); return false; }
            }
            // No transaction is held across a host await. Unsubscribe before our own commit emits
            // document/selection notifications; unrelated work above invalidates the frozen target.
            session.DocumentChanged -= Changed;
            session.SelectionChanged -= Changed;
            if (!Current() || pending.IsCancellationRequested) return false;
            using (var transaction = session.Transactions.Begin($"Change {descriptor.DisplayName} event"))
            {
                var snapshot = DesignerModelMutationSnapshot.CaptureSelected(session);
                try
                {
                    if (!descriptor.TryCommit(name, out var error))
                    {
                        snapshot.RecordChanges(session.Transactions);
                        transaction.Rollback();
                        session.Log($"Event binding was not changed: {error}");
                        return false;
                    }
                    snapshot.RecordChanges(session.Transactions);
                    transaction.Commit();
                    committedBinding = true;
                }
                catch
                {
                    if (session.Transactions.HasActiveTransaction) snapshot.RecordChanges(session.Transactions);
                    throw;
                }
            }
            session.Log(name.Length == 0 ? $"Cleared {descriptor.DisplayName}; user methods were retained."
                : $"Bound {descriptor.DisplayName} to {name}.");
            if (navigate && prepared is not null)
            {
                if (pending.IsCancellationRequested || !descriptor.IsCurrentTarget()
                    || !ReferenceEquals(session.Document, document) || !selection.Matches(session.Host.Selection)
                    || session.Transactions.HasActiveTransaction
                    || !string.Equals(documentPath, session.CurrentDocumentPath, StringComparison.OrdinalIgnoreCase))
                    return true;
                // The worker already resolved this location against the post-insertion snapshot.
                // The editor compares that snapshot again before moving its caret.
                if (!await files.NavigateToEventHandlerAsync(prepared, prepared.Plan.NavigationOffset, cancellationToken))
                    session.Log($"Handler is ready in {prepared.Path}; this host could not navigate to the current code location.");
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            session.Log(committedBinding ? "Event binding committed; code navigation was canceled."
                : "Event editing canceled; no stale binding was applied. User methods were retained.");
            return committedBinding;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { session.Log($"Event editing failed: {ex.Message}"); return false; }
        finally
        {
            session.DocumentChanged -= Changed;
            session.SelectionChanged -= Changed;
        }
    }
}
