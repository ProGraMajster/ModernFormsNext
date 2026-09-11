namespace ModernFormsNext;

public sealed partial class SkiaControlSurface
{
    private readonly int keyboardThreadId = Environment.CurrentManagedThreadId;
    private long keyboardGeneration;
    private long keyboardTreeVersion;
    private bool resettingKeyboardState;

    /// <summary>Routes a key-down and reports whether framework input consumed it.</summary>
    /// <param name="e">A caller-owned event. Its handled/suppression state is preserved.</param>
    /// <param name="isTextInput">True for software keyboard/IME editing that must bypass shortcuts.</param>
    /// <param name="isDeadKey">True for a native dead key; bypasses control/shortcut processing and retires stale suppression for this key.</param>
    /// <returns>True when handled or suppressed, including when callbacks retire the captured route.</returns>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="ObjectDisposedException">The surface was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The call is not on the owning UI thread.</exception>
    /// <remarks>
    /// Uses the existing control/ancestor/surface/Application resolver. Each delivered repeat is
    /// evaluated once. Control handlers run before synthetic Enter/Tab input; their handling and
    /// lifetime/focus changes are respected. Tab uses ordinary framework traversal unless the
    /// control requests Tab input. Other characters must arrive through the separate text path.
    /// No native keyboard layout is interpreted. Exceptions propagate; a command-consumed event
    /// remains marked in <paramref name="e"/> even when its command throws. Retain neither this
    /// event nor a control obtained from it as a replacement for native focus ownership.
    /// </remarks>
    public bool TryProcessKeyDown(KeyEventArgs e, bool isTextInput = false, bool isDeadKey = false)
    {
        VerifyKeyboardAccess();
        ArgumentNullException.ThrowIfNull(e);
        if (isDeadKey) inputBindingResolver.ResetKey(e.KeyData);
        if (resettingKeyboardState) e.SuppressKeyPress = true;
        if (e.Handled || e.SuppressKeyPress) return true;
        // A dead-key identity is not an editing command, even when Control/Alt accompany it.
        // Native translation owns its subsequent composed text; do not select/delete/activate.
        if (isDeadKey) return false;
        if (Root.IsDisposed || !Root.Enabled || !Root.Visible || e.KeyCode == Keys.None) return false;

        var selected = FindSelectedControl();
        var parent = selected?.Parent;
        var generation = keyboardGeneration;
        var treeVersion = keyboardTreeVersion;
        if (!isTextInput && !isDeadKey && !textInputHost.IsCompositionEditingKey(e.KeyData))
            inputBindingResolver.ProcessKeyDown(e, selected, Root, null);

        if (!IsKeyboardRouteCurrent(selected, parent, generation, treeVersion))
            RetireKeyboardDown(e, generation);
        else if (!e.Handled && !e.SuppressKeyPress)
        {
            selected?.RaiseKeyDown(e);
            if (!IsKeyboardRouteCurrent(selected, parent, generation, treeVersion))
                RetireKeyboardDown(e, generation);
            else if (!e.Handled && !e.SuppressKeyPress && !isDeadKey)
            {
                if (e.KeyCode == Keys.Tab && selected?.WantsTabKey != true)
                {
                    // The surface root is a Control, not a ControlAdapter. Reuse the same
                    // traversal that the adapter invokes for translated Tab, exactly once.
                    e.SuppressKeyPress = true;
                    Root.SelectNextControl(selected, !e.Shift, true, true, true);
                }
                else if (e.KeyCode is Keys.Enter or Keys.Tab && selected is not null)
                {
                    var text = new KeyPressEventArgs(e.KeyCode == Keys.Tab ? "\t" : "\r", e.KeyData);
                    selected.RaiseKeyPress(text);
                    if (text.Handled) e.Handled = true;
                    if (!IsKeyboardRouteCurrent(selected, parent, generation, treeVersion))
                        RetireKeyboardDown(e, generation);
                }
            }
        }

        if (!disposed && !Root.IsDisposed) Invalidated?.Invoke(this, EventArgs.Empty);
        if (!IsKeyboardRouteCurrent(selected, parent, generation, treeVersion))
            RetireKeyboardDown(e, generation);
        return e.Handled || e.SuppressKeyPress;
    }

    /// <summary>Routes a key-up or cancels its keyboard interaction without activating a control.</summary>
    /// <param name="e">A caller-owned event whose handled/suppression state is preserved.</param>
    /// <param name="isTextInput">True for IME editing releases that bypass shortcut state.</param>
    /// <param name="isCanceled">True for a canceled native release; no control KeyUp or click is dispatched.</param>
    /// <returns>True for a consumed, handled, suppressed, canceled or retired input route.</returns>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <exception cref="ObjectDisposedException">The surface was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The call is not on the owning UI thread.</exception>
    /// <remarks>
    /// Call on the owning UI thread. Releases never execute a shortcut. A canceled release clears
    /// this key's consumed state and keyboard-only visual/effect state, preserving pointer gestures,
    /// text, selection and focus. It does not synthesize normal KeyUp or a release ripple. Callback
    /// exceptions propagate after mandatory cancellation. No event is retargeted after a callback.
    /// </remarks>
    public bool TryProcessKeyUp(KeyEventArgs e, bool isTextInput = false, bool isCanceled = false)
    {
        VerifyKeyboardAccess();
        ArgumentNullException.ThrowIfNull(e);
        if (resettingKeyboardState)
        {
            e.SuppressKeyPress = true;
            return true;
        }
        var selected = FindSelectedControl();
        var parent = selected?.Parent;
        var generation = keyboardGeneration;
        var treeVersion = keyboardTreeVersion;
        if (isCanceled)
        {
            inputBindingResolver.ResetKey(e.KeyData);
            e.SuppressKeyPress = true;
            selected?.CancelKeyboardInteraction();
        }
        else if (!isTextInput && inputBindingResolver.ProcessKeyUp(e))
        {
            // A consumed Down owns its release even when focus has moved elsewhere.
        }
        else if (!e.Handled && !e.SuppressKeyPress && IsKeyboardRouteCurrent(selected, parent, generation, treeVersion))
            selected?.RaiseKeyUp(e);

        if (!disposed && !Root.IsDisposed) Invalidated?.Invoke(this, EventArgs.Empty);
        if (!IsKeyboardRouteCurrent(selected, parent, generation, treeVersion)) e.SuppressKeyPress = true;
        return e.Handled || e.SuppressKeyPress;
    }

    /// <summary>Clears consumed keys and cancels keyboard-only interaction after native focus loss or suspension.</summary>
    /// <remarks>
    /// Call on the owning UI thread before a later native host resumes input. No KeyUp, Click,
    /// text, focus or binding-registration change is generated. Independent pointers retain their
    /// state. All observed controls are cleaned even if an effect or invalidation callback fails;
    /// one original exception or an aggregate of multiple failures is then propagated.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The surface was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The call is not on the owning UI thread.</exception>
    public void ResetKeyboardState()
    {
        VerifyKeyboardAccess();
        ResetKeyboardStateCore();
    }

    private void ResetKeyboardStateCore()
    {
        keyboardGeneration++;
        inputBindingResolver.Reset();
        if (resettingKeyboardState) return;
        resettingKeyboardState = true;
        var failures = new List<Exception>();
        try
        {
            foreach (var control in observedControls.ToArray())
                CaptureCleanupFailure(control.CancelKeyboardInteraction, failures);
        }
        finally { resettingKeyboardState = false; }
        ThrowCleanupFailures(failures);
    }

    private bool IsKeyboardRouteCurrent(Control? selected, Control? parent, long generation, long treeVersion)
        => !disposed && !resettingKeyboardState && generation == keyboardGeneration &&
           treeVersion == keyboardTreeVersion && !Root.IsDisposed && !Root.Disposing &&
           Root.Enabled && Root.Visible && ReferenceEquals(selected, FindSelectedControl()) &&
           (selected is null || (!selected.IsDisposed && !selected.Disposing && selected.Enabled &&
            selected.Visible && ReferenceEquals(selected.Parent, parent) && observedControls.Contains(selected) &&
            IsSelfOrDescendant(selected, Root)));

    private void RetireKeyboardDown(KeyEventArgs e, long generation)
    {
        e.SuppressKeyPress = true;
        // A newer reset owns native lifetime. Do not recreate old held-key state after it.
        if (!disposed && generation == keyboardGeneration)
            inputBindingResolver.SuppressUntilRelease(e.KeyData);
    }

    private void VerifyKeyboardAccess()
    {
        ThrowIfDisposed();
        if (Environment.CurrentManagedThreadId != keyboardThreadId)
            throw new InvalidOperationException("Surface keyboard input requires the owning UI thread.");
    }
}
