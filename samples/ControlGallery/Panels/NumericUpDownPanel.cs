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
        }
    }
}
