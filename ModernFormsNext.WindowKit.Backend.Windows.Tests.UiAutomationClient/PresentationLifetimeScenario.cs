using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

internal static class PresentationLifetimeScenario
{
    internal static int Run(string handle)
    {
        try {
            var root = AutomationElement.FromHandle(new nint(long.Parse(handle)));
            var action = Find(root, "presentation.advance");
            var first = Find(root, "item.0");
            int[] identity = first.GetRuntimeId();
            using var winEvents = new VisibilityEvents(new nint(long.Parse(handle)));
            int delivered = 0;
            var elapsed = Stopwatch.StartNew();
            for (int cycle = 0; cycle < 100; cycle++) {
                using var changed = new AutoResetEvent(false);
                int expected = cycle * 2 + 1;
                AutomationPropertyChangedEventHandler listener = (_, e) => {
                    if (e.Property == AutomationElement.NameProperty && Equals(e.NewValue, "Presentation " + Volatile.Read(ref expected))) {
                        Interlocked.Increment(ref delivered); changed.Set();
                    }
                };
                Automation.AddAutomationPropertyChangedEventHandler(root, TreeScope.Element, listener, AutomationElement.NameProperty);
                try {
                    ((InvokePattern)action.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    Require(changed.WaitOne(TimeSpan.FromSeconds(5)), "Missing hide event at cycle " + cycle);
                    expected++;
                    ((InvokePattern)action.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    Require(changed.WaitOne(TimeSpan.FromSeconds(5)), "Missing show event at cycle " + cycle);
                    var rediscovered = Find(root, "item.0");
                    Require(identity.SequenceEqual(rediscovered.GetRuntimeId()), "Reparent changed canonical runtime identity.");
                    Require(!rediscovered.Current.IsOffscreen, "Shown child remained offscreen.");
                    Require(first.Current.Name == "0", "Retained provider became unavailable after reparent.");
                } finally { Automation.RemoveAutomationPropertyChangedEventHandler(root, listener); }
            }
            Require(delivered == 200, "Lost or duplicated root Name events: " + delivered);
            Require(SpinWait.SpinUntil(() => winEvents.Hidden >= 100 && winEvents.Shown >= 100, TimeSpan.FromSeconds(5)), "MSAA visibility events were lost.");
            Require(winEvents.RootHidden == 0, "A descendant HIDE was addressed to CHILDID_SELF.");
            Require(winEvents.Hidden < 500 && winEvents.Shown < 500, "MSAA subtree visibility was emitted per descendant.");
            Console.WriteLine(JsonSerializer.Serialize(new { cycles = 100, delivered, elapsedMs = elapsed.Elapsed.TotalMilliseconds,
                stableIdentity = true, msaaHidden = winEvents.Hidden, msaaShown = winEvents.Shown, rootHidden = winEvents.RootHidden }));
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static AutomationElement Find(AutomationElement root, string id) => root.FindFirst(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, id)) ?? throw new InvalidOperationException("Missing " + id);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    // Out-of-context WinEvent hooks need their own message loop. Filter the owned HWND
    // and OBJID_CLIENT; OS window visibility and unrelated applications are excluded.
    private sealed class VisibilityEvents : IDisposable
    {
        private readonly Thread thread;
        private readonly HookCallback callback;
        private uint threadId;
        private int hidden, shown, rootHidden;
        public int Hidden => Volatile.Read(ref hidden);
        public int Shown => Volatile.Read(ref shown);
        public int RootHidden => Volatile.Read(ref rootHidden);
        public VisibilityEvents(nint hwnd)
        {
            using var ready = new ManualResetEventSlim();
            Exception? failure = null;
            callback = (_, kind, target, objectId, childId, _, _) => {
                if (target != hwnd || objectId != -4) return;
                if (kind == 0x8003) { Interlocked.Increment(ref hidden); if (childId == 0) Interlocked.Increment(ref rootHidden); }
                if (kind == 0x8002) Interlocked.Increment(ref shown);
            };
            thread = new Thread(() => {
                threadId = GetCurrentThreadId();
                _ = GetWindowThreadProcessId(hwnd, out uint process);
                nint hook = SetWinEventHook(0x8002, 0x8003, 0, callback, process, 0, 0);
                if (hook == 0) failure = new InvalidOperationException("SetWinEventHook failed.");
                ready.Set();
                if (hook == 0) return;
                try { while (GetMessage(out var message, 0, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); } }
                finally { _ = UnhookWinEvent(hook); }
            }) { IsBackground = true };
            thread.Start();
            Require(ready.Wait(TimeSpan.FromSeconds(5)), "WinEvent hook did not initialize.");
            if (failure is not null) throw failure;
        }
        public void Dispose() { _ = PostThreadMessage(threadId, 0x0012, 0, 0); Require(thread.Join(TimeSpan.FromSeconds(5)), "WinEvent loop did not terminate."); GC.KeepAlive(callback); }
        private delegate void HookCallback(nint hook, uint kind, nint hwnd, int objectId, int childId, uint thread, uint time);
        [StructLayout(LayoutKind.Sequential)] private struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
        [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, HookCallback callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint hwnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern nint DispatchMessage(ref Message message);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    }
}
