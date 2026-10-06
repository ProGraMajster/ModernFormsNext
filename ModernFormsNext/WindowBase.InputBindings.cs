using System.ComponentModel;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private InputBindingCollection? inputBindings;
    private readonly DataBinding.InputBindingResolver inputBindingResolver = new();

    /// <summary>Gets runtime keyboard bindings available throughout this window, including focused children.</summary>
    /// <remarks>
    /// Access on the owning UI thread. Window KeyDown preview retains first refusal. Binding lookup
    /// then checks the focused control and ancestors, this window and Application, before normal
    /// control input. Actual close/disposal releases this collection and descendant registrations;
    /// cancelling close preserves them. No native/global hotkey is registered.
    /// </remarks>
    /// <example><code>
    /// form.InputBindings.Add(new KeyBinding(save, new KeyGesture(Keys.S, KeyModifiers.Control)));
    /// </code></example>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public InputBindingCollection InputBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(InputBindingsClosed, this);
            var bindings = inputBindings ??= new InputBindingCollection(this);
            bindings.VerifyAccess();
            return bindings;
        }
    }

    internal bool InputBindingsClosed { get; private set; }
    internal InputBindingCollection? InputBindingsInternal => inputBindings;

    private void ReleaseInputBindings()
    {
        if (InputBindingsClosed) return;
        InputBindingsClosed = true;
        ReleaseCommandBindings();
        inputBindingResolver.Reset();
        inputBindings?.Release();
        inputBindings = null;
        adapter.ReleaseInputBindingsForSubtree();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        try
        {
            // Component can finalize a Form whose backend factory threw before WindowBase's
            // constructor ran. Finalizers must not touch its incomplete adapter or invoke managed
            // control/command callbacks on the finalizer thread, even for a completed constructor.
            if (disposing)
            {
                List<Exception>? failures = null;
                void Cleanup(Action action)
                {
                    try { action(); }
                    catch (Exception failure) { (failures ??= []).Add(failure); }
                }
                // Closing bindings makes this root ineligible before any LostFocus callback
                // can reenter Show/Select and resume its otherwise suspended focus scope.
                Cleanup(ReleaseInputBindings);
                Cleanup(() => adapter?.FindExistingFocusScope()?.SetSuspended(true));
                Cleanup(DetachInsetsProvider);
                Cleanup(() => renderSurface?.Dispose());
                if (failures?.Count == 1)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                if (failures?.Count > 1)
                    throw new AggregateException("Window focus and binding cleanup failed.", failures);
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
