using System.Collections.Generic;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Base class for gradient brushes.
    /// </summary>
    public abstract class GradientBrush : Brush
    {
        /// <summary>
        /// Gets the collection of gradient stops.
        /// </summary>
        public List<GradientStop> GradientStops { get; } = new ();
    }
}
