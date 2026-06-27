namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how printing-related dialogs should render their user interface.
    /// </summary>
    /// <remarks>
    /// Use this value to choose between a platform-native common dialog and a dialog drawn by
    /// ModernFormsNext controls. Native dialogs usually provide the closest operating-system
    /// behavior, while the ModernFormsNext dialog visually matches the rest of a ModernFormsNext
    /// application and can be used as a cross-backend fallback.
    /// </remarks>
    public enum PrintingDialogRenderingMode
    {
        /// <summary>
        /// Uses the platform dialog when the backend provides one; otherwise falls back to the
        /// ModernFormsNext-rendered dialog.
        /// </summary>
        Auto,

        /// <summary>
        /// Uses the platform-native printing dialog.
        /// </summary>
        /// <remarks>
        /// On Windows, this mode uses Win32 common printing dialogs through the Windows backend.
        /// Other backends must register a platform print dialog service to support this mode.
        /// </remarks>
        System,

        /// <summary>
        /// Uses a printing dialog composed from ModernFormsNext controls and rendered by the framework.
        /// </summary>
        ModernFormsNext
    }
}
