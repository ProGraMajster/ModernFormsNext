using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Automation;

public sealed partial class AutomationSession
{
    private const AccessibleActions AllowedActions = AccessibleActions.Invoke | AccessibleActions.Focus
        | AccessibleActions.SetValue | AccessibleActions.Select | AccessibleActions.Toggle
        | AccessibleActions.Expand | AccessibleActions.Collapse | AccessibleActions.Increment
        | AccessibleActions.Decrement | AccessibleActions.ScrollIntoView;

    /// <summary>Requests one currently advertised canonical action on the UI dispatcher.</summary>
    /// <param name="rootId">The exact allowed root registration identity.</param>
    /// <param name="handle">The session and canonical runtime identity, re-resolved before execution.</param>
    /// <param name="action">Exactly one advertised action from the Phase 1a allowlist.</param>
    /// <param name="value">Only SetValue accepts a payload: text, a finite numeric range value, or null to clear text.</param>
    /// <param name="cancellationToken">Cancels before execution or between reachability reads; it cannot undo an accepted action.</param>
    /// <returns>Accepted when PerformAction returns true, independently of future async work.</returns>
    /// <remarks>
    /// This calls AccessibleObject.PerformAction only. Button Invoke follows ordinary activation,
    /// Click and command routing; no command is executed directly. Numeric range limits are
    /// checked before dispatch; the canonical peer retains final validation of units and step rules.
    /// A throwing application action may already have produced effects: do not automatically retry.
    /// </remarks>
    public async Task<AutomationActionResult> PerformActionAsync(string rootId, AutomationNodeHandle handle,
        AccessibleActions action, AutomationActionValue? value = null, CancellationToken cancellationToken = default)
    {
        if (stopped) return ActionResult(AutomationErrorCode.SessionEnded);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dispatcher.CheckAccess()) return PerformCore(rootId, handle, action, value, cancellationToken);
            return await dispatcher.InvokeAsync(() => PerformCore(rootId, handle, action, value, cancellationToken),
                DispatcherPriority.Default, cancellationToken).GetTask().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return ActionResult(AutomationErrorCode.ApplicationError); }
    }

    private AutomationActionResult PerformCore(string rootId, AutomationNodeHandle handle,
        AccessibleActions action, AutomationActionValue? value, CancellationToken token)
    {
        var error = ValidateHandle(handle);
        if (error != AutomationErrorCode.None) return ActionResult(error);
        int flag = (int)action;
        if (flag <= 0 || (flag & (flag - 1)) != 0) return ActionResult(AutomationErrorCode.InvalidArgument);
        if ((AllowedActions & action) == 0) return ActionResult(AutomationErrorCode.ActionUnsupported);
        if (action != AccessibleActions.SetValue && value is not null) return ActionResult(AutomationErrorCode.InvalidArgument);
        if (value?.Text is { Length: > SemanticTraversal.MaxTextLength } || value?.Number is double number && !double.IsFinite(number))
            return ActionResult(AutomationErrorCode.InvalidArgument);

        error = Begin(rootId, AutomationCapability.Actions, token, out var root, out var traversal);
        if (error != AutomationErrorCode.None) return ActionResult(error);
        if (traversal!.Error != AutomationErrorCode.None) return ActionResult(traversal.Error);
        var entry = traversal.Find(handle.RuntimeId);
        if (entry is null) return ActionResult(AutomationErrorCode.NodeUnavailable);
        var peer = entry.Peer;

        // All validation and the final canonical call share one dispatcher turn. No stale
        // snapshot supplies availability, capabilities, range values or the target reference.
        var state = peer.State;
        if ((state & (AccessibleStates.Unavailable | AccessibleStates.Invisible)) != 0)
            return ActionResult(AutomationErrorCode.ActionRejected);
        if ((peer.SupportedActions & action) == 0) return ActionResult(AutomationErrorCode.ActionUnsupported);
        object? parameter = null;
        if (action == AccessibleActions.SetValue)
        {
            if ((state & AccessibleStates.ReadOnly) != 0) return ActionResult(AutomationErrorCode.ActionRejected);
            var range = peer.RangeValue;
            if (range is { } r)
            {
                if (r.IsReadOnly) return ActionResult(AutomationErrorCode.ActionRejected);
                if (value?.Number is not double numeric || numeric < r.Minimum || numeric > r.Maximum)
                    return ActionResult(AutomationErrorCode.InvalidArgument);
                // TrackBar's canonical natural type is an integral range. Other custom ranges
                // can use fractional values; never infer the type from a CLR control name.
                if (peer is Control.ControlAccessibleObject { Owner: TrackBar } && numeric != Math.Truncate(numeric))
                    return ActionResult(AutomationErrorCode.InvalidArgument);
                parameter = numeric;
            }
            else
            {
                if (value?.Number is not null) return ActionResult(AutomationErrorCode.InvalidArgument);
                parameter = value?.Text;
            }
        }

        // Custom getter callbacks can reenter application code and detach/close a target. A
        // second bounded structural pass after parameter reads rejects those stale references.
        error = Begin(rootId, AutomationCapability.Actions, token, out root, out var current);
        if (error != AutomationErrorCode.None) return ActionResult(error);
        if (current!.Error != AutomationErrorCode.None) return ActionResult(current.Error);
        var live = current.Find(handle.RuntimeId);
        if (live is null || !ReferenceEquals(live.Peer, peer)) return ActionResult(AutomationErrorCode.NodeUnavailable);
        token.ThrowIfCancellationRequested();
        if ((peer.State & (AccessibleStates.Unavailable | AccessibleStates.Invisible)) != 0)
            return ActionResult(AutomationErrorCode.ActionRejected);
        if ((peer.SupportedActions & action) == 0) return ActionResult(AutomationErrorCode.ActionUnsupported);
        if (stopped) return ActionResult(AutomationErrorCode.SessionEnded);
        if (!root!.IsRegistered) return ActionResult(AutomationErrorCode.StaleNode);
        return peer.PerformAction(action, parameter)
            ? new(AutomationActionStatus.Accepted, AutomationErrorCode.None)
            : ActionResult(AutomationErrorCode.ActionRejected);
    }

    private static AutomationActionResult ActionResult(AutomationErrorCode error) => new(error switch
    {
        AutomationErrorCode.ActionUnsupported => AutomationActionStatus.Unsupported,
        AutomationErrorCode.InvalidArgument => AutomationActionStatus.InvalidArgument,
        AutomationErrorCode.GetterFault or AutomationErrorCode.ApplicationError => AutomationActionStatus.ApplicationError,
        AutomationErrorCode.ActionRejected or AutomationErrorCode.CapabilityDenied => AutomationActionStatus.Rejected,
        _ => AutomationActionStatus.NodeUnavailable
    }, error);
}
