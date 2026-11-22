using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Displays a short status message at the bottom of its parent by default.
    /// </summary>
    /// <remarks>
    /// Assign <see cref="Control.Text"/> on the UI thread to update the message and request repaint.
    /// </remarks>
    public class StatusBar : Control
    {
        /// <summary>
        /// Initializes a new instance of the StatusBar class.
        /// </summary>
        public StatusBar ()
        {
            Dock = DockStyle.Bottom;
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 25);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Top.Width = 1;
                style.FontSize = 13;
            });

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
