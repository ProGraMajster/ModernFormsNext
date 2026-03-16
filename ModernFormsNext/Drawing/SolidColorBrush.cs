using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a brush that paints an area with a solid color.
    /// </summary>
    public class SolidColorBrush : Brush
    {
        /// <summary>
        /// Gets or sets the brush color.
        /// </summary>
        public SKColor Color { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidColorBrush"/> class.
        /// </summary>
        /// <param name="color">The solid color of the brush.</param>
        public SolidColorBrush (SKColor color)
        {
            Color = color;
        }
    }
}
