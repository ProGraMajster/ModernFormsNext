namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how <see cref="FontDialog"/> should render its user interface.
    /// </summary>
    /// <remarks>
    /// Use this value to choose between a platform-native font dialog and a dialog drawn by
    /// ModernFormsNext controls. The native dialog usually provides the closest operating-system
    /// behavior, while the ModernFormsNext dialog visually matches the rest of a ModernFormsNext
    /// application and can be used as a cross-backend fallback.
    /// </remarks>
    public enum FontDialogRenderingMode
    {
        /// <summary>
        /// Uses the platform dialog when the backend provides one; otherwise falls back to the
        /// ModernFormsNext-rendered dialog.
        /// </summary>
        Auto,

        /// <summary>
        /// Uses the platform-native font dialog.
        /// </summary>
        /// <remarks>
        /// On Windows, this mode uses the Win32 common font dialog through the Windows backend.
        /// Other backends must register a platform font dialog service to support this mode.
        /// </remarks>
        System,

        /// <summary>
        /// Uses a font dialog composed from ModernFormsNext controls and rendered by the framework.
        /// </summary>
        ModernFormsNext
    }
}
