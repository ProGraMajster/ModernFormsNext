using System.Collections.ObjectModel;
using ModernFormsNext.Animations;

namespace ModernFormsNext.Testing;

/// <summary>Owns a deterministic headless ModernFormsNext application context for automated tests.</summary>
/// <remarks>
/// <para>
/// The host substitutes only WindowKit's top-level implementation and UI dispatcher. Controls,
/// Forms, layout engines, data binding, resources, theme resolution, invalidation, and animation
/// ownership continue through production framework code.
/// </para>
/// <para>
/// One host may own multiple windows, but only one host may be active in a process. Tests using this
/// type must be serialized because ModernFormsNext application and dispatcher state is process-wide.
/// This host does not replace Windows/Android end-to-end validation.
/// </para>
/// </remarks>
public sealed class ModernFormsTestHost : IDisposable
{
    private static int activeHost;
    private readonly List<TestWindowHost> windows = [];
    private readonly List<HeadlessWindowImpl> createdBackends = [];
    private readonly HashSet<Form> baselineForms;
    private readonly KeyValuePair<object, object?>[] baselineApplicationResources;
    private readonly ThemeDefinition baselineTheme;
    private readonly IDisposable windowFactoryScope;
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private bool disposed;

    private ModernFormsTestHost(TestViewport defaultViewport)
    {
        DefaultViewport = defaultViewport;
        Dispatcher = new UiTestDispatcher();
        try
        {
            windowFactoryScope = TestWindowFactoryScope.Push(CreateHeadlessWindow);
            baselineForms = Application.OpenForms.ToHashSet();
            baselineApplicationResources = Application.Resources.ToArray();
            baselineTheme = Dispatcher.Run(() => ThemeManager.Current.ActiveTheme ?? BuiltInThemes.Light);
        }
        catch
        {
            Dispatcher.Dispose();
            throw;
        }
    }

    /// <summary>Gets the deterministic UI dispatcher owned by this host.</summary>
    public UiTestDispatcher Dispatcher { get; }

    /// <summary>Gets the default viewport used by Show overloads without explicit dimensions.</summary>
    public TestViewport DefaultViewport { get; }

    /// <summary>Gets a detached list of windows currently owned by this host.</summary>
    public IReadOnlyList<TestWindowHost> Windows => new ReadOnlyCollection<TestWindowHost>(windows.ToArray());

    /// <summary>Creates a host with an 800 by 600 logical viewport at 100 percent scale.</summary>
    /// <returns>The new deterministic headless host.</returns>
    public static ModernFormsTestHost Create() => Create(new TestViewport(800, 600));

    /// <summary>Creates a host with a caller-controlled default viewport and render scale.</summary>
    /// <param name="defaultViewport">The viewport used by Show overloads without dimensions.</param>
    /// <returns>The new deterministic headless host.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultViewport"/> is the invalid default struct value.</exception>
    /// <exception cref="InvalidOperationException">Another host is already active in this process.</exception>
    public static ModernFormsTestHost Create(TestViewport defaultViewport)
    {
        defaultViewport.Validate(nameof(defaultViewport));
        if (Interlocked.CompareExchange(ref activeHost, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Only one ModernFormsTestHost may be active in a process. Serialize tests that use the headless host.");
        }

        try
        {
            return new ModernFormsTestHost(defaultViewport);
        }
        catch
        {
            Interlocked.Exchange(ref activeHost, 0);
            throw;
        }
    }

    /// <summary>Hosts a Form using the default viewport.</summary>
    /// <param name="form">A Form constructed after this host was created.</param>
    /// <returns>The hosted window handle.</returns>
    public TestWindowHost Show(Form form) => Show(form, DefaultViewport);

    /// <summary>Hosts a Form with explicit logical dimensions and 100 percent scale.</summary>
    /// <param name="form">A Form constructed after this host was created.</param>
    /// <param name="width">The logical viewport width.</param>
    /// <param name="height">The logical viewport height.</param>
    /// <returns>The hosted window handle.</returns>
    public TestWindowHost Show(Form form, int width, int height) => Show(form, new TestViewport(width, height));

    /// <summary>Hosts a Form with a complete deterministic viewport.</summary>
    /// <param name="form">A Form constructed after this host was created.</param>
    /// <param name="viewport">The logical size and render scale.</param>
    /// <returns>The hosted window handle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="viewport"/> is the invalid default struct value.</exception>
    public TestWindowHost Show(Form form, TestViewport viewport)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(form);
        viewport.Validate(nameof(viewport));
        return Dispatcher.Run(() => ShowCore(form, controlRoot: null, viewport));
    }

    /// <summary>Hosts a UserControl, Panel, or other control root using the default viewport.</summary>
    /// <param name="root">The unparented control root to host.</param>
    /// <returns>The hosted window handle.</returns>
    public TestWindowHost Show(Control root) => Show(root, DefaultViewport);

    /// <summary>Hosts a control root with explicit logical dimensions and 100 percent scale.</summary>
    /// <param name="root">The unparented control root to host.</param>
    /// <param name="width">The logical viewport width.</param>
    /// <param name="height">The logical viewport height.</param>
    /// <returns>The hosted window handle.</returns>
    public TestWindowHost Show(Control root, int width, int height) => Show(root, new TestViewport(width, height));

    /// <summary>Hosts a control root through an internal undecorated real Form wrapper.</summary>
    /// <param name="root">The unparented UserControl, Panel, or other framework control.</param>
    /// <param name="viewport">The logical size and render scale.</param>
    /// <returns>The hosted window handle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="viewport"/> is the invalid default struct value.</exception>
    public TestWindowHost Show(Control root, TestViewport viewport)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);
        viewport.Validate(nameof(viewport));
        if (root.Parent is not null)
            throw new ArgumentException("A headless control root must not already have a parent.", nameof(root));

        return Dispatcher.Run(() =>
        {
            var wrapper = new Form
            {
                Name = $"{(string.IsNullOrWhiteSpace(root.Name) ? root.GetType().Name : root.Name)}TestWindow",
                StartPosition = FormStartPosition.Manual,
                UseSystemDecorations = true
            };
            wrapper.Controls.Add(root);
            return ShowCore(wrapper, root, viewport);
        });
    }

    /// <summary>Runs one explicit production layout pass for every hosted window.</summary>
    public void PerformLayout()
    {
        ThrowIfDisposed();
        foreach (TestWindowHost window in windows.ToArray())
            window.PerformLayout();
    }

    /// <summary>Runs every hosted tree until layout and queued UI work are stable.</summary>
    /// <param name="maximumPasses">The per-window layout pass limit.</param>
    public void LayoutUntilStable(int maximumPasses = 16)
    {
        ThrowIfDisposed();
        foreach (TestWindowHost window in windows.ToArray())
            window.LayoutUntilStable(maximumPasses);
    }

    /// <summary>Resizes the first hosted window, matching the common one-window test workflow.</summary>
    /// <param name="width">The new logical width.</param>
    /// <param name="height">The new logical height.</param>
    public void Resize(int width, int height) => GetPrimaryWindow().Resize(width, height);

    /// <summary>Captures the first hosted window's detached control tree.</summary>
    /// <returns>The primary window snapshot.</returns>
    public ControlTreeSnapshot CaptureTree() => GetPrimaryWindow().CaptureTree();

    /// <summary>Drains and lays out every hosted window until all Phase 1 work is stable.</summary>
    public void ProcessPendingWork()
    {
        ThrowIfDisposed();
        Dispatcher.Drain();
        foreach (TestWindowHost window in windows.ToArray())
            window.ProcessPendingWork();
        Dispatcher.Drain();
    }

    /// <summary>Captures host, dispatcher, invalidation, animation, and control-tree diagnostics.</summary>
    /// <returns>A detached diagnostic snapshot.</returns>
    public TestHostDiagnostics GetDiagnostics()
    {
        ThrowIfDisposed();
        return Dispatcher.Run(() => new TestHostDiagnostics(
            windows.Count,
            Dispatcher.PendingWorkCount,
            windows.Sum(window => window.Backend.PendingInvalidationCount),
            AnimationScheduler.GetDefaultDiagnosticsIfInitialized()?.ActiveAnimationCount ?? 0,
            windows.Where(window => !window.IsClosed).Select(window => window.CaptureTree()),
            Dispatcher.UnhandledExceptions));
    }

    /// <summary>Closes every hosted window while keeping the host available for another test tree.</summary>
    /// <exception cref="AggregateException">One or more application close handlers or cleanup steps failed.</exception>
    public void Close()
    {
        ThrowIfDisposed();
        var failures = new List<Exception>();
        foreach (TestWindowHost window in windows.ToArray())
            TryCleanup(window.Close, failures);
        TryCleanup(() => Dispatcher.Drain(), failures);

        if (failures.Count > 0)
            throw new AggregateException("One or more deterministic ModernFormsNext test windows failed to close cleanly.", failures);
    }

    /// <summary>
    /// Closes all trees, drains pending work, restores process state, and removes all testing scopes.
    /// </summary>
    /// <exception cref="InvalidOperationException">The host is disposed from a thread other than its owner thread.</exception>
    /// <exception cref="AggregateException">One or more cleanup steps failed; independent restoration steps still ran.</exception>
    public void Dispose()
    {
        if (disposed)
            return;
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("ModernFormsTestHost must be disposed on the thread that created it.");

        var failures = new List<Exception>();
        TryCleanup(Close, failures);
        TryCleanup(DisposeUntrackedHeadlessForms, failures);
        TryCleanup(() =>
        {
            foreach (HeadlessWindowImpl backend in createdBackends)
            {
                if (!backend.IsDisposed)
                    backend.Dispose();
            }
        }, failures);
        TryCleanup(() => RestoreThemeAndResources(), failures);
        TryCleanup(() => Dispatcher.Drain(), failures);
        TryCleanup(() => windowFactoryScope.Dispose(), failures);
        TryCleanup(() => Dispatcher.Dispose(), failures);

        disposed = true;
        Interlocked.Exchange(ref activeHost, 0);

        if (failures.Count > 0)
            throw new AggregateException("The deterministic ModernFormsNext test host reported cleanup failures.", failures);
    }

    internal void NotifyWindowClosed(TestWindowHost window) => windows.Remove(window);

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private HeadlessWindowImpl CreateHeadlessWindow()
    {
        var backend = new HeadlessWindowImpl(DefaultViewport);
        createdBackends.Add(backend);
        return backend;
    }

    private TestWindowHost ShowCore(Form form, Control? controlRoot, TestViewport viewport)
    {
        if (form.window is not HeadlessWindowImpl backend || !createdBackends.Contains(backend))
        {
            throw new InvalidOperationException(
                "The Form was not constructed inside this ModernFormsTestHost. Create the host before constructing Forms.");
        }
        if (backend.IsDisposed || backend.IsShown)
            throw new InvalidOperationException("The Form's headless window has already been shown or disposed.");

        var window = new TestWindowHost(this, form, backend, controlRoot, viewport);
        windows.Add(window);
        try
        {
            window.Show();
            return window;
        }
        catch
        {
            window.Close();
            throw;
        }
    }

    private TestWindowHost GetPrimaryWindow()
    {
        ThrowIfDisposed();
        if (windows.Count == 0)
            throw new InvalidOperationException("The test host does not own a window. Call Show first.");
        return windows[0];
    }

    private void RestoreThemeAndResources()
    {
        ThemeApplyResult result = ThemeManager.Current.Apply(
            baselineTheme,
            new ThemeApplyOptions
            {
                Transition = new ThemeTransitionOptions { Enabled = false }
            });
        if (!result.Success)
            throw new InvalidOperationException("The test host could not restore the active theme after disposal.");

        Application.Resources.Clear();
        foreach ((object key, object? value) in baselineApplicationResources)
            Application.Resources.Add(key, value);
    }

    private void DisposeUntrackedHeadlessForms()
    {
        foreach (Form form in Application.OpenForms.ToArray())
        {
            if (baselineForms.Contains(form) || form.window is not HeadlessWindowImpl backend || !createdBackends.Contains(backend))
                continue;

            Application.OpenForms.Remove(form);
            form.adapter.CancelOwnedControlAnimationsForSubtree();
            if (!backend.IsDisposed)
                backend.Dispose();
            form.adapter.Dispose();
            form.Dispose();
        }
    }

    private static void TryCleanup(Action action, ICollection<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }
}
