using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ModernFormsNext;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Threading;

// Exercises actual MessageBoxW HWNDs and their standard buttons in this isolated host process.
internal static class MessageDialogScenario
{
    internal static int Run(bool exitBeforeShow = false)
    {
        try {
            using var owner = new Form { Text = "MFN native message owner" };
            Exception? failure = null;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            owner.Shown += (_, _) => Dispatcher.UIThread.Post(async () => {
                try {
                    if (exitBeforeShow)
                    {
                        var pending = SystemMessageBox.ShowAsync(owner, "", "MFN never shown");
                        Require(!pending.IsCompleted, "ShowAsync did not return a pending Task.");
                        Application.Exit();
                        // Shutdown may stop the UI synchronization context. Observe the public
                        // Task independently, proving that its result mapping cannot leak.
                        await ExpectStatus(pending, PlatformServiceStatus.Shutdown).ConfigureAwait(false);
                        RequireNoDialog();
                        Console.WriteLine("MESSAGE:EXIT-BEFORE-SHOW");
                        return;
                    }
                    await BeforeShow(owner);
                    await BusyDuringShow(owner);
                    await Choice(owner, MessageBoxButtons.OK, 2, DialogResult.OK);
                    await Choice(owner, MessageBoxButtons.YesNo, 6, DialogResult.Yes);
                    await Choice(owner, MessageBoxButtons.YesNo, 7, DialogResult.No);
                    await Choice(owner, MessageBoxButtons.YesNoCancel, 2, DialogResult.Cancel);
                    await Choice(owner, MessageBoxButtons.RetryCancel, 4, DialogResult.Retry);
                    await Choice(null, MessageBoxButtons.OK, 2, DialogResult.OK);
                    using var cancellation = new CancellationTokenSource();
                    nint canceled = 0;
                    var automation = Observe("MFN cancel", hwnd => { canceled = hwnd; cancellation.Cancel(); }, 6);
                    await ExpectCancellation(SystemMessageBox.ShowAsync(owner, "Synthetic body", "MFN cancel",
                        MessageBoxButtons.YesNo, cancellationToken: cancellation.Token));
                    await automation;
                    Require(!IsWindow(canceled), "Cancellation left a native dialog.");
                    Console.WriteLine("MESSAGE:CANCEL:CLEAN");
                    using var pre = new CancellationTokenSource(); pre.Cancel();
                    await ExpectCancellation(SystemMessageBox.ShowAsync(owner, "", "", cancellationToken: pre.Token));
                    Console.WriteLine("MESSAGE:PRE-CANCEL");
                    using (var secondary = new Form { Text = "MFN secondary owner" })
                    {
                        secondary.Show();
                        var closing = Observe("MFN owner loss", _ => Dispatcher.UIThread.Post(secondary.Close));
                        try {
                            await SystemMessageBox.ShowAsync(secondary, "", "MFN owner loss");
                            throw new InvalidOperationException("Owner loss returned a button.");
                        }
                        catch (PlatformServiceException e) { Require(e.Status == PlatformServiceStatus.HostLost, "Owner loss status."); }
                        await closing;
                        Console.WriteLine("MESSAGE:OWNER-LOSS");
                    }
                    var exiting = Observe("MFN exit", _ => Dispatcher.UIThread.Post(Application.Exit));
                    try {
                        await SystemMessageBox.ShowAsync(owner, "", "MFN exit").ConfigureAwait(false);
                        throw new InvalidOperationException("Shutdown returned a button.");
                    }
                    catch (PlatformServiceException e) { Require(e.Status == PlatformServiceStatus.Shutdown, "Shutdown status."); }
                    await exiting.ConfigureAwait(false);
                    Console.WriteLine("MESSAGE:SHUTDOWN");
                }
                catch (Exception error) { failure = error; }
                finally {
                    if (Dispatcher.UIThread.CheckAccess()) owner.Close();
                    finished.TrySetResult();
                }
            });
            Application.Run(owner);
            finished.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if (failure is not null) throw failure;
            Console.WriteLine("MESSAGE:NATIVE:PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task BeforeShow(Form owner)
    {
        using (var cancellation = new CancellationTokenSource())
        {
            var task = SystemMessageBox.ShowAsync(owner, "", "MFN never shown", cancellationToken: cancellation.Token);
            // This runs on the same UI turn, before any native show callback can execute.
            Require(!task.IsCompleted, "ShowAsync waited for dismissal.");
            RequireNoDialog();
            await ExpectStatus(SystemMessageBox.ShowAsync(owner, "", "MFN overlap"), PlatformServiceStatus.Busy);
            cancellation.Cancel();
            await ExpectCancellation(task);
            RequireNoDialog();
            Console.WriteLine("MESSAGE:RETURNS-BEFORE-SHOW:BUSY:CANCEL");
        }
        using (var closed = new Form { Text = "MFN close before show" })
        {
            closed.Show();
            var task = SystemMessageBox.ShowAsync(closed, "", "MFN never shown");
            closed.Close();
            await ExpectStatus(task, PlatformServiceStatus.HostLost);
            RequireNoDialog();
            Console.WriteLine("MESSAGE:OWNER-CLOSE-BEFORE-SHOW");
        }
        using (var hidden = new Form { Text = "MFN hide before show" })
        {
            hidden.Show();
            var task = SystemMessageBox.ShowAsync(hidden, "", "MFN never shown");
            hidden.Hide();
            await ExpectStatus(task, PlatformServiceStatus.HostLost);
            RequireNoDialog();
            hidden.Close();
            Console.WriteLine("MESSAGE:OWNER-HIDE-BEFORE-SHOW");
        }
    }

    private static async Task BusyDuringShow(Form owner)
    {
        var checkedBusy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var automation = Observe("MFN busy during show", hwnd => Dispatcher.UIThread.Post(async () => {
            try {
                await ExpectStatus(SystemMessageBox.ShowAsync(owner, "", "MFN overlap"), PlatformServiceStatus.Busy);
                checkedBusy.TrySetResult();
            }
            catch (Exception error) { checkedBusy.TrySetException(error); }
            finally { PostMessageW(hwnd, 0x0111, 2, GetDlgItem(hwnd, 2)); }
        }));
        Require(await SystemMessageBox.ShowAsync(owner, "", "MFN busy during show") == DialogResult.OK, "First request lost its result.");
        await automation;
        await checkedBusy.Task;
        RequireNoDialog();
        Console.WriteLine("MESSAGE:BUSY-DURING-SHOW");
    }

    private static async Task ExpectStatus(Task<DialogResult> task, PlatformServiceStatus expected)
    {
        try { await task.ConfigureAwait(false); throw new InvalidOperationException("Expected a service failure."); }
        catch (PlatformServiceException error) { Require(error.Status == expected, $"Expected {expected}, got {error.Status}."); }
    }

    private static void RequireNoDialog()
    {
        EnumWindows((hwnd, _) => {
            GetWindowThreadProcessId(hwnd, out uint process);
            if (process != Environment.ProcessId || !IsWindowVisible(hwnd)) return true;
            var name = new StringBuilder(32); GetClassNameW(hwnd, name, name.Capacity);
            Require(name.ToString() != "#32770", "Unexpected or leaked native dialog.");
            return true;
        }, 0);
    }

    private static async Task Choice(Form? owner, MessageBoxButtons buttons, int nativeButton, DialogResult expected)
    {
        nint expectedOwner = owner?.PlatformHandle.Handle ?? 0;
        nint found = 0;
        var automation = Observe("MFN native choice", hwnd => {
            found = hwnd;
            Require(GetWindow(hwnd, 4) == expectedOwner, "Wrong native owner HWND.");
            if (expectedOwner != 0) Require(!IsWindowEnabled(expectedOwner), "Owner was not disabled modally.");
            nint button = GetDlgItem(hwnd, nativeButton);
            Require(button != 0 && IsWindowEnabled(button), "Expected native button absent.");
            PostMessageW(hwnd, 0x0111, nativeButton, button); // WM_COMMAND/BN_CLICKED reaches the real modal dialog procedure.
        }, nativeButton);
        var result = await SystemMessageBox.ShowAsync(owner, "Synthetic body", "MFN native choice", buttons, MessageBoxIcon.Information);
        await automation;
        Require(result == expected, "Wrong native DialogResult.");
        Require(!IsWindow(found), "Native HWND leaked.");
        if (expectedOwner != 0) Require(IsWindowEnabled(expectedOwner), "Owner remained disabled.");
        Console.WriteLine($"MESSAGE:CHOICE:{buttons}:{result}:OWNER:{expectedOwner != 0}:CLEAN");
    }
    private static async Task ExpectCancellation(Task<DialogResult> task)
    {
        try { await task; throw new InvalidOperationException("Cancellation returned a button."); }
        catch (OperationCanceledException) { Require(task.IsCanceled, "Task not canceled."); }
    }
    private static Task Observe(string title, Action<nint> action, int readyButton = 2) => Task.Run(() => {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(10)) {
            nint found = 0;
            EnumWindows((hwnd, _) => {
                GetWindowThreadProcessId(hwnd, out uint process);
                if (process != Environment.ProcessId || !IsWindowVisible(hwnd)) return true;
                var text = new StringBuilder(100); GetWindowTextW(hwnd, text, text.Capacity);
                var name = new StringBuilder(32); GetClassNameW(hwnd, name, name.Capacity);
                if (text.ToString() == title && name.ToString() == "#32770") found = hwnd;
                return true;
            }, 0);
            // MB_OK uses an IDCANCEL control internally (Escape is OK); other layouts use their result IDs.
            // Wait for that real child before injecting input.
            if (found != 0 && GetDlgItem(found, readyButton) is var ready && ready != 0 && IsWindowEnabled(ready))
            { action(found); return; }
            Thread.Sleep(25);
        }
        throw new TimeoutException("Native message HWND did not appear.");
    });
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private delegate bool EnumProc(nint hwnd, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessageW(nint hwnd, uint message, nint wParam, nint lParam);
}
