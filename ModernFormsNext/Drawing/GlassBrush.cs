using System;
using DrawingColor = System.Drawing.Color;
using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a translucent glass-like visual effect without background blur.
    /// </summary>
    public class GlassBrush : Brush
    {
        private DrawingColor tint = DrawingColor.FromArgb(28, 255, 255, 255);
        private DrawingColor secondaryTint = DrawingColor.FromArgb(12, 255, 255, 255);
        private DrawingColor highlight = DrawingColor.FromArgb(38, 255, 255, 255);
        private DrawingColor border = DrawingColor.FromArgb(65, 255, 255, 255);
        private bool showHighlight = true;
        private bool showInnerBorder = true;

        /// <summary>
        /// Gets or sets the platform-neutral main tint color, including alpha.
        /// </summary>
        public DrawingColor Tint
        {
            get => tint;
            set => SetColor(ref tint, value);
        }

        /// <summary>
        /// Gets or sets the platform-neutral secondary tint used for vertical depth.
        /// </summary>
        public DrawingColor SecondaryTint
        {
            get => secondaryTint;
            set => SetColor(ref secondaryTint, value);
        }

        /// <summary>
        /// Gets or sets the platform-neutral soft highlight color.
        /// </summary>
        public DrawingColor Highlight
        {
            get => highlight;
            set => SetColor(ref highlight, value);
        }

        /// <summary>
        /// Gets or sets the platform-neutral border color.
        /// </summary>
        public DrawingColor Border
        {
            get => border;
            set => SetColor(ref border, value);
        }

        /// <summary>
        /// Gets or sets whether the glass surface draws the soft top highlight.
        /// </summary>
        public bool ShowHighlight
        {
            get => showHighlight;
            set
            {
                if (showHighlight == value)
                    return;
                showHighlight = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets whether the glass surface draws the inner border.
        /// </summary>
        public bool ShowInnerBorder
        {
            get => showInnerBorder;
            set
            {
                if (showInnerBorder == value)
                    return;
                showInnerBorder = value;
                OnChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="Tint"/>.
        /// </summary>
        public SKColor TintColor
        {
            get => ToSkia(tint);
            set => Tint = FromSkia(value);
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="SecondaryTint"/>.
        /// </summary>
        public SKColor SecondaryTintColor
        {
            get => ToSkia(secondaryTint);
            set => SecondaryTint = FromSkia(value);
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="Highlight"/>.
        /// </summary>
        public SKColor HighlightColor
        {
            get => ToSkia(highlight);
            set => Highlight = FromSkia(value);
        }

        /// <summary>
        /// Gets or sets the Skia-compatible view of <see cref="Border"/>.
        /// </summary>
        public SKColor BorderColor
        {
            get => ToSkia(border);
            set => Border = FromSkia(value);
        }

        private void SetColor(ref DrawingColor field, DrawingColor value)
        {
            DrawingColor normalized = DrawingColor.FromArgb(value.ToArgb());
            if (field.ToArgb() == normalized.ToArgb())
                return;

            field = normalized;
            OnChanged(EventArgs.Empty);
        }

        private static DrawingColor FromSkia(SKColor color)
            => DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

        private static SKColor ToSkia(DrawingColor color) => new(color.R, color.G, color.B, color.A);
    }
}
