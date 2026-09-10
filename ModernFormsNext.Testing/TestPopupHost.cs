namespace ModernFormsNext.Testing;

/// <summary>Provides deterministic access to one production popup owned by a hosted window.</summary>
/// <remarks>
/// Obtain handles from <see cref="TestWindowHost.Popups"/> or <see cref="TestWindowHost.ActivePopup"/>
/// after a control opens a popup. All live operations require the parent host UI thread. The
/// existing PopupWindow owns its controls; no controls are copied or reparented. Parent closure
/// destroys the popup, while normal Hide retains it for later reuse. Native focus/activation and
/// window-manager shadows are outside this headless contract.
/// </remarks>
/// <example>
/// <code>
/// window.Input.Click(comboBox);
/// var popup = window.ActivePopup!;
/// using var image = popup.CaptureRenderedSnapshot();
/// popup.Input.Click(new System.Drawing.Point(12, 12));
/// </code>
/// </example>
public sealed class TestPopupHost : IDisposable, ITestInputTarget
{
    private TestWindowHost? parent;
    private HeadlessPopupImpl? backend;
    private bool capturing;

    internal TestPopupHost(TestWindowHost parent, HeadlessPopupImpl backend)
    {
        this.parent = parent;
        this.backend = backend;
        Input = new TestInput(this);
        backend.HostedPopup!.InputClock = () => parent.HostedForm.InputClock?.Invoke() ?? TimeSpan.Zero;
    }

    /// <summary>Gets the real production popup window on the test UI thread.</summary>
    /// <exception cref="ObjectDisposedException">The popup or parent has closed.</exception>
    public PopupWindow Window
    {
        get { VerifyInputAccess(); return backend!.HostedPopup!; }
    }

    /// <summary>Gets the shared input helper routing to this popup's real backend callback.</summary>
    public TestInput Input { get; }

    /// <summary>Gets whether the popup or its parent has closed.</summary>
    public bool IsClosed => parent is null || parent.IsClosed || backend?.State.IsDisposed != false;

    /// <summary>Gets whether the live popup is currently shown.</summary>
    public bool IsVisible => !IsClosed && Window.Visible && backend!.State.IsShown;

    /// <summary>Gets the popup's current logical dimensions and render scale.</summary>
    public TestViewport Viewport
    {
        get
        {
            VerifyInputAccess();
            return new TestViewport((int)backend!.ClientSize.Width, (int)backend.ClientSize.Height, backend.RenderScaling);
        }
    }

    /// <summary>Gets the popup's canonical keyboard focus owner.</summary>
    public Control? FocusedControl => Window.adapter.SelectedControl;

    /// <summary>Stabilizes the existing popup control layout without advancing test time.</summary>
    /// <param name="maximumPasses">Maximum complete layout passes, greater than zero.</param>
    /// <returns>The number of layout passes performed.</returns>
    /// <exception cref="InvalidOperationException">Layout does not stabilize within the limit.</exception>
    public int LayoutUntilStable(int maximumPasses = 16)
    {
        VerifyInputAccess();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPasses);
        return parent!.Dispatcher.Run(() =>
        {
            string? previousSignature = null;
            for (int pass = 1; pass <= maximumPasses; pass++)
            {
                parent!.Dispatcher.Drain();
                VerifyInputAccess();
                PerformLayout(Window.adapter);
                VerifyInputAccess();
                parent!.Dispatcher.Drain();
                VerifyInputAccess();
                backend!.State.ConsumePendingInvalidations();
                string signature = CaptureTree().GetStabilitySignature();
                if (signature == previousSignature && !parent!.Dispatcher.HasReadyWork) return pass;
                previousSignature = signature;
            }
            throw new InvalidOperationException($"The popup tree did not stabilize after {maximumPasses} layout passes." +
                Environment.NewLine + CaptureTree().Dump());
        });
    }

    /// <summary>Captures a detached snapshot of the real popup adapter and user-control tree.</summary>
    /// <returns>An immutable tree snapshot that survives popup disposal.</returns>
    public ControlTreeSnapshot CaptureTree()
        => ControlTreeSnapshotCapture.Capture(Window.adapter, Viewport.RenderScale);

    /// <summary>Captures a caller-owned image through the popup's production Paint callback.</summary>
    /// <param name="maximumPixels">Framebuffer pixel budget from 1 through 16,777,216.</param>
    /// <returns>A detached image that survives popup and parent disposal; dispose it when finished.</returns>
    /// <remarks>
    /// Stabilizes layout, then paints once without advancing time. DPI, clipping, image ownership,
    /// limits, and rendering-environment caveats match <see cref="TestWindowHost.CaptureRenderedSnapshot"/>.
    /// The popup must be shown. Work posted during painting remains pending.
    /// </remarks>
    public RenderedSnapshot CaptureRenderedSnapshot(int maximumPixels = HeadlessWindowImpl.MaximumSnapshotPixels)
    {
        VerifyInputAccess();
        return parent!.Dispatcher.Run(() =>
        {
            if (capturing) throw new InvalidOperationException("A popup snapshot cannot recursively capture the same popup.");
            backend!.State.ValidateSnapshotSize(maximumPixels);
            capturing = true;
            try
            {
                LayoutUntilStable();
                VerifyInputAccess();
                return backend!.State.CaptureRenderedSnapshot(maximumPixels);
            }
            finally { capturing = false; }
        });
    }

    /// <summary>Hides the popup through its production lifecycle so it may be shown again.</summary>
    public void Hide() => Window.Hide();

    /// <summary>Destroys the popup and releases its input, controls, animations and backend callbacks.</summary>
    /// <remarks>Idempotent on the test UI thread. The parent also performs this cleanup automatically.</remarks>
    public void Close()
    {
        if (IsClosed) return;
        VerifyInputAccess();
        Window.Close();
    }

    /// <inheritdoc/>
    public void Dispose() => Close();

    WindowBase ITestInputTarget.HostedWindow => Window;
    HeadlessWindowImpl ITestInputTarget.Backend => backend!.State;
    ulong ITestInputTarget.InputTimestamp => parent!.InputTimestamp;
    bool ITestInputTarget.CanReceiveInput => IsVisible && parent!.CanReceiveInput;
    void ITestInputTarget.VerifyInputAccess() => VerifyInputAccess();
    bool ITestInputTarget.Contains(Control candidate) => ReferenceEquals(candidate.FindWindow(), Window);

    internal void Revoke()
    {
        Input.Dispose();
        backend = null;
        parent = null;
    }

    private void VerifyInputAccess()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        parent!.VerifyInputAccess();
    }

    private static void PerformLayout(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls.ToArray()) PerformLayout(child);
    }
}
