using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Paints an angular (conical) gradient around a center point.
    /// </summary>
    public class SweepGradientBrush : GradientBrush
    {
        private PointF centerPoint = new(0.5f, 0.5f);
        private float startAngle;
        private float endAngle = 360f;

        /// <summary>
        /// Gets or sets the platform-neutral normalized center of the angular gradient.
        /// </summary>
        /// <value>A finite point relative to current paint bounds. The default is (0.5, 0.5).</value>
        /// <remarks>
        /// Coordinates do not receive a second DPI conversion. Changing this property raises
        /// <see cref="Brush.Changed"/> and affects rendering, not layout.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when either component is not finite.</exception>
        public PointF CenterPoint
        {
            get => centerPoint;
            set
            {
                ValidatePoint(value, nameof(CenterPoint));
                if (centerPoint.Equals(value))
                    return;

                centerPoint = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the start angle in clockwise degrees.
        /// </summary>
        /// <value>A finite angle. The default is 0 degrees.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite.</exception>
        public float StartAngle
        {
            get => startAngle;
            set
            {
                ValidateAngle(value, nameof(StartAngle));
                if (startAngle.Equals(value))
                    return;

                startAngle = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the end angle in clockwise degrees.
        /// </summary>
        /// <value>A finite angle. The default is 360 degrees.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite.</exception>
        public float EndAngle
        {
            get => endAngle;
            set
            {
                ValidateAngle(value, nameof(EndAngle));
                if (endAngle.Equals(value))
                    return;

                endAngle = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="CenterPoint"/>.
        /// </summary>
        /// <remarks>
        /// This ModernFormsNext 1.8 compatibility property shares the same backing value. Prefer
        /// <see cref="CenterPoint"/> in platform-neutral code.
        /// </remarks>
        public SKPoint Center
        {
            get => new(centerPoint.X, centerPoint.Y);
            set => CenterPoint = new PointF(value.X, value.Y);
        }

        private static void ValidateAngle(float value, string parameterName)
        {
            if (!float.IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName, value, "The angle must be finite.");
        }
    }
}
