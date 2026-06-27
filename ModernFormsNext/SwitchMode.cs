namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how many logical positions a <see cref="Switch"/> exposes.
    /// </summary>
    public enum SwitchMode
    {
        /// <summary>
        /// The switch behaves like a normal Boolean switch and uses the values 0 and 1.
        /// </summary>
        TwoState,

        /// <summary>
        /// The switch exposes three positions and uses the values -1, 0, and 1.
        /// </summary>
        ThreeState
    }
}
