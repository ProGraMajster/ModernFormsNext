using System;
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
    public static class Application
    {
        private static CancellationTokenSource? _mainLoopCancellationTokenSource;
        private static bool is_exiting;
        private static FormCollection? open_forms;
        private static string? startup_path;
        private static readonly ResourceDictionary resources = new();

        /// <summary>
        /// Gets the resources available to every ModernFormsNext window and control in the application.
        /// </summary>
        /// <remarks>
        /// Application resources are the final fallback after control, ancestor-control, and window
        /// scopes. Update resources used by live controls on the UI/dispatcher thread.
        /// </remarks>
        /// <example>
        /// <code>
        /// Application.Resources["Spacing.Medium"] = 12;
        /// </code>
        /// </example>
        public static ResourceDictionary Resources => resources;

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
            foreach (Form form in OpenForms)
                form.OnThemeChanged(EventArgs.Empty);
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
        /// </remarks>
        public static void Exit()
        {
            is_exiting = true;

            Animations.AnimationScheduler.ShutdownDefaultIfInitialized();

            OnExit?.Invoke(null, EventArgs.Empty);

            _mainLoopCancellationTokenSource?.Cancel();
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
        {
            FrameworkBootstrap.EnsureInitialized();
            AvaloniaSynchronizationContext.InstallIfNeeded();

            mainForm.Show();
            Run((ICloseable)mainForm);
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
        {
            FrameworkBootstrap.EnsureInitialized();

            if (_mainLoopCancellationTokenSource != null)
                throw new InvalidOperationException("Run should only be called once");

            AvaloniaSynchronizationContext.InstallIfNeeded();
            closable.Closed += (s, e) => Exit();

            _mainLoopCancellationTokenSource = new CancellationTokenSource();

            Dispatcher.UIThread.MainLoop(_mainLoopCancellationTokenSource.Token);

            Animations.AnimationScheduler.ShutdownDefaultIfInitialized();

            if (!is_exiting)
                OnExit?.Invoke(null, EventArgs.Empty);
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
