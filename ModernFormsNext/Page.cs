using System.ComponentModel;

namespace ModernFormsNext;

/// <summary>Identifies the committed logical presentation state of a hosted page.</summary>
public enum PageLifecycleState
{
    /// <summary>The page has no owning page host.</summary>
    Detached,
    /// <summary>The host retains the page without presenting it.</summary>
    Attached,
    /// <summary>The page is current and its appearance notification or transition is in progress.</summary>
    Appearing,
    /// <summary>The page is current and its appearance has settled.</summary>
    Appeared,
    /// <summary>The page is leaving its current presentation.</summary>
    Disappearing,
    /// <summary>Managed page disposal has begun; the page cannot be hosted again.</summary>
    Disposed
}

/// <summary>Provides a lightweight control root with host-driven page lifecycle notifications.</summary>
/// <remarks>
/// A page is not a native window. Mount it in <see cref="PageHost"/> or a page container inside
/// an existing Form or control surface. Layout, resources, binding, input and accessibility use
/// the ordinary control tree. Native focus loss, application suspension and Activity recreation
/// do not themselves remove a page. Access page state and controls on their owning UI thread.
/// </remarks>
public abstract class Page : Control
{
    private PageLifecycleState lifecycleState;
    private long lifecycleVersion;
    private bool retiring;
    internal PageHost? OwnerHost { get; private set; }
    internal Control? HostParent { get; private set; }
    internal bool IsRetiringPage => retiring || IsDisposed || Disposing;

    /// <summary>Initializes an unhosted page without starting a native host or animation.</summary>
    protected Page() => TabStop = false;

    /// <summary>Gets or sets the page title through the existing Text property.</summary>
    /// <remarks>UI thread only. Raises the ordinary TextChanged notification and rendering/layout invalidation.</remarks>
    [DefaultValue("")]
    public string Title { get => Text; set { VerifyPageAccess(); Text = value; } }

    /// <summary>Gets the state committed before the corresponding page notifications run.</summary>
    /// <remarks>A retained inactive stack entry is Attached. State remains readable after disposal.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PageLifecycleState LifecycleState => lifecycleState;

    /// <summary>Gets whether this page owns the host's current logical presentation.</summary>
    /// <remarks>Appearance may still be animating. This is independent of native window focus.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPresented => lifecycleState is PageLifecycleState.Appearing or PageLifecycleState.Appeared;

    /// <summary>Occurs after a logical state change commits on the UI thread.</summary>
    public event EventHandler? LifecycleStateChanged;
    /// <summary>Occurs after this page becomes current, before its appearance settles.</summary>
    public event EventHandler? Appearing;
    /// <summary>Occurs after the current appearance or its visual cancellation has settled.</summary>
    public event EventHandler? Appeared;
    /// <summary>Occurs as the page leaves its current presentation.</summary>
    public event EventHandler? Disappearing;
    /// <summary>Occurs after the page is no longer presented; a retained page remains owned.</summary>
    public event EventHandler? Disappeared;

    /// <summary>Verifies UI-thread access and rejects a page whose disposal has begun.</summary>
    /// <remarks>Derived page mutators should call this before changing page state.</remarks>
    protected void VerifyPageAccess()
    {
        ObjectDisposedException.ThrowIf(IsRetiringPage, this);
        GetFocusScope().VerifyAccess();
    }

    /// <summary>Raises Appearing. Overrides must call base to publish the event.</summary>
    protected virtual void OnAppearing(EventArgs e) => RaiseCurrent(Appearing, e);
    /// <summary>Raises Appeared. Overrides must call base to publish the event.</summary>
    protected virtual void OnAppeared(EventArgs e) => RaiseCurrent(Appeared, e);
    /// <summary>Raises Disappearing. Overrides must call base to publish the event.</summary>
    protected virtual void OnDisappearing(EventArgs e) => RaiseCurrent(Disappearing, e);
    /// <summary>Raises Disappeared. Overrides must call base to publish the event.</summary>
    protected virtual void OnDisappeared(EventArgs e) => RaiseCurrent(Disappeared, e);

    internal void AssignHost(PageHost? host, Control? parent)
    {
        OwnerHost = host;
        HostParent = parent;
        lifecycleVersion++;
    }

    internal void PublishState(PageLifecycleState state, PageNotification notification = PageNotification.None)
    {
        if (lifecycleState == PageLifecycleState.Disposed) return;
        bool changed = lifecycleState != state;
        lifecycleState = state;
        long version = ++lifecycleVersion;
        if (changed) RaiseCurrent(LifecycleStateChanged, EventArgs.Empty);
        if (version != lifecycleVersion || IsDisposed) return;
        switch (notification)
        {
            case PageNotification.Appearing: OnAppearing(EventArgs.Empty); break;
            case PageNotification.Appeared: OnAppeared(EventArgs.Empty); break;
            case PageNotification.Disappearing: OnDisappearing(EventArgs.Empty); break;
            case PageNotification.Disappeared: OnDisappeared(EventArgs.Empty); break;
        }
    }

    private void RaiseCurrent(EventHandler? handlers, EventArgs e)
    {
        long version = lifecycleVersion;
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            if (version != lifecycleVersion || IsDisposed) break;
            handler(this, e);
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        // Relinquish an externally reparented page before application ParentChanged observers
        // inspect the old host. The host must never reclaim or dispose a new owner's control.
        if (OwnerHost is { } owner && !ReferenceEquals(Parent, HostParent)) owner.PageParentLost(this);
        base.OnParentChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e)
    {
        OwnerHost?.RefreshPagePresentation();
        base.OnVisibleChanged(e);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        List<Exception>? failures = null;
        void Attempt(Action action) { try { action(); } catch (Exception error) { (failures ??= []).Add(error); } }
        if (OwnerHost is { } host) Attempt(() => host.ReleasePage(this, disposingPage: true));
        Attempt(() => PublishState(PageLifecycleState.Disposed));
        Attempt(() => base.Dispose(true));
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Page disposal callbacks failed.", failures);
    }
}

internal enum PageNotification { None, Appearing, Appeared, Disappearing, Disappeared }
