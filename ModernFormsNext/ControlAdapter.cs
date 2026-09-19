using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Diagnostics;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using SkiaSharp;

namespace ModernFormsNext
{
    internal class ControlAdapter : ScrollableControl, IInputRoot, IPlatformAccessibilityHost, IControlTextInputRoot
    {
        private Control? selected_control;

        public ControlAdapter (WindowBase parent)
        {
            ParentForm = parent;
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        public WindowBase ParentForm { get; }

        ControlTextInputHost? IControlTextInputRoot.TextInputHost => ParentForm.TextInputHost;

        internal override bool IsCommandRoutingRoot => true;

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

            PaintChildren (e, LogicalToDeviceUnits (form_x), LogicalToDeviceUnits (form_y));
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
