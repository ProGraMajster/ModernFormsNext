namespace ModernFormsNext;

public partial class AppShell
{
    /// <summary>Explicitly attaches this shell to a borrowed application lifecycle facade.</summary>
    /// <param name="lifecycle">The existing current Application.Lifecycle facade, never disposed by the binding.</param>
    /// <param name="linkBase">An optional absolute URI base ending in a slash; only this origin/path accepts URI or Protocol activation.</param>
    /// <param name="stateKey">The explicit bounded snapshot namespace, such as main.</param>
    /// <param name="includeState">Whether to supply state when Data is null and attempt restoration into an empty shell.</param>
    /// <returns>An owned subscription lease; dispose it on the creating UI thread before replacing the provider.</returns>
    /// <remarks>
    /// Opt-in only; constructors never subscribe. At most one lease is attached per shell. The lease
    /// uses the existing dispatcher and rich notifications, including read-only provider overrides.
    /// It never registers protocols, forwards IPC, persists data, replaces services or activates a window.
    /// Earlier application state assignments and later subscriber overrides remain authoritative.
    /// Inspect LastOperation on the returned lease for the outcome of automatic delivery.
    /// </remarks>
    public AppShellLifecycleBinding AttachLifecycle(ApplicationLifecycle lifecycle, Uri? linkBase, string stateKey, bool includeState = false)
    {
        VerifyPageAccess();
        ObjectDisposedException.ThrowIf(retiring, this);
        ArgumentNullException.ThrowIfNull(lifecycle);
        StatePrefix(stateKey);
        if (linkBase is null && !includeState) throw new ArgumentException("A shell binding needs an explicit link base or state integration.", nameof(linkBase));
        if (lifecycleBindings.Count != 0) throw new InvalidOperationException("Dispose the existing shell lifecycle binding before attaching another.");
        var binding = new AppShellLifecycleBinding(this, lifecycle, linkBase, stateKey, includeState);
        lifecycleBindings.Add(binding);
        return binding;
    }

    // A queued startup activation may survive its first mount. Later user navigation has a
    // separate intent stamp so a hide/reparent callback cannot be confused with user approval.
    internal long UserNavigationVersion => userIntentVersion;
    internal long PresentationVersion => mountVersion;
    internal bool IsRetiringShell => retiring;
    internal void RemoveLifecycleBinding(AppShellLifecycleBinding binding) => lifecycleBindings.Remove(binding);
}
