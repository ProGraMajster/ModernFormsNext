using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="ToolTip.Draw"/> event.
    /// </summary>
    /// <remarks>
    /// Unlike Windows Forms, ModernFormsNext does not expose a <c>System.Drawing.Graphics</c>
    /// object for tooltip owner drawing. Tooltip rendering is platform-neutral and SkiaSharp-
    /// based, so custom drawing uses <see cref="SKCanvas"/> and ModernFormsNext drawing helpers.
    /// </remarks>
    public class DrawToolTipEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawToolTipEventArgs"/> class.
        /// </summary>
        /// <param name="canvas">The Skia canvas used to draw the tooltip.</param>
        /// <param name="associatedWindow">The ModernFormsNext window that owns the popup.</param>
        /// <param name="associatedControl">The control associated with the tooltip.</param>
        /// <param name="bounds">The drawing bounds, in device pixels.</param>
        /// <param name="toolTipText">The text displayed by the tooltip.</param>
        /// <param name="backColor">The background color configured on the tooltip.</param>
        /// <param name="foreColor">The foreground color configured on the tooltip.</param>
        /// <param name="font">The typeface used by default tooltip text rendering.</param>
        /// <param name="fontSize">The font size, in device pixels.</param>
        public DrawToolTipEventArgs(
            SKCanvas canvas,
            WindowBase? associatedWindow,
            Control? associatedControl,
            Rectangle bounds,
            string? toolTipText,
            SKColor backColor,
            SKColor foreColor,
            SKTypeface font,
            int fontSize)
        {
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            AssociatedWindow = associatedWindow;
            AssociatedControl = associatedControl;
            Bounds = bounds;
            ToolTipText = toolTipText;
            BackColor = backColor;
            ForeColor = foreColor;
            Font = font ?? throw new ArgumentNullException(nameof(font));
            FontSize = fontSize;
        }

        /// <summary>
        /// Gets the Skia canvas used to draw the tooltip.
        /// </summary>
        public SKCanvas Canvas { get; }

        /// <summary>
        /// Gets the ModernFormsNext window that owns the popup.
        /// </summary>
        public WindowBase? AssociatedWindow { get; }

        /// <summary>
        /// Gets the control associated with the tooltip.
        /// </summary>
        public Control? AssociatedControl { get; }

        /// <summary>
        /// Gets the drawing bounds, in device pixels.
        /// </summary>
        public Rectangle Bounds { get; }

        /// <summary>
        /// Gets the text displayed by the tooltip.
        /// </summary>
        public string? ToolTipText { get; }

        /// <summary>
        /// Gets the background color configured on the tooltip.
        /// </summary>
        public SKColor BackColor { get; }

        /// <summary>
        /// Gets the foreground color configured on the tooltip.
        /// </summary>
        public SKColor ForeColor { get; }

        /// <summary>
        /// Gets the typeface used by default tooltip text rendering.
        /// </summary>
        public SKTypeface Font { get; }

        /// <summary>
        /// Gets the font size, in device pixels.
        /// </summary>
        public int FontSize { get; }

        /// <summary>
        /// Draws the tooltip background using <see cref="BackColor"/>.
        /// </summary>
        public void DrawBackground()
        {
            using var paint = new SKPaint { Color = BackColor, IsAntialias = true };
            Canvas.DrawRect(Bounds.ToSKRect(), paint);
        }

        /// <summary>
        /// Draws the tooltip text using <see cref="ForeColor"/>.
        /// </summary>
        public void DrawText()
        {
            if (!string.IsNullOrWhiteSpace(ToolTipText))
                Canvas.DrawText(ToolTipText, Font, FontSize, Bounds, ForeColor, ContentAlignment.MiddleLeft);
        }

        /// <summary>
        /// Draws the standard tooltip border.
        /// </summary>
        public void DrawBorder()
        {
            Canvas.DrawRectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, Theme.BorderHighColor);
        }
    }
}
