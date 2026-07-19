using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Paints a linear gradient between two points relative to the current paint bounds.
    /// </summary>
    public class LinearGradientBrush : GradientBrush
    {
        private PointF start = new(0f, 0f);
        private PointF end = new(1f, 1f);

        /// <summary>
        /// Gets or sets the platform-neutral normalized start position.
        /// </summary>
        /// <value>
        /// A finite point resolved as <c>bounds.Left + bounds.Width * X</c> and
        /// <c>bounds.Top + bounds.Height * Y</c>. Values outside 0..1 are allowed so repeat and
        /// reflect patterns can be positioned beyond the painted area.
        /// </value>
        /// <remarks>
        /// Coordinates are relative and do not receive a second DPI conversion. Changing the
        /// point raises <see cref="Brush.Changed"/> and affects rendering, not layout.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when either component is not finite.</exception>
        public PointF Start
        {
            get => start;
            set
            {
                ValidatePoint(value, nameof(Start));
                if (start.Equals(value))
                    return;

                start = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the platform-neutral normalized end position.
        /// </summary>
        /// <value>
        /// A finite point resolved against the current paint bounds. Values outside 0..1 are
        /// allowed.
        /// </value>
        /// <remarks>
        /// Coordinates are relative and do not receive a second DPI conversion. Changing the
        /// point raises <see cref="Brush.Changed"/> and affects rendering, not layout.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when either component is not finite.</exception>
        public PointF End
        {
            get => end;
            set
            {
                ValidatePoint(value, nameof(End));
                if (end.Equals(value))
                    return;

                end = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="Start"/>.
        /// </summary>
        /// <remarks>
        /// This ModernFormsNext 1.8 compatibility property shares the same backing value. Prefer
        /// <see cref="Start"/> in platform-neutral code.
        /// </remarks>
        public SKPoint StartPoint
        {
            get => new(start.X, start.Y);
            set => Start = new PointF(value.X, value.Y);
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="End"/>.
        /// </summary>
        /// <remarks>
        /// This ModernFormsNext 1.8 compatibility property shares the same backing value. Prefer
        /// <see cref="End"/> in platform-neutral code.
        /// </remarks>
        public SKPoint EndPoint
        {
            get => new(end.X, end.Y);
            set => End = new PointF(value.X, value.Y);
        }
    }
}
