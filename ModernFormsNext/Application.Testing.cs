using ModernFormsNext.WindowKit;

namespace ModernFormsNext;

public static partial class Application
{
    private static object lifecycleRuntimeIdentity = new();

    internal static object RuntimeIdentity => lifecycleRuntimeIdentity;

    // Test hosts borrow the process, rather than closing a caller's windows or releasing its
    // command subscriptions. Swap complete owned runtime state before constructing test Forms.
    internal static IDisposable PushRuntimeStateForTesting() => new TestingRuntimeScope();

    private sealed class TestingRuntimeScope : IDisposable
    {
        private readonly CancellationTokenSource? previousCancellation = _mainLoopCancellationTokenSource;
        private readonly bool previousExiting = is_exiting;
        private readonly bool previousLoopStarted = mainLoopStarted;
        private readonly bool previousExitCleanupCompleted = exitCleanupCompleted;
        private readonly bool previousExitCleanupRunning = exitCleanupRunning;
        private readonly ApplicationLifetimeMode previousLifetimeMode = lifetimeMode;
        private readonly ICloseable? previousRoot = lifetimeRoot;
        private readonly FormCollection? previousForms = open_forms;
        private readonly EventHandler? previousExit = OnExit;
        private readonly InputBindingCollection? previousInputs = inputBindings;
        private readonly CommandBindingCollection? previousCommands = commandBindings;
        private readonly MenuBase? previousMenu = ActiveMenu;
        private readonly PopupWindow? previousPopup = ActivePopupWindow;
        private readonly ApplicationLifecycle previousLifecycle = applicationLifecycle;
        private readonly object previousIdentity = lifecycleRuntimeIdentity;
        private readonly SynchronizationContext? previousSynchronizationContext = SynchronizationContext.Current;
        private readonly object identity = new();
        private readonly int ownerThreadId = Environment.CurrentManagedThreadId;
        private bool disposed;

        internal TestingRuntimeScope()
        {
            _mainLoopCancellationTokenSource = null;
            is_exiting = false;
            mainLoopStarted = false;
            exitCleanupCompleted = false;
            exitCleanupRunning = false;
            lifetimeMode = ApplicationLifetimeMode.MainWindowClosed;
            lifetimeRoot = null;
            open_forms = new FormCollection();
            OnExit = null;
            inputBindings = null;
            commandBindings = null;
            ActiveMenu = null;
            ActivePopupWindow = null;
            applicationLifecycle = new ApplicationLifecycle();
            lifecycleRuntimeIdentity = identity;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (Environment.CurrentManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("The application test scope must be restored on its owner thread.");
            if (!ReferenceEquals(lifecycleRuntimeIdentity, identity))
                throw new InvalidOperationException("Application test scopes must be restored in reverse creation order.");
            disposed = true;
            List<Exception> failures = [];
            try
            {
                // A host can be disposed inside its Run callback. Cancel that local frame before
                // restoring borrowed state; Run's identity check prevents its later finally from
                // touching the restored application. Run owns disposal of its local token source.
                Attempt(() => _mainLoopCancellationTokenSource?.Cancel(), failures);
                Attempt(applicationLifecycle.Dispose, failures);
                Attempt(ReleaseInputBindings, failures);
                Attempt(ReleaseCommandBindings, failures);
            }
            finally
            {
                _mainLoopCancellationTokenSource = previousCancellation;
                is_exiting = previousExiting;
                mainLoopStarted = previousLoopStarted;
                exitCleanupCompleted = previousExitCleanupCompleted;
                exitCleanupRunning = previousExitCleanupRunning;
                lifetimeMode = previousLifetimeMode;
                lifetimeRoot = previousRoot;
                open_forms = previousForms;
                OnExit = previousExit;
                inputBindings = previousInputs;
                commandBindings = previousCommands;
                ActiveMenu = previousMenu;
                ActivePopupWindow = previousPopup;
                applicationLifecycle = previousLifecycle;
                lifecycleRuntimeIdentity = previousIdentity;
                SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
            }
            if (failures.Count > 0)
                throw new AggregateException("Application test runtime cleanup reported failures after restoring borrowed state.", failures);
        }

        private static void Attempt(Action action, ICollection<Exception> failures)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }
}
