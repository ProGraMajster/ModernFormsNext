using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a 2D color selection surface for saturation and value in the HSV color space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This control allows selecting:
    /// <list type="bullet">
    /// <item><description>Saturation (X axis: left to right)</description></item>
    /// <item><description>Value/Brightness (Y axis: bottom to top)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The hue component is provided externally through the <see cref="Hue"/> property.
    /// </para>
    /// <para>
    /// This control is typically used together with a <see cref="HueSlider"/> to build
    /// a complete HSV color picker.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var colorBox = new ColorBox();
    /// colorBox.ColorChanged += (s, e) =>
    /// {
    ///     Console.WriteLine($"HSV: {colorBox.Hue}, {colorBox.Saturation}, {colorBox.Value}");
    /// };
    /// </code>
    /// </example>
    public class ColorBox : Control
    {
        private bool isDragging;
        private float hue;
        private float saturation = 1f;
        private float value = 1f;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorBox"/> class.
        /// </summary>
        /// <remarks>
        /// The control is configured as non-selectable and hoverable, and uses a cross cursor
        /// to indicate precise color picking behavior.
        /// </remarks>
        public ColorBox()
        {
            SetControlBehavior(ControlBehaviors.Selectable, false);
            SetControlBehavior(ControlBehaviors.Hoverable);
            Cursor = Cursors.Cross;
        }

        /// <summary>
        /// Gets the default style for the <see cref="ColorBox"/> control.
        /// </summary>
        /// <remarks>
        /// This style defines the default border width and background color used by the control.
        /// </remarks>
        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
            style => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        /// <summary>
        /// Gets the style applied to this control instance.
        /// </summary>
        /// <remarks>
        /// The style is initialized from <see cref="DefaultStyle"/> and can be customized per instance.
        /// </remarks>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets the default size of the control.
        /// </summary>
        /// <value>
        /// A <see cref="Size"/> representing the default width and height of the control.
        /// </value>
        /// <remarks>
        /// The default size is intended to provide a comfortable square area for saturation
        /// and value selection.
        /// </remarks>
        protected override Size DefaultSize => new Size(260, 260);

        /// <summary>
        /// Gets or sets the hue component used for rendering the color box.
        /// </summary>
        /// <value>
        /// A hue value in degrees.
        /// </value>
        /// <remarks>
        /// Changing this property updates the visual representation of the control but does not raise
        /// the <see cref="ColorChanged"/> event.
        /// </remarks>
        public float Hue
        {
            get => hue;
            set
            {
                float normalized = ColorHelper.NormalizeHue(value);
                if (Math.Abs(hue - normalized) > float.Epsilon)
                {
                    hue = normalized;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the saturation component of the selected HSV color.
        /// </summary>
        /// <value>
        /// A value between 0 and 1.
        /// </value>
        /// <remarks>
        /// Changing this property raises the <see cref="ColorChanged"/> event.
        /// </remarks>
        public float Saturation
        {
            get => saturation;
            set
            {
                float clamped = ColorHelper.Clamp01(value);
                if (Math.Abs(saturation - clamped) > float.Epsilon)
                {
                    saturation = clamped;
                    OnColorChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value (brightness) component of the selected HSV color.
        /// </summary>
        /// <value>
        /// A value between 0 and 1.
        /// </value>
        /// <remarks>
        /// Changing this property raises the <see cref="ColorChanged"/> event.
        /// </remarks>
        public float Value
        {
            get => this.value;
            set
            {
                float clamped = ColorHelper.Clamp01(value);
                if (Math.Abs(this.value - clamped) > float.Epsilon)
                {
                    this.value = clamped;
                    OnColorChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Occurs when the selected saturation or value changes.
        /// </summary>
        /// <remarks>
        /// This event is raised when the user changes the selected point inside the control
        /// or when the <see cref="Saturation"/> or <see cref="Value"/> properties are updated.
        /// </remarks>
        public event EventHandler? ColorChanged;

        /// <summary>
        /// Sets the HSV components without raising the <see cref="ColorChanged"/> event.
        /// </summary>
        /// <param name="hue">The hue component in degrees.</param>
        /// <param name="saturation">The saturation component in the range from 0 to 1.</param>
        /// <param name="value">The value (brightness) component in the range from 0 to 1.</param>
        /// <remarks>
        /// This method is useful when initializing the control or synchronizing it with another
        /// color source without triggering change notifications.
        /// </remarks>
        public void SetColorComponents(float hue, float saturation, float value)
        {
            this.hue = ColorHelper.NormalizeHue(hue);
            this.saturation = ColorHelper.Clamp01(saturation);
            this.value = ColorHelper.Clamp01(value);

            Invalidate();
        }

        /// <summary>
        /// Raises the <see cref="ColorChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// Derived classes can override this method to customize color change handling.
        /// </remarks>
        protected virtual void OnColorChanged(EventArgs e)
            => ColorChanged?.Invoke(this, e);

        /// <summary>
        /// Handles mouse button press events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Begins drag-based color selection when the left mouse button is pressed.
        /// </remarks>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if ((e.Button & MouseButtons.Left) == 0)
                return;

            isDragging = true;
            UpdateFromPoint(e.Location);
        }

        /// <summary>
        /// Handles mouse movement events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Updates the selected saturation and value while dragging is active.
        /// </remarks>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!isDragging)
                return;

            UpdateFromPoint(e.Location);
        }

        /// <summary>
        /// Handles mouse button release events.
        /// </summary>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>
        /// Ends the current drag operation.
        /// </remarks>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isDragging = false;
        }

        /// <summary>
        /// Renders the control using the associated renderer.
        /// </summary>
        /// <param name="e">The paint event data.</param>
        /// <remarks>
        /// Rendering is delegated to the <see cref="RenderManager"/> and the registered
        /// <c>ColorBoxRenderer</c> implementation.
        /// </remarks>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderManager.Render(this, e);
        }

        /// <summary>
        /// Updates the saturation and value based on the specified mouse position.
        /// </summary>
        /// <param name="location">The mouse position relative to the control.</param>
        /// <remarks>
        /// Horizontal movement changes saturation, while vertical movement changes value.
        /// The top edge represents full brightness and the bottom edge represents zero brightness.
        /// </remarks>
        private void UpdateFromPoint(Point location)
        {
            var renderer = RenderManager.GetRenderer<ColorBoxRenderer>();
            if (renderer is null)
                return;

            var content = renderer.GetContentBounds(this, null);

            if (content.Width <= 1 || content.Height <= 1)
                return;

            float s = (location.X - content.Left) / (float)Math.Max(1, content.Width - 1);
            float v = 1f - ((location.Y - content.Top) / (float)Math.Max(1, content.Height - 1));

            s = ColorHelper.Clamp01(s);
            v = ColorHelper.Clamp01(v);

            bool changed = Math.Abs(saturation - s) > float.Epsilon || Math.Abs(value - v) > float.Epsilon;

            saturation = s;
            value = v;

            if (changed)
                OnColorChanged(EventArgs.Empty);

            Invalidate();
        }
    }
}