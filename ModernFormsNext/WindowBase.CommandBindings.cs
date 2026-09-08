using System.ComponentModel;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private CommandBindingCollection? commandBindings;

    /// <summary>Gets runtime handlers visited after the target's control ancestors in this window.</summary>
    /// <remarks>
    /// Access on the UI thread. The route stops at this window; Form.Owner and popup ownership are
    /// not traversed. Actual close/disposal releases registrations; cancelled close preserves them.
    /// Edits requery affected commands and may update action-source enabled/rendering state.
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBindingCollection CommandBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(InputBindingsClosed, this);
            var bindings = commandBindings ??= new CommandBindingCollection(this);
            bindings.VerifyAccess();
            return bindings;
        }
    }

    internal CommandBindingCollection? CommandBindingsInternal => commandBindings;

    private void ReleaseCommandBindings()
    {
        commandBindings?.Release();
        commandBindings = null;
        adapter.ReleaseCommandBindingsForSubtree();
    }
}
