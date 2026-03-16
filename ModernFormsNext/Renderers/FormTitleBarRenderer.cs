using System;
using System.Drawing;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Represents a class that can render a FormTitleBar.
    /// </summary>
    public class FormTitleBarRenderer : Renderer<FormTitleBar>
    {
        /// <inheritdoc/>
        protected override void Render (FormTitleBar control, PaintEventArgs e)
        {
            // Form text
            e.Canvas.DrawText (control.Text.Trim (), Theme.UIFont, e.LogicalToDeviceUnits (Theme.FontSize), control.ScaledBounds, Theme.ForegroundColorOnAccent, ContentAlignment.MiddleCenter);
        }
    }
}
