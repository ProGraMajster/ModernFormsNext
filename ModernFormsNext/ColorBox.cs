using System;
using System.Drawing;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// HSV Saturation/Value selection box.
    /// </summary>
    public class ColorBox : Control
    {
        private bool isDragging;
        private float hue;
        private float saturation = 1f;
        private float value = 1f;

        public ColorBox ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, false);
            SetControlBehavior (ControlBehaviors.Hoverable);
            Cursor = Cursors.Cross;
        }

        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            style => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        protected override Size DefaultSize => new Size (260, 260);

        public float Hue {
            get => hue;
            set {
                float normalized = ColorHelper.NormalizeHue (value);
                if (Math.Abs (hue - normalized) > float.Epsilon) {
                    hue = normalized;
                    Invalidate ();
                }
            }
        }

        public float Saturation {
            get => saturation;
            set {
                float clamped = ColorHelper.Clamp01 (value);
                if (Math.Abs (saturation - clamped) > float.Epsilon) {
                    saturation = clamped;
                    OnColorChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        public float Value {
            get => this.value;
            set {
                float clamped = ColorHelper.Clamp01 (value);
                if (Math.Abs (this.value - clamped) > float.Epsilon) {
                    this.value = clamped;
                    OnColorChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        public event EventHandler? ColorChanged;

        public void SetColorComponents (float hue, float saturation, float value)
        {
            this.hue = ColorHelper.NormalizeHue (hue);
            this.saturation = ColorHelper.Clamp01 (saturation);
            this.value = ColorHelper.Clamp01 (value);

            Invalidate ();
        }

        protected virtual void OnColorChanged (EventArgs e)
            => ColorChanged?.Invoke (this, e);

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
            var renderer = RenderManager.GetRenderer<ColorBoxRenderer> ();
            if (renderer is null)
                return;

            var content = renderer.GetContentBounds (this, null);

            if (content.Width <= 1 || content.Height <= 1)
                return;

            float s = (location.X - content.Left) / (float)Math.Max (1, content.Width - 1);
            float v = 1f - ((location.Y - content.Top) / (float)Math.Max (1, content.Height - 1));

            s = ColorHelper.Clamp01 (s);
            v = ColorHelper.Clamp01 (v);

            bool changed = Math.Abs (saturation - s) > float.Epsilon || Math.Abs (value - v) > float.Epsilon;

            saturation = s;
            value = v;

            if (changed)
                OnColorChanged (EventArgs.Empty);

            Invalidate ();
        }
    }
}
