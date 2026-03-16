using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a sweep (angular) gradient brush.
    /// </summary>
    public class SweepGradientBrush : GradientBrush
    {
        private SKPoint center = new (0.5f, 0.5f);
        private float startAngle = 0f;
        private float endAngle = 360f;

        /// <summary>
        /// Gets or sets the normalized center point of the gradient.
        /// Values are relative to the painted bounds, usually in the range from 0 to 1.
        /// </summary>
        public SKPoint Center {
            get => center;
            set => center = value;
        }

        /// <summary>
        /// Gets or sets the start angle of the gradient in degrees.
        /// </summary>
        public float StartAngle {
            get => startAngle;
            set => startAngle = value;
        }

        /// <summary>
        /// Gets or sets the end angle of the gradient in degrees.
        /// </summary>
        public float EndAngle {
            get => endAngle;
            set => endAngle = value;
        }
    }
}
