using System;
using System.Threading;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Threading;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides static methods and properties to manage an application, such as methods to start and stop an application.
    /// </summary>
    public static class Application
    {
        private static CancellationTokenSource? _mainLoopCancellationTokenSource;
        private static bool is_exiting;
        private static FormCollection? open_forms;
        private static string? startup_path;

        /// <summary>
        /// This is the top level active menu, if any.
        /// </summary>
        internal static MenuBase? ActiveMenu { get; set; }

        /// <summary>
        /// This is the open popup window, like the ComboBox dropdown, if any.
        /// </summary>
        internal static PopupWindow? ActivePopupWindow { get; set; }

        internal static void ClosePopups(bool closeMenus = true, bool closePopups = true)
        {
            if (closeMenus)
                ActiveMenu?.Deactivate();

            if (closePopups)
                ActivePopupWindow?.Hide();
        }

        internal static void DoThemeChanged()
        {
            foreach (Form form in OpenForms)
                form.OnThemeChanged(EventArgs.Empty);
        }

        public static void Exit()
        {
            is_exiting = true;

            OnExit?.Invoke(null, EventArgs.Empty);

            _mainLoopCancellationTokenSource?.Cancel();
        }

        public static event EventHandler? OnExit;

        public static FormCollection OpenForms => open_forms ??= new FormCollection();

        public static void Run(Form mainForm)
        {
            FrameworkBootstrap.EnsureInitialized();

            mainForm.Show();
            Run((ICloseable)mainForm);
        }

        public static void Run(ICloseable closable)
        {
            FrameworkBootstrap.EnsureInitialized();

            if (_mainLoopCancellationTokenSource != null)
                throw new InvalidOperationException("Run should only be called once");

            closable.Closed += (s, e) => Exit();

            _mainLoopCancellationTokenSource = new CancellationTokenSource();

            Dispatcher.UIThread.MainLoop(_mainLoopCancellationTokenSource.Token);

            if (!is_exiting)
                OnExit?.Invoke(null, EventArgs.Empty);
        }

        public static void RunOnUIThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        public static string StartupPath => startup_path ??= AppContext.BaseDirectory;
    }
}