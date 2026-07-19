using System;
using DrawingColor = System.Drawing.Color;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents an observable color stop in a gradient brush.
    /// </summary>
    public class GradientStop
    {
        private DrawingColor paintColor;
        private float offset;

        /// <summary>
        /// Initializes a gradient stop with a platform-neutral color and validated offset.
        /// </summary>
        /// <param name="color">The stop color, including alpha.</param>
        /// <param name="offset">The finite stop position in the inclusive range 0 through 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="offset"/> is not finite or is outside 0..1.
        /// </exception>
        public GradientStop(DrawingColor color, float offset)
        {
            paintColor = Normalize(color);
            Offset = offset;
        }

        /// <summary>
        /// Initializes a gradient stop from an existing Skia-compatible color.
        /// </summary>
        /// <param name="color">The stop color, including alpha.</param>
        /// <param name="offset">The finite stop position in the inclusive range 0 through 1.</param>
        /// <remarks>
        /// This overload preserves the ModernFormsNext 1.8 source surface. New platform-neutral
        /// code can use <see cref="GradientStop(DrawingColor, float)"/>.
        /// </remarks>
        public GradientStop(SKColor color, float offset)
            : this(DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue), offset)
        {
        }

        /// <summary>
        /// Occurs when the color or offset changes.
        /// </summary>
        /// <remarks>
        /// A containing <see cref="GradientStopCollection"/> forwards this event to its brush, so
        /// mutating one stop invalidates controls using that brush without reassigning the brush.
        /// The event is raised synchronously on the mutation thread.
        /// </remarks>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets or sets the platform-neutral stop color, including alpha.
        /// </summary>
        public DrawingColor PaintColor
        {
            get => paintColor;
            set
            {
                DrawingColor normalized = Normalize(value);
                if (paintColor.ToArgb() == normalized.ToArgb())
                    return;

                paintColor = normalized;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="PaintColor"/>.
        /// </summary>
        /// <remarks>
        /// This compatibility property and <see cref="PaintColor"/> share one backing value.
        /// Prefer <see cref="PaintColor"/> in new renderer-neutral code.
        /// </remarks>
        public SKColor Color
        {
            get => new(paintColor.R, paintColor.G, paintColor.B, paintColor.A);
            set => PaintColor = DrawingColor.FromArgb(value.Alpha, value.Red, value.Green, value.Blue);
        }

        /// <summary>
        /// Gets or sets the normalized position of the gradient stop.
        /// </summary>
        /// <value>A finite value in the inclusive range 0 through 1.</value>
        /// <remarks>
        /// Stops need not be inserted in offset order. The renderer uses a stable ordered snapshot,
        /// preserving insertion order for multiple stops at the same offset. Changing the offset
        /// raises <see cref="Changed"/> and invalidates the containing brush's ordered snapshot.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is not finite or is outside 0..1.
        /// </exception>
        public float Offset
        {
            get => offset;
            set
            {
                Brush.ValidateUnitValue(value, nameof(Offset));
                if (offset.Equals(value))
                    return;

                offset = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises <see cref="Changed"/> after a rendered value changes.
        /// </summary>
        /// <param name="e">Event data for the change.</param>
        /// <remarks>
        /// Derived stops should update their state before calling this method. The current paint
        /// system does not require derived stop types, but this hook preserves the existing
        /// non-sealed extension surface.
        /// </remarks>
        protected virtual void OnChanged(EventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            Changed?.Invoke(this, e);
        }

        private static DrawingColor Normalize(DrawingColor color) => DrawingColor.FromArgb(color.ToArgb());
    }
}
