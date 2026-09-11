using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private bool backendClosed;
    private readonly object applicationRuntimeIdentity = Application.RuntimeIdentity;
    private IWindowInsetsProvider? insetsProvider;

    /// <summary>Gets whether the backend currently reports this individual window as active.</summary>
    /// <remarks>
    /// Read on the UI thread. Window activity differs from Application.Lifecycle's process-wide
    /// activity. Hidden and closed windows report false; popup activation is independent of Forms.
    /// </remarks>
    public bool IsActive { get; private set; }

    /// <summary>Occurs on the UI thread when this window becomes active, after IsActive commits.</summary>
    public event EventHandler? Activated;

    /// <summary>Gets native occlusion overlapping this window's client area, in logical pixels.</summary>
    /// <remarks>
    /// An absent backend feature reports zero. Existing window client sizing remains unchanged;
    /// applications can apply these values to embedded content. SkiaControlSurface applies its
    /// SafeArea to its borrowed content root. IME avoidance remains an application policy.
    /// </remarks>
    public WindowInsets Insets { get; private set; }

    /// <summary>Occurs on the UI thread after the native client inset snapshot changes.</summary>
    public event EventHandler<WindowInsetsChangedEventArgs>? InsetsChanged;

    private void AttachInsetsProvider()
    {
        insetsProvider = window.TryGetFeature(typeof(IWindowInsetsProvider)) as IWindowInsetsProvider;
        if (insetsProvider is null) return;
        Insets = insetsProvider.CurrentInsets;
        insetsProvider.InsetsChanged += OnInsetsChanged;
    }

    private void OnInsetsChanged(object? sender, WindowInsetsChangedEventArgs args)
    {
        if (backendClosed || Insets == args.Insets) return;
        Insets = args.Insets;
        InsetsChanged?.Invoke(this, args);
    }

    private void DetachInsetsProvider()
    {
        if (insetsProvider is null) return;
        insetsProvider.InsetsChanged -= OnInsetsChanged;
        insetsProvider = null;
    }

    private void OnBackendClosed()
    {
        if (backendClosed) return;
        backendClosed = true;
        IsActive = false;
        Visible = false;
        if (this is Form closedForm && ReferenceEquals(applicationRuntimeIdentity, Application.RuntimeIdentity))
            Application.OpenForms.Remove(closedForm);
        var failures = new List<Exception>();
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        Cleanup(DetachInsetsProvider);
        Cleanup(TextInputHost.Dispose);
        Cleanup(ReleaseInputBindings);
        Cleanup(adapter.CancelOwnedControlAnimationsForSubtree);
        // All close observers, modal ownership and application lifetime see the committed close,
        // even if an earlier user subscriber fails. Reentrant closure is already terminal.
        if (Closed is { } handlers)
            foreach (EventHandler handler in handlers.GetInvocationList())
                Cleanup(() => handler(this, EventArgs.Empty));
        if (this is Form form)
            Cleanup(form.CompleteDialogClose);
        if (ReferenceEquals(applicationRuntimeIdentity, Application.RuntimeIdentity))
            Cleanup(() => Application.NotifyWindowClosed(this));
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Window close callbacks or lifecycle cleanup failed.", failures);
    }
}
