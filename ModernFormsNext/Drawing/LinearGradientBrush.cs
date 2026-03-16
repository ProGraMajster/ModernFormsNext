using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a linear gradient brush.
    /// </summary>
    public class LinearGradientBrush : GradientBrush
    {
        /// <summary>
        /// Gets or sets the normalized start point of the gradient.
        /// Values are relative to the painted bounds, usually in the range from 0 to 1.
        /// </summary>
        public SKPoint StartPoint { get; set; } = new (0f, 0f);

        /// <summary>
        /// Gets or sets the normalized end point of the gradient.
        /// Values are relative to the painted bounds, usually in the range from 0 to 1.
        /// </summary>
        public SKPoint EndPoint { get; set; } = new (1f, 1f);
    }
}
