using System.Drawing;

namespace ModernFormsNext.Renderers
{
    /// <summary>
    /// Renders <see cref="NumericUpDown"/> controls.
    /// </summary>
    public sealed class NumericUpDownRenderer : Renderer<NumericUpDown>
    {
        /// <summary>
        /// Renders the specified control.
        /// </summary>
        /// <param name="control">The control to render.</param>
        /// <param name="e">The paint event data.</param>
        protected override void Render (NumericUpDown control, PaintEventArgs e)
        {
            DrawButtons (control, e);
            DrawSeparator (control, e);

            if (control.Focused && control.ShowFocusCues)
                DrawFocus (control, e);
        }

        /// <summary>
        /// Draws the spinner buttons.
        /// </summary>
        /// <param name="control">The control being rendered.</param>
        /// <param name="e">The paint event data.</param>
        private void DrawButtons (NumericUpDown control, PaintEventArgs e)
        {
            var upBounds = control.UpButtonBounds;
            var downBounds = control.DownButtonBounds;

            var buttonColor = Theme.ControlLowColor;
            var hoverColor = Theme.ControlMidColor;
            var pressedColor = Theme.ControlHighColor;

            e.Canvas.FillRectangle (
                upBounds,
                control.UpButtonPressed ? pressedColor : control.UpButtonHovered ? hoverColor : buttonColor);

            e.Canvas.FillRectangle (
                downBounds,
                control.DownButtonPressed ? pressedColor : control.DownButtonHovered ? hoverColor : buttonColor);

            ControlPaint.DrawArrowGlyph (
                e,
                upBounds,
                control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor,
                ArrowDirection.Up);

            ControlPaint.DrawArrowGlyph (
                e,
                downBounds,
                control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor,
                ArrowDirection.Down);
        }

        /// <summary>
        /// Draws the separator between the embedded editor and the spinner buttons.
        /// </summary>
        /// <param name="control">The control being rendered.</param>
        /// <param name="e">The paint event data.</param>
        private void DrawSeparator (NumericUpDown control, PaintEventArgs e)
        {
            var separatorX = control.UpButtonBounds.Left;
            var separatorBounds = new Rectangle (separatorX, 0, 1, control.Height);

            e.Canvas.FillRectangle (separatorBounds, Theme.BorderLowColor);
        }

        /// <summary>
        /// Draws the focus indicator for the spinner area.
        /// </summary>
        /// <param name="control">The control being rendered.</param>
        /// <param name="e">The paint event data.</param>
        private void DrawFocus (NumericUpDown control, PaintEventArgs e)
        {
            var focusBounds = Rectangle.Inflate (control.TextBounds, -1, -1);
            e.Canvas.DrawRectangle (focusBounds, Theme.AccentColor, control.LogicalToDeviceUnits (1));
        }
    }
}
