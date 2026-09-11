using System.Drawing;
using SkiaSharp;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;

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
/// Programmatic control selection, including accessibility requests for keyboard focus, replaces
/// the previous input target. Screen-reader accessibility focus does not change keyboard focus.
/// </remarks>
public sealed partial class SkiaControlSurface : IDisposable, IPlatformAccessibilityHost, IPlatformAccessibilitySurface
{
    private readonly HashSet<Control> observedControls = [];
    private readonly Dictionary<int, PointerState> pointers = [];
    private readonly SurfaceRootControl surfaceRoot = new();
    private readonly Action<string>? pointerDiagnosticSink;
    private int pointerDragThreshold = 8;
    private int pointerDownRouteDepth;
    private bool disposed;
    private WindowKit.WindowInsets insets;
    private readonly DataBinding.InputBindingResolver inputBindingResolver = new();
    private readonly ControlTextInputHost textInputHost;

    /// <summary>
    /// Creates an adapter for a framework control tree.
    /// </summary>
    /// <param name="root">The root control rendered into the platform surface.</param>
    /// <param name="pointerDiagnosticSink">
    /// Optional disabled-by-default destination for pointer routing diagnostics.
    /// </param>
    public SkiaControlSurface(Control root, Action<string>? pointerDiagnosticSink = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        textInputHost = new(surfaceRoot, FindSelectedControl);
        surfaceRoot.TextInputHost = textInputHost;
        this.pointerDiagnosticSink = pointerDiagnosticSink;
        surfaceRoot.AccessibilityNotification = (source, eventId, objectId, childId) =>
            accessibilityNotification?.Invoke(source, eventId, objectId, childId);
        surfaceRoot.Controls.Add(Root);
        surfaceRoot.CreateControl();
        ObserveTree(surfaceRoot);
    }

    /// <summary>Occurs when the control tree requires another platform render.</summary>
    public event EventHandler? Invalidated;

    /// <summary>Gets the borrowed root control.</summary>
    public Control Root { get; }

    /// <summary>Gets the current revocable client with caret geometry in logical surface coordinates.</summary>
    /// <remarks>Use on the UI thread. Retired clients reject later callbacks instead of targeting another control.</remarks>
    public ITextInputClient? TextInputClient => textInputHost.Client;

    /// <summary>Gets detached session diagnostics containing no entered text or control names.</summary>
    public TextInputDiagnostics TextInputDiagnostics => textInputHost.GetDiagnostics();

    /// <summary>Attaches a borrowed native input method to the existing surface and canonical focus.</summary>
    /// <param name="method">Optional native feature; null detaches the old feature without disposing it.</param>
    /// <remarks>Use on the UI thread. The surface retires connections on disposal but never owns the native view.</remarks>
    public void AttachTextInputMethod(ITextInputMethod? method) => textInputHost.Attach(method);

    /// <summary>Relinquishes or reacquires shared text ownership during native pause or editor handoff.</summary>
    /// <param name="active">False retires native callbacks while preserving visible provisional text.</param>
    /// <remarks>Use on the UI thread. Reacquisition follows current framework focus and creates a fresh session.</remarks>
    public void SetTextInputActive(bool active) => textInputHost.SetActive(active);

    /// <summary>Requests software keyboard visibility through the attached native input method.</summary>
    /// <param name="visible">True to show the current editor's keyboard; false to dismiss it.</param>
    /// <returns>Whether the native method accepted the request, subject to OS policy.</returns>
    public bool RequestSoftwareKeyboard(bool visible) => textInputHost.SetKeyboardVisible(visible);

    // Borrowing semantic services must observe surface disposal without retaining the surface or
    // consulting a platform accessibility adapter. This is a lifetime seam, not another tree.
    internal bool IsDisposed => disposed;

    // The Android host borrows this existing adapter. Surface coordinates are logical pixels
    // relative to the native surface, since this control tree has no WindowBase screen origin.
    IPlatformAccessibleObject? IPlatformAccessibilityHost.AccessibilityRoot
        => disposed || Root.IsDisposed ? null : PlatformAccessibleObjectAdapter.From(Root.AccessibilityObject);

    private event Action<IPlatformAccessibleObject, int, int, int>? accessibilityNotification;

    event Action<IPlatformAccessibleObject, int, int, int>? IPlatformAccessibilitySurface.AccessibilityNotification
    {
        add => accessibilityNotification += value;
        remove => accessibilityNotification -= value;
    }

    /// <summary>Gets the most recently assigned logical surface size.</summary>
    public Size LogicalSize { get; private set; }

    /// <summary>Gets or sets native occlusion overlapping this surface, in logical pixels.</summary>
    /// <remarks>
    /// Set on the owning UI thread after native inset or density changes. SafeArea constrains
    /// the existing content root and triggers layout and rendering; application Padding is
    /// preserved. Fractional sides round outward and clamp to the surface size. Ime is reported
    /// for application keyboard-avoidance policy and does not resize content automatically.
    /// Input, rendering and accessibility continue to use the same surface coordinate system.
    /// </remarks>
    public WindowKit.WindowInsets Insets
    {
        get => insets;
        set
        {
            ThrowIfDisposed();
            if (insets == value)
                return;
            insets = value;
            LayoutContent();
            InsetsChanged?.Invoke(this, new WindowKit.WindowInsetsChangedEventArgs(value));
        }
    }

    /// <summary>Occurs after inset-driven content layout on the owning UI thread.</summary>
    public event EventHandler<WindowKit.WindowInsetsChangedEventArgs>? InsetsChanged;

    /// <summary>Gets or sets the drag distance, in logical pixels, that cancels a tap.</summary>
    /// <remarks>
    /// A scrollable ancestor may take ownership after this distance is exceeded. Values are
    /// interpreted after the platform has converted physical coordinates by its density scale.
    /// </remarks>
    public int PointerDragThreshold
    {
        get => pointerDragThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            pointerDragThreshold = value;
        }
    }

    /// <summary>Gets the number of pointer sequences currently tracked by the surface.</summary>
    public int ActivePointerCount => pointers.Count;

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
        LayoutContent();
    }

    private void LayoutContent()
    {
        var safe = insets.SafeArea;
        var left = (int)Math.Min(LogicalSize.Width, Math.Ceiling(safe.Left));
        var top = (int)Math.Min(LogicalSize.Height, Math.Ceiling(safe.Top));
        var right = (int)Math.Min(LogicalSize.Width - left, Math.Ceiling(safe.Right));
        var bottom = (int)Math.Min(LogicalSize.Height - top, Math.Ceiling(safe.Bottom));
        // Reuse the real surface parent so Dock/Anchor, rendering offsets, hit testing and
        // accessibility all see identical geometry. User-owned root Padding is never changed.
        surfaceRoot.ContentRectangle = new Rectangle(left, top,
            LogicalSize.Width - left - right, LogicalSize.Height - top - bottom);
        Root.Bounds = surfaceRoot.ContentRectangle;
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
        if (action == ControlSurfacePointerAction.Cancel)
        {
            CancelAllPointers();
            return;
        }

        ProcessPointer(0, action, x, y);
    }

    /// <summary>Routes one identified pointer through hit testing, capture, gestures, and click generation.</summary>
    /// <param name="pointerId">A platform-stable identifier for the complete pointer sequence.</param>
    /// <param name="action">The pointer transition.</param>
    /// <param name="x">The horizontal position in logical pixels.</param>
    /// <param name="y">The vertical position in logical pixels.</param>
    /// <remarks>
    /// Every active pointer owns independent capture state. Touch movement does not synthesize
    /// hover events. A valid tap raises exactly one framework click before its mouse-up transition,
    /// matching the existing window-host ordering.
    /// </remarks>
    public void ProcessPointer(int pointerId, ControlSurfacePointerAction action, int x, int y)
    {
        ThrowIfDisposed();
        var location = new Point(x, y);
        var hit = HitTest(surfaceRoot, location);
        var clickGenerated = false;
        var cancelled = false;
        PointerState? processedState = null;

        switch (action)
        {
            case ControlSurfacePointerAction.Down:
                if (pointers.Remove(pointerId, out var replaced))
                    CancelPointer(replaced);

                pointerDownRouteDepth++;
                try
                {
                    FinishComposingText();
                    foreach (var control in observedControls.Where(control => control.Selected).ToArray())
                        control.Deselect(preservePointerInteraction: IsPointerOwnedBy(control));
                }
                finally
                {
                    pointerDownRouteDepth--;
                }

                var target = hit?.Control;
                var scrollCandidate = FindScrollableAncestor(target);
                var downState = new PointerState(pointerId, location, target, scrollCandidate);
                pointers.Add(pointerId, downState);
                processedState = downState;

                if (target is not null)
                {
                    target.RaiseMouseDown(CreateMouseArgs(target, location, MouseButtons.Left, 0, pointerId));
                    downState.CapturedControl = target;
                }

                if (FindSelectedControl() is null)
                    FindSelectableAt(surfaceRoot, location)?.Select();
                break;

            case ControlSurfacePointerAction.Move:
                if (!pointers.TryGetValue(pointerId, out var moveState))
                    break;
                processedState = moveState;

                var totalX = location.X - moveState.DownLocation.X;
                var totalY = location.Y - moveState.DownLocation.Y;
                var exceededThreshold = (long)totalX * totalX + (long)totalY * totalY >
                    (long)PointerDragThreshold * PointerDragThreshold;

                if (exceededThreshold && moveState.ClickEligible)
                {
                    moveState.ClickEligible = false;
                    if (moveState.ScrollCandidate is not null)
                    {
                        moveState.CapturedControl?.CancelPointerInteraction(pointerId);
                        moveState.CapturedControl = null;
                        moveState.GestureOwner = moveState.ScrollCandidate;
                        moveState.GestureOwner.Capture = true;
                        cancelled = true;
                    }
                }

                var delta = new Point(location.X - moveState.LastLocation.X, location.Y - moveState.LastLocation.Y);
                if (!moveState.ClickEligible && moveState.ScrollCandidate?.ScrollByTouchDelta(delta) == true)
                    moveState.GestureOwner = moveState.ScrollCandidate;
                else if (moveState.GestureOwner is null && moveState.CapturedControl is not null)
                    moveState.CapturedControl.RaiseMouseMove(
                        CreateMouseArgs(moveState.CapturedControl, location, MouseButtons.Left, 0, pointerId));

                moveState.LastLocation = location;
                break;

            case ControlSurfacePointerAction.Up:
                if (!pointers.Remove(pointerId, out var upState))
                    break;
                processedState = upState;

                if (upState.GestureOwner is not null)
                {
                    upState.GestureOwner.CancelPointerInteraction(pointerId);
                }
                else if (upState.CapturedControl is not null)
                {
                    var releasedOnCapture = hit is not null && ReferenceEquals(hit.Value.Control, upState.CapturedControl);
                    var upArgs = CreateMouseArgs(upState.CapturedControl, location, MouseButtons.Left, 1, pointerId);
                    if (upState.ClickEligible && releasedOnCapture)
                    {
                        upState.CapturedControl.RaiseClick(upArgs);
                        clickGenerated = true;
                    }

                    upState.CapturedControl.RaiseMouseUp(upArgs);
                }
                break;

            case ControlSurfacePointerAction.Cancel:
                if (pointers.Remove(pointerId, out var cancelState))
                {
                    processedState = cancelState;
                    CancelPointer(cancelState);
                    cancelled = true;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        WritePointerDiagnostic(pointerId, action, location, hit?.Control,
            pointers.TryGetValue(pointerId, out var active) ? active : processedState,
            clickGenerated, cancelled);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Routes committed platform text to the selected framework control.</summary>
    /// <param name="text">The complete Unicode text committed by the platform IME.</param>
    public void CommitText(string text) => CommitText(text, 1);

    /// <summary>Routes committed platform text and its requested caret position to the selected editor.</summary>
    /// <param name="text">The complete Unicode text committed by the platform IME.</param>
    /// <param name="newCursorPosition">
    /// A position relative to the inserted text: positive values are relative to its end minus one;
    /// zero or negative values are relative to its start.
    /// </param>
    public void CommitText(string text, int newCursorPosition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);

        var selected = FindSelectedControl();
        if (selected is null)
            return;

        if (TextInputClient is { } client)
            client.CommitText(text, newCursorPosition);
        else if (text.Length > 0)
            selected.RaiseKeyPress(new KeyPressEventArgs(text));

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the active IME composition in the selected framework text box.</summary>
    /// <param name="text">The complete current composing text supplied by the platform IME.</param>
    /// <remarks>
    /// The existing composition is replaced atomically. Composition and the visible selection are
    /// tracked independently in the selected text document. Call this method on the surface's UI
    /// thread.
    /// </remarks>
    public void SetComposingText(string text) => SetComposingText(text, 1);

    /// <summary>Replaces the active IME composition and applies the requested caret position.</summary>
    /// <param name="text">The complete current composing text.</param>
    /// <param name="newCursorPosition">
    /// A position relative to the inserted text: positive values are relative to its end minus one;
    /// zero or negative values are relative to its start.
    /// </param>
    public void SetComposingText(string text, int newCursorPosition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);
        TextInputClient?.SetComposingText(text, newCursorPosition);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks an existing UTF-16 range as the active IME composition.</summary>
    /// <param name="start">One edge of the requested range.</param>
    /// <param name="end">The other edge of the requested range.</param>
    /// <remarks>
    /// Values are clipped to the current text. This operation does not change text, caret, or
    /// selection, matching Android's input-connection contract.
    /// </remarks>
    public void SetComposingRegion(int start, int end)
    {
        ThrowIfDisposed();
        TextInputClient?.SetComposingRegion(start, end);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Finishes the current IME composition without removing its committed text.</summary>
    public void FinishComposingText()
    {
        ThrowIfDisposed();
        TextInputClient?.FinishComposition();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets a text, caret, selection, and composition snapshot for the platform IME.</summary>
    /// <returns>The selected text-box state, or <see langword="null"/> when no text box is selected.</returns>
    public ControlSurfaceTextInputState? GetTextInputState()
    {
        ThrowIfDisposed();
        if (FindSelectedControl() is not TextBox textBox)
            return null;

        var cursor = textBox.document.CursorIndex;
        var selectionStart = textBox.SelectionStart >= 0 ? textBox.SelectionStart : cursor;
        var selectionEnd = textBox.SelectionEnd >= 0 ? textBox.SelectionEnd : cursor;
        return new ControlSurfaceTextInputState(
            textBox.Text,
            selectionStart,
            selectionEnd,
            textBox.document.CompositionStart,
            textBox.document.CompositionEnd,
            textBox.document.Revision);
    }

    /// <summary>Sets the selected text range requested by a platform input method.</summary>
    /// <param name="start">The inclusive UTF-16 selection start.</param>
    /// <param name="end">The exclusive UTF-16 selection end.</param>
    public void SetTextSelection(int start, int end)
    {
        ThrowIfDisposed();
        TextInputClient?.SetSelection(start, end);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deletes text around the caret in response to a platform input connection.</summary>
    /// <param name="beforeLength">Maximum UTF-16 code units to remove before the caret.</param>
    /// <param name="afterLength">Maximum UTF-16 code units to remove after the caret.</param>
    /// <remarks>
    /// The framework deletes complete Unicode text elements, so a request that intersects a
    /// surrogate pair, emoji sequence, or combining sequence removes that element atomically.
    /// </remarks>
    public void DeleteSurroundingText(int beforeLength, int afterLength)
    {
        ThrowIfDisposed();
        if (beforeLength < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeLength));
        if (afterLength < 0)
            throw new ArgumentOutOfRangeException(nameof(afterLength));
        TextInputClient?.DeleteSurroundingText(beforeLength, afterLength);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Routes a platform key-down transition to the selected framework control.</summary>
    /// <param name="key">The platform-neutral framework key.</param>
    public void ProcessKeyDown(Keys key)
        => ProcessKeyDown(key, isTextInput: false);

    /// <summary>Routes an existing key-down transition, distinguishing physical keyboard input from IME editing requests.</summary>
    /// <param name="key">The framework key with any current modifier flags.</param>
    /// <param name="isTextInput">True for software keyboard/IME editing requests, which must bypass shortcut lookup.</param>
    /// <remarks>
    /// Call on the owning UI thread. Hardware events resolve local/ancestor and application bindings
    /// before normal control input. A standalone surface has no WindowBase scope. Repeats execute
    /// once per delivered KeyDown; successful shortcuts consume their corresponding KeyUp.
    /// Text/composition APIs continue to use the editing path and never evaluate shortcuts.
    /// </remarks>
    public void ProcessKeyDown(Keys key, bool isTextInput)
        => TryProcessKeyDown(new KeyEventArgs(key), isTextInput);

    /// <summary>Routes a platform key-up transition to the selected framework control.</summary>
    /// <param name="key">The platform-neutral framework key.</param>
    public void ProcessKeyUp(Keys key)
        => ProcessKeyUp(key, isTextInput: false);

    /// <summary>Routes a key-up transition without executing keyboard bindings again.</summary>
    /// <param name="key">The framework key with any current modifiers.</param>
    /// <param name="isTextInput">True for software keyboard/IME editing requests that bypass shortcut state.</param>
    /// <remarks>Call on the owning UI thread. A consumed hardware press cannot activate a button again on release.</remarks>
    public void ProcessKeyUp(Keys key, bool isTextInput)
        => TryProcessKeyUp(new KeyEventArgs(key), isTextInput);

    /// <summary>Routes a backward-delete request to the selected framework control.</summary>
    public void DeleteBackward()
    {
        ThrowIfDisposed();
        FindSelectedControl()?.RaiseKeyDown(new KeyEventArgs(Keys.Back));
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Detaches event handlers without disposing the borrowed control tree.</summary>
    /// <remarks>
    /// Call on the owning UI thread. Disposal is reentrant and idempotent. Pointer/composition
    /// callback failures do not suppress the remaining cleanup; one failure is rethrown and multiple
    /// failures are aggregated after detaching the borrowed root. Public operations then reject access.
    /// </remarks>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        surfaceRoot.IsRetired = true;
        // Revoke surface callbacks before invoking control-owned cancellation/composition hooks.
        // They can throw or recursively dispose the surface, but cannot resurrect this adapter.
        accessibilityNotification = null;
        surfaceRoot.AccessibilityNotification = null;
        Invalidated = null;
        InsetsChanged = null;
        var controls = observedControls.ToArray();
        var failures = new List<Exception>();
        CaptureCleanupFailure(textInputHost.Dispose, failures);
        CaptureCleanupFailure(ResetKeyboardStateCore, failures);
        foreach (var control in controls)
            CaptureCleanupFailure(() => Unobserve(control), failures);
        observedControls.Clear();
        CaptureCleanupFailure(() => CancelAllPointersCore(invalidate: false), failures);
        foreach (var textBox in controls.OfType<TextBox>())
            if (!textBox.IsDisposed)
                CaptureCleanupFailure(() => textBox.document.FinishComposition(), failures);
        CaptureCleanupFailure(() => surfaceRoot.Controls.Remove(Root), failures);
        // A throwing AssignParent notification may leave the collection already empty but its
        // parent field stale. Reconcile through the existing internal parent setter before the
        // owned synthetic root is disposed, so the borrowed tree remains reusable.
        if (ReferenceEquals(Root.Parent, surfaceRoot))
            CaptureCleanupFailure(() => Root.SetParentInternal(null), failures);
        if (!ReferenceEquals(Root.Parent, surfaceRoot))
            CaptureCleanupFailure(surfaceRoot.Dispose, failures);
        ThrowCleanupFailures(failures);
    }

    private Control? FindSelectedControl()
        => observedControls.LastOrDefault(control => control.Selected);

    private static Control? FindSelectableAt(Control parent, Point point)
    {
        foreach (var child in parent.Controls.GetAllControls().Reverse())
        {
            if (!child.Visible || !child.Enabled || !child.PresentationContains(point))
                continue;

            Point childPoint = child.ParentPresentationPointToClient(point);
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
        control.LostFocus += OnControlLostFocus;
        control.GotFocus += OnControlGotFocus;
        control.Click += OnControlClick;
        foreach (var child in control.Controls.GetAllControls())
            ObserveTree(child);
    }

    private void Unobserve(Control control)
    {
        control.Invalidated -= OnControlInvalidated;
        control.ControlAdded -= OnControlAdded;
        control.ControlRemoved -= OnControlRemoved;
        control.LostFocus -= OnControlLostFocus;
        control.GotFocus -= OnControlGotFocus;
        control.Click -= OnControlClick;
        observedControls.Remove(control);
    }

    private void OnControlInvalidated(object? sender, EventArgs<Rectangle> e)
        => Invalidated?.Invoke(this, EventArgs.Empty);

    private void OnControlClick(object? sender, MouseEventArgs e)
    {
        if (accessibilityNotification is not null && sender is Control control)
            accessibilityNotification(PlatformAccessibleObjectAdapter.From(control.AccessibilityObject)!,
                PlatformAccessibilitySurfaceEvents.Invoked, 0, 0);
    }

    private void OnControlAdded(object? sender, EventArgs<Control> e)
        => ObserveTree(e.Value);

    private void OnControlRemoved(object? sender, EventArgs<Control> e)
    {
        // A detached/reinserted ancestor retires an in-flight key even if the editor's immediate
        // parent and eventual focus identity remain unchanged after application callbacks.
        keyboardTreeVersion++;
        CancelPointersOwnedBy(e.Value);

        if (e.Value is TextBox textBox)
            textBox.document.FinishComposition();
        UnobserveTree(e.Value);
    }

    private void OnControlGotFocus(object? sender, EventArgs e)
    {
        // Programmatic Select (including accessibility ACTION_FOCUS) has no window adapter
        // to deselect the old editor. Keep the same single input target as pointer routing.
        // Snapshot because focus-loss handlers may mutate the tree or redirect focus.
        foreach (var previous in observedControls.Where(control => control.Selected && control != sender).ToArray())
            previous.Deselect();
    }

    private void OnControlLostFocus(object? sender, EventArgs e)
    {
        // Starting another touch pointer deselects the previously focused control, but does not
        // terminate that control's independent pointer sequence. External focus loss remains a
        // terminal condition and clears the corresponding router ownership below.
        if (pointerDownRouteDepth > 0)
            return;

        if (sender is Control control)
            CancelPointersOwnedBy(control);
    }

    private void CancelPointersOwnedBy(Control control)
    {
        // Focus loss and detach are terminal for a control-owned gesture. Remove the router entry
        // as well as clearing Control.Capture so a later move/up cannot reach a stale text editor.
        foreach (var pointer in pointers.Values.Where(pointer =>
                     IsSelfOrDescendant(pointer.CapturedControl, control) ||
                     IsSelfOrDescendant(pointer.GestureOwner, control) ||
                     IsSelfOrDescendant(pointer.ScrollCandidate, control)).ToArray())
        {
            pointers.Remove(pointer.PointerId);
            CancelPointer(pointer);
        }
    }

    private bool IsPointerOwnedBy(Control control)
        => pointers.Values.Any(pointer =>
            IsSelfOrDescendant(pointer.CapturedControl, control) ||
            IsSelfOrDescendant(pointer.GestureOwner, control));

    private void CancelAllPointers(bool invalidate = true)
    {
        ThrowIfDisposed();
        CancelAllPointersCore(invalidate);
    }

    private void CancelAllPointersCore(bool invalidate)
    {
        var captured = pointers.Values.ToArray();
        // Clear ownership before callbacks: recursive cancellation must see an empty old gesture
        // set, and a failure in one pointer cannot leave a second pointer owned by a retired host.
        pointers.Clear();
        var failures = new List<Exception>();
        foreach (var pointer in captured)
            CaptureCleanupFailure(() => CancelPointer(pointer), failures);
        CaptureCleanupFailure(() => surfaceRoot.Capture = false, failures);
        if (invalidate)
            CaptureCleanupFailure(() => Invalidated?.Invoke(this, EventArgs.Empty), failures);
        ThrowCleanupFailures(failures);
    }

    private static void CancelPointer(PointerState pointer)
    {
        var failures = new List<Exception>();
        CaptureCleanupFailure(() => pointer.CapturedControl?.CancelPointerInteraction(pointer.PointerId), failures);
        if (!ReferenceEquals(pointer.GestureOwner, pointer.CapturedControl))
            CaptureCleanupFailure(() => pointer.GestureOwner?.CancelPointerInteraction(pointer.PointerId), failures);
        ThrowCleanupFailures(failures);
    }

    private static void CaptureCleanupFailure(Action action, List<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }

    private static void ThrowCleanupFailures(List<Exception> failures)
    {
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Surface input or lifecycle cleanup failed.", failures);
    }

    private void UnobserveTree(Control control)
    {
        foreach (var child in control.Controls.GetAllControls().ToArray())
            UnobserveTree(child);
        Unobserve(control);
    }

    private static HitTarget? HitTest(Control control, Point localPoint)
    {
        foreach (var child in control.Controls.GetAllControls().Reverse())
        {
            if (!child.Visible || !child.Enabled || !child.PresentationContains(localPoint))
                continue;

            Point childPoint = child.ParentPresentationPointToClient(localPoint);
            var descendant = HitTest(child, childPoint);
            if (descendant is not null)
                return descendant;
            if (child.GetControlBehavior(ControlBehaviors.ReceivesMouseEvents))
                return new HitTarget(child);
        }

        return control.Enabled && control.GetControlBehavior(ControlBehaviors.ReceivesMouseEvents)
            ? new HitTarget(control)
            : null;
    }

    private static ScrollableControl? FindScrollableAncestor(Control? target)
    {
        for (var current = target; current is not null; current = current.Parent)
        {
            if (current.Parent is ScrollableControl owner && owner.IsInternalScrollControl(current))
                return null;
            if (current is ScrollableControl scrollable && scrollable.AutoScroll)
                return scrollable;
        }

        return null;
    }

    private static MouseEventArgs CreateMouseArgs(
        Control target,
        Point surfaceLocation,
        MouseButtons button,
        int clicks,
        int pointerId)
    {
        var local = SurfaceToControl(target, surfaceLocation);
        return new MouseEventArgs(
            button,
            clicks,
            local.X,
            local.Y,
            Point.Empty,
            surfaceLocation.X,
            surfaceLocation.Y,
            Keys.None,
            pointerId,
            PointerDeviceKind.Touch);
    }

    private static Point SurfaceToControl(Control target, Point surfaceLocation)
    {
        var result = surfaceLocation;
        var ancestors = new Stack<Control>();
        for (var current = target; current.Parent is not null; current = current.Parent)
            ancestors.Push(current);
        while (ancestors.TryPop(out var control))
            result = control.ParentPresentationPointToClient(result);
        return result;
    }

    private static bool IsSelfOrDescendant(Control? control, Control ancestor)
    {
        for (var current = control; current is not null; current = current.Parent)
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }

    private void WritePointerDiagnostic(
        int pointerId,
        ControlSurfacePointerAction action,
        Point location,
        Control? hit,
        PointerState? state,
        bool clickGenerated,
        bool cancelled)
    {
        if (pointerDiagnosticSink is null)
            return;

        pointerDiagnosticSink(
            $"pointer={pointerId} action={action} logical=({location.X},{location.Y}) " +
            $"hit={DescribeControl(hit)} captured={DescribeControl(state?.CapturedControl)} " +
            $"gesture={DescribeControl(state?.GestureOwner)} click={clickGenerated} cancelled={cancelled}");
    }

    private static string DescribeControl(Control? control)
        => control is null ? "none" : string.IsNullOrWhiteSpace(control.Name)
            ? control.GetType().Name
            : $"{control.GetType().Name}#{control.Name}";

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed class SurfaceRootControl : Control, IControlSurfaceAccessibilitySink, IControlTextInputRoot
    {
        public ControlTextInputHost? TextInputHost { get; set; }
        public bool IsRetired { get; set; }

        protected override ControlCollection CreateControlsInstance() => new SurfaceControlCollection(this);

        private sealed class SurfaceControlCollection(SurfaceRootControl owner) : ControlCollection(owner)
        {
            public override void Insert(int index, Control value)
            {
                // Detach notifications may try to return the borrowed tree to its old parent.
                // Reject adoption before mutating the canonical collection, otherwise synthetic
                // parent disposal could dispose a caller-owned tree reinserted during cleanup.
                ObjectDisposedException.ThrowIf(owner.IsRetired, owner);
                base.Insert(index, value);
            }
        }

        internal override bool IsCommandRoutingRoot => true;

        public Rectangle ContentRectangle { get; set; }

        public override Rectangle DisplayRectangle => ContentRectangle;

        public Action<IPlatformAccessibleObject, int, int, int>? AccessibilityNotification { get; set; }

        public void NotifyAccessibility(IPlatformAccessibleObject source, int eventId, int objectId, int childId)
            => AccessibilityNotification?.Invoke(source, eventId, objectId, childId);

        public override bool Visible
        {
            get => true;
            set
            {
                // The native surface owns visibility. Child controls still keep their own state.
            }
        }
    }

    private readonly record struct HitTarget(Control Control);

    private sealed class PointerState(
        int pointerId,
        Point downLocation,
        Control? target,
        ScrollableControl? scrollCandidate)
    {
        public int PointerId { get; } = pointerId;
        public Point DownLocation { get; } = downLocation;
        public Point LastLocation { get; set; } = downLocation;
        public Control? CapturedControl { get; set; } = target;
        public ScrollableControl? ScrollCandidate { get; } = scrollCandidate;
        public ScrollableControl? GestureOwner { get; set; }
        public bool ClickEligible { get; set; } = true;
    }
}
