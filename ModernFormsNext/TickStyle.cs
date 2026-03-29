namespace ModernFormsNext
{
    /// <summary>
    /// Specifies the position of tick marks on a TrackBar control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration defines where tick marks are rendered relative to the track.
    /// </para>
    /// <para>
    /// The meaning of <see cref="TopLeft"/> and <see cref="BottomRight"/> depends on the orientation:
    /// <list type="bullet">
    /// <item><description>Horizontal TrackBar → Top / Bottom</description></item>
    /// <item><description>Vertical TrackBar → Left / Right</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public enum TickStyle
    {
        /// <summary>
        /// No tick marks are displayed.
        /// </summary>
        None,

        /// <summary>
        /// Tick marks are displayed on the top (horizontal) or left (vertical) side.
        /// </summary>
        TopLeft,

        /// <summary>
        /// Tick marks are displayed on the bottom (horizontal) or right (vertical) side.
        /// </summary>
        BottomRight,

        /// <summary>
        /// Tick marks are displayed on both sides of the track.
        /// </summary>
        Both
    }
}