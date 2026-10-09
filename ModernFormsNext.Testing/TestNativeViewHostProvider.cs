using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Testing;

/// <summary>Records native-host contracts without creating an operating-system peer.</summary>
/// <remarks>Deterministic TestHost simulation only; capability flags describe the simulated baseline.</remarks>
public sealed class TestNativeViewHostProvider : INativeViewHostProvider
{
    private readonly List<TestNativeViewSession> sessions = [];
    private readonly List<TestNativeViewSession> history = [];
    private readonly Func<bool> enabled;
    internal TestNativeViewHostProvider(Func<bool> enabled) { this.enabled = enabled; }
    /// <inheritdoc/>
    public NativeViewCapabilities Capabilities { get { CapabilityQueries++; return NativeViewCapabilities.Baseline; } }
    /// <summary>Gets backend capability reads, for detecting unnecessary idle-host queries.</summary>
    public int CapabilityQueries { get; private set; }
    /// <inheritdoc/>
    public bool IsAvailable { get; private set; } = true;
    /// <inheritdoc/>
    public bool IsVisible => IsAvailable;
    /// <inheritdoc/>
    public bool IsEnabled => IsAvailable && enabled();
    internal void NotifyState() => Changed?.Invoke();
    /// <inheritdoc/>
    public event Action? Changed;
    /// <summary>Gets all created sessions, including retired sessions, for lifecycle assertions.</summary>
    public IReadOnlyList<TestNativeViewSession> Sessions => history;
    /// <summary>Gets the number of returns to the simulated framework input surface.</summary>
    public int FrameworkFocusRequests { get; private set; }
    /// <summary>Simulates presentation retirement/recreation. Old leases are disposed before notification.</summary>
    public void SetAvailable(bool available)
    {
        if (IsAvailable == available) return;
        IsAvailable = available;
        if (!available) foreach (var session in sessions.ToArray()) session.Dispose();
        Changed?.Invoke();
    }
    /// <inheritdoc/>
    public INativeViewSession CreateSession(INativeViewFactory factory, INativeViewHostCallbacks callbacks)
    {
        if (!IsAvailable) throw new InvalidOperationException("The simulated presentation is unavailable.");
        var session = new TestNativeViewSession(this, callbacks);
        try { session.Attach(factory); }
        catch { session.Dispose(); throw; }
        sessions.Add(session); history.Add(session);
        return session;
    }
    /// <inheritdoc/>
    public void FocusFramework() => FrameworkFocusRequests++;
    internal void Remove(TestNativeViewSession session) => sessions.Remove(session);
    internal void Retire() { SetAvailable(false); Changed = null; }
}

/// <summary>Records one simulated session and exposes guarded native callback entry points.</summary>
public sealed class TestNativeViewSession : INativeViewSession, INativeViewSite
{
    private readonly TestNativeViewHostProvider provider;
    private readonly INativeViewHostCallbacks callbacks;
    private INativeViewPeer? peer;
    internal TestNativeViewSession(TestNativeViewHostProvider provider, INativeViewHostCallbacks callbacks)
    { this.provider = provider; this.callbacks = callbacks; }
    internal void Attach(INativeViewFactory factory) => peer = factory.CreatePeer(this);
    /// <inheritdoc/>
    public long Generation => callbacks.Generation;
    /// <inheritdoc/>
    public bool IsCurrent => !IsDisposed && provider.IsAvailable && callbacks.IsCurrent;
    /// <inheritdoc/>
    public bool IsFocused => IsCurrent && callbacks.IsFocused;
    /// <summary>Gets whether this lease has retired.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Gets the last applied geometry and effective state.</summary>
    public NativeViewPlacement Placement { get; private set; }
    /// <summary>Gets the number of changed composition updates.</summary>
    public int UpdateCount { get; private set; }
    /// <summary>Gets the number of shared-to-native focus requests.</summary>
    public int FocusRequests { get; private set; }
    /// <summary>Gets the generation of the native host in front of this one.</summary>
    public long? InFrontGeneration { get; private set; }
    /// <inheritdoc/>
    public bool TryFocus()
    {
        bool accepted = IsCurrent && callbacks.TryFocus();
        if (!accepted && IsCurrent) callbacks.RestoreFocus();
        return accepted;
    }
    /// <inheritdoc/>
    public bool MoveFocus(bool forward) => IsCurrent && callbacks.MoveFocus(forward);
    /// <inheritdoc/>
    public void RestoreFocus() { if (IsCurrent) callbacks.RestoreFocus(); }
    /// <inheritdoc/>
    public void Update(NativeViewPlacement placement)
    {
        if (!IsCurrent || (UpdateCount > 0 && Placement == placement)) return;
        Placement = placement; UpdateCount++;
        peer?.Resize(placement.PixelBounds.Size);
    }
    /// <inheritdoc/>
    public void RequestFocus() { if (IsCurrent) { FocusRequests++; peer?.RequestFocus(); } }
    /// <inheritdoc/>
    public void ReturnFocus() { if (IsCurrent) provider.FocusFramework(); }
    /// <inheritdoc/>
    public void PlaceAbove(INativeViewSession? previous)
        => InFrontGeneration = (previous as TestNativeViewSession)?.Generation;
    /// <summary>Revokes callbacks and disposes the peer lease exactly once.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        provider.Remove(this);
        var previous = peer; peer = null;
        previous?.Dispose();
    }
}
