using System.ComponentModel;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly object s_validatingEvent = new();
    private static readonly object s_validatedEvent = new();
    private static readonly int s_validationActiveProperty = PropertyStore.CreateKey();

    /// <summary>Occurs before this control's pending OnValidation bindings are written.</summary>
    /// <remarks>
    /// Runs synchronously on the owning UI thread. Set Cancel to reject validation before any
    /// validation binding writes. During automatic departure validation this control still owns
    /// focus and its text-input session. Exceptions abort validation and propagate to the caller.
    /// </remarks>
    /// <example><code>
    /// editor.Validating += (_, e) => e.Cancel = string.IsNullOrWhiteSpace(editor.Text);
    /// </code></example>
    [Category("Behavior")]
    public event CancelEventHandler? Validating
    {
        add => Events.AddHandler(s_validatingEvent, value);
        remove => Events.RemoveHandler(s_validatingEvent, value);
    }

    /// <summary>Occurs after accepted validation and successful OnValidation binding writes.</summary>
    /// <remarks>
    /// Raised on the owning UI thread before any automatic focus commit. It does not mean that
    /// focus has left this control. Exceptions propagate; newer focus requests supersede the old
    /// request. Already executed model setter side effects are not rolled back.
    /// </remarks>
    [Category("Behavior")]
    public event EventHandler? Validated
    {
        add => Events.AddHandler(s_validatedEvent, value);
        remove => Events.RemoveHandler(s_validatedEvent, value);
    }

    /// <summary>Gets or sets whether selecting this control validates the departing focus owner.</summary>
    /// <remarks>
    /// Use on the UI thread. Defaults to true. False skips only automatic departure validation;
    /// it does not disable this control's explicit Validate or OnPropertyChanged bindings.
    /// Changing this policy does not change focus, layout or rendering. Forced retirement never validates.
    /// </remarks>
    [DefaultValue(true)]
    [Category("Behavior")]
    public bool CausesValidation
    {
        get => GetState(States.CausesValidation);
        set => SetState(States.CausesValidation, value);
    }

    /// <summary>Validates this control and writes its pending OnValidation bindings without moving focus.</summary>
    /// <returns>True only if observers, bindings and lifetime checks accept the operation.</returns>
    /// <remarks>
    /// Call on the owning UI thread. Live detached, hidden and disabled controls may be explicitly
    /// validated; disposed/retiring controls and recursive validation of the same control return
    /// false. A callback that changes focus or ancestry invalidates the unfinished operation.
    /// Bindings run in collection order after Validating; the first failure stops processing.
    /// Earlier setter side effects are not rolled back. Callback/legacy setter exceptions propagate;
    /// formatting-enabled binding errors retain BindingComplete reporting and return false.
    /// </remarks>
    public bool Validate()
    {
        var scope = GetFocusScope();
        scope.VerifyAccess();
        long version = scope.Version;
        return ValidateCore(() => scope.Version == version);
    }

    /// <summary>Validates a snapshot of this control's explicit descendants in collection order.</summary>
    /// <returns>False on the first canceled, failed or invalidated validation; otherwise true.</returns>
    /// <remarks>
    /// Call on the owning UI thread. Uses depth-first parent-before-child order, excluding this
    /// control and implicit framework controls. Hidden/disabled descendants are included; disposed
    /// or moved snapshot entries are skipped. Additions wait for the next call. Exceptions propagate.
    /// The operation does not move focus or transfer text input. Earlier model writes are not rolled back.
    /// </remarks>
    /// <example><code>
    /// if (fields.ValidateChildren()) SaveModel();
    /// </code></example>
    public bool ValidateChildren() => ValidateChildrenCore(Controls);

    /// <summary>Raises the cancelable pre-binding validation notification.</summary>
    /// <param name="e">The shared cancellation decision for this validation attempt.</param>
    /// <remarks>Overrides must call base to notify subscribers. Do not perform the binding phase here.</remarks>
    protected virtual void OnValidating(CancelEventArgs e)
        => (Events[s_validatingEvent] as CancelEventHandler)?.Invoke(this, e);

    /// <summary>Raises the accepted validation notification before any automatic focus commit.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Overrides must call base to notify subscribers. This method does not change focus.</remarks>
    protected virtual void OnValidated(EventArgs e)
        => (Events[s_validatedEvent] as EventHandler)?.Invoke(this, e);

    internal bool ValidateCore(Func<bool> requestCurrent)
    {
        GetFocusScope().VerifyAccess();
        if (Properties.GetInteger(s_validationActiveProperty) != 0) return false;
        var treeCurrent = CaptureValidationTree();
        bool Current() => treeCurrent() && requestCurrent();
        if (!Current()) return false;
        Properties.SetInteger(s_validationActiveProperty, 1);
        try
        {
            var args = new CancelEventArgs();
            OnValidating(args);
            if (args.Cancel || !Current()) return false;

            // Snapshot after public observers: their add/remove/mode/source changes participate.
            // Changes from a binding callback cannot invalidate an enumerator or add work forever.
            if (Properties.TryGetValue(s_bindingsProperty, out ControlBindingsCollection? bindings) && bindings is not null)
            {
                var snapshot = bindings.Cast<Binding>().Where(binding =>
                    binding.DataSourceUpdateMode == DataSourceUpdateMode.OnValidation).ToArray();
                // A source notification from an earlier setter must not overwrite pending
                // values on later participants. Explicit ReadValue remains an intentional read.
                foreach (var binding in snapshot) binding.BeginControlValidation();
                try
                {
                    foreach (Binding binding in snapshot)
                    {
                        if (!Current()) return false;
                        if (!ReferenceEquals(binding.BindableComponent, this) ||
                            binding.DataSourceUpdateMode != DataSourceUpdateMode.OnValidation) continue;
                        if (!binding.ValidateTarget(this, Current)) return false;
                    }
                }
                finally { foreach (var binding in snapshot) binding.EndControlValidation(); }
            }
            if (!Current()) return false;
            OnValidated(EventArgs.Empty);
            return Current();
        }
        finally { Properties.RemoveInteger(s_validationActiveProperty); }
    }

    internal bool ValidateChildrenCore(ControlCollection children)
    {
        var scope = GetFocusScope();
        scope.VerifyAccess();
        long version = scope.Version;
        var rootCurrent = CaptureValidationTree();
        bool Current() => scope.Version == version && rootCurrent();
        if (!Current()) return false;
        var snapshot = new List<(Control Control, Func<bool> Current, Binding[] Bindings)>();
        Collect(children);
        // A setter for one field may refresh every binding on its source. Protect fields
        // still awaiting their turn, otherwise a successful traversal silently loses edits.
        foreach (var entry in snapshot)
            foreach (var binding in entry.Bindings) binding.BeginControlValidation();
        int released = 0;
        try
        {
            foreach (var entry in snapshot)
            {
                // The current field's public observers retain normal binding/source-change
                // behavior. ValidateCore takes its own fresh binding snapshot after observers.
                foreach (var binding in entry.Bindings) binding.EndControlValidation();
                released++;
                if (!Current()) return false;
                if (!entry.Current()) continue;
                if (!entry.Control.ValidateCore(Current)) return false;
            }
            return Current();
        }
        finally
        {
            for (int i = released; i < snapshot.Count; i++)
                foreach (var binding in snapshot[i].Bindings) binding.EndControlValidation();
        }

        void Collect(ControlCollection collection)
        {
            foreach (var child in collection.ToArray())
            {
                if (child.ImplicitControl || child.IsDisposed || child.Disposing) continue;
                var pending = child.Properties.TryGetValue(s_bindingsProperty, out ControlBindingsCollection? bindings)
                    && bindings is not null
                    ? bindings.Cast<Binding>().Where(binding => binding.DataSourceUpdateMode == DataSourceUpdateMode.OnValidation).ToArray()
                    : Array.Empty<Binding>();
                snapshot.Add((child, child.CaptureValidationTree(), pending));
                Collect(child.Controls);
            }
        }
    }

    internal Func<bool> CaptureValidationTree()
    {
        var ancestry = new List<(Control Control, int Version)>();
        for (Control? current = this; current is not null; current = current.Parent)
            ancestry.Add((current, current.parentAssignmentVersion));
        return () => ancestry.All(entry => !entry.Control.IsDisposed && !entry.Control.Disposing &&
            entry.Control.FocusRetirementDepth == 0 && entry.Control.parentAssignmentVersion == entry.Version) &&
            ancestry[^1].Control.IsFocusRootAvailable;
    }
}
