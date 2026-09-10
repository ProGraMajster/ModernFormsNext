using System;
using System.Collections.Generic;
using System.Threading;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides static methods and properties for managing the lifetime of a ModernFormsNext application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is responsible for starting the UI message loop, tracking open forms,
    /// dispatching actions to the UI thread, and shutting down the application.
    /// </para>
    /// <para>
    /// Before the main loop starts, the framework backend is initialized automatically
    /// through <see cref="FrameworkBootstrap"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var form = new MainForm();
    /// Application.Run(form);
    /// </code>
    /// </example>
    public static partial class Application
    {
        private static CancellationTokenSource? _mainLoopCancellationTokenSource;
        private static bool is_exiting;
        private static bool mainLoopStarted;
        private static bool exitCleanupCompleted;
        private static bool exitCleanupRunning;
        private static ApplicationLifetimeMode lifetimeMode;
        private static ICloseable? lifetimeRoot;
        private static FormCollection? open_forms;
        private static string? startup_path;
        private static readonly ResourceDictionary resources = new();
        private static readonly ResourceDictionary themeResources = new();
        private static readonly IReadOnlyDictionary<object, object?> themeResourcesView =
            new System.Collections.ObjectModel.ReadOnlyDictionary<object, object?>(themeResources);
        [ThreadStatic]
        private static VisualInvalidationBatchState? visualInvalidationBatch;
        [ThreadStatic]
        private static int themeTransitionFrameDepth;

        /// <summary>
        /// Gets the resources available to every ModernFormsNext window and control in the application.
        /// </summary>
        /// <remarks>
        /// Application resources are checked after control, ancestor-control, and window scopes,
        /// and before manager-owned theme defaults. Update resources used by live controls on the
        /// UI/dispatcher thread.
        /// </remarks>
        /// <example>
        /// <code>
        /// Application.Resources["Spacing.Medium"] = 12;
        /// </code>
        /// </example>
        public static ResourceDictionary Resources => resources;

        /// <summary>
        /// Gets the resolved resources owned by <see cref="ThemeManager.Current"/>.
        /// </summary>
        /// <remarks>
        /// Theme resources are the final fallback after <see cref="Resources"/>. Applications
        /// should use the manager and its typed tokens instead of mutating this dictionary.
        /// </remarks>
        public static IReadOnlyDictionary<object, object?> ThemeResources => themeResourcesView;

        internal static ResourceDictionary ThemeResourcesInternal => themeResources;

        /// <summary>
        /// Gets or sets the currently active top-level menu, if one is open.
        /// </summary>
        /// <remarks>
        /// This property is used internally to track the active menu instance so it can be
        /// deactivated when focus changes or popups are closed.
        /// </remarks>
        internal static MenuBase? ActiveMenu { get; set; }

        /// <summary>
        /// Gets or sets the currently active popup window, if one is open.
        /// </summary>
        /// <remarks>
        /// This property is used internally for popup windows such as ComboBox drop-downs or
        /// other temporary floating UI elements.
        /// </remarks>
        internal static PopupWindow? ActivePopupWindow { get; set; }

        /// <summary>
        /// Closes the currently active menus and popup windows.
        /// </summary>
        /// <param name="closeMenus">
        /// <see langword="true"/> to deactivate the active menu; otherwise, <see langword="false"/>.
        /// </param>
        /// <param name="closePopups">
        /// <see langword="true"/> to hide the active popup window; otherwise, <see langword="false"/>.
        /// </param>
        /// <remarks>
        /// This method is intended for internal UI state cleanup when focus changes or when the
        /// user interacts outside of a menu or popup.
        /// </remarks>
        internal static void ClosePopups(bool closeMenus = true, bool closePopups = true)
        {
            if (closeMenus)
                ActiveMenu?.Deactivate();

            if (closePopups)
                ActivePopupWindow?.Hide();
        }

        /// <summary>
        /// Notifies all open forms that the application theme has changed.
        /// </summary>
        /// <remarks>
        /// This method iterates through <see cref="OpenForms"/> and calls each form's theme
        /// change handler.
        /// </remarks>
        internal static void DoThemeChanged()
        {
            var windows = new List<WindowBase>();
            foreach (Form form in OpenForms)
                windows.Add(form);

            if (ActivePopupWindow is { Visible: true } popup)
                windows.Add(popup);

            DoThemeChanged(windows);
        }

        internal static void DoThemeChanged(IEnumerable<WindowBase> windows)
        {
            ArgumentNullException.ThrowIfNull(windows);

            // Theme defaults are already updated when this method runs. Refresh each complete
            // visual tree before scheduling one repaint per window so normal and interaction
            // states all resolve against the same committed theme snapshot.
            using VisualInvalidationBatchScope batch = BeginVisualInvalidationBatch();
            foreach (WindowBase window in windows)
            {
                if (themeTransitionFrameDepth > 0)
                    window.RefreshThemeFrameVisuals();
                else if (window is Form form)
                    form.OnThemeChanged(EventArgs.Empty);
                else
                    window.RefreshThemeVisuals(EventArgs.Empty);
            }
        }

        internal static ThemeTransitionFrameScope BeginThemeTransitionFrame()
        {
            themeTransitionFrameDepth++;
            return new ThemeTransitionFrameScope();
        }

        internal static VisualInvalidationBatchScope BeginVisualInvalidationBatch()
        {
            VisualInvalidationBatchState state = visualInvalidationBatch ??= new VisualInvalidationBatchState();
            state.Depth++;
            return new VisualInvalidationBatchScope();
        }

        internal static void RequestVisualInvalidation(WindowBase window)
        {
            ArgumentNullException.ThrowIfNull(window);

            if (visualInvalidationBatch is { Depth: > 0 } state)
            {
                state.Windows.Add(window);
                return;
            }

            window.InvalidateCore();
        }

        internal static bool HasPendingVisualInvalidations
            => visualInvalidationBatch is { Depth: > 0 } || visualInvalidationBatch?.Windows.Count > 0;

        private static void EndVisualInvalidationBatch()
        {
            VisualInvalidationBatchState? state = visualInvalidationBatch;
            if (state is null || state.Depth <= 0)
                throw new InvalidOperationException("A visual invalidation batch was disposed on the wrong thread or out of order.");

            state.Depth--;
            if (state.Depth != 0)
                return;

            var windows = new List<WindowBase>(state.Windows);
            state.Windows.Clear();

            List<Exception>? failures = null;
            foreach (WindowBase window in windows)
            {
                try
                {
                    window.InvalidateCore();
                }
                catch (Exception exception)
                {
                    (failures ??= []).Add(exception);
                }
            }

            if (failures is { Count: > 0 })
                throw new AggregateException("One or more windows rejected a batched visual invalidation.", failures);
        }

        internal readonly struct VisualInvalidationBatchScope : IDisposable
        {
            public void Dispose() => EndVisualInvalidationBatch();
        }

        internal readonly struct ThemeTransitionFrameScope : IDisposable
        {
            public void Dispose()
            {
                if (themeTransitionFrameDepth <= 0)
                    throw new InvalidOperationException("A theme transition frame was disposed on the wrong thread or out of order.");

                themeTransitionFrameDepth--;
            }
        }

        private sealed class VisualInvalidationBatchState
        {
            public int Depth { get; set; }
            public HashSet<WindowBase> Windows { get; } = [];
        }

        /// <summary>
        /// Terminates the application by signaling the main UI loop to stop.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method marks the application as exiting, raises the <see cref="OnExit"/> event,
        /// and cancels the main loop cancellation token if the application loop is running.
        /// </para>
        /// <para>
        /// Calling this method does not forcefully terminate the process. It requests a graceful
        /// shutdown of the UI loop.
        /// </para>
        /// <para>
        /// Repeated requests are idempotent. Background-thread requests are posted to the UI thread.
        /// A request inside a lifecycle notification is latched immediately; its save/cleanup/OnExit
        /// transaction runs after that notification through the UI dispatcher. Observer failures do
        /// not prevent cleanup or loop cancellation; multiple failures are reported together.
        /// </para>
        /// </remarks>
        public static void Exit()
        {
            IPlatformDispatcher? platformDispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
            if (!(platformDispatcher?.CheckAccess() ?? Dispatcher.UIThread.CheckAccess()))
            {
                var requestIdentity = RuntimeIdentity;
                PostLifecycleWork(() =>
                {
                    if (ReferenceEquals(requestIdentity, RuntimeIdentity)) Exit();
                });
                return;
            }
            if (is_exiting)
                return;
            is_exiting = true;
            if (Lifecycle.Controller?.IsPublishing == true)
            {
                var identity = RuntimeIdentity;
                PostLifecycleWork(() =>
                {
                    if (ReferenceEquals(identity, RuntimeIdentity)) CompleteExit();
                });
                return;
            }
            CompleteExit();
        }

        private static void PostLifecycleWork(Action action)
        {
            // Android supplies its real main-thread dispatcher without a WindowKit message loop.
            // Using the shared service also keeps headless hosts on their scoped production queue.
            if (PlatformServiceRegistry.GetService<IPlatformDispatcher>() is { } dispatcher)
                dispatcher.Post(action);
            else
                Dispatcher.UIThread.Post(action);
        }

        private static void CompleteExit()
        {
            if (exitCleanupCompleted || exitCleanupRunning) return;
            exitCleanupRunning = true;
            var identity = RuntimeIdentity;
            // Commit the exit guard before callbacks. A failing save/lifecycle/OnExit observer
            // must not suppress another subscriber, resource cleanup, or loop cancellation.
            var failures = new List<Exception>();
            void CleanupOwned(Action action)
            {
                if (ReferenceEquals(identity, RuntimeIdentity))
                    AttemptLifecycleCleanup(action, failures);
            }
            CleanupOwned(NotifyLifecycleExiting);
            CleanupOwned(ReleaseInputBindings);
            CleanupOwned(ReleaseCommandBindings);
            CleanupOwned(Animations.AnimationScheduler.ShutdownDefaultIfInitialized);
            if (ReferenceEquals(identity, RuntimeIdentity) && OnExit is { } handlers)
                foreach (EventHandler handler in handlers.GetInvocationList())
                    CleanupOwned(() => handler(null, EventArgs.Empty));
            CleanupOwned(() => _mainLoopCancellationTokenSource?.Cancel());
            if (ReferenceEquals(identity, RuntimeIdentity))
            {
                exitCleanupRunning = false;
                exitCleanupCompleted = true;
                if (_mainLoopCancellationTokenSource is null)
                    AttemptLifecycleCleanup(NotifyLifecycleExited, failures);
            }
            ThrowLifecycleFailures(failures);
        }

        /// <summary>
        /// Occurs when the application is exiting.
        /// </summary>
        /// <remarks>
        /// This event is raised when <see cref="Exit"/> is called or when the main application
        /// loop finishes without the application already being marked as exiting.
        /// </remarks>
        public static event EventHandler? OnExit;

        /// <summary>
        /// Gets the collection of forms currently known to the application.
        /// </summary>
        /// <remarks>
        /// The collection is created lazily on first access.
        /// </remarks>
        public static FormCollection OpenForms => open_forms ??= new FormCollection();

        /// <summary>
        /// Starts the application using the specified main form.
        /// </summary>
        /// <param name="mainForm">The main form to show before entering the UI loop.</param>
        /// <remarks>
        /// <para>
        /// This method ensures that the backend is initialized, shows the specified form,
        /// and then starts the UI message loop.
        /// </para>
        /// <para>
        /// When the provided form is closed, the application exits automatically.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var mainForm = new MainForm();
        /// Application.Run(mainForm);
        /// </code>
        /// </example>
        public static void Run(Form mainForm)
            => Run(mainForm, ApplicationLifetimeMode.MainWindowClosed);

        /// <summary>Shows a main Form and runs the message loop using an explicit lifetime policy.</summary>
        /// <param name="mainForm">The initial Form, shown after lifetime and lifecycle handlers attach.</param>
        /// <param name="mode">The shutdown policy; the default overload uses main-root closure.</param>
        /// <remarks>
        /// Call once on the UI thread. Closing from Shown is supported. Cleanup and Exited notification
        /// run even when startup, callbacks or the message loop fail. This method does not dispose
        /// other application-owned Forms. Android Activity recreation is independent of this policy.
        /// </remarks>
        /// <example><code>Application.Run(new MainForm(), ApplicationLifetimeMode.LastWindowClosed);</code></example>
        public static void Run(Form mainForm, ApplicationLifetimeMode mode)
        {
            ArgumentNullException.ThrowIfNull(mainForm);
            RunMainLoop(mainForm, mode, mainForm.Show);
        }

        /// <summary>
        /// Starts the application using the specified closeable root object.
        /// </summary>
        /// <param name="closable">
        /// An object that controls application lifetime and exposes a <c>Closed</c> event.
        /// </param>
        /// <remarks>
        /// <para>
        /// This overload starts the UI message loop without requiring a <see cref="Form"/> instance.
        /// It is useful for advanced hosting scenarios where a custom closeable root object controls
        /// the lifetime of the application.
        /// </para>
        /// <para>
        /// This method can only be called once during the lifetime of the process.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the application main loop has already been started.
        /// </exception>
        public static void Run(ICloseable closable)
            => Run(closable, ApplicationLifetimeMode.MainWindowClosed);

        /// <summary>Runs the existing UI loop with a custom root and an explicit lifetime policy.</summary>
        /// <param name="closable">The designated lifetime root; it is not shown or disposed by this overload.</param>
        /// <param name="mode">The shutdown policy. LastWindowClosed observes Form closure independently of this root.</param>
        /// <remarks>Call once on the UI thread. The root subscription is detached when the loop returns or fails.</remarks>
        public static void Run(ICloseable closable, ApplicationLifetimeMode mode)
            => RunMainLoop(closable, mode, null);

        private static void RunMainLoop(ICloseable closable, ApplicationLifetimeMode mode, Action? show)
        {
            ArgumentNullException.ThrowIfNull(closable);
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mainLoopStarted || is_exiting)
                throw new InvalidOperationException("Run may only be called once, before application exit.");
            // The supported TestHost already scopes the production backend-facing services.
            // Starting its real application loop must not discover or initialize native services.
            if (!TestWindowFactoryScope.HasActiveFactory)
                FrameworkBootstrap.EnsureInitialized();
            Dispatcher.UIThread.VerifyAccess();
            var lifecycleController = Lifecycle.Controller;
            if (lifecycleController?.IsPublishing == true)
                throw new InvalidOperationException("Run cannot start inside a lifecycle notification; post startup to the UI dispatcher.");
            if (lifecycleController?.Snapshot.Phase is WindowKit.Backend.Lifecycle.PlatformApplicationPhase.Exiting or
                WindowKit.Backend.Lifecycle.PlatformApplicationPhase.Exited)
                throw new InvalidOperationException("Run cannot start after the platform has begun application shutdown.");
            Animations.AnimationScheduler.Default.RefreshPlatformPolicy();
            AvaloniaSynchronizationContext.InstallIfNeeded();
            mainLoopStarted = true;
            lifetimeMode = mode;
            lifetimeRoot = closable;
            var loopCancellation = new CancellationTokenSource();
            var runtimeIdentity = RuntimeIdentity;
            _mainLoopCancellationTokenSource = loopCancellation;
            EventHandler rootClosed = (_, _) =>
            {
                if (ReferenceEquals(runtimeIdentity, RuntimeIdentity) && mode == ApplicationLifetimeMode.MainWindowClosed)
                    Exit();
            };
            var failures = new List<Exception>();
            var subscribed = false;
            try
            {
                closable.Closed += rootClosed;
                subscribed = true;
                var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
                NotifyLifecycleStarting(new WindowKit.Backend.Lifecycle.PlatformApplicationActivation(
                    arguments.Length == 0 ? WindowKit.Backend.Lifecycle.PlatformActivationKind.Launch :
                    WindowKit.Backend.Lifecycle.PlatformActivationKind.Arguments, arguments));
                if (ReferenceEquals(runtimeIdentity, RuntimeIdentity) && !is_exiting)
                    show?.Invoke();
                if (ReferenceEquals(runtimeIdentity, RuntimeIdentity) && !is_exiting)
                    Dispatcher.UIThread.MainLoop(loopCancellation.Token);
            }
            catch (Exception exception) { failures.Add(exception); }
            finally
            {
                if (subscribed)
                    AttemptLifecycleCleanup(() => closable.Closed -= rootClosed, failures);
                // TestHost may be disposed from a running callback. Its scope then restores a
                // borrowed application; never tear down that restored runtime from this old loop.
                if (ReferenceEquals(runtimeIdentity, RuntimeIdentity))
                {
                    AttemptLifecycleCleanup(Exit, failures);
                    if (ReferenceEquals(runtimeIdentity, RuntimeIdentity))
                        AttemptLifecycleCleanup(CompleteExit, failures);
                    if (ReferenceEquals(runtimeIdentity, RuntimeIdentity))
                    {
                        _mainLoopCancellationTokenSource = null;
                        lifetimeRoot = null;
                        AttemptLifecycleCleanup(NotifyLifecycleExited, failures);
                    }
                }
                loopCancellation.Dispose();
            }
            ThrowLifecycleFailures(failures);
        }

        internal static void NotifyWindowClosed(WindowBase window)
        {
            NotifyLifecycleWindowStateChanged();
            if (!mainLoopStarted || is_exiting)
                return;
            if ((lifetimeMode == ApplicationLifetimeMode.MainWindowClosed && ReferenceEquals(window, lifetimeRoot)) ||
                (lifetimeMode == ApplicationLifetimeMode.LastWindowClosed && window is Form && OpenForms.Count == 0))
                Exit();
        }

        private static void AttemptLifecycleCleanup(Action action, List<Exception> failures)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }

        private static void ThrowLifecycleFailures(List<Exception> failures)
        {
            if (failures.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1)
                throw new AggregateException("Application lifecycle callbacks or cleanup failed.", failures);
        }

        /// <summary>
        /// Schedules the specified action to run on the UI thread.
        /// </summary>
        /// <param name="action">The action to execute on the UI thread.</param>
        /// <remarks>
        /// This method posts the action asynchronously to the UI dispatcher.
        /// </remarks>
        /// <example>
        /// <code>
        /// Application.RunOnUIThread(() =>
        /// {
        ///     myForm.Text = "Updated from another thread";
        /// });
        /// </code>
        /// </example>
        public static void RunOnUIThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        /// <summary>
        /// Gets the startup path of the current application.
        /// </summary>
        /// <value>
        /// The base directory of the current application.
        /// </value>
        /// <remarks>
        /// The value is initialized lazily from <see cref="AppContext.BaseDirectory"/>.
        /// </remarks>
        public static string StartupPath => startup_path ??= AppContext.BaseDirectory;
    }
}
