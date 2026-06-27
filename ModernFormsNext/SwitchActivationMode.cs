namespace ModernFormsNext
{
    /// <summary>
    /// Specifies how a <see cref="Switch"/> chooses its next value when activated by click,
    /// tap, keyboard, or programmatic activation.
    /// </summary>
    public enum SwitchActivationMode
    {
        /// <summary>
        /// Uses a Boolean toggle in <see cref="SwitchMode.TwoState"/> and pointer-position
        /// selection in <see cref="SwitchMode.ThreeState"/>.
        /// </summary>
        Automatic,

        /// <summary>
        /// Toggles between the off and on values. In three-state mode this skips the neutral
        /// value unless the value is already neutral, in which case it moves to the on value.
        /// </summary>
        Toggle,

        /// <summary>
        /// Advances through each available value in order.
        /// </summary>
        Cycle,

        /// <summary>
        /// Chooses the nearest value to the pointer location. Keyboard activation uses
        /// <see cref="Automatic"/> semantics because no pointer location is available.
        /// </summary>
        SetByPointerPosition
    }
}
