using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext;

/// <summary>
/// Adapts a real ModernFormsNext <see cref="Control"/> tree to a platform-provided Skia surface.
/// </summary>
/// <remarks>
/// This is a transitional backend integration API. It reuses the framework's control rendering,
/// layout, hit testing, capture, focus, and keyboard routing while a platform backend supplies the
/// native window and canvas. It does not create a native window and must be called on the owning UI
/// thread. The adapter borrows, but never disposes, <see cref="Root"/> so a host can preserve the
/// application tree while recreating its native activity or surface.
/// </remarks>
public sealed class SkiaControlSurface : IDisposable
{
    private readonly HashSet<Control> observedControls = [];
    private readonly SurfaceRootControl surfaceRoot = new();
    private bool disposed;

    /// <summary>
    /// Creates an adapter for a framework control tree.
    /// </summary>
    /// <param name="root">The root control rendered into the platform surface.</param>
    public SkiaControlSurface(Control root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        surfaceRoot.Controls.Add(Root);
        surfaceRoot.CreateControl();
        ObserveTree(surfaceRoot);
    }

    /// <summary>Occurs when the control tree requires another platform render.</summary>
    public event EventHandler? Invalidated;

    /// <summary>Gets the borrowed root control.</summary>
    public Control Root { get; }

    /// <summary>Gets the most recently assigned logical surface size.</summary>
    public Size LogicalSize { get; private set; }

    /// <summary>Gets the currently selected descendant that receives committed text.</summary>
    public Control? SelectedControl => FindSelectedControl();

    /// <summary>
    /// Updates the root bounds and runs framework layout when the platform surface changes size.
    /// </summary>
    /// <param name="width">The width in logical pixels.</param>
    /// <param name="height">The height in logical pixels.</param>
    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var size = new Size(width, height);
        if (LogicalSize == size)
            return;

        LogicalSize = size;
        surfaceRoot.Size = size;
        Root.SetBounds(0, 0, width, height);
        surfaceRoot.PerformLayout();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Renders the root through the normal ModernFormsNext paint pipeline.
    /// </summary>
    /// <param name="canvas">The borrowed Skia canvas supplied by the platform.</param>
    /// <param name="scaling">The platform density scale used for paint metadata.</param>
    /// <remarks>
    /// The platform host remains responsible for applying its density transform to the canvas.
    /// The adapter does not retain or dispose <paramref name="canvas"/>.
    /// </remarks>
    public void Render(SKCanvas canvas, double scaling = 1)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(canvas);
        if (!double.IsFinite(scaling) || scaling <= 0)
            throw new ArgumentOutOfRangeException(nameof(scaling));

        var info = new SKImageInfo(
            Math.Max(1, LogicalSize.Width),
            Math.Max(1, LogicalSize.Height),
            SKImageInfo.PlatformColorType,
            SKAlphaType.Premul);
        var args = new PaintEventArgs(info, canvas, scaling);
        surfaceRoot.RaisePaintBackground(args);
        surfaceRoot.RaisePaint(args);
    }

    /// <summary>
    /// Routes a primary pointer transition through framework hit testing and capture.
    /// </summary>
    /// <param name="action">The pointer transition.</param>
    /// <param name="x">The horizontal position in logical pixels.</param>
    /// <param name="y">The vertical position in logical pixels.</param>
    public void ProcessPointer(ControlSurfacePointerAction action, int x, int y)
    {
        ThrowIfDisposed();
        var button = action is ControlSurfacePointerAction.Down or ControlSurfacePointerAction.Up
            ? MouseButtons.Left
            : MouseButtons.None;
        var args = new MouseEventArgs(button, action == ControlSurfacePointerAction.Up ? 1 : 0, x, y, Point.Empty);

        switch (action)
        {
            case ControlSurfacePointerAction.Down:
                // A normal ControlAdapter owns exclusive selection for window-backed controls.
                // Surface-hosted roots do not have that adapter, so preserve the same invariant
                // here before framework hit testing selects the pointer target.
                foreach (var control in observedControls.Where(control => control.Selected).ToArray())
                    control.Deselect();
                surfaceRoot.RaiseMouseDown(args);
                if (FindSelectedControl() is null)
                    FindSelectableAt(surfaceRoot, new Point(x, y))?.Select();
                break;
            case ControlSurfacePointerAction.Move:
                surfaceRoot.RaiseMouseMove(args);
                break;
            case ControlSurfacePointerAction.Up:
                surfaceRoot.RaiseMouseUp(args);
                break;
            case ControlSurfacePointerAction.Cancel:
                foreach (var control in observedControls.Where(control => control.Capture).ToArray())
                    control.Capture = false;
                surfaceRoot.Capture = false;
                surfaceRoot.RaiseMouseLeave(EventArgs.Empty);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Routes committed platform text to the selected framework control.</summary>
    /// <param name="text">The complete Unicode text committed by the platform IME.</param>
    public void CommitText(string text)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return;

        FindSelectedControl()?.RaiseKeyPress(new KeyPressEventArgs(text));
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Routes a backward-delete request to the selected framework control.</summary>
    public void DeleteBackward()
    {
        ThrowIfDisposed();
        FindSelectedControl()?.RaiseKeyDown(new KeyEventArgs(Keys.Back));
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Detaches event handlers without disposing the borrowed control tree.</summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        foreach (var control in observedControls.ToArray())
            Unobserve(control);
        observedControls.Clear();
        surfaceRoot.Controls.Remove(Root);
        surfaceRoot.Dispose();
        Invalidated = null;
    }

    private Control? FindSelectedControl()
        => observedControls.LastOrDefault(control => control.Selected);

    private static Control? FindSelectableAt(Control parent, Point point)
    {
        foreach (var child in parent.Controls.GetAllControls().Reverse())
        {
            if (!child.Visible || !child.Enabled || !child.Bounds.Contains(point))
                continue;

            var childPoint = new Point(point.X - child.Left, point.Y - child.Top);
            var descendant = FindSelectableAt(child, childPoint);
            if (descendant is not null)
                return descendant;
            if (child.CanSelect)
                return child;
        }

        return parent.CanSelect ? parent : null;
    }

    private void ObserveTree(Control control)
    {
        if (!observedControls.Add(control))
            return;

        control.Invalidated += OnControlInvalidated;
        control.ControlAdded += OnControlAdded;
        control.ControlRemoved += OnControlRemoved;
        foreach (var child in control.Controls.GetAllControls())
            ObserveTree(child);
    }

    private void Unobserve(Control control)
    {
        control.Invalidated -= OnControlInvalidated;
        control.ControlAdded -= OnControlAdded;
        control.ControlRemoved -= OnControlRemoved;
        observedControls.Remove(control);
    }

    private void OnControlInvalidated(object? sender, EventArgs<Rectangle> e)
        => Invalidated?.Invoke(this, EventArgs.Empty);

    private void OnControlAdded(object? sender, EventArgs<Control> e)
        => ObserveTree(e.Value);

    private void OnControlRemoved(object? sender, EventArgs<Control> e)
        => Unobserve(e.Value);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class SurfaceRootControl : Control
    {
        public override bool Visible
        {
            get => true;
            set
            {
                // The native surface owns visibility. Child controls still keep their own state.
            }
        }
    }
}
