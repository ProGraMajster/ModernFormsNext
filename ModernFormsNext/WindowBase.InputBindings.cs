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
        inputBindingResolver.Reset();
        inputBindings?.Release();
        inputBindings = null;
        adapter.ReleaseInputBindingsForSubtree();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        ReleaseInputBindings();
        base.Dispose(disposing);
    }
}
