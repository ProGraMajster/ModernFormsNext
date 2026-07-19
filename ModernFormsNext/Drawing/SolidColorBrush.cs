using System;
using DrawingColor = System.Drawing.Color;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a brush that paints an area with a solid color.
    /// </summary>
    public class SolidColorBrush : Brush
    {
        private DrawingColor paintColor;

        /// <summary>
        /// Initializes a new solid brush with a transparent color.
        /// </summary>
        public SolidColorBrush()
            : this(DrawingColor.Transparent)
        {
        }

        /// <summary>
        /// Initializes a new solid brush with a platform-neutral color.
        /// </summary>
        /// <param name="color">The color, including its alpha channel.</param>
        public SolidColorBrush(DrawingColor color)
        {
            paintColor = Normalize(color);
        }

        /// <summary>
        /// Initializes a new solid brush from an existing Skia-compatible color.
        /// </summary>
        /// <param name="color">The color, including its alpha channel.</param>
        /// <remarks>
        /// This overload preserves the ModernFormsNext 1.8 source surface. New platform-neutral
        /// code can use <see cref="SolidColorBrush(DrawingColor)"/>.
        /// </remarks>
        public SolidColorBrush(SKColor color)
            : this(DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue))
        {
        }

        /// <summary>
        /// Gets or sets the platform-neutral solid color, including alpha.
        /// </summary>
        /// <remarks>
        /// Changing the color raises <see cref="Brush.Changed"/> synchronously and invalidates
        /// controls using the brush. It does not affect layout. The same behavior is used on
        /// Windows and Android.
        /// </remarks>
        /// <example>
        /// <code>
        /// var brush = new SolidColorBrush(System.Drawing.Color.CornflowerBlue)
        /// {
        ///     Opacity = 0.8f
        /// };
        /// </code>
        /// </example>
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

        private static DrawingColor Normalize(DrawingColor color) => DrawingColor.FromArgb(color.ToArgb());
    }
}
