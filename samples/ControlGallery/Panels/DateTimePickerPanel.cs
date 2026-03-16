using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class DateTimePickerPanel : Panel
    {
        public DateTimePickerPanel ()
        {
            Controls.Add (new Label { Text = "DateTimePicker", Left = 10, Top = 10, Width = 200 });
            var dtp1 = Controls.Add (new DateTimePicker { Left = 10, Top = 35 , AutoSize = true});
            dtp1.ValueChanged += (o, e) => Console.WriteLine ($"Value changed: {dtp1.Value}");
            dtp1.Format = DateTimePickerFormat.Long;
            Controls.Add (new Label { Text = "Disabled", Left = 10, Top = 70, Width = 200 });
            var disabled = Controls.Add (new DateTimePicker { Left = 10, Top = 95, Enabled = false });
            disabled.Value = new DateTime (2024, 6, 15);
        }
    }
}
