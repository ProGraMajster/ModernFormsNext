using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a vertical slider used to select a hue value in the HSV color space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hue value ranges from 0° to 360°, where:
    /// <list type="bullet">
    /// <item><description>0° = Red</description></item>
    /// <item><description>120° = Green</description></item>
    /// <item><description>240° = Blue</description></item>
    /// <item><description>360° = Red (wrap-around)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The control is typically used together with a <see cref="ColorBox"/> to build
    /// a full HSV color picker.
    /// </para>
    /// <para>
    /// The top of the control corresponds to 0°, while the bottom approaches 360°.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var hueSlider = new HueSlider();
    /// hueSlider.HueChanged += (s, e) =>
    /// {
    ///     Console.WriteLine($"Hue: {hueSlider.Hue}");
    /// };
    /// </code>
    /// </example>
    public class HueSlider : Control
    {
        private bool isDragging;
        private float hue;


        /// <summary>
        /// Initializes a new instance of the <see cref="HueSlider"/> class.
        /// </summary>
        /// <remarks>
        /// The control is configured as non-selectable and hoverable, and uses a hand cursor
        /// to indicate interactive behavior.
        /// </remarks>
        public HueSlider ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, false);
            SetControlBehavior (ControlBehaviors.Hoverable);
            Cursor = Cursors.Hand;
        }

        /// <summary>
        /// Gets the default style for the <see cref="HueSlider"/> control.
        /// </summary>
        /// <remarks>
        /// This style defines the base appearance, including border width and background color.
        /// </remarks>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            style => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        /// <summary>
        /// Gets the style applied to this control instance.
        /// </summary>
        /// <remarks>
        /// The style is based on <see cref="DefaultStyle"/> and can be customized further if needed.
        /// </remarks>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets the default size of the control.
        /// </summary>
        /// <value>
        /// A <see cref="Size"/> representing the default width and height of the control.
        /// </value>
        /// <remarks>
        /// The default size is optimized for vertical hue selection.
        /// </remarks>
        protected override Size DefaultSize => new Size (24, 260);

        /// <summary>
        /// Gets or sets the current hue value in degrees.
        /// </summary>
        /// <value>
        /// A value between 0 and 360 (exclusive of 360).
        /// </value>
        /// <remarks>
        /// The value is automatically normalized using <see cref="ColorHelper.NormalizeHue(float)"/>.
        /// Changing this property triggers the <see cref="HueChanged"/> event.
        /// </remarks>
        public float Hue {
            get => hue;
            set {
                float normalized = ColorHelper.NormalizeHue (value);
                if (Math.Abs (hue - normalized) > float.Epsilon) {
                    hue = normalized;
                    HueChanged?.Invoke (this, EventArgs.Empty);
                    Invalidate ();
                }
            }
        }


        /// <summary>
        /// Occurs when the <see cref="Hue"/> value changes.
        /// </summary>
        public event EventHandler? HueChanged;

        /// <summary>
        /// Sets the hue value without raising the <see cref="HueChanged"/> event.
        /// </summary>
        /// <param name="value">The new hue value in degrees.</param>
        /// <remarks>
        /// This method is useful when synchronizing UI components without triggering feedback loops.
        /// </remarks>
        public void SetHueSilently (float value)
        {
            hue = ColorHelper.NormalizeHue (value);
            Invalidate ();
        }

        /// <summary>
        /// Handles mouse movement events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Updates the hue value while dragging.
        /// </remarks>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if ((e.Button & MouseButtons.Left) == 0)
                return;

            isDragging = true;
            UpdateFromPoint (e.Location);
        }

        /// <summary>
        /// Handles mouse movement events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Updates the hue value while dragging.
        /// </remarks>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (!isDragging)
                return;

            UpdateFromPoint (e.Location);
        }

        /// <summary>
        /// Handles mouse button release events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Stops the dragging operation.
        /// </remarks>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);
            isDragging = false;
        }


        /// <summary>
        /// Renders the control using the associated renderer.
        /// </summary>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// Delegates rendering to <see cref="RenderManager"/> and the associated renderer.
        /// </remarks>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Updates the hue value based on the specified mouse position.
        /// </summary>
        /// <param name="location">The mouse position relative to the control.</param>
        /// <remarks>
        /// The vertical position is converted into a percentage and mapped to the 0–360° hue range.
        /// </remarks>
        private void UpdateFromPoint (Point location)
        {
            var renderer = RenderManager.GetRenderer<HueSliderRenderer> ();
            if (renderer is null)
                return;

            var bounds = renderer.GetContentBounds (this, null);
            if (bounds.Height <= 1)
                return;

            float percent = (location.Y - bounds.Top) / (float)Math.Max (1, bounds.Height - 1);
            percent = ColorHelper.Clamp01 (percent);

            // Top = 0°, bottom approaches 360°.
            float newHue = percent * 360f;

            if (newHue >= 360f)
                newHue = 359.999f;

            hue = newHue;
            HueChanged?.Invoke (this, EventArgs.Empty);
            Invalidate ();
        }
    }
}
