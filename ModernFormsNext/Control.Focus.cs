namespace ModernFormsNext;

public partial class Control
{
    private readonly int focusThreadId = Environment.CurrentManagedThreadId;
    private ControlFocusScope? focusScope;
    private long focusStateVersion;
    internal int FocusRetirementDepth { get; private set; }

    internal virtual bool PreserveFocusPointerInteraction(Control previous) => false;
    internal virtual void OnFocusOwnerLost(Control previous) { }

    internal virtual bool IsFocusRootAvailable => !IsDisposed && !Disposing;

    internal ControlFocusScope GetFocusScope()
    {
        Control root = this;
        while (root.Parent is { } ancestor) root = ancestor;
        return root.focusScope ??= new ControlFocusScope(root, root.focusThreadId);
    }

    internal ControlFocusScope? FindExistingFocusScope()
    {
        Control root = this;
        while (root.Parent is { } ancestor) root = ancestor;
        return root.focusScope;
    }

    internal long CommitFocusState(bool selected)
    {
        Selected = selected;
        // Keep the logical visual-focus bit coherent without invalidation callbacks between
        // the owner writes. The normal notifications refresh rendering after the commit.
        hasVisualFocus = selected;
        return ++focusStateVersion;
    }

    internal bool IsFocusStateCurrent(long version, bool selected)
        => version == focusStateVersion && Selected == selected;

    internal void NotifyFocusGained()
    {
        long version = focusStateVersion;
        OnGotFocus(EventArgs.Empty);
        if (IsFocusStateCurrent(version, selected: true) && !IsDisposed && !Disposing &&
            FindWindow()?.InputBindingsClosed != true)
            Invalidate();
    }

    internal void NotifyFocusLost(bool preservePointerInteraction)
    {
        int preservationDepth = Properties.GetInteger(s_focusLossPointerPreservationProperty);
        if (preservePointerInteraction)
            Properties.SetInteger(s_focusLossPointerPreservationProperty, preservationDepth + 1);
        try { OnDeselected(EventArgs.Empty); }
        finally
        {
            if (preservePointerInteraction)
            {
                if (preservationDepth == 0) Properties.RemoveInteger(s_focusLossPointerPreservationProperty);
                else Properties.SetInteger(s_focusLossPointerPreservationProperty, preservationDepth);
            }
        }
    }

    // Parent assignment temporarily blocks the entire subtree even while callbacks still
    // see the old ancestry. A new root does not implicitly inherit keyboard selection.
    internal void BeginFocusRetirement()
    {
        var scope = FindExistingFocusScope();
        FocusRetirementDepth++;
        scope?.VerifyAccess();
        scope?.Retire(this);
    }

    internal void EndFocusRetirement() => FocusRetirementDepth--;

    private void RetireUnavailableFocus() => FindExistingFocusScope()?.Retire(this);
}
