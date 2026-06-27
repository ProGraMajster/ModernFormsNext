namespace ModernFormsNext
{
    /// <summary>
    /// Specifies a built-in vector icon that can be drawn inside a <see cref="Switch"/>.
    /// </summary>
    /// <remarks>
    /// Use these icons for lightweight states such as check/cross, light/dark theme, or neutral
    /// markers without adding bitmap assets. Bitmap icons can still be supplied through the
    /// switch image properties.
    /// </remarks>
    public enum SwitchIconKind
    {
        /// <summary>
        /// No built-in icon is drawn.
        /// </summary>
        None,

        /// <summary>
        /// A check mark icon.
        /// </summary>
        Check,

        /// <summary>
        /// A cross icon.
        /// </summary>
        Cross,

        /// <summary>
        /// A horizontal minus icon.
        /// </summary>
        Minus,

        /// <summary>
        /// A filled dot icon.
        /// </summary>
        Dot,

        /// <summary>
        /// A sun icon.
        /// </summary>
        Sun,

        /// <summary>
        /// A crescent moon icon.
        /// </summary>
        Moon
    }
}
