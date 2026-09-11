using System.Drawing;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;
using RawPoint = ModernFormsNext.WindowKit.Point;

namespace ModernFormsNext.Testing;

/// <summary>Delivers deterministic input through a hosted window's normal framework input pipeline.</summary>
/// <remarks>
/// All methods run on the host owner thread. Pointer coordinates are logical client pixels of the
/// complete window, including managed chrome. Control overloads translate the current presentation
/// geometry, then use ordinary hit testing: another control can intercept an obscured target.
/// Key events do not invent characters; use <see cref="TextInput"/> for committed text. Native IME,
/// OS activation, hardware layouts, touch gestures and drag-and-drop require their own integration tests.
/// </remarks>
/// <example>
/// <code>
/// using var host = ModernFormsTestHost.Create();
/// var root = new Panel();
/// var button = root.Controls.Add(new Button { Text = "Save" });
/// host.Show(root);
/// host.Input.Click(button);
/// host.Input.PressKey(Keys.Control | Keys.S);
/// host.ProcessPendingWork();
/// </code>
/// </example>
public sealed partial class TestInput
{
    private const int HistoryLimit = 64;
    private const Keys SupportedModifiers = Keys.Control | Keys.Shift | Keys.Alt | Keys.Meta | Keys.AltGraph;
    private static readonly IReadOnlyDictionary<Keys, Key> KeyMap = Enum.GetValues<Key>()
        .Select(key => (Raw: key, Forms: WindowKitKeyMapper.ToFormsKey(key)))
        .Where(pair => pair.Forms != Keys.None)
        .GroupBy(pair => pair.Forms)
        .ToDictionary(group => group.Key, group => group.First().Raw);
    private readonly ITestInputTarget window;
    private readonly KeyboardDevice keyboard = new();
    private readonly MouseDevice mouse = new();
    private readonly Queue<string> recentEvents = new();
    private MouseButtons buttons;
    private Point position;
    private bool disposed;

    internal TestInput(ITestInputTarget window) => this.window = window;

    /// <summary>Gets a detached, bounded history of input kinds without key values or committed text.</summary>
    public IReadOnlyList<string> RecentEvents
    {
        get { VerifyAccess(); return Array.AsReadOnly(recentEvents.ToArray()); }
    }

    /// <summary>Moves the pointer to logical window-client coordinates using production hit testing.</summary>
    /// <param name="location">The logical client point; outside-window values can test capture and leave behavior.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this event.</param>
    public void Move(Point location, Keys modifiers = Keys.None)
        => SendPointer(RawPointerEventType.Move, location, modifiers);

    /// <summary>Moves to a hosted control's current rendered center through the normal pointer path.</summary>
    /// <param name="control">The hosted control whose presentation center supplies the point.</param>
    public void Move(Control control) => Move(GetCenter(control));

    /// <summary>Presses a left, middle, or right pointer button at logical client coordinates.</summary>
    /// <param name="location">The logical client point.</param>
    /// <param name="button">One supported button.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this event.</param>
    public void PointerDown(Point location, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None)
    {
        VerifyAccess();
        RawPointerEventType type = GetButtonType(button, down: true);
        _ = GetModifiers(modifiers);
        if (window.CanReceiveInput)
            buttons |= button;
        SendPointer(type, location, modifiers);
    }

    /// <summary>Releases a pointer button through normal capture, click, and mouse-up routing.</summary>
    /// <param name="location">The logical client point.</param>
    /// <param name="button">One supported button.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this event.</param>
    public void PointerUp(Point location, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None)
    {
        VerifyAccess();
        RawPointerEventType type = GetButtonType(button, down: false);
        _ = GetModifiers(modifiers);
        buttons &= ~button;
        SendPointer(type, location, modifiers);
    }

    /// <summary>Moves, presses, and releases one pointer button without advancing test time.</summary>
    /// <param name="location">The logical window-client point.</param>
    /// <param name="button">One supported pointer button.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this click.</param>
    /// <remarks>Successive clicks within the production double-click interval can raise DoubleClick.</remarks>
    public void Click(Point location, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None)
    {
        VerifyAccess();
        _ = GetButtonType(button, down: true);
        _ = GetModifiers(modifiers);
        try
        {
            Move(location, modifiers);
            if (IsWindowClosed()) return;
            PointerDown(location, button, modifiers);
            if (!IsWindowClosed()) PointerUp(location, button, modifiers);
        }
        catch (Exception inputFailure)
        {
            // A failed Down must not synthesize a successful Click. Cancel through the existing
            // capture-loss route so pointer flags and production pressed/capture state unwind.
            buttons = MouseButtons.None;
            if (!IsWindowClosed())
            {
                try { LoseCapture(); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Input and capture cleanup both failed.", inputFailure, cleanupFailure);
                }
            }
            throw;
        }
    }

    /// <summary>Clicks a hosted control's presentation center through production hit testing.</summary>
    /// <param name="control">A live control in this window. Hidden/disabled controls receive no direct activation.</param>
    /// <param name="button">One supported pointer button.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this click.</param>
    public void Click(Control control, MouseButtons button = MouseButtons.Left, Keys modifiers = Keys.None)
        => Click(GetCenter(control), button, modifiers);

    /// <summary>Delivers two complete clicks at the same controlled timestamp.</summary>
    /// <param name="control">The control whose current presentation center supplies the point.</param>
    public void DoubleClick(Control control)
    {
        Point center = GetCenter(control);
        Click(center);
        if (!IsWindowClosed()) Click(center);
    }

    /// <summary>Routes a wheel delta at a logical client point through the normal scroll hierarchy.</summary>
    /// <param name="location">The logical client point.</param>
    /// <param name="delta">The framework's existing horizontal/vertical wheel delta units.</param>
    /// <param name="modifiers">The explicit modifier flags accompanying this event.</param>
    public void Wheel(Point location, Point delta, Keys modifiers = Keys.None)
    {
        VerifyAccess();
        RawInputModifiers rawModifiers = GetModifiers(modifiers) | GetButtons();
        position = location;
        Send(new RawMouseWheelEventArgs(mouse, window.InputTimestamp, window.HostedWindow.adapter,
            new RawPoint(location.X, location.Y), new WindowKit.Vector(delta.X, delta.Y), rawModifiers), "Wheel");
    }

    /// <summary>Reports pointer capture loss through the same path used by a native backend.</summary>
    public void LoseCapture()
    {
        VerifyAccess();
        buttons = MouseButtons.None;
        SendPointer(RawPointerEventType.CaptureLost, position, Keys.None);
    }

    /// <summary>Reports that the pointer left this window.</summary>
    public void Leave() => SendPointer(RawPointerEventType.LeaveWindow, position, Keys.None);

    /// <summary>Delivers one physical key-down without synthesizing text.</summary>
    /// <param name="keyData">A supported framework key code combined with explicit modifier flags.</param>
    /// <returns>Whether the production preview, command resolver, or focused control handled the event.</returns>
    /// <remarks>Repeated calls deliver repeated KeyDown events through the existing command repeat policy.</remarks>
    public bool KeyDown(Keys keyData) => SendKey(keyData, RawKeyEventType.KeyDown);

    /// <summary>Delivers one key-up, preserving command-consumed release behavior.</summary>
    /// <param name="keyData">A supported framework key code combined with explicit modifier flags.</param>
    /// <returns>Whether the production input pipeline handled the event.</returns>
    public bool KeyUp(Keys keyData) => SendKey(keyData, RawKeyEventType.KeyUp);

    /// <summary>Delivers key-down and key-up without assuming a hardware keyboard layout.</summary>
    /// <param name="keyData">A supported framework key and explicit modifiers.</param>
    /// <returns>Whether key-down was handled.</returns>
    /// <remarks>Tab includes the production translated Tab text event unless key-down was handled.</remarks>
    public bool PressKey(Keys keyData)
    {
        VerifyAccess();
        _ = GetKey(keyData);
        bool handled;
        try
        {
            handled = KeyDown(keyData);
            if (!IsWindowClosed() && !handled && (keyData & Keys.KeyCode) == Keys.Tab)
                TextInput("\t", keyData & SupportedModifiers);
        }
        catch (Exception inputFailure)
        {
            // The resolver marks a key consumed before calling application commands. Release
            // even after an exception, otherwise the next unbound press stays falsely consumed.
            if (!IsWindowClosed())
            {
                try { KeyUp(keyData); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Input and key-release cleanup both failed.", inputFailure, cleanupFailure);
                }
            }
            throw;
        }
        if (!IsWindowClosed()) KeyUp(keyData);
        return handled;
    }

    /// <summary>Delivers committed Unicode text through the production raw text-input path.</summary>
    /// <param name="text">The committed text. An empty string is a no-op.</param>
    /// <param name="modifiers">Explicit text-event modifiers, including AltGraph when applicable.</param>
    /// <remarks>No key events are generated and no text is retained in input diagnostics.</remarks>
    public void TextInput(string text, Keys modifiers = Keys.None)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(text);
        RawInputModifiers rawModifiers = GetModifiers(modifiers);
        if (text.Length == 0) return;
        Send(new RawTextInputEventArgs(keyboard, window.InputTimestamp, window.HostedWindow.adapter,
            text, rawModifiers), "TextInput");
    }

    /// <summary>Moves focus using the existing forward or backward Tab event sequence.</summary>
    /// <param name="backwards">Whether Shift+Tab should be sent.</param>
    public void Tab(bool backwards = false) => PressKey(Keys.Tab | (backwards ? Keys.Shift : Keys.None));

    /// <summary>Selects a hosted control through the canonical framework focus operation.</summary>
    /// <param name="control">The control to select; disabled/hidden controls retain ordinary runtime behavior.</param>
    /// <returns>Whether the requested control is the resulting canonical focus owner.</returns>
    public bool Focus(Control control)
    {
        VerifyControl(control);
        if (!window.CanReceiveInput) return false;
        control.Select();
        return !IsWindowClosed() && ReferenceEquals(window.FocusedControl, control);
    }

    internal void Dispose()
    {
        if (disposed) return;
        disposed = true;
        buttons = MouseButtons.None;
        recentEvents.Clear();
        mouse.Dispose();
    }

    private Point GetCenter(Control control)
    {
        VerifyControl(control);
        Point screen = control.PointToScreen(new Point(control.ScaledWidth / 2, control.ScaledHeight / 2));
        Point origin = window.HostedWindow.Location;
        double scale = window.Viewport.RenderScale;
        return new Point((int)Math.Round((screen.X - origin.X) / scale),
            (int)Math.Round((screen.Y - origin.Y) / scale));
    }

    private bool SendKey(Keys keyData, RawKeyEventType type)
    {
        VerifyAccess();
        RawInputModifiers modifiers = GetModifiers(keyData & ~Keys.KeyCode);
        Key key = GetKey(keyData);
        var input = new RawKeyEventArgs(keyboard, window.InputTimestamp, window.HostedWindow.adapter, type, key, modifiers);
        Send(input, type.ToString());
        return input.Handled;
    }

    private void SendPointer(RawPointerEventType type, Point location, Keys modifiers)
    {
        VerifyAccess();
        RawInputModifiers flags = GetModifiers(modifiers) | GetButtons();
        position = location;
        Send(new RawPointerEventArgs(mouse, window.InputTimestamp, window.HostedWindow.adapter,
            type, new RawPoint(location.X, location.Y), flags), type.ToString());
    }

    private void Send(RawInputEventArgs input, string kind)
    {
        if (!window.CanReceiveInput) return;
        if (recentEvents.Count == HistoryLimit) recentEvents.Dequeue();
        recentEvents.Enqueue(kind);
        window.Backend.Input?.Invoke(input);
    }

    private void VerifyControl(Control control)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(control);
        if (control.IsDisposed || !window.Contains(control))
            throw new ArgumentException("The control must be a live member of this hosted tree.", nameof(control));
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        window.VerifyInputAccess();
    }

    private bool IsWindowClosed() => disposed || window.IsClosed || window.Backend.IsDisposed;

    private RawInputModifiers GetButtons()
        => ((buttons & MouseButtons.Left) != 0 ? RawInputModifiers.LeftMouseButton : 0)
         | ((buttons & MouseButtons.Right) != 0 ? RawInputModifiers.RightMouseButton : 0)
         | ((buttons & MouseButtons.Middle) != 0 ? RawInputModifiers.MiddleMouseButton : 0);

    private static RawInputModifiers GetModifiers(Keys modifiers)
    {
        if ((modifiers & ~SupportedModifiers) != 0)
            throw new ArgumentOutOfRangeException(nameof(modifiers), "Only modifier flags are allowed.");
        return ((modifiers & Keys.Control) != 0 ? RawInputModifiers.Control : 0)
             | ((modifiers & Keys.Shift) != 0 ? RawInputModifiers.Shift : 0)
             | ((modifiers & Keys.Alt) != 0 ? RawInputModifiers.Alt : 0)
             | ((modifiers & Keys.Meta) != 0 ? RawInputModifiers.Meta : 0)
             | ((modifiers & Keys.AltGraph) != 0 ? RawInputModifiers.AltGraph : 0);
    }

    private static Key GetKey(Keys keyData)
    {
        _ = GetModifiers(keyData & ~Keys.KeyCode);
        if (!KeyMap.TryGetValue(keyData & Keys.KeyCode, out Key key))
            throw new ArgumentOutOfRangeException(nameof(keyData), "The production WindowKit mapper does not support this key.");
        return key;
    }

    private static RawPointerEventType GetButtonType(MouseButtons button, bool down) => (button, down) switch
    {
        (MouseButtons.Left, true) => RawPointerEventType.LeftButtonDown,
        (MouseButtons.Left, false) => RawPointerEventType.LeftButtonUp,
        (MouseButtons.Right, true) => RawPointerEventType.RightButtonDown,
        (MouseButtons.Right, false) => RawPointerEventType.RightButtonUp,
        (MouseButtons.Middle, true) => RawPointerEventType.MiddleButtonDown,
        (MouseButtons.Middle, false) => RawPointerEventType.MiddleButtonUp,
        _ => throw new ArgumentOutOfRangeException(nameof(button), "Specify one left, right, or middle pointer button.")
    };
}
