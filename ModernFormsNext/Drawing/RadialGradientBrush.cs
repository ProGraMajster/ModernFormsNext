using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Paints a circular radial gradient with an optional focal origin.
    /// </summary>
    public class RadialGradientBrush : GradientBrush
    {
        private PointF centerPoint = new(0.5f, 0.5f);
        private PointF gradientOrigin = new(0.5f, 0.5f);
        private float radius = 0.5f;
        private bool originWasSet;

        /// <summary>
        /// Gets or sets the platform-neutral normalized center of the outer gradient circle.
        /// </summary>
        /// <value>A finite point relative to the painted bounds. The default is (0.5, 0.5).</value>
        /// <remarks>
        /// Until <see cref="GradientOrigin"/> is assigned explicitly, changing this property keeps
        /// the origin at the same point for compatibility with the original centered radial brush.
        /// Coordinates do not receive a second DPI conversion.
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
                if (!originWasSet)
                    gradientOrigin = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the platform-neutral normalized focal origin of the gradient.
        /// </summary>
        /// <value>A finite point relative to the painted bounds. The default follows the center.</value>
        /// <remarks>
        /// A different origin creates a two-point conical gradient whose outer circle remains
        /// centered at <see cref="CenterPoint"/>. Changing it invalidates rendering only.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when either component is not finite.</exception>
        public PointF GradientOrigin
        {
            get => gradientOrigin;
            set
            {
                ValidatePoint(value, nameof(GradientOrigin));
                originWasSet = true;
                if (gradientOrigin.Equals(value))
                    return;

                gradientOrigin = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the normalized circular radius.
        /// </summary>
        /// <value>
        /// A finite non-negative multiplier of the smaller painted dimension. The default is 0.5.
        /// A zero radius is valid and renders the final stop color across the area.
        /// </value>
        /// <remarks>
        /// The smaller-dimension rule preserves the established circular behavior across control
        /// resizing and on Windows and Android. Changing the radius invalidates rendering only.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is negative, NaN, or infinity.
        /// </exception>
        public float Radius
        {
            get => radius;
            set
            {
                if (!float.IsFinite(value) || value < 0f)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The radius must be finite and non-negative.");
                if (radius.Equals(value))
                    return;

                radius = value;
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
    }
}
