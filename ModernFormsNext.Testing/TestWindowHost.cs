using System.Drawing;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext.Testing;

/// <summary>Hosts one real ModernFormsNext Form or control root without a native OS window.</summary>
/// <remarks>
/// Layout, ownership, visibility, invalidation, and close behavior use production framework paths.
/// The only substituted component is the WindowKit top-level implementation, which records state
/// and never creates a native platform handle. Explicit snapshots use a temporary off-screen
/// framebuffer; ordinary hosting does not retain a rendering surface.
/// </remarks>
public sealed partial class TestWindowHost : IDisposable
{
    private const int DefaultLayoutPassLimit = 16;
    private readonly ModernFormsTestHost owner;
    private readonly Form hostedForm;
    private readonly HeadlessWindowImpl backend;
    private readonly Control? controlRoot;
    private TestViewport viewport;
    private bool closed;
    private bool closing;

    internal TestWindowHost(
        ModernFormsTestHost owner,
        Form hostedForm,
        HeadlessWindowImpl backend,
        Control? controlRoot,
        TestViewport viewport,
        TestWindowHost? modalOwner = null)
    {
        this.owner = owner;
        this.hostedForm = hostedForm;
        this.backend = backend;
        this.controlRoot = controlRoot;
        this.viewport = viewport;
        ModalOwner = modalOwner;
        Input = new TestInput(this);
        hostedForm.InputClock = () => owner.Clock.CurrentTime;
    }

    /// <summary>Gets input helpers that route through this window's production backend input callback.</summary>
    public TestInput Input { get; }

    /// <summary>Gets the original production dialog-result task, or null for a nonmodal window.</summary>
    /// <remarks>
    /// The task completes when Form modal cleanup runs. A canceled Form.Close keeps it pending;
    /// explicit test-window/host disposal still forces deterministic cleanup. A DialogResult set
    /// before ShowDialog returns an already completed task without displaying the Form.
    /// </remarks>
    public Task<DialogResult>? DialogCompletion { get; private set; }

    /// <summary>Gets the canonical keyboard focus owner of this window on the test UI thread.</summary>
    /// <remarks>Focus belongs to each window; this does not emulate operating-system foreground activation.</remarks>
    public Control? FocusedControl
    {
        get
        {
            ThrowIfClosed();
            return owner.Dispatcher.Run(() => hostedForm.adapter.SelectedControl);
        }
    }

    /// <summary>Gets the directly hosted Form, or null when a control root uses an internal Form wrapper.</summary>
    public Form? FormRoot => controlRoot is null ? hostedForm : null;

    /// <summary>Gets the directly hosted UserControl, Panel, or other control root.</summary>
    public Control? ControlRoot => controlRoot;

    /// <summary>Gets the current immutable viewport configuration.</summary>
    public TestViewport Viewport => viewport;

    /// <summary>Gets whether this host deliberately has no native window or visible desktop surface.</summary>
    public bool IsHeadless => true;

    /// <summary>Gets whether the window has begun closing or was closed by application code or host cleanup.</summary>
    /// <remarks>The host still releases managed tree resources during cleanup after an application closes its Form.</remarks>
    public bool IsClosed => closing || closed || backend.IsDisposed;

    /// <summary>Runs one explicit production layout pass over the complete hosted tree.</summary>
    public void PerformLayout()
    {
        ThrowIfClosed();
        owner.Dispatcher.Run(() =>
        {
            owner.Dispatcher.Drain();
            PerformLayoutCore();
            owner.Dispatcher.Drain();
            backend.ConsumePendingInvalidations();
        });
    }

    /// <summary>Runs production layout until captured geometry and pending work are stable.</summary>
    /// <param name="maximumPasses">The maximum number of complete layout passes.</param>
    /// <returns>The number of passes executed.</returns>
    /// <exception cref="InvalidOperationException">The tree does not stabilize within the limit.</exception>
    public int LayoutUntilStable(int maximumPasses = DefaultLayoutPassLimit)
    {
        ThrowIfClosed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPasses);
        return owner.Dispatcher.Run(() =>
        {
            string? previousSignature = null;
            for (var pass = 1; pass <= maximumPasses; pass++)
            {
                owner.Dispatcher.Drain();
                PerformLayoutCore();
                owner.Dispatcher.Drain();
                backend.ConsumePendingInvalidations();

                ControlTreeSnapshot snapshot = CaptureTreeCore();
                string signature = snapshot.GetStabilitySignature();
                if (signature == previousSignature && !owner.Dispatcher.HasReadyWork)
                    return pass;
                previousSignature = signature;
            }

            throw new InvalidOperationException(
                $"The hosted control tree did not stabilize after {maximumPasses} layout passes." +
                Environment.NewLine + CaptureTreeCore().Dump());
        });
    }

    /// <summary>Resizes the logical viewport and applies the real runtime resize/layout path.</summary>
    /// <param name="width">The new logical width.</param>
    /// <param name="height">The new logical height.</param>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is not positive.</exception>
    public void Resize(int width, int height)
    {
        ThrowIfClosed();
        var next = viewport.Resize(width, height);
        owner.Dispatcher.Run(() =>
        {
            viewport = next;
            hostedForm.ClientSize = new Size(width, height);
            if (controlRoot is not null)
                controlRoot.Bounds = new Rectangle(Point.Empty, next.Size());
            LayoutUntilStable();
        });
    }

    /// <summary>Changes the deterministic logical-to-device render scale without reading a monitor.</summary>
    /// <param name="renderScale">The scale where 1, 1.25, 1.5, and 2 represent 100–200 percent.</param>
    /// <exception cref="ArgumentOutOfRangeException">Render scale is not finite and greater than zero.</exception>
    public void SetRenderScale(double renderScale)
    {
        ThrowIfClosed();
        TestViewport next = viewport.WithRenderScale(renderScale);
        owner.Dispatcher.Run(() =>
        {
            viewport = next;
            backend.SetRenderScale(renderScale);
            backend.ConsumePendingInvalidations();
        });
    }

    /// <summary>Marks the root or a hosted descendant invalid and records the headless invalidation.</summary>
    /// <param name="control">A hosted control, or null to invalidate the root.</param>
    public void Invalidate(Control? control = null)
    {
        ThrowIfClosed();
        owner.Dispatcher.Run(() =>
        {
            if (control is null)
            {
                if (controlRoot is not null)
                    controlRoot.Invalidate();
                else
                    hostedForm.Invalidate();
                return;
            }

            if (!Contains(control))
                throw new ArgumentException("The control is not part of this hosted tree.", nameof(control));
            control.Invalidate();
        });
    }

    /// <summary>Drains dispatcher work, applies layout until stable, and consumes invalidations.</summary>
    /// <returns>The number of layout passes executed.</returns>
    public int ProcessPendingWork() => LayoutUntilStable();

    /// <summary>Captures an immutable control-tree and geometry snapshot.</summary>
    /// <returns>A detached snapshot that is safe to retain after later UI mutations.</returns>
    public ControlTreeSnapshot CaptureTree()
    {
        ThrowIfClosed();
        return owner.Dispatcher.Run(CaptureTreeCore);
    }

    /// <summary>Closes and disposes the complete hosted tree deterministically.</summary>
    /// <remarks>
    /// Test-host ownership is authoritative: cleanup completes even if a Form Closing handler would
    /// cancel a normal user close. Reentrant Close/Dispose calls do not repeat close callbacks.
    /// A callback may request host disposal; it takes effect after the current window's independent
    /// cleanup steps finish. This does not emulate native modal-window activation.
    /// </remarks>
    /// <exception cref="Exception">An application close handler failed after deterministic cleanup completed.</exception>
    public void Close()
    {
        if (closed || closing)
            return;
        owner.Dispatcher.Run(CloseCore);
    }

    /// <inheritdoc/>
    public void Dispose() => Close();

    internal HeadlessWindowImpl Backend => backend;

    internal Form HostedForm => hostedForm;

    internal UiTestDispatcher Dispatcher => owner.Dispatcher;

    internal TestWindowHost? ModalOwner { get; }

    internal ulong InputTimestamp => (ulong)owner.Clock.CurrentTime.TotalMilliseconds;

    internal void VerifyInputAccess()
    {
        ThrowIfClosed();
        owner.Dispatcher.VerifyAccess();
    }

    internal bool CanReceiveInput => backend.IsShown && backend.IsEnabled && hostedForm.Visible;

    internal void Show()
    {
        PrepareViewport();
        hostedForm.Show();
        if (!IsClosed)
            LayoutUntilStable();
    }

    internal void ShowDialog()
    {
        PrepareViewport();
        DialogCompletion = hostedForm.ShowDialog(ModalOwner!.HostedForm);
        // Shown handlers can close the Form or dispose the entire host. Keep the original result
        // task, release a directly closed Form's remaining test resources, and avoid further layout.
        if (IsClosed)
            Close();
        else if (backend.IsShown)
            LayoutUntilStable();
    }

    private void PrepareViewport()
    {
        backend.SetRenderScale(viewport.RenderScale);
        hostedForm.ClientSize = new Size(viewport.Width, viewport.Height);
        if (controlRoot is not null)
            controlRoot.Bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
    }

    private void PerformLayoutCore()
    {
        if (controlRoot is not null)
            controlRoot.Bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);

        // The adapter owns managed Form chrome and the real client-area control. The public
        // Form.Controls owner is then laid out before recursively processing application children.
        hostedForm.adapter.PerformLayout();
        hostedForm.Controls.Owner.PerformLayout();
        foreach (Control control in hostedForm.Controls.ToArray())
            PerformLayoutRecursively(control);
    }

    private ControlTreeSnapshot CaptureTreeCore()
        => controlRoot is null
            ? ControlTreeSnapshotCapture.Capture(hostedForm, viewport.RenderScale)
            : ControlTreeSnapshotCapture.Capture(controlRoot, viewport.RenderScale);

    internal bool Contains(Control candidate)
    {
        Control? current = candidate;
        Control root = controlRoot ?? hostedForm.Controls.Owner;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
                return true;
            current = current.Parent;
        }

        return false;
    }

    private void CloseCore()
    {
        if (closed || closing)
            return;

        var failures = new List<Exception>();
        closing = true;
        owner.BeginWindowClose();
        try
        {
            TryCleanup(() => owner.CloseOwnedDialogs(this), failures);
            if (!backend.IsDisposed)
                TryCleanup(hostedForm.Close, failures);

            // Normal Close may be canceled or a Closing handler may fail. Test-host ownership
            // remains authoritative, and a reentrant host Dispose waits for this cleanup boundary.
            TryCleanup(() => Application.OpenForms.Remove(hostedForm), failures);
            TryCleanup(hostedForm.adapter.CancelOwnedControlAnimationsForSubtree, failures);
            TryCleanup(() =>
            {
                if (!backend.IsDisposed)
                    backend.Dispose();
            }, failures);
            TryCleanup(hostedForm.CompleteDialogClose, failures);
            TryCleanup(hostedForm.adapter.Dispose, failures);
            TryCleanup(hostedForm.Dispose, failures);
            hostedForm.InputClock = null;
            TryCleanup(Input.Dispose, failures);
        }
        finally
        {
            closed = true;
            closing = false;
            TryCleanup(() => owner.NotifyWindowClosed(this), failures);
            TryCleanup(owner.EndWindowClose, failures);
        }

        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("The deterministic ModernFormsNext test window reported cleanup failures.", failures);
    }

    private static void PerformLayoutRecursively(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls.ToArray())
            PerformLayoutRecursively(child);
    }

    private void ThrowIfClosed()
    {
        owner.ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(closing || closed || backend.IsDisposed, this);
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

internal static class TestViewportExtensions
{
    internal static Size Size(this TestViewport viewport) => new(viewport.Width, viewport.Height);
}
