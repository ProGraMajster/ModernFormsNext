using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using SkiaSharp;

namespace ModernFormsNext
{
    internal class ControlAdapter : ScrollableControl, IInputRoot, IPlatformAccessibilityHost
    {
        private Control? selected_control;

        public ControlAdapter (WindowBase parent)
        {
            ParentForm = parent;
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        // We need to override this because the ControlAdapter doesn't need to be scaled
        public override Rectangle ClientRectangle {
            get {
                var x = CurrentStyle.Border.Left.GetWidth ();
                var y = CurrentStyle.Border.Top.GetWidth ();
                var w = Width - CurrentStyle.Border.Right.GetWidth () - x;
                var h = Height - CurrentStyle.Border.Bottom.GetWidth () - y;
                return new Rectangle (x, y, w, h);
            }
        }

        public WindowBase ParentForm { get; }

        /// <inheritdoc/>
        public IPlatformAccessibleObject? AccessibilityRoot => PlatformAccessibleObjectAdapter.From(AccessibilityObject);

        protected override void OnPaint (PaintEventArgs e)
        {
            // We have this special version for the Adapter because it is
            // given the Form's native surface including any managed Form
            // borders, and it needs to not draw on top of those borders.
            // That is, this often needs to start drawing at (1, 1) instead of (0, 0)
            // This could probably eliminated in the future with Canvas.Translate.
            var form_border = ParentForm.CurrentStyle.Border;

            var form_x = form_border.Left.GetWidth ();
            var form_y = form_border.Top.GetWidth ();

            // ControlCollection enumerates from back to front, matching Control.OnPaint.
            foreach (var control in Controls.GetAllControls ().Where (c => c.Visible).ToArray ()) {
                if (control.Width <= 0 || control.Height <= 0)
                    continue;

                //var info = new SKImageInfo (control.Width, control.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                var info = new SKImageInfo (control.ScaledSize.Width, control.ScaledSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                var buffer = control.GetBackBuffer ();

                if (control.NeedsPaint) {
                    using (var canvas = new SKCanvas (buffer)) {
                        // start drawing
                        var args = new PaintEventArgs(info, canvas, Scaling);

                        control.RaisePaintBackground (args);
                        control.RaisePaint (args);

                        canvas.Flush ();
                    }
                }

                control.DrawBackBuffer (e.Canvas, buffer, form_x, form_y);
            }
        }

        public override bool Visible {
            get => ParentForm != null;
            set { }
        }

        internal Control? SelectedControl {
            get => selected_control;
            set {
                if (selected_control == value)
                    return;

                selected_control?.Deselect ();

                if (value is ControlAdapter)
                    return;

                // Note they could be setting this to null
                selected_control = value;
                selected_control?.Select ();
            }
        }

        internal void RaiseParentVisibleChanged (EventArgs e)
        {
            OnParentVisibleChanged (e);
        }
    }
}
