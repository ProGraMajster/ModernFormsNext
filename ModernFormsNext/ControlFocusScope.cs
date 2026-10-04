using System.Runtime.ExceptionServices;

namespace ModernFormsNext;

/// <summary>Owns the single keyboard selection transaction for one existing control root.</summary>
/// <remarks>
/// Window adapters, windowless surfaces and detached trees use this same mechanism. Walking
/// ancestry is bounded by depth; a transition never enumerates the entire control tree.
/// </remarks>
internal sealed class ControlFocusScope(Control root, int threadId)
{
    private long generation;
    private int transitionDepth;
    private int preflightDepth;
    private bool suspended;

    internal Control? Owner { get; private set; }

    // The sole optional pre-commit seam. Validation can later veto a voluntary transition;
    // mandatory retirement deliberately bypasses it. No public validation API is introduced.
    internal Func<Control?, Control?, bool>? Preflight { get; set; }

    internal void VerifyAccess()
    {
        if (threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Control focus requires the owning UI thread.");
    }

    internal bool IsEligible(Control candidate)
    {
        if (suspended || !root.IsFocusRootAvailable || !candidate.CanSelect) return false;
        for (Control? current = candidate; current is not null; current = current.Parent)
        {
            if (current.IsDisposed || current.Disposing || current.FocusRetirementDepth != 0) return false;
            if (ReferenceEquals(current, root)) return root.Parent is null;
        }
        return false;
    }

    internal bool Request(Control? destination, bool forced = false, bool preservePointerInteraction = false)
    {
        VerifyAccess();
        if (destination is not null && !IsEligible(destination)) return false;
        if (ReferenceEquals(Owner, destination))
        {
            // Selecting the existing owner from a preflight cancels the pending departure.
            if (preflightDepth != 0) generation++;
            return destination is not null;
        }
        // Immediate nested requests have latest-valid-wins semantics. Bound pathological
        // callback cycles without an unbounded queue or allowing an obsolete outer commit.
        if (!forced && transitionDepth >= 32)
            throw new InvalidOperationException("Control focus did not settle after 32 nested transitions.");
        transitionDepth++;
        long version = ++generation;
        try
        {
            Control? previous = Owner;
            if (!forced && Preflight is { } preflight)
            {
                preflightDepth++;
                bool accepted;
                try { accepted = preflight(previous, destination); }
                finally { preflightDepth--; }
                if (!accepted) return false;
            }
            if ((destination is not null && !IsEligible(destination)) || version != generation) return false;

            // No callbacks between these writes: every observer sees zero or one owner.
            long previousState = previous?.CommitFocusState(false) ?? 0;
            Owner = destination;
            destination?.CommitFocusState(true);
            List<Exception>? failures = null;
            void Complete(Action action)
            {
                try { action(); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }

            // Retained IME proxies are revoked before focus observers. Finish/attach may
            // redirect focus; the host already revalidates the canonical owner itself.
            Complete(root.NotifyTextInputFocusChanged);
            if (previous is not null && previous.IsFocusStateCurrent(previousState, selected: false))
            {
                bool preserve = preservePointerInteraction || root.PreserveFocusPointerInteraction(previous);
                Complete(() => previous.NotifyFocusLost(preserve));
                // Router cleanup cannot be a public LostFocus subscriber: an earlier throwing
                // observer would otherwise strand a surface pointer after focus has changed.
                if (!preserve && previous.IsFocusStateCurrent(previousState, selected: false))
                    Complete(() => root.OnFocusOwnerLost(previous));
            }
            if (destination is not null && IsEligible(destination) && version == generation &&
                ReferenceEquals(Owner, destination))
                Complete(destination.NotifyFocusGained);

            // A callback can invalidate the destination without going through a normal setter
            // (for example a custom root). Required retirement must still finish after errors.
            if (Owner is { } owner && !IsEligible(owner) && ReferenceEquals(Owner, owner))
                Complete(() => Request(null, forced: true));
            if (failures?.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures?.Count > 1) throw new AggregateException("Focus transition callbacks failed.", failures);
            return destination is not null && ReferenceEquals(Owner, destination);
        }
        finally { transitionDepth--; }
    }

    internal void Retire(Control subtree)
    {
        if (Owner is null) return;
        VerifyAccess();
        for (Control? current = Owner; current is not null; current = current.Parent)
            if (ReferenceEquals(current, subtree))
            {
                Request(null, forced: true);
                return;
            }
    }

    internal void SetSuspended(bool value)
    {
        VerifyAccess();
        suspended = value;
        if (value) Request(null, forced: true);
    }
}
