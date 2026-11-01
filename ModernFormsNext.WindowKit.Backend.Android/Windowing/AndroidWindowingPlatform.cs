using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Raw;

namespace ModernFormsNext.WindowKit.Backend.Android.Windowing;

// Process-owned framework windows contain no Activity, View or Java peer. Only the Activity
// owns its native presentation. Replacement revokes the old generation before any callbacks.
internal sealed class AndroidWindowingPlatform(Action verifyAccess) : IWindowingPlatform
{
    private readonly WeakHostReference<IAndroidWindowHost> host = new();
    private readonly List<AndroidWindowImpl> windows = [];
    private readonly KeyboardDevice backKeyboard = new();
    private bool routingBack;
    internal long Generation { get; private set; }
    internal IAndroidWindowHost? Host => host.Target;
    internal AndroidWindowImpl? MainWindow { get; private set; }
    internal bool Exited { get; private set; }
    internal ModernFormsNext.WindowKit.Platform.Storage.IStorageProvider? StorageProvider { get; set; }
    internal event Action? ShuttingDown;
    internal bool StartupInvoked { get; set; }
    internal IReadOnlyList<AndroidWindowImpl> Windows => windows;
    internal void VerifyAccess() => verifyAccess();

    public IWindowImpl CreateWindow()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(Exited, this);
        var window = new AndroidWindowImpl(this);
        windows.Add(window);
        return window;
    }

    internal AndroidPopupImpl CreatePopup(AndroidWindowImpl owner)
    {
        VerifyAccess();
        var popup = new AndroidPopupImpl(this, owner);
        windows.Add(popup);
        return popup;
    }

    internal long Attach(IAndroidWindowHost next)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(Exited, this);
        if (ReferenceEquals(Host, next)) return Generation;
        if (Host is { CanReplace: false })
            throw new PlatformNotSupportedException("Android supports one Activity host; concurrent Activity hosts are not supported.");
        if (Host is { } old) Detach(old, Generation);
        host.Set(next);
        Generation++;
        long operation = Generation;
        try
        {
            foreach (var window in windows.ToArray())
            {
                if (!IsCurrent(next, operation)) break;
                if (window.Visible && !window.IsPopup && !window.IsClosed)
                {
                    window.BeginPresentation();
                    next.Present(window);
                }
            }
        }
        catch (Exception attachFailure)
        {
            try { Detach(next, operation); }
            catch (Exception cleanupFailure) { throw new AggregateException("Android host attachment and cleanup failed.", attachFailure, cleanupFailure); }
            throw;
        }
        return Generation;
    }

    internal bool IsCurrent(IAndroidWindowHost candidate, long generation)
        => generation == Generation && ReferenceEquals(Host, candidate) && !Exited;

    internal void Detach(IAndroidWindowHost previous, long generation)
    {
        VerifyAccess();
        if (!IsCurrent(previous, generation)) return;
        host.ClearIfCurrent(previous);
        Generation++;
        var cleanup = new List<Action>();
        foreach (var window in windows.ToArray())
        {
            long epoch = window.PresentationEpoch;
            if (window.IsPopup && window.Visible) cleanup.Add(window.DismissPopup);
            cleanup.Add(() => window.RetirePresentation(epoch));
        }
        cleanup.Add(previous.Clear);
        Complete(cleanup);
    }

    internal void Show(AndroidWindowImpl window, bool dialog)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(Exited, this);
        var current = Host ?? throw new InvalidOperationException(
            "Attach an AndroidActivityHost from Activity.OnCreate before showing a Form.");
        // Shared Show may be repeated on a visible Form. Its existing native View must
        // retain its epoch; changing only the contract epoch would retire all View callbacks.
        if (window.Visible) { current.Update(window); return; }
        if (window.IsPopup)
        {
            if (window.Owner is not { Visible: true, IsClosed: false })
                throw new InvalidOperationException("A popup requires a visible, live owner.");
        }
        else if (dialog)
        {
            if (window.Owner is not { Visible: true, IsClosed: false } owner || !windows.Contains(owner))
                throw new InvalidOperationException("An Android modal Form requires a visible owner in the current host.");
        }
        else if (MainWindow is null || ReferenceEquals(MainWindow, window))
            MainWindow = window;
        else
            throw new PlatformNotSupportedException(
                "Android supports one main Form per Activity host. Use ShowDialog(owner) for a modal descendant.");
        window.Visible = true;
        window.IsDialog = dialog;
        // This registry is also the presentation order. Construction order is unrelated
        // to modal nesting or popup reuse; Back and replacement must use the shown order.
        windows.Remove(window);
        windows.Add(window);
        window.BeginPresentation();
        current.Present(window);
    }

    internal void Hide(AndroidWindowImpl window)
    {
        VerifyAccess();
        var previous = Host;
        long epoch = window.PresentationEpoch;
        window.Visible = false;
        Complete([
            () => HideDescendants(window),
            () => { if (window.PresentationEpoch == epoch) previous?.Hide(window); },
            () => window.RetirePresentation(epoch)
        ]);
    }

    private void HideDescendants(AndroidWindowImpl owner)
        => Complete(windows.Where(w => ReferenceEquals(w.Owner, owner) && w.Visible).Reverse()
            .Select(w => w.IsPopup ? (Action)w.DismissPopup : w.Dispose).ToArray());

    internal void Remove(AndroidWindowImpl window)
    {
        windows.Remove(window);
        if (ReferenceEquals(MainWindow, window)) MainWindow = null;
    }

    internal void CloseDescendants(AndroidWindowImpl owner)
        => Complete(windows.Where(w => ReferenceEquals(w.Owner, owner)).Reverse()
            .Select(w => (Action)w.Dispose).ToArray());

    internal bool Back()
    {
        VerifyAccess();
        if (routingBack) return true;
        var target = BackTarget();
        if (target is null) return false;
        if (target.IsPopup) target.DismissPopup();
        else if (!RouteBack(target)) target.RequestClose();
        return true;
    }

    private AndroidWindowImpl? BackTarget() => windows.LastOrDefault(w => w.Visible && w.IsPopup) ??
        windows.LastOrDefault(w => w.Visible && w.IsDialog) ?? MainWindow;

    private bool RouteBack(AndroidWindowImpl target)
    {
        if (target.IsClosed || !target.Visible || target.InputRoot is not { } root || target.Input is not { } input) return false;
        long generation = Generation, epoch = target.PresentationEpoch;
        bool IsCurrent() => !Exited && Generation == generation && !target.IsClosed && target.Visible &&
            target.PresentationEpoch == epoch && ReferenceEquals(target.InputRoot, root) && ReferenceEquals(target.Input, input);
        ulong timestamp = unchecked((ulong)Environment.TickCount64);
        var down = new RawKeyEventArgs(backKeyboard, timestamp, root, RawKeyEventType.KeyDown, Key.BrowserBack, RawInputModifiers.None);
        RawKeyEventArgs? up = null;
        routingBack = true;
        try
        {
            Complete([() => input(down), () =>
            {
                // Pair a delivered press only on its original live route. Hide/close/recreation
                // already retire that route's input state; never release into a replacement peer.
                if (IsCurrent())
                {
                    up = new(backKeyboard, timestamp, root, RawKeyEventType.KeyUp, Key.BrowserBack, RawInputModifiers.None);
                    input(up);
                }
            }]);
            // Reentrant native replacement consumes the old Back even without Handled, so it
            // cannot close a newly shown dialog/Activity after application code has taken over.
            return !IsCurrent() || !ReferenceEquals(BackTarget(), target) || down.Handled || up?.Handled == true;
        }
        finally { routingBack = false; }
    }

    internal void Shutdown()
    {
        VerifyAccess();
        if (Exited) return;
        Exited = true;
        ShuttingDown?.Invoke();
        var previous = Host;
        if (previous is not null) host.ClearIfCurrent(previous);
        Generation++;
        var cleanup = windows.AsEnumerable().Reverse().Select(w => (Action)w.Dispose).ToList();
        if (previous is not null) { cleanup.Add(previous.Clear); cleanup.Add(previous.Finish); }
        Complete(cleanup);
    }

    internal static void Complete(IEnumerable<Action> actions)
    {
        List<Exception> failures = [];
        foreach (var action in actions)
            try { action(); } catch (Exception e) { failures.Add(e); }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Android window cleanup failed.", failures);
    }
}

// Native implementation and deterministic tests use this same presentation seam. It owns
// native resources, not framework windows, and is retained only weakly by the platform.
internal interface IAndroidWindowHost
{
    bool CanReplace { get; }
    void Present(AndroidWindowImpl window);
    void Hide(AndroidWindowImpl window);
    void Update(AndroidWindowImpl window);
    // Native hosts can update composition/input without repeating geometry/layout each frame.
    void UpdatePopupPresentation(AndroidWindowImpl window) => Update(window);
    void Activate(AndroidWindowImpl window);
    void Invalidate(AndroidWindowImpl window);
    void Clear();
    void Finish();
}
