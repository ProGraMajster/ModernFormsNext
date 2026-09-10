using System.Collections.ObjectModel;
using ModernFormsNext.Animations;

namespace ModernFormsNext.Testing;

/// <summary>Owns a deterministic headless ModernFormsNext application context for automated tests.</summary>
/// <remarks>
/// <para>
/// The host scopes WindowKit window/dispatcher services and controlled platform services, and
/// supplies time to the real animation scheduler. Controls, Forms, layout engines, data binding,
/// resources, theme resolution, invalidation, and animation ownership use production framework code.
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
    private readonly IDisposable applicationRuntimeScope;
    private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
    private bool disposed;
    private bool disposing;
    private bool disposalRequested;
    private bool closingWindows;
    private int activeWindowClosures;

    private ModernFormsTestHost(TestViewport defaultViewport)
    {
        DefaultViewport = defaultViewport;
        Dispatcher = new UiTestDispatcher();
        try
        {
            applicationRuntimeScope = Application.PushRuntimeStateForTesting();
            Services = new TestPlatformServices(Dispatcher);
            Clock = new TestClock(Dispatcher);
            windowFactoryScope = TestWindowFactoryScope.Push(CreateHeadlessWindow);
            baselineForms = Application.OpenForms.ToHashSet();
            baselineApplicationResources = Application.Resources.ToArray();
            baselineTheme = Dispatcher.Run(() => ThemeManager.Current.ActiveTheme ?? BuiltInThemes.Light);
        }
        catch (Exception creationFailure)
        {
            // Each acquired scope must be revoked even if another cleanup callback fails.
            var failures = new List<Exception> { creationFailure };
            TryCleanup(() => windowFactoryScope?.Dispose(), failures);
            TryCleanup(() => Clock?.Dispose(), failures);
            TryCleanup(() => applicationRuntimeScope?.Dispose(), failures);
            TryCleanup(() => Services?.Dispose(), failures);
            TryCleanup(Dispatcher.Dispose, failures);
            if (failures.Count > 1)
                throw new AggregateException("The test host could not be created or restored cleanly.", failures);
            throw;
        }
    }

    /// <summary>Gets the deterministic UI dispatcher owned by this host.</summary>
    public UiTestDispatcher Dispatcher { get; }

    /// <summary>Gets the manually advanced clock driving the production animation scheduler.</summary>
    public TestClock Clock { get; }

    /// <summary>Gets controlled platform services scoped to this host without using native OS services.</summary>
    public TestPlatformServices Services { get; }

    /// <summary>Gets the first hosted window's production input helpers.</summary>
    /// <remarks>Use <see cref="TestWindowHost.Input"/> when testing more than one window.</remarks>
    public TestInput Input => GetPrimaryWindow().Input;

    /// <summary>Gets the first hosted window's canonical focused control.</summary>
    public Control? FocusedControl => GetPrimaryWindow().FocusedControl;

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

    /// <summary>Hosts a modal Form through the production dialog path using the default viewport.</summary>
    /// <param name="dialog">An unshown Form constructed inside this host.</param>
    /// <param name="owner">A live, shown, enabled window belonging to this host.</param>
    /// <returns>The dialog window, whose <see cref="TestWindowHost.DialogCompletion"/> exposes its production result task.</returns>
    /// <remarks>
    /// Call on the host UI thread. The production Form disables its owner until the dialog closes.
    /// Use the returned window's Input to exercise the dialog; no nested message loop or OS activation is simulated.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var host = ModernFormsTestHost.Create();
    /// var owner = host.Show(new Form());
    /// var form = new Form();
    /// var dialog = host.ShowDialog(form, owner);
    /// form.DialogResult = DialogResult.OK;
    /// Assert.Equal(DialogResult.OK, await dialog.DialogCompletion!);
    /// </code>
    /// </example>
    public TestWindowHost ShowDialog(Form dialog, TestWindowHost owner)
        => ShowDialog(dialog, owner, DefaultViewport);

    /// <summary>Hosts a modal Form with a controlled viewport using <see cref="Form.ShowDialog(Form)"/>.</summary>
    /// <param name="dialog">An unshown Form constructed inside this host.</param>
    /// <param name="owner">A live, shown, enabled window belonging to this host.</param>
    /// <param name="viewport">The dialog's logical dimensions and render scale.</param>
    /// <returns>The tracked dialog window and its production completion task.</returns>
    /// <exception cref="ArgumentException">The owner is not a window belonging to this host, or the dialog is its own owner.</exception>
    /// <exception cref="InvalidOperationException">The owner cannot accept a modal dialog, or the dialog is already hosted, shown, or disposed.</exception>
    /// <remarks>
    /// All ownership checks run before mutating a Form or disabling its owner. For a nested dialog,
    /// pass the currently enabled dialog window as owner. A pre-set DialogResult completes without
    /// showing the Form, matching production behavior; the host retains its tree until cleanup.
    /// </remarks>
    public TestWindowHost ShowDialog(Form dialog, TestWindowHost owner, TestViewport viewport)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(owner);
        viewport.Validate(nameof(viewport));
        Dispatcher.VerifyAccess();
        if (!windows.Contains(owner))
            throw new ArgumentException("The modal owner does not belong to this ModernFormsTestHost.", nameof(owner));
        if (ReferenceEquals(dialog, owner.HostedForm))
            throw new ArgumentException("A dialog cannot own itself.", nameof(dialog));
        if (owner.IsClosed || !owner.Backend.IsShown || !owner.Backend.IsEnabled)
            throw new InvalidOperationException("The modal owner must be a live, shown, enabled window.");
        ValidateUnhostedForm(dialog);
        return Dispatcher.Run(() => ShowCore(dialog, controlRoot: null, viewport, owner));
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
        foreach (TestWindowHost window in windows.Where(window => !window.IsClosed).ToArray())
            window.PerformLayout();
    }

    /// <summary>Runs every hosted tree until layout and queued UI work are stable.</summary>
    /// <param name="maximumPasses">The per-window layout pass limit.</param>
    public void LayoutUntilStable(int maximumPasses = 16)
    {
        ThrowIfDisposed();
        foreach (TestWindowHost window in windows.Where(window => !window.IsClosed).ToArray())
            window.LayoutUntilStable(maximumPasses);
    }

    /// <summary>Resizes the first hosted window, matching the common one-window test workflow.</summary>
    /// <param name="width">The new logical width.</param>
    /// <param name="height">The new logical height.</param>
    public void Resize(int width, int height) => GetPrimaryWindow().Resize(width, height);

    /// <summary>Captures the first hosted window's detached control tree.</summary>
    /// <returns>The primary window snapshot.</returns>
    public ControlTreeSnapshot CaptureTree() => GetPrimaryWindow().CaptureTree();

    /// <summary>Drains ready dispatcher work and stabilizes every hosted window's layout.</summary>
    public void ProcessPendingWork()
    {
        ThrowIfDisposed();
        Dispatcher.Drain();
        foreach (TestWindowHost window in windows.Where(window => !window.IsClosed).ToArray())
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
            Dispatcher.UnhandledExceptions,
            windows.Where(window => !window.IsClosed).Select(window => window.FocusedControl?.Name ?? string.Empty),
            windows.Where(window => !window.IsClosed).SelectMany(window => window.Input.RecentEvents)));
    }

    /// <summary>Closes every hosted window while keeping the host available for another test tree.</summary>
    /// <remarks>
    /// Reentrant Close calls do not repeat application close callbacks. If a close callback requests
    /// host disposal, disposal completes after the outermost close operation releases its windows.
    /// </remarks>
    /// <exception cref="AggregateException">One or more application close handlers or cleanup steps failed.</exception>
    public void Close()
    {
        if (closingWindows)
            return;
        ThrowIfDisposed();
        Dispatcher.VerifyAccess();
        CloseWindowsCore();
    }

    private void CloseWindowsCore()
    {
        if (closingWindows)
            return;

        var failures = new List<Exception>();
        closingWindows = true;
        try
        {
            foreach (TestWindowHost window in windows.ToArray())
                TryCleanup(window.Close, failures);
            TryCleanup(() => Dispatcher.Drain(), failures);
        }
        finally
        {
            closingWindows = false;
        }

        if (disposalRequested && activeWindowClosures == 0 && !disposing)
            TryCleanup(Dispose, failures);

        if (failures.Count > 0)
            throw new AggregateException("One or more deterministic ModernFormsNext test windows failed to close cleanly.", failures);
    }

    /// <summary>
    /// Closes all trees, drains pending work, restores process state, and removes all testing scopes.
    /// </summary>
    /// <remarks>
    /// Reentrant calls during disposal are no-ops. When called from an explicit window or host
    /// Close callback, disposal is deferred until that close operation finishes; the dispatcher,
    /// scheduler and services remain alive while application close callbacks unwind. Cleanup
    /// failures are reported by the outermost operation after independent restoration steps run.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The host is disposed from a thread other than its owner thread.</exception>
    /// <exception cref="AggregateException">One or more cleanup steps failed; independent restoration steps still ran.</exception>
    public void Dispose()
    {
        if (disposed)
            return;
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("ModernFormsTestHost must be disposed on the thread that created it.");
        if (disposing)
            return;
        if (activeWindowClosures > 0 || closingWindows)
        {
            disposalRequested = true;
            return;
        }

        disposing = true;
        disposalRequested = false;
        var failures = new List<Exception>();
        try
        {
            TryCleanup(CloseWindowsCore, failures);
            TryCleanup(DisposeUntrackedHeadlessForms, failures);
            foreach (HeadlessWindowImpl backend in createdBackends)
            {
                TryCleanup(() =>
                {
                    if (!backend.IsDisposed)
                        backend.Dispose();
                }, failures);
            }
            TryCleanup(RestoreThemeAndResources, failures);
            TryCleanup(() => Dispatcher.Drain(), failures);
            TryCleanup(() => Clock.Dispose(), failures);
            TryCleanup(applicationRuntimeScope.Dispose, failures);
            TryCleanup(() => Services.Dispose(), failures);
            TryCleanup(() => windowFactoryScope.Dispose(), failures);
            TryCleanup(() => Dispatcher.Dispose(), failures);
        }
        finally
        {
            disposed = true;
            disposing = false;
            Interlocked.Exchange(ref activeHost, 0);
        }

        if (failures.Count > 0)
            throw new AggregateException("The deterministic ModernFormsNext test host reported cleanup failures.", failures);
    }

    internal void NotifyWindowClosed(TestWindowHost window) => windows.Remove(window);

    internal void CloseOwnedDialogs(TestWindowHost owner)
    {
        var failures = new List<Exception>();
        // Release descendants before their parent backend. Form.Close can then reactivate its
        // still-live owner through the canonical modal cleanup path, including nested dialogs.
        foreach (TestWindowHost dialog in windows.Where(window => ReferenceEquals(window.ModalOwner, owner)).ToArray())
            TryCleanup(dialog.Close, failures);
        if (failures.Count > 0)
            throw new AggregateException("One or more owned test dialogs failed to close cleanly.", failures);
    }

    internal void BeginWindowClose() => activeWindowClosures++;

    internal void EndWindowClose()
    {
        activeWindowClosures--;
        if (activeWindowClosures == 0 && disposalRequested && !closingWindows && !disposing)
            Dispose();
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed || disposing || disposalRequested, this);

    private HeadlessWindowImpl CreateHeadlessWindow()
    {
        ThrowIfDisposed();
        var backend = new HeadlessWindowImpl(DefaultViewport);
        createdBackends.Add(backend);
        return backend;
    }

    private HeadlessWindowImpl ValidateUnhostedForm(Form form)
    {
        if (form.window is not HeadlessWindowImpl backend || !createdBackends.Contains(backend))
        {
            throw new InvalidOperationException(
                "The Form was not constructed inside this ModernFormsTestHost. Create the host before constructing Forms.");
        }
        if (backend.IsDisposed || backend.IsShown || windows.Any(window => ReferenceEquals(window.HostedForm, form)))
            throw new InvalidOperationException("The Form's headless window has already been shown or disposed.");
        return backend;
    }

    private TestWindowHost ShowCore(Form form, Control? controlRoot, TestViewport viewport, TestWindowHost? modalOwner = null)
    {
        HeadlessWindowImpl backend = ValidateUnhostedForm(form);
        var window = new TestWindowHost(this, form, backend, controlRoot, viewport, modalOwner);
        windows.Add(window);
        try
        {
            if (modalOwner is null)
                window.Show();
            else
                window.ShowDialog();
            return window;
        }
        catch (Exception showFailure)
        {
            try { window.Close(); }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("The test window failed to show and cleanup also reported a failure.", showFailure, cleanupFailure);
            }
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
        var failures = new List<Exception>();
        TryCleanup(() =>
        {
            ThemeApplyResult result = ThemeManager.Current.Apply(
                baselineTheme,
                new ThemeApplyOptions
                {
                    Transition = new ThemeTransitionOptions { Enabled = false }
                });
            if (!result.Success)
                throw new InvalidOperationException("The test host could not restore the active theme after disposal.", result.Exception);
        }, failures);

        // A theme observer or individual resource setter may fail. Continue restoring unrelated
        // resource entries and report every failure after independent cleanup has run.
        foreach (object key in Application.Resources.Keys)
            TryCleanup(() => Application.Resources.Remove(key), failures);
        foreach ((object key, object? value) in baselineApplicationResources)
            TryCleanup(() => Application.Resources[key] = value, failures);

        if (failures.Count > 0)
            throw new AggregateException("The test host could not completely restore theme and resource state.", failures);
    }

    private void DisposeUntrackedHeadlessForms()
    {
        var failures = new List<Exception>();
        foreach (Form form in Application.OpenForms.ToArray())
        {
            if (baselineForms.Contains(form) || form.window is not HeadlessWindowImpl backend || !createdBackends.Contains(backend))
                continue;

            TryCleanup(() => Application.OpenForms.Remove(form), failures);
            TryCleanup(form.adapter.CancelOwnedControlAnimationsForSubtree, failures);
            TryCleanup(() =>
            {
                if (!backend.IsDisposed)
                    backend.Dispose();
            }, failures);
            TryCleanup(form.CompleteDialogClose, failures);
            TryCleanup(form.adapter.Dispose, failures);
            TryCleanup(form.Dispose, failures);
        }
        if (failures.Count > 0)
            throw new AggregateException("Untracked headless Form cleanup reported failures.", failures);
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
