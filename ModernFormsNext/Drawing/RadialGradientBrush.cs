using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a radial gradient brush.
    /// </summary>
    public class RadialGradientBrush : GradientBrush
    {
        /// <summary>
        /// Gets or sets the normalized center point of the gradient.
        /// Values are relative to the painted bounds, usually in the range from 0 to 1.
        /// </summary>
        public SKPoint Center { get; set; } = new (0.5f, 0.5f);

        /// <summary>
        /// Gets or sets the normalized radius of the gradient.
        /// A value of 0.5 means half of the smaller painted dimension.
        /// </summary>
        public float Radius { get; set; } = 0.5f;
    }
}
