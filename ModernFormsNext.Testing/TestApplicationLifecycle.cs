using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.Testing;

/// <summary>Controls the existing platform foreground/background lifecycle in a headless host.</summary>
/// <remarks>
/// This fake implements the production lifecycle contract; it does not simulate activation,
/// Android Activity recreation, or native application events. Changes run synchronously on the
/// host UI thread. The default state is <see cref="PlatformApplicationLifecycleState.Foreground"/>.
/// </remarks>
public sealed class TestApplicationLifecycle : IPlatformApplicationLifecycle
{
    private readonly UiTestDispatcher dispatcher;
    private PlatformApplicationLifecycleState state = PlatformApplicationLifecycleState.Foreground;
    private EventHandler<PlatformApplicationLifecycleChangedEventArgs>? stateChanged;
    private bool disposed;

    internal TestApplicationLifecycle(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <inheritdoc/>
    public PlatformApplicationLifecycleState State
    {
        get { VerifyAccess(); return state; }
    }

    /// <inheritdoc/>
    /// <remarks>Subscribers observe the new state. Reassigning the same state raises no event.</remarks>
    public event EventHandler<PlatformApplicationLifecycleChangedEventArgs>? StateChanged
    {
        add { VerifyAccess(); stateChanged += value; }
        remove { if (!disposed) { dispatcher.VerifyAccess(); stateChanged -= value; } }
    }

    /// <summary>Publishes a state change through the production platform lifecycle event.</summary>
    /// <param name="value">An existing platform lifecycle state.</param>
    /// <remarks>
    /// Call on the host UI thread. The state changes before callbacks run. Callback failures
    /// propagate to the caller without reverting the published state; no native event is raised.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined lifecycle state.</exception>
    public void SetState(PlatformApplicationLifecycleState value)
    {
        VerifyAccess();
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (state == value)
            return;

        var previous = state;
        state = value;
        stateChanged?.Invoke(this, new PlatformApplicationLifecycleChangedEventArgs(previous, value));
    }

    internal void Dispose()
    {
        if (disposed)
            return;
        dispatcher.VerifyAccess();
        disposed = true;
        stateChanged = null;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispatcher.VerifyAccess();
    }
}
