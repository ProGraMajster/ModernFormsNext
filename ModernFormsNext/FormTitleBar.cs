using System;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a FormTitleBar control.
    /// </summary>
    public class FormTitleBar : Control
    {
        private readonly TitleBarButton minimize_button;
        private readonly TitleBarButton maximize_button;
        private readonly TitleBarButton close_button;
        private readonly TitleBarImage form_image;

        private bool show_image = true;

        /// <summary>
        /// Initializes a new instance of the FormTitleBar class.
        /// </summary>
        public FormTitleBar()
        {
            Dock = DockStyle.Top;

            SetControlBehavior(ControlBehaviors.InvalidateOnTextChanged);
            SetControlBehavior(ControlBehaviors.Selectable, false);

            minimize_button = Controls.AddImplicitControl(new TitleBarButton(TitleBarButton.TitleBarButtonGlyph.Minimize));
            minimize_button.Click += (o, e) => {
                var form_min = FindForm();

                if (form_min is not null)
                    form_min.WindowState = FormWindowState.Minimized;
            };

            maximize_button = Controls.AddImplicitControl(new TitleBarButton(TitleBarButton.TitleBarButtonGlyph.Maximize));
            maximize_button.Click += (o, e) => {
                var form = FindForm();

                if (form is not null)
                {
                    form.WindowState = form.WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;

                    UpdateMaximizeButtonGlyph();
                }
            };

            close_button = Controls.AddImplicitControl(new TitleBarButton(TitleBarButton.TitleBarButtonGlyph.Close));
            close_button.Click += (o, e) => { FindForm()?.Close(); };

            form_image = Controls.AddImplicitControl(new TitleBarImage
            {
                Width = DefaultSize.Height,
                Dock = DockStyle.Left,
                Visible = false
            });

            // It's ok to hardcode SKColors.Transparent here.
            form_image.Style.BackgroundColor = SKColors.Transparent;
            form_image.SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
        }

        /// <summary>
        /// Gets or sets whenther the Maximize button is shown.
        /// </summary>
        public bool AllowMaximize
        {
            get => maximize_button.Visible;
            set
            {
                maximize_button.Visible = value;
                UpdateMaximizeButtonGlyph();
                Invalidate(); // TODO: Shouldn't be necessary, should automatically be triggered
            }
        }

        /// <summary>
        /// Gets or sets whether the Minimize button is shown.
        /// </summary>
        public bool AllowMinimize
        {
            get => minimize_button.Visible;
            set
            {
                minimize_button.Visible = value;
                Invalidate(); // TODO: Shouldn't be necessary, should automatically be triggered
            }
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size(600, 34);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle(Control.DefaultStyle,
           (style) => style.BackgroundColor = Theme.AccentColor2);

        /// <summary>
        /// Gets or sets the image used as the upper-left icon of the titlebar.
        /// </summary>
        /// <remarks>
        /// Set on the UI thread. The bitmap is borrowed: keep it alive while assigned, and
        /// dispose it after replacement or title-bar disposal. Assignment invalidates painting
        /// and updates the icon slot's visibility/layout. The image fits a centered 16-logical-
        /// pixel square (or the available smaller slot), preserving its aspect ratio. Monitor
        /// DPI is applied once when painting. Supply a sufficiently detailed bitmap for high
        /// DPI; this property receives decoded pixels and cannot select another ICO frame or
        /// recover detail absent from a low-resolution source. This managed title-bar image
        /// is separate from the platform's native window/launcher icon.
        /// </remarks>
        public SKBitmap? Image
        {
            get => form_image.Image;
            set
            {
                if (form_image.Image != value)
                {
                    form_image.Image = value;

                    form_image.Visible = form_image.Image is not null && show_image;
                    Invalidate();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // We won't get a MouseUp from the system for this, so don't capture the mouse
            Capture = false;
            FindForm()?.BeginMoveDrag();
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            RenderManager.Render(this, e);
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            // Keep our form image a square
            form_image.Width = Height;

            UpdateMaximizeButtonGlyph();
        }

        /// <summary>
        /// Specifies whether the Form's Image should be shown in the left corner.
        /// </summary>
        public bool ShowImage
        {
            get => show_image;
            set
            {
                if (show_image != value)
                {
                    show_image = value;
                    form_image.Visible = value && form_image.Image is not null;
                    Invalidate(); // TODO: Shouldn't be required
                }
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        private void UpdateMaximizeButtonGlyph()
        {
            var form = FindForm();

            if (form == null || !maximize_button.Visible)
            {
                maximize_button.Glyph = TitleBarButton.TitleBarButtonGlyph.Maximize;
                return;
            }

            maximize_button.Glyph = form.WindowState == FormWindowState.Maximized
                ? TitleBarButton.TitleBarButtonGlyph.Restore
                : TitleBarButton.TitleBarButtonGlyph.Maximize;
        }

        // Keep icon destination sizing local to managed chrome. Ordinary PictureBox.CenterImage
        // intentionally uses source device pixels, so changing that renderer would affect apps.
        private sealed class TitleBarImage : Control
        {
            private SKBitmap? image;

            internal TitleBarImage()
            {
                SetControlBehavior(ControlBehaviors.Selectable, false);
                // Preserve the former PictureBox's image semantics even though destination
                // sizing is now private to chrome. Visibility still controls the exposed view.
                AccessibleRole = AccessibleRole.Graphic;
                AccessibleControlType = AccessibleControlType.Image;
                AccessibilityView = AccessibilityView.Content;
            }

            internal SKBitmap? Image
            {
                get => image;
                set
                {
                    if (image == value) return;
                    image = value;
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (image is null || image.Width <= 0 || image.Height <= 0) return;

                // Child bounds and this client rectangle are already in device pixels. Scale
                // only the intended 100%-DPI icon edge, not the bitmap's decoded dimensions.
                var client = PaddedClientRectangle;
                int edge = Math.Min(LogicalToDeviceUnits(16), Math.Min(client.Width, client.Height));
                if (edge <= 0) return;
                int width = image.Width >= image.Height ? edge : Math.Max(1, (int)(edge * (double)image.Width / image.Height));
                int height = image.Height >= image.Width ? edge : Math.Max(1, (int)(edge * (double)image.Height / image.Width));
                var destination = new Rectangle(client.Left + (client.Width - width) / 2,
                    client.Top + (client.Height - height) / 2, width, height);
                e.Canvas.DrawBitmap(image, destination, !Enabled);
            }
        }

        internal sealed class TitleBarButton : Button
        {
            private const int BUTTON_PADDING = 10;

            private TitleBarButtonGlyph glyph;

            /// <summary>
            /// Gets or sets the glyph displayed by the button.
            /// </summary>
            public TitleBarButtonGlyph Glyph
            {
                get => glyph;
                set
                {
                    if (glyph != value)
                    {
                        glyph = value;
                        Invalidate();
                    }
                }
            }

            public TitleBarButton(TitleBarButtonGlyph glyph)
            {
                this.glyph = glyph;
                Width = 46;
                Dock = DockStyle.Right;

                Style.BackgroundColor = SKColors.Transparent;
                Style.Border.Width = 0;
                StyleHover.Border.Width = 0;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                if (IsHovering)
                    e.Canvas.Clear(glyph == TitleBarButtonGlyph.Close ? Theme.WarningHighlightColor : Theme.AccentColor);

                var glyph_bounds = glyph == TitleBarButtonGlyph.Minimize ?
                    DrawingExtensions.CenterRectangle(ClientRectangle, e.LogicalToDeviceUnits(new Size(BUTTON_PADDING, 1))) :
                    DrawingExtensions.CenterSquare(ClientRectangle, e.LogicalToDeviceUnits(BUTTON_PADDING));

                switch (glyph)
                {
                    case TitleBarButtonGlyph.Close:
                        ControlPaint.DrawCloseGlyph(e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Minimize:
                        ControlPaint.DrawMinimizeGlyph(e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Maximize:
                        ControlPaint.DrawMaximizeGlyph(e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Restore:
                        ControlPaint.DrawRestoreGlyph(e, glyph_bounds);
                        break;
                }
            }

            /// <summary>
            /// Specifies which glyph is displayed by the title bar button.
            /// </summary>
            public enum TitleBarButtonGlyph
            {
                Close,
                Minimize,
                Maximize,
                Restore
            }
        }
    }
}
