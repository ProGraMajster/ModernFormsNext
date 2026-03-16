using SkiaSharp;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Represents a translucent glass-like background brush.
    /// This is a visual glass effect, not a true background blur.
    /// </summary>
    public class GlassBrush : Brush
    {
        /// <summary>
        /// Gets or sets the main tint color of the glass surface.
        /// </summary>
        public SKColor TintColor { get; set; } = new SKColor (255, 255, 255, 28);

        /// <summary>
        /// Gets or sets the optional secondary tint color used to create a subtle vertical depth gradient.
        /// </summary>
        public SKColor SecondaryTintColor { get; set; } = new SKColor (255, 255, 255, 12);

        /// <summary>
        /// Gets or sets the highlight color drawn near the top of the glass.
        /// </summary>
        public SKColor HighlightColor { get; set; } = new SKColor (255, 255, 255, 38);

        /// <summary>
        /// Gets or sets the border color of the glass surface.
        /// </summary>
        public SKColor BorderColor { get; set; } = new SKColor (255, 255, 255, 65);

        /// <summary>
        /// Gets or sets whether the glass surface should draw the soft top highlight.
        /// </summary>
        public bool ShowHighlight { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the glass surface should draw the inner border.
        /// </summary>
        public bool ShowInnerBorder { get; set; } = true;
    }
}
