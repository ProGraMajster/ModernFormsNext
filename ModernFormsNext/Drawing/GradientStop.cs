using System;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a color stop in a gradient brush.
    /// </summary>
    public class GradientStop
    {
        private float offset;

        /// <summary>
        /// Gets or sets the color of the gradient stop.
        /// </summary>
        public SKColor Color { get; set; }

        /// <summary>
        /// Gets or sets the position of the gradient stop in the range from 0 to 1.
        /// </summary>
        public float Offset {
            get => offset;
            set => offset = Math.Clamp (value, 0f, 1f);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GradientStop"/> class.
        /// </summary>
        /// <param name="color">The stop color.</param>
        /// <param name="offset">The stop position in the range from 0 to 1.</param>
        public GradientStop (SKColor color, float offset)
        {
            Color = color;
            Offset = offset;
        }
    }
}
