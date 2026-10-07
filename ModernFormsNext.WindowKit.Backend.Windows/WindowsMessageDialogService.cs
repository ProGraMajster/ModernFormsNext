using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Backend.Windows;

internal sealed class WindowsMessageDialogService : IPlatformMessageDialogService
{
    private NativeSession? active;
    private bool shutdown;

    internal WindowsMessageDialogService(IPlatformApplicationLifecycleNotifications lifecycle)
    {
        lifecycle.LifecycleChanged += (_, e) => {
            if (e.Current.Phase is not (PlatformApplicationPhase.Exiting or PlatformApplicationPhase.Exited)) return;
            shutdown = true;
            active?.Abort(new PlatformServiceException(PlatformServiceStatus.Shutdown));
        };
    }

    public Task<int> ShowAsync(IWindowBaseImpl? owner, PlatformMessageDialogRequest request, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (shutdown) throw new PlatformServiceException(PlatformServiceStatus.Shutdown);
        if (active is not null) throw new PlatformServiceException(PlatformServiceStatus.Busy);
        nint handle = owner?.Handle?.Handle ?? 0;
        if (owner is not null && (handle == 0 || !IsWindow(handle) || !IsWindowVisible(handle) ||
            GetWindowThreadProcessId(handle, out _) != GetCurrentThreadId()))
            throw new PlatformServiceException(PlatformServiceStatus.Unavailable);
        // Reserve before scheduling: a reentrant caller cannot enqueue a second native modal.
        var session = new NativeSession(owner, handle, request, cancellationToken, Release);
        active = session;
        session.Schedule();
        return session.Task;
    }

    private void Release(NativeSession session)
    {
        if (ReferenceEquals(active, session)) active = null;
    }

    internal static uint Flags(PlatformMessageDialogRequest request, bool noOwner) => (request.Buttons switch {
        MessageBoxButtons.OK => 0u, MessageBoxButtons.OKCancel => 1u,
        MessageBoxButtons.YesNo => 4u, MessageBoxButtons.YesNoCancel => 3u,
        MessageBoxButtons.RetryCancel => 5u, _ => throw new ArgumentOutOfRangeException(nameof(request))
    }) | (request.Icon switch {
        MessageBoxIcon.None => 0u, MessageBoxIcon.Information => 0x40u, MessageBoxIcon.Warning => 0x30u,
        MessageBoxIcon.Error => 0x10u, MessageBoxIcon.Question => 0x20u,
        _ => throw new ArgumentOutOfRangeException(nameof(request))
    }) | (noOwner ? 0x2000u : 0u); // MB_TASKMODAL disables this thread's windows for no-owner calls.

    internal static int Selection(MessageBoxButtons buttons, int result) => (buttons, result) switch {
        (MessageBoxButtons.OK or MessageBoxButtons.OKCancel, 1) => 0,
        (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, 6) => 0,
        (MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel, 7) => 1,
        (MessageBoxButtons.RetryCancel, 4) => 0,
        (MessageBoxButtons.OKCancel or MessageBoxButtons.RetryCancel, 2) => 1,
        (MessageBoxButtons.YesNoCancel, 2) => 2,
        _ => throw new InvalidOperationException("MessageBox returned an unexpected button.")
    };

    private sealed class NativeSession(IWindowBaseImpl? window, nint owner, PlatformMessageDialogRequest request,
        CancellationToken token, Action<NativeSession> release)
    {
        private readonly TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private nint dialog, hook;
        private HookProc? callback;
        private DispatcherOperation? operation;
        private CancellationTokenRegistration registration;
        private Exception? failure;
        private bool showing, disposed;
        internal Task<int> Task => completion.Task;

        internal void Schedule()
        {
            try
            {
                if (window is not null) window.Closed += OwnerClosed;
                registration = token.Register(() => Dispatcher.UIThread.Post(() => Abort(new OperationCanceledException(token))));
                // InvokeAsync always queues this Action, including on the UI thread. Retain the
                // operation so failed scheduling/shutdown cannot silently strand the public Task.
                operation = Dispatcher.UIThread.InvokeAsync((Action)Show);
                operation.Aborted += SchedulingAborted;
                if (operation.Status == DispatcherOperationStatus.Aborted) SchedulingAborted(null, EventArgs.Empty);
            }
            catch (Exception error) { Abort(error); }
        }

        private void OwnerClosed() => Abort(new PlatformServiceException(PlatformServiceStatus.HostLost));
        private void SchedulingAborted(object? sender, EventArgs e)
            => Abort(new PlatformServiceException(PlatformServiceStatus.Shutdown));

        private void Show()
        {
            if (disposed) return;
            int selection = -1;
            try
            {
                token.ThrowIfCancellationRequested();
                // Ownership can change after ShowAsync returns but before this callback runs.
                // Never fall back to an unowned dialog or a reused native handle.
                if (window is not null && (window.Handle?.Handle != owner || !IsWindow(owner) ||
                    !IsWindowVisible(owner) || GetWindowThreadProcessId(owner, out _) != GetCurrentThreadId()))
                    throw new PlatformServiceException(PlatformServiceStatus.HostLost);
                showing = true;
                // The actual call stays on the UI thread and uses only the OS modal loop.
                // The thread-local hook lets dispatcher cancellation close even Yes/No dialogs.
                callback = Observe;
                hook = SetWindowsHookExW(5, callback, 0, GetCurrentThreadId());
                if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                int result = MessageBoxW(owner, request.Message, request.Title, Flags(request, owner == 0));
                if (failure is null)
                {
                    if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                    selection = Selection(request.Buttons, result);
                }
            }
            catch (Exception error) { failure ??= error; }
            finally
            {
                showing = false;
                Finish(selection);
            }
        }
        private nint Observe(int code, nint wParam, nint lParam)
        {
            if (code == 5 && dialog == 0) // HCBT_ACTIVATE
            {
                var name = new StringBuilder(32);
                GetClassNameW(wParam, name, name.Capacity);
                if (name.ToString() == "#32770" && GetWindow(wParam, 4) == owner)
                {
                    dialog = wParam;
                    if (failure is not null) Dispatcher.UIThread.Post(() => { if (!disposed && dialog != 0) EndDialog(dialog, 0); });
                }
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }
        internal void Abort(Exception reason)
        {
            if (disposed || failure is not null) return;
            failure = reason;
            if (dialog != 0) EndDialog(dialog, 0);
            // While MessageBoxW is unwinding, retain the Busy slot and native callback.
            // Before show, retire immediately even if the dispatcher will never run again.
            if (!showing) Finish(-1);
        }
        private void Finish(int selection)
        {
            if (disposed) return;
            disposed = true;
            registration.Dispose();
            if (window is not null) window.Closed -= OwnerClosed;
            if (operation is not null)
            {
                operation.Aborted -= SchedulingAborted;
                operation.Abort();
                operation = null;
            }
            if (hook != 0) UnhookWindowsHookEx(hook);
            hook = dialog = 0;
            callback = null;
            release(this);
            if (failure is OperationCanceledException canceled) completion.TrySetCanceled(canceled.CancellationToken);
            else if (failure is not null) completion.TrySetException(failure);
            else completion.TrySetResult(selection);
        }
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool EndDialog(nint dialog, nint result);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(nint window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
