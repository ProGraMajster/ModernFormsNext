namespace ModernFormsNext.WindowKit.Controls
{
    
    /// <summary>
    /// Identifies the edge or corner of a window that participates in a platform resize operation.
    /// </summary>
    /// <remarks>
    /// The numeric order is significant for backend conversion code and must remain aligned
    /// with the platform mapping used by existing implementations.
    /// </remarks>
    public enum WindowEdge
    {
        //Please don't reorder stuff here, I was lazy to write proper conversion code
        //so the order of values is matching one from GTK
        /// <summary>
        /// The top-left resize corner.
        /// </summary>
        NorthWest = 0,
        /// <summary>
        /// The top resize edge.
        /// </summary>
        North,
        /// <summary>
        /// The top-right resize corner.
        /// </summary>
        NorthEast,
        /// <summary>
        /// The left resize edge.
        /// </summary>
        West,
        /// <summary>
        /// The right resize edge.
        /// </summary>
        East,
        /// <summary>
        /// The bottom-left resize corner.
        /// </summary>
        SouthWest,
        /// <summary>
        /// The bottom resize edge.
        /// </summary>
        South,
        /// <summary>
        /// The bottom-right resize corner.
        /// </summary>
        SouthEast,
    }
}
