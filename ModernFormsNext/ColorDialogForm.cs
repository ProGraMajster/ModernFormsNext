using System;
using System.Drawing;
using SkiaSharp;

namespace ModernFormsNext
{
    internal class ColorDialogForm : Form
    {
        public SKColor SelectedColor { get; private set; }

        private readonly SKColor originalColor;

        private readonly ColorBox colorBox;
        private readonly HueSlider hueSlider;

        private readonly Panel oldPreview;
        private readonly Panel newPreview;

        private readonly Label argbValueLabel;
        private readonly Label hexValueLabel;
        private readonly Label hsvValueLabel;
        private readonly Label hslValueLabel;

        private readonly TrackBar aTrackBar;
        private readonly TrackBar rTrackBar;
        private readonly TrackBar gTrackBar;
        private readonly TrackBar bTrackBar;

        private readonly Button okButton;
        private readonly Button cancelButton;

        private bool isUpdating;
        private float hue;
        private float saturation;
        private float value;

        public string TextForm { get; set; } = "Select Color";
        public string TextCurrent { get; set; } = "Current";
        public string TextNew { get; set; } = "New";
        public string TextValues { get; set; } = "Values";
        public string TextButtonOk { get; set; } = "OK";
        public string TextButtonCancel { get; set; } = "Cancel";

        public ColorDialogForm (SKColor initialColor)
        {
            originalColor = initialColor;
            SelectedColor = initialColor;
            Text = TextForm;
            Size = new Size (760, 520);
            StartPosition = FormStartPosition.CenterParent;
            Resizeable = false;
            AllowMaximize = false;
            AllowMinimize = false;

            SelectedColor = initialColor;
            ColorHelper.ToHsv (initialColor, out hue, out saturation, out value);

            colorBox = new ColorBox {
                Location = new Point (20, 40),
                Size = new Size (320, 320)
            };
            colorBox.SetColorComponents (hue, saturation, value);

            hueSlider = new HueSlider {
                Location = new Point (350, 40),
                Size = new Size (28, 320)
            };
            hueSlider.SetHueSilently (hue);

            var rightColumnX = 400;

            var oldLabel = CreateCaptionLabel (TextCurrent, rightColumnX, 40);
            oldPreview = CreatePreviewPanel (rightColumnX, 62, initialColor);

            var newLabel = CreateCaptionLabel (TextNew, rightColumnX + 90, 40);
            newPreview = CreatePreviewPanel (rightColumnX + 90, 62, initialColor);

            var valuesTitle = CreateCaptionLabel (TextValues, rightColumnX, 130);
            valuesTitle.Width = 220;

            var argbLabel = CreateCaptionLabel ("ARGB:", rightColumnX, 160);
            argbValueLabel = CreateValueLabel (rightColumnX + 55, 160, 260);

            var hexLabel = CreateCaptionLabel ("Hex:", rightColumnX, 188);
            hexValueLabel = CreateValueLabel (rightColumnX + 55, 188, 260);

            var hsvLabel = CreateCaptionLabel ("HSV:", rightColumnX, 216);
            hsvValueLabel = CreateValueLabel (rightColumnX + 55, 216, 260);

            var hslLabel = CreateCaptionLabel ("HSL:", rightColumnX, 244);
            hslValueLabel = CreateValueLabel (rightColumnX + 55, 244, 260);

            var slidersTop = 360;

            aTrackBar = CreateChannelTrackBar (50, slidersTop);
            rTrackBar = CreateChannelTrackBar (50, slidersTop + 52);
            gTrackBar = CreateChannelTrackBar (50, slidersTop + 84);
            bTrackBar = CreateChannelTrackBar (50, slidersTop + 116);

            Controls.Add (CreateCaptionLabel ("A", 20, slidersTop + 26));
            Controls.Add (CreateCaptionLabel ("R", 20, slidersTop + 58));
            Controls.Add (CreateCaptionLabel ("G", 20, slidersTop + 90));
            Controls.Add (CreateCaptionLabel ("B", 20, slidersTop + 122));

            okButton = new Button {
                Text = TextButtonOk,
                Location = new Point (560, 460),
                Size = new Size (80, 30)
            };

            cancelButton = new Button {
                Text = TextButtonCancel,
                Location = new Point (650, 460),
                Size = new Size (80, 30)
            };

            okButton.Click += (s, e) => {
                DialogResult = DialogResult.OK;
                Close ();
            };

            cancelButton.Click += (s, e) => {
                DialogResult = DialogResult.Cancel;
                Close ();
            };

            colorBox.ColorChanged += ColorBox_ColorChanged;
            hueSlider.HueChanged += HueSlider_HueChanged;

            aTrackBar.ValueChanged += ArgbTrackBar_ValueChanged;
            rTrackBar.ValueChanged += ArgbTrackBar_ValueChanged;
            gTrackBar.ValueChanged += ArgbTrackBar_ValueChanged;
            bTrackBar.ValueChanged += ArgbTrackBar_ValueChanged;

            Controls.Add (colorBox);
            Controls.Add (hueSlider);

            Controls.Add (oldLabel);
            Controls.Add (oldPreview);
            Controls.Add (newLabel);
            Controls.Add (newPreview);

            Controls.Add (valuesTitle);
            Controls.Add (argbLabel);
            Controls.Add (argbValueLabel);
            Controls.Add (hexLabel);
            Controls.Add (hexValueLabel);
            Controls.Add (hsvLabel);
            Controls.Add (hsvValueLabel);
            Controls.Add (hslLabel);
            Controls.Add (hslValueLabel);

            Controls.Add (aTrackBar);
            Controls.Add (rTrackBar);
            Controls.Add (gTrackBar);
            Controls.Add (bTrackBar);

            Controls.Add (okButton);
            Controls.Add (cancelButton);

            ApplyColorToUi (initialColor, updateOriginalPreview: true);
        }

        private void HueSlider_HueChanged (object? sender, EventArgs e)
        {
            if (isUpdating)
                return;

            hue = hueSlider.Hue;
            var color = ColorHelper.FromHsv (hue, colorBox.Saturation, colorBox.Value, (byte)aTrackBar.Value);
            ApplyColorToUi (color, updateOriginalPreview: false);
        }

        private void ColorBox_ColorChanged (object? sender, EventArgs e)
        {
            if (isUpdating)
                return;

            saturation = colorBox.Saturation;
            value = colorBox.Value;

            var color = ColorHelper.FromHsv (hueSlider.Hue, saturation, value, (byte)aTrackBar.Value);
            ApplyColorToUi (color, updateOriginalPreview: false);
        }

        private void ArgbTrackBar_ValueChanged (object? sender, EventArgs e)
        {
            if (isUpdating)
                return;

            var color = new SKColor (
                (byte)rTrackBar.Value,
                (byte)gTrackBar.Value,
                (byte)bTrackBar.Value,
                (byte)aTrackBar.Value);

            ApplyColorToUi (color, updateOriginalPreview: false);
        }

        private void ApplyColorToUi (SKColor color, bool updateOriginalPreview)
        {
            isUpdating = true;

            try {
                SelectedColor = color;

                ColorHelper.ToHsv (color, out float newHue, out float newSaturation, out float newValue);

                
                if (newSaturation > 0f && newValue > 0f)
                    hue = newHue;

                saturation = newSaturation;
                value = newValue;

                colorBox.SetColorComponents (hue, saturation, value);
                hueSlider.SetHueSilently (hue);

                aTrackBar.Value = color.Alpha;
                rTrackBar.Value = color.Red;
                gTrackBar.Value = color.Green;
                bTrackBar.Value = color.Blue;

                if (updateOriginalPreview) {
                    oldPreview.Style.BackgroundColor = originalColor;
                    oldPreview.Invalidate ();
                }

                newPreview.Style.BackgroundColor = color;
                newPreview.Invalidate ();

                UpdateValueLabels (color);
            } finally {
                isUpdating = false;
            }
        }

        private void UpdateValueLabels (SKColor color)
        {
            ColorHelper.ToHsv (color, out float h, out float s, out float v);
            ColorHelper.ToHsl (color, out float h2, out float s2, out float l2);

            argbValueLabel.Text = $"{color.Alpha}, {color.Red}, {color.Green}, {color.Blue}";
            hexValueLabel.Text = ColorHelper.ToHex (color, includeAlpha: true);
            hsvValueLabel.Text = $"{h:0.##}°, {s * 100f:0.#}%, {v * 100f:0.#}%";
            hslValueLabel.Text = $"{h2:0.##}°, {s2 * 100f:0.#}%, {l2 * 100f:0.#}%";
        }

        private static Label CreateCaptionLabel (string text, int x, int y)
        {
            return new Label {
                Text = text,
                Location = new Point (x, y),
                Size = new Size (60, 22)
            };
        }

        private static Label CreateValueLabel (int x, int y, int width)
        {
            return new Label {
                Location = new Point (x, y),
                Size = new Size (width, 22)
            };
        }

        private static Panel CreatePreviewPanel (int x, int y, SKColor color)
        {
            var panel = new Panel {
                Location = new Point (x, y),
                Size = new Size (72, 52)
            };

            panel.Style.Border.Width = 1;
            panel.Style.Border.Color = Theme.BorderLowColor;
            panel.Style.BackgroundColor = color;

            return panel;
        }

        private static TrackBar CreateChannelTrackBar (int x, int y)
        {
            return new TrackBar {
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 16,
                SmallChange = 1,
                LargeChange = 8,
                AutoSize = false,
                Height = 28,
                Width = 360,
                Location = new Point (x, y)
            };
        }
    }
}
