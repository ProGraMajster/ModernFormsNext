using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Input.Platform;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.Testing;

/// <summary>Owns scoped platform service substitutes for a deterministic headless test host.</summary>
/// <remarks>
/// <para>
/// The existing platform registries resolve these services in the host execution context and
/// contexts captured from it. Native registrations are not replaced. Host disposal revokes all
/// overrides, including ones held by previously captured execution contexts.
/// </para>
/// <para>
/// Clipboard and lifecycle operations require the host UI thread. These substitutes exercise
/// production framework consumers; they provide no Windows, Android, or physical-device evidence.
/// The host owns their lifetime. Access to retained fake instances after disposal fails.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var host = ModernFormsTestHost.Create();
/// host.Services.Clipboard.SetTextAsync("copied text").GetAwaiter().GetResult();
/// host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Background);
/// host.Services.Lifecycle.SetState(PlatformApplicationLifecycleState.Foreground);
/// </code>
/// </example>
public sealed class TestPlatformServices
{
    private readonly UiTestDispatcher dispatcher;
    private readonly List<IDisposable> scopes = [];
    private readonly TestPlatformDispatcher platformDispatcher;
    private bool disposed;

    internal TestPlatformServices(UiTestDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        Clipboard = new TestClipboard(dispatcher);
        Lifecycle = new TestApplicationLifecycle(dispatcher);
        ThemeSettings = new TestThemeSettings(dispatcher);
        AnimationSettings = new TestAnimationSettings(dispatcher);
        Settings = new TestPlatformSettings(dispatcher);
        platformDispatcher = new TestPlatformDispatcher(dispatcher);
        try
        {
            scopes.Add(AvaloniaGlobals.PushServiceForTesting<IClipboard>(Clipboard));
            scopes.Add(AvaloniaGlobals.PushServiceForTesting<IPlatformSettings>(Settings));
            scopes.Add(PlatformServiceRegistry.PushServiceForTesting<IPlatformApplicationLifecycle>(Lifecycle));
            scopes.Add(PlatformServiceRegistry.PushServiceForTesting<IPlatformThemeSettings>(ThemeSettings));
            scopes.Add(PlatformServiceRegistry.PushServiceForTesting<IPlatformAnimationSettings>(AnimationSettings));
            scopes.Add(PlatformServiceRegistry.PushServiceForTesting<IPlatformDispatcher>(platformDispatcher));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the isolated clipboard used by production clipboard and text-editing APIs.</summary>
    public TestClipboard Clipboard { get; }

    /// <summary>Gets the controllable production foreground/background lifecycle service.</summary>
    public TestApplicationLifecycle Lifecycle { get; }

    /// <summary>Gets platform theme preferences read by the production ThemeManager.</summary>
    public TestThemeSettings ThemeSettings { get; }

    /// <summary>Gets deterministic motion preferences consumed by the production animation scheduler.</summary>
    public TestAnimationSettings AnimationSettings { get; }

    /// <summary>Gets settings with optional detected contrast and text-scale preferences.</summary>
    public TestPlatformSettings Settings { get; }

    internal void Dispose()
    {
        if (disposed)
            return;
        dispatcher.VerifyAccess();
        disposed = true;
        // Revoke lookup before clearing fake state. Holders clear their service references, so a
        // captured async context cannot resurrect these fakes or retain the host through them.
        for (var index = scopes.Count - 1; index >= 0; index--)
            scopes[index].Dispose();
        scopes.Clear();
        platformDispatcher.Dispose();
        Lifecycle.Dispose();
        Clipboard.Dispose();
        ThemeSettings.Dispose();
        AnimationSettings.Dispose();
        Settings.Dispose();
    }

    private sealed class TestPlatformDispatcher(UiTestDispatcher dispatcher) : IPlatformDispatcher
    {
        private readonly object gate = new();
        private readonly HashSet<Action> pending = [];
        private bool disposed;

        public bool CheckAccess()
        {
            ThrowIfDisposed();
            return dispatcher.CheckAccess();
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ThrowIfDisposed();
            dispatcher.Post(() =>
            {
                if (!disposed)
                    action();
            });
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            return InvokeAsync(() => { action(); return true; }, cancellationToken);
        }

        public Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(function);
            ThrowIfDisposed();
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);
            if (dispatcher.CheckAccess())
            {
                try { return Task.FromResult(function()); }
                catch (Exception exception) { return Task.FromException<T>(exception); }
            }

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action cancel = () => completion.TrySetException(new ObjectDisposedException(nameof(TestPlatformServices)));
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                pending.Add(cancel);
                try
                {
                    dispatcher.Post(() =>
                    {
                        lock (gate)
                        {
                            if (!pending.Remove(cancel))
                                return;
                        }
                        if (cancellationToken.IsCancellationRequested)
                            completion.TrySetCanceled(cancellationToken);
                        else
                        {
                            try { completion.TrySetResult(function()); }
                            catch (Exception exception) { completion.TrySetException(exception); }
                        }
                    });
                }
                catch
                {
                    pending.Remove(cancel);
                    throw;
                }
            }
            return completion.Task;
        }

        internal void Dispose()
        {
            Action[] abandoned;
            lock (gate)
            {
                disposed = true;
                abandoned = pending.ToArray();
                pending.Clear();
            }
            foreach (var cancel in abandoned)
                cancel();
        }

        private void ThrowIfDisposed()
        {
            lock (gate)
                ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}

/// <summary>Controls the existing animation-settings provider without observing native preferences.</summary>
/// <remarks>
/// The initial snapshot permits animation with no reduced motion and duration scale 1. Snapshot
/// reads and refresh are thread-safe and perform no I/O. Changes require the host UI thread and
/// raise the existing production event outside the implementation lock. Timestamps remain null;
/// this provider reports test settings, not a native platform observation.
/// </remarks>
public sealed class TestAnimationSettings : IPlatformAnimationSettings
{
    private readonly UiTestDispatcher dispatcher;
    private readonly object gate = new();
    private PlatformAnimationSettingsSnapshot current = CreateSnapshot(false, true, 1d);
    private EventHandler<PlatformAnimationSettingsChangedEventArgs>? changed;
    private bool disposed;

    internal TestAnimationSettings(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <inheritdoc/>
    public PlatformAnimationSettingsSnapshot Current
    {
        get
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return current;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<PlatformAnimationSettingsChangedEventArgs>? Changed
    {
        add
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                changed += value;
            }
        }
        remove { lock (gate) changed -= value; }
    }

    /// <inheritdoc/>
    /// <remarks>Returns the current immutable snapshot without reading a clock or native service.</remarks>
    public PlatformAnimationSettingsSnapshot Refresh() => Current;

    /// <summary>Publishes deterministic motion preferences to existing scheduler subscribers.</summary>
    /// <param name="reducedMotion">Whether animations should respect a reduced-motion request.</param>
    /// <param name="animationsEnabled">Whether platform policy permits animations.</param>
    /// <param name="durationScale">A finite, nonnegative multiplier; zero means endpoint completion.</param>
    /// <remarks>
    /// Call on the host UI thread. Equal values raise no event. State changes before callbacks;
    /// observer exceptions propagate without reverting the published snapshot.
    /// </remarks>
    public void SetPreferences(bool reducedMotion = false, bool animationsEnabled = true, double durationScale = 1d)
    {
        dispatcher.VerifyAccess();
        var replacement = CreateSnapshot(reducedMotion, animationsEnabled, durationScale);
        PlatformAnimationSettingsSnapshot previous;
        EventHandler<PlatformAnimationSettingsChangedEventArgs>? handlers;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (current.ReducedMotion == reducedMotion && current.AnimationsEnabled == animationsEnabled && current.DurationScale == durationScale)
                return;
            previous = current;
            current = replacement;
            handlers = changed;
        }
        handlers?.Invoke(this, new PlatformAnimationSettingsChangedEventArgs(previous, replacement));
    }

    internal void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            changed = null;
        }
    }

    private static PlatformAnimationSettingsSnapshot CreateSnapshot(bool reducedMotion, bool animationsEnabled, double durationScale)
        => new("HeadlessTestHost", reducedMotion, animationsEnabled, durationScale, null, false, PlatformAnimationProviderState.Ready, null);
}

/// <summary>Supplies controllable values for the existing platform theme-settings contract.</summary>
/// <remarks>
/// The default color scheme is Light and reduced motion is false. Changes are read on the next
/// production theme application; they do not invent system-change notifications or automatically
/// reapply themes. Read and change preferences on the host UI thread.
/// </remarks>
public sealed class TestThemeSettings : IPlatformThemeSettings
{
    private readonly UiTestDispatcher dispatcher;
    private PlatformColorScheme preferredVariant = PlatformColorScheme.Light;
    private bool? reducedMotion = false;
    private bool disposed;

    internal TestThemeSettings(UiTestDispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <summary>Gets or sets the light/dark preference read on the next system-theme resolution.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The scheme is not a defined enum value.</exception>
    public PlatformColorScheme PreferredVariant
    {
        get { VerifyAccess(); return preferredVariant; }
        set
        {
            VerifyAccess();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            preferredVariant = value;
        }
    }

    /// <summary>Gets or sets the reduced-motion preference; null represents an unavailable setting.</summary>
    public bool? ReducedMotion
    {
        get { VerifyAccess(); return reducedMotion; }
        set { VerifyAccess(); reducedMotion = value; }
    }

    /// <inheritdoc/>
    public PlatformColorScheme GetPreferredVariant() => PreferredVariant;

    /// <inheritdoc/>
    public bool? GetReducedMotion() => ReducedMotion;

    internal void Dispose()
    {
        if (disposed)
            return;
        dispatcher.VerifyAccess();
        disposed = true;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispatcher.VerifyAccess();
    }
}
