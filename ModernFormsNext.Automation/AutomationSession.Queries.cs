using System.Collections.Immutable;

namespace ModernFormsNext.Automation;

public sealed partial class AutomationSession
{
    /// <summary>Enumerates explicitly registered live roots in registration order, subject to MaxResults.</summary>
    /// <param name="cancellationToken">Cancels queued work or traversal between semantic getter calls.</param>
    /// <returns>Detached root descriptors. Only roots allowing Inspect are listed.</returns>
    public Task<AutomationResult<ImmutableArray<AutomationRootInfo>>> GetRootsAsync(CancellationToken cancellationToken = default)
        => Dispatch(captureId =>
        {
            if (stopped) return new AutomationResult<ImmutableArray<AutomationRootInfo>>(default, AutomationErrorCode.SessionEnded, captureId);
            if ((Capabilities & AutomationCapability.Inspect) == 0)
                return new(default, AutomationErrorCode.CapabilityDenied, captureId);
            PruneRoots();
            var result = ImmutableArray.CreateBuilder<AutomationRootInfo>();
            bool truncated = false;
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((root.Capabilities & AutomationCapability.Inspect) == 0 || !root.TryGetPeer(out var peer)) continue;
                if (result.Count == Limits.MaxResults) { truncated = true; break; }
                result.Add(new(root.RootId, new(SessionId, Id(peer!.RuntimeId)), root.Capabilities, root.CoordinateSpace));
            }
            return new(result.ToImmutable(), truncated ? AutomationErrorCode.LimitExceeded : AutomationErrorCode.None, captureId, truncated);
        }, cancellationToken);

    /// <summary>Captures a handle currently reachable from the specified registered root.</summary>
    /// <param name="rootId">The exact root registration identity; no cross-root fallback occurs.</param>
    /// <param name="handle">The session and canonical runtime identity.</param>
    /// <param name="cancellationToken">Cancels queued work or traversal between getters.</param>
    /// <returns>A detached snapshot, or a structured stale/unavailable/fault/limit result.</returns>
    public Task<AutomationResult<AutomationNodeSnapshot>> InspectAsync(string rootId, AutomationNodeHandle handle,
        CancellationToken cancellationToken = default)
        => Dispatch(captureId => InspectCore(rootId, handle, captureId, cancellationToken), cancellationToken);

    /// <summary>Captures direct active semantic children in ascending canonical child-index order.</summary>
    /// <param name="rootId">The exact allowed root registration identity.</param>
    /// <param name="handle">The currently reachable parent handle.</param>
    /// <param name="cancellationToken">Cancels queued work or traversal between getters.</param>
    /// <returns>Immutable child snapshots with explicit incomplete-capture diagnostics.</returns>
    public Task<AutomationResult<ImmutableArray<AutomationNodeSnapshot>>> GetChildrenAsync(string rootId, AutomationNodeHandle handle,
        CancellationToken cancellationToken = default)
        => Dispatch(captureId =>
        {
            var error = ValidateHandle(handle);
            if (error != AutomationErrorCode.None)
                return new AutomationResult<ImmutableArray<AutomationNodeSnapshot>>(default, error, captureId);
            error = Begin(rootId, AutomationCapability.Query, cancellationToken, out var root, out var traversal);
            if (error != AutomationErrorCode.None) return new(default, error, captureId);
            var entry = traversal!.Find(handle.RuntimeId);
            if (entry is null) return new(default, Missing(traversal), captureId, traversal.Truncated, traversal.Issues);
            var result = ImmutableArray.CreateBuilder<AutomationNodeSnapshot>();
            foreach (var child in entry.Children)
            {
                if (result.Count == Limits.MaxResults) { traversal.Limit(); break; }
                result.Add(traversal.Capture(child, SessionId, rootId, root!.CoordinateSpace, captureId));
            }
            return Result(result.ToImmutable(), traversal, captureId);
        }, cancellationToken);

    /// <summary>Finds matches by depth-first preorder within one root, including the root itself.</summary>
    /// <param name="rootId">The exact allowed root registration identity.</param>
    /// <param name="query">Exact ordinal filters combined with AND; no selector language is parsed.</param>
    /// <param name="cancellationToken">Cancels queued work or traversal between getters.</param>
    /// <returns>Up to MaxResults matches. Limits/faults explicitly prevent a completeness guarantee.</returns>
    public Task<AutomationResult<ImmutableArray<AutomationNodeSnapshot>>> FindAllAsync(string rootId, AutomationQuery query,
        CancellationToken cancellationToken = default)
        => Dispatch(captureId => FindCore(rootId, query, false, captureId, cancellationToken), cancellationToken);

    /// <summary>Requires exactly one match in a complete root traversal; never guesses among duplicate locators.</summary>
    /// <param name="rootId">The exact allowed root registration identity.</param>
    /// <param name="query">The exact semantic filters.</param>
    /// <param name="cancellationToken">Cancels queued work or traversal between getters.</param>
    /// <returns>NodeNotFound for zero, a snapshot for one, or AmbiguousMatch for at least two. Incomplete searches cannot certify uniqueness.</returns>
    public Task<AutomationResult<AutomationNodeSnapshot>> FindOneAsync(string rootId, AutomationQuery query,
        CancellationToken cancellationToken = default)
        => Dispatch(captureId =>
        {
            var found = FindCore(rootId, query, true, captureId, cancellationToken);
            var error = found.Error;
            if (found.Value.Length > 1) error = AutomationErrorCode.AmbiguousMatch;
            else if (error == AutomationErrorCode.None && found.Value.Length == 0) error = AutomationErrorCode.NodeNotFound;
            return new AutomationResult<AutomationNodeSnapshot>(error == AutomationErrorCode.None ? found.Value[0] : null,
                error, captureId, found.Truncated, found.Issues);
        }, cancellationToken);

    private AutomationResult<AutomationNodeSnapshot> InspectCore(string rootId, AutomationNodeHandle handle, string captureId, CancellationToken token)
    {
        var error = ValidateHandle(handle);
        if (error != AutomationErrorCode.None) return new(null, error, captureId);
        error = Begin(rootId, AutomationCapability.Inspect, token, out var root, out var traversal);
        if (error != AutomationErrorCode.None) return new(null, error, captureId);
        var entry = traversal!.Find(handle.RuntimeId);
        if (entry is null) return new(null, Missing(traversal), captureId, traversal.Truncated, traversal.Issues);
        var snapshot = traversal.Capture(entry, SessionId, rootId, root!.CoordinateSpace, captureId);
        return Result(snapshot, traversal, captureId);
    }

    private AutomationResult<ImmutableArray<AutomationNodeSnapshot>> FindCore(string rootId, AutomationQuery query,
        bool unique, string captureId, CancellationToken token)
    {
        if (query is null || (query.RequiredStates & query.ExcludedStates) != 0
            || query.Name is { Length: > SemanticTraversal.MaxTextLength } || query.AutomationId is { Length: > SemanticTraversal.MaxTextLength })
            return new([], AutomationErrorCode.InvalidArgument, captureId);
        var error = Begin(rootId, AutomationCapability.Query, token, out var root, out var traversal);
        if (error != AutomationErrorCode.None) return new([], error, captureId);
        var result = ImmutableArray.CreateBuilder<AutomationNodeSnapshot>();
        foreach (var entry in traversal!.Entries)
        {
            var snapshot = traversal.Capture(entry, SessionId, rootId, root!.CoordinateSpace, captureId);
            if (!query.Matches(snapshot)) continue;
            // FindOne must detect a second match even when MaxResults is one. Only its single
            // final snapshot can escape; this second candidate is solely an ambiguity witness.
            if (unique && result.Count == 1) { result.Add(snapshot); break; }
            if (result.Count == Limits.MaxResults) { traversal.Limit(); break; }
            result.Add(snapshot);
        }
        return Result(result.ToImmutable(), traversal, captureId);
    }

    private AutomationErrorCode Begin(string rootId, AutomationCapability capability, CancellationToken token,
        out AutomationRootRegistration? root, out SemanticTraversal? traversal)
    {
        traversal = null;
        var error = ResolveRoot(rootId, capability, out root);
        if (error != AutomationErrorCode.None) return error;
        if (!root!.TryGetPeer(out var peer)) return AutomationErrorCode.NodeUnavailable;
        traversal = new(Limits, token);
        traversal.Walk(peer!);
        if (stopped) return AutomationErrorCode.SessionEnded;
        if (!root.IsRegistered) return AutomationErrorCode.StaleNode;
        return AutomationErrorCode.None;
    }

    private static AutomationErrorCode Missing(SemanticTraversal traversal)
        => traversal.Error == AutomationErrorCode.None ? AutomationErrorCode.NodeUnavailable : traversal.Error;

    private AutomationResult<T> Result<T>(T value, SemanticTraversal traversal, string captureId)
        => stopped ? new(default, AutomationErrorCode.SessionEnded, captureId)
        : new(value, traversal.Error, captureId, traversal.Truncated, traversal.Issues);
}
