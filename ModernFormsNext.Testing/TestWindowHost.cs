using System.Drawing;

namespace ModernFormsNext.Testing;

/// <summary>Hosts one real ModernFormsNext Form or control root without a native OS window.</summary>
/// <remarks>
/// Layout, ownership, visibility, invalidation, and close behavior use production framework paths.
/// The only substituted component is the WindowKit top-level implementation, which records state
/// and never creates a platform handle or rendering surface.
/// </remarks>
public sealed class TestWindowHost : IDisposable
{
    private const int DefaultLayoutPassLimit = 16;
    private readonly ModernFormsTestHost owner;
    private readonly Form hostedForm;
    private readonly HeadlessWindowImpl backend;
    private readonly Control? controlRoot;
    private TestViewport viewport;
    private bool closed;

    internal TestWindowHost(
        ModernFormsTestHost owner,
        Form hostedForm,
        HeadlessWindowImpl backend,
        Control? controlRoot,
        TestViewport viewport)
    {
        this.owner = owner;
        this.hostedForm = hostedForm;
        this.backend = backend;
        this.controlRoot = controlRoot;
        this.viewport = viewport;
    }

    /// <summary>Gets the directly hosted Form, or null when a control root uses an internal Form wrapper.</summary>
    public Form? FormRoot => controlRoot is null ? hostedForm : null;

    /// <summary>Gets the directly hosted UserControl, Panel, or other control root.</summary>
    public Control? ControlRoot => controlRoot;

    /// <summary>Gets the current immutable viewport configuration.</summary>
    public TestViewport Viewport => viewport;

    /// <summary>Gets whether this host deliberately has no native window or visible desktop surface.</summary>
    public bool IsHeadless => true;

    /// <summary>Gets whether the hosted tree has been deterministically closed and disposed.</summary>
    public bool IsClosed => closed;

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
                if (signature == previousSignature && owner.Dispatcher.PendingWorkCount == 0)
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
    /// cancel a normal user close. Modal/focus/cancellation semantics are outside Phase 1.
    /// </remarks>
    public void Close()
    {
        if (closed)
            return;
        owner.Dispatcher.Run(CloseCore);
    }

    /// <inheritdoc/>
    public void Dispose() => Close();

    internal HeadlessWindowImpl Backend => backend;

    internal void Show()
    {
        backend.SetRenderScale(viewport.RenderScale);
        hostedForm.ClientSize = new Size(viewport.Width, viewport.Height);
        if (controlRoot is not null)
            controlRoot.Bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
        hostedForm.Show();
        LayoutUntilStable();
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

    private bool Contains(Control candidate)
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
        if (closed)
            return;

        try
        {
            hostedForm.Close();
        }
        finally
        {
            // Normal Close may be canceled. A test host must still release its owned tree and all
            // control-owned scheduler entries so the next test starts from an isolated baseline.
            Application.OpenForms.Remove(hostedForm);
            hostedForm.adapter.CancelOwnedControlAnimationsForSubtree();
            if (!backend.IsDisposed)
                backend.Dispose();
            hostedForm.adapter.Dispose();
            hostedForm.Dispose();
            closed = true;
            owner.NotifyWindowClosed(this);
        }
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
        ObjectDisposedException.ThrowIf(closed, this);
    }
}

internal static class TestViewportExtensions
{
    internal static Size Size(this TestViewport viewport) => new(viewport.Width, viewport.Height);
}
