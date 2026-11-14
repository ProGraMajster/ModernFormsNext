using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class NumericUpDownPanel : Panel
    {
        public NumericUpDownPanel ()
        {
            var numericUpDown = new NumericUpDown {
                Location = new Point (10, 10),
                Minimum = 0,
                Maximum = 100,
                Value = 50
            };
            Controls.Add (numericUpDown);

            var disabledNumericUpDown = new NumericUpDown {
                Location = new Point (10, 50),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Enabled = false
            };

            Controls.Add (disabledNumericUpDown);

            var incrementNumericUpDown = new NumericUpDown {
                Location = new Point (10, 90),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Increment = 2,
                DecimalPlaces = 5,
                AllowDecimalValues = true,
                AutoIncrement = true
            };
            Controls.Add (incrementNumericUpDown);

            var typographyNumber = Controls.Add(new NumericUpDown {
                Location = new Point(10, 150), Width = 300, Maximum = 10000,
                DecimalPlaces = 2, ThousandsSeparator = true, Value = 1234.5m
            });
            var readOnlyNumber = Controls.Add(new NumericUpDown {
                Location = new Point(330, 150), Width = 300, Maximum = 10000,
                DecimalPlaces = 2, Value = 1234.5m, ReadOnly = true
            });
            var originalFont = typographyNumber.Font;
            double[] textScales = [1, 1.25, 1.5, 1.75, 2];
            int scaleIndex = 0;
            var textSize = Controls.Add(new Button {
                Location = new Point(10, 230), Size = new Size(220, 36), Text = "Text size: 100%"
            });
            textSize.Click += (_, _) => {
                scaleIndex = (scaleIndex + 1) % textScales.Length;
                var font = new ModernFormsNext.Font(originalFont.FamilyName, (float)(originalFont.SizeInPoints * textScales[scaleIndex]));
                typographyNumber.Font = readOnlyNumber.Font = font;
                textSize.Text = $"Text size: {textScales[scaleIndex]:P0}";
            };
            Controls.Add(new Label {
                Location = new Point(10, 280), Size = new Size(650, 50),
                Text = "Editable decimal / read-only. Both keep active spinner buttons."
            });
        }
    }
}
