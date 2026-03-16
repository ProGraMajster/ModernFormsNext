using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Vertical hue selection slider.
    /// Top = 0° (red), bottom = 360° (red again).
    /// </summary>
    public class HueSlider : Control
    {
        private bool isDragging;
        private float hue;

        public HueSlider ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, false);
            SetControlBehavior (ControlBehaviors.Hoverable);
            Cursor = Cursors.Hand;
        }

        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            style => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        protected override Size DefaultSize => new Size (24, 260);

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

        public event EventHandler? HueChanged;

        public void SetHueSilently (float value)
        {
            hue = ColorHelper.NormalizeHue (value);
            Invalidate ();
        }

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if ((e.Button & MouseButtons.Left) == 0)
                return;

            isDragging = true;
            UpdateFromPoint (e.Location);
        }

        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (!isDragging)
                return;

            UpdateFromPoint (e.Location);
        }

        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);
            isDragging = false;
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

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
