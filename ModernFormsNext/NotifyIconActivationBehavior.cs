namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how a <see cref="NotifyIcon"/> should use its assigned activation window
    /// when the notification area icon is clicked.
    /// </summary>
    /// <remarks>
    /// This setting is evaluated by the framework component after the tray icon click event
    /// is raised by the active platform backend. It does not require platform-specific
    /// application code. Windows is currently the primary backend that provides tray icon
    /// support.
    /// </remarks>
    public enum NotifyIconActivationBehavior
    {
        /// <summary>
        /// Do not automatically show, hide, restore, or activate a window.
        /// </summary>
        None = 0,

        /// <summary>
        /// Show the assigned window if it is hidden, restore it if minimized, and activate it.
        /// </summary>
        ShowWindow = 1,

        /// <summary>
        /// Hide the assigned window when it is visible and not minimized; otherwise show,
        /// restore, and activate it.
        /// </summary>
        ToggleWindow = 2
    }
}
