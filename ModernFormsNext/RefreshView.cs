using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;
using ModernFormsNext.Accessibility;
using ModernFormsNext.DataBinding;

namespace ModernFormsNext;

/// <summary>Hosts ordinary child controls with edge-pull, button, F5 and accessibility refresh actions.</summary>
/// <remarks>
/// All control operations require the owning UI thread. Add content through Controls and normal
/// Dock/Anchor layout. The fixed header provides real framework buttons; nested scrolling uses
/// the existing surface router. The command and parameter are borrowed. AsyncCommand owns its
/// single-flight task, completion and cooperative cancellation; hiding or disposing this source
/// never cancels or disposes an application-owned command.
/// </remarks>
/// <example><code>
/// var refresh = new RefreshView { Dock = DockStyle.Fill };
/// refresh.Command = new AsyncCommand(async (_, token) =&gt; await ReloadAsync(token));
/// refresh.Controls.Add(new CollectionView { Dock = DockStyle.Fill, ItemsSource = entries });
/// </code></example>
[DefaultProperty(nameof(PullThreshold)), DefaultEvent(nameof(RefreshRequested))]
public class RefreshView : ScrollableControl, ICommandBindingTargetProvider
{
    private readonly RefreshButton refreshButton;
    private readonly Button cancelButton;
    private CommandSource? commandSource;
    private int pullThreshold = 64, headerHeight = 32, pullDistance;
    // Request identity only; AsyncCommand remains the sole owner of execution state.
    private long requestVersion, pullRequestVersion;
    private string cancelText = "Cancel";
    private object? pullGesture;
    private bool dispatching;
    private (bool Busy, bool Cancel, bool Requested) publishedState;

    /// <summary>Creates an empty refresh host with a 32-logical-pixel header and a 64-pixel pull threshold.</summary>
    public RefreshView()
    {
        refreshButton = Controls.AddImplicitControl(new RefreshButton(this));
        cancelButton = Controls.AddImplicitControl(new Button { Text = cancelText, Visible = false });
        cancelButton.Click += (_, _) => CancelRefresh();
        Text = "Refresh";
        AutoScroll = true;
        InputBindings.Add(new KeyBinding(new DelegateCommand(RequestRefresh, () => CanRequestRefresh), new(Keys.F5)));
    }

    /// <summary>Gets or sets the borrowed command executed by every refresh activation path.</summary>
    /// <remarks>
    /// UI thread only. Null keeps synchronous RefreshRequested-only behavior. CommandSource
    /// supplies availability, routed commands, guarded execution and background UI requery.
    /// An AsyncCommand supplies observable busy/cancel state; other ICommand implementations
    /// execute synchronously and expose no manufactured asynchronous lifetime. Runtime-only.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ICommand? Command
    {
        get => commandSource?.Command;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(Command, value)) return;
            long request = ++requestVersion;
            try { (commandSource ??= new CommandSource(this)).Command = value; }
            finally { if (!IsDisposed && request == requestVersion) ClearPull(); }
        }
    }

    /// <summary>Gets or sets the borrowed nullable command parameter.</summary>
    /// <remarks>UI thread only. Replacement retires a pending pull/request and uses normal command requery. Runtime-only.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? CommandParameter
    {
        get => commandSource?.Parameter;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(CommandParameter, value)) return;
            long request = ++requestVersion;
            try { (commandSource ??= new CommandSource(this)).Parameter = value; }
            finally { if (!IsDisposed && request == requestVersion) ClearPull(); }
        }
    }

    /// <summary>Gets or sets an explicit target used only by a borrowed RoutedCommand.</summary>
    /// <remarks>UI thread only. Null routes from this host through the existing command system. Runtime-only.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? CommandTarget
    {
        get => commandSource?.Target;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(CommandTarget, value)) return;
            long request = ++requestVersion;
            try { (commandSource ??= new CommandSource(this)).Target = value; }
            finally { if (!IsDisposed && request == requestVersion) ClearPull(); }
        }
    }

    /// <summary>Gets or sets the positive pull distance required before release requests refresh, in logical pixels.</summary>
    /// <remarks>UI thread only. Changing the threshold cancels the pending pull and invalidates its indicator.</remarks>
    [DefaultValue(64)]
    public int PullThreshold
    {
        get => pullThreshold;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            if (pullThreshold == value) return;
            pullThreshold = value;
            requestVersion++;
            ClearPull();
            UpdateRefreshState();
        }
    }

    /// <summary>Gets or sets the positive fixed header height in logical pixels.</summary>
    /// <remarks>UI thread only. Requests normal child layout and cancels any pending pull.</remarks>
    [DefaultValue(32)]
    public int HeaderHeight
    {
        get => headerHeight;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            if (headerHeight == value) return;
            headerHeight = value;
            requestVersion++;
            ClearPull();
            if (!IsDisposed && !Disposing) PerformLayout();
        }
    }

    /// <summary>Gets or sets the cancel button caption. The inherited Text is the refresh caption.</summary>
    /// <remarks>UI thread only. Captions can use the existing binding/resource system; changing this value invalidates the button.</remarks>
    [DefaultValue("Cancel")]
    public string CancelText
    {
        get => cancelText;
        set { VerifyAccess(); ArgumentNullException.ThrowIfNull(value); cancelText = value; UpdateRefreshState(); }
    }

    /// <summary>Gets whether the current borrowed AsyncCommand owns a running invocation.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsRefreshing => Command is AsyncCommand { IsExecuting: true };

    /// <summary>Gets whether the current borrowed AsyncCommand accepts cooperative cancellation.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanCancelRefresh => Command is AsyncCommand { CanCancel: true };

    /// <summary>Gets the current clamped pull progress, from zero to one.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double PullProgress => (double)pullDistance / pullThreshold;

    /// <summary>Gets the current UI availability snapshot without invoking a command predicate.</summary>
    /// <remarks>Commands must raise CanExecuteChanged after availability changes, following the normal source contract.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanRequestRefresh => !IsDisposed && !Disposing && Enabled && Visible && !dispatching &&
        !IsRefreshing && refreshButton is { Enabled: true };

    /// <summary>Occurs after button Click and initial availability, before a fresh guarded command execution.</summary>
    /// <remarks>
    /// UI thread only. Recursive requests are ignored. Replacing the binding, hiding, moving or
    /// disposing the host prevents the old request from executing. Exceptions prevent execution.
    /// With no command this synchronous notification is the complete refresh operation.
    /// </remarks>
    public event EventHandler? RefreshRequested;

    /// <summary>Occurs on the UI thread after the current command's observable busy/cancel state changes.</summary>
    /// <remarks>Background command notifications use CommandSource's existing dispatcher and subscription guards.</remarks>
    public event EventHandler? RefreshStateChanged;

    /// <summary>Occurs on the UI thread after pull progress changes, including cancellation and release reset.</summary>
    public event EventHandler? PullProgressChanged;

    /// <summary>Requests refresh through the same guarded path as pull, F5 and the accessible refresh button.</summary>
    /// <remarks>UI thread only. Unavailable, hidden, disposed, busy and recursive requests do nothing.</remarks>
    public void RequestRefresh() => RequestRefresh(null);

    private void RequestRefresh(MouseEventArgs? click)
    {
        if (IsDisposed || Disposing) return;
        VerifyAccess();
        if (!CanRequestRefresh) return;
        var lifetime = new AccessibilityControlLifetime(this);
        long version = ++requestVersion;
        dispatching = true;
        try
        {
            ClearPull();
            if (!IsCurrentRequest()) return;
            if (commandSource is not null && !commandSource.CanExecute()) return;
            if (!IsCurrentRequest()) return;
            refreshButton.DispatchClick(click ?? new(MouseButtons.Left, 1, 0, 0, Point.Empty));
            if (!IsCurrentRequest()) return;
            RefreshRequested?.Invoke(this, EventArgs.Empty);
            if (IsCurrentRequest()) commandSource?.Execute();
        }
        finally { dispatching = false; }

        bool IsCurrentRequest() => lifetime.IsCurrent && version == requestVersion &&
            !IsDisposed && !Disposing && Enabled && Visible && !IsRefreshing && refreshButton.Enabled;
    }

    /// <summary>Requests cooperative cancellation through the current borrowed AsyncCommand, when supported.</summary>
    /// <remarks>UI thread only. Other commands are a no-op. Cancellation does not manufacture completion.</remarks>
    public void CancelRefresh()
    {
        VerifyAccess();
        if (Command is AsyncCommand command && command.CanCancel) command.Cancel();
    }

    bool ICommandBindingTargetProvider.Enabled => Enabled && refreshButton is { Enabled: true };
    bool ICommandBindingTargetProvider.IsCommandSourceDisposed => IsDisposed || Disposing;
    bool ICommandBindingTargetProvider.IsCommandSourceActive => Visible && CommandRouting.IsSourceActive(this);
    void ICommandBindingTargetProvider.SetCommandEnabled(bool enabled)
    {
        if (IsDisposed || refreshButton is null) return;
        refreshButton.SetCommandEnabled(enabled);
        if (!enabled) ClearPull();
        UpdateRefreshState();
    }

    internal override void RefreshRoutedCommandSource() => commandSource?.RefreshRouted();

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    private void SetPullDistance(int value)
    {
        value = Math.Clamp(value, 0, pullThreshold);
        if (pullDistance == value) return;
        pullDistance = value;
        UpdateRefreshState();
        if (!IsDisposed) PullProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearPull()
    {
        pullGesture = null;
        SetPullDistance(0);
    }

    private void UpdateRefreshState()
    {
        if (IsDisposed || refreshButton is null || cancelButton is null) return;
        var state = (Busy: IsRefreshing, Cancel: CanCancelRefresh,
            Requested: Command is AsyncCommand { IsCancellationRequested: true });
        refreshButton.Text = state.Busy ? Text + "…" : pullDistance == 0 ? Text : $"{Text} {Math.Round(PullProgress * 100)}%";
        refreshButton.AccessibleName = Text;
        cancelButton.Text = cancelText;
        cancelButton.Visible = state.Cancel || state.Requested;
        cancelButton.Enabled = state.Cancel;
        Invalidate();
        if (publishedState == state) return;
        publishedState = state;
        RefreshStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var rectangle = base.DisplayRectangle;
            int height = Math.Min(headerHeight, Math.Max(0, rectangle.Height));
            return new(rectangle.X, rectangle.Y + height, rectangle.Width, Math.Max(0, rectangle.Height - height));
        }
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (refreshButton is not null && cancelButton is not null && !IsDisposed)
        {
            var rectangle = LogicalPaddedClientRectangle;
            int height = Math.Min(headerHeight, rectangle.Height);
            int cancelWidth = cancelButton.Visible ? Math.Min(rectangle.Width, Math.Max(80, height * 3)) : 0;
            refreshButton.SetBounds(rectangle.X, rectangle.Y, Math.Max(0, rectangle.Width - cancelWidth), height);
            cancelButton.SetBounds(rectangle.Right - cancelWidth, rectangle.Y, cancelWidth, height);
        }
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); UpdateRefreshState(); }
    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        requestVersion++;
        ClearPull();
        base.OnParentChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }
    /// <inheritdoc/>
    protected override void OnVisibleChanged(EventArgs e)
    {
        requestVersion++;
        ClearPull();
        base.OnVisibleChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }
    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        requestVersion++;
        ClearPull();
        base.OnEnabledChanged(e);
        if (!IsDisposed) commandSource?.RefreshOwner();
    }

    internal override bool CanStartTouchScroll(Point origin, Point movement)
        => base.CanStartTouchScroll(origin, movement) || CanPull(origin, movement);

    private bool CanPull(Point origin, Point movement)
        => pullGesture is null && CanRequestRefresh && movement.Y > Math.Abs((long)movement.X) &&
           origin.Y >= LogicalPaddedClientRectangle.Top + Math.Min(headerHeight, LogicalPaddedClientRectangle.Height) &&
           VerticalScrollProperties.Value == VerticalScrollProperties.Minimum;

    internal override void BeginTouchScroll(object gesture, Point origin, Point movement)
    {
        if (!CanPull(origin, movement)) return;
        pullGesture = gesture;
        pullRequestVersion = ++requestVersion;
    }

    internal override bool ContinueTouchScroll(object gesture, Point movement, Point delta)
    {
        if (!ReferenceEquals(pullGesture, gesture)) return base.ContinueTouchScroll(gesture, movement, delta);
        if (!CanRequestRefresh || requestVersion != pullRequestVersion) { ClearPull(); return false; }
        SetPullDistance(movement.Y);
        return true;
    }

    internal override void EndTouchScroll(object gesture, bool canceled)
    {
        if (!ReferenceEquals(pullGesture, gesture)) return;
        bool ready = !canceled && pullDistance >= pullThreshold && pullRequestVersion == requestVersion;
        pullGesture = null;
        long version = requestVersion;
        var lifetime = new AccessibilityControlLifetime(this);
        SetPullDistance(0);
        if (ready && lifetime.IsCurrent && version == requestVersion && !IsDisposed) RequestRefresh();
    }

    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new RefreshAccessibleObject(this);

    private sealed class RefreshAccessibleObject(RefreshView owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is RefreshView { IsDisposed: false } current)
            {
                yield return current.refreshButton.AccessibilityObject;
                yield return current.cancelButton.AccessibilityObject;
            }
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    private sealed class RefreshButton(RefreshView owner) : Button
    {
        protected override void OnClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) owner.RequestRefresh(e);
            else base.OnClick(e);
        }
        internal void DispatchClick(MouseEventArgs e) => base.OnClick(e);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pullGesture = null;
            commandSource?.Dispose();
            commandSource = null;
        }
        base.Dispose(disposing);
    }
}
