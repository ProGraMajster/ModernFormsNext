using System;
using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class TextBoxPanel : Panel
    {
        public TextBoxPanel ()
        {
            Controls.Add (new TextBox { Text = "Option 1", Left = 10, Top = 10, Width = 150, Placeholder = "Type Here" });
            Controls.Add (new TextBox { Left = 10, Top = 50, Width = 150, Placeholder = "Type Here" });
            Controls.Add (new TextBox { Left = 10, Top = 90, Width = 150, Text = "Read Only", ReadOnly = true });
            Controls.Add (new TextBox { Left = 10, Top = 130, Width = 300, Height = 56, Text = "Not\nMultiline" }).Style.Border.Radius = 3;
            
            var multi = Controls.Add (new TextBox { Left = 10, Top = 200, Width = 300, Height = 100, Text = "The quick brown fox\njumped over the lazy\ndogs.", MultiLine = true });
            multi.Style.FontSize = 16;

            Controls.Add (new TextBox { Left = 10, Top = 315, Width = 150, Text = "Disabled", Enabled = false });
            Controls.Add (new TextBox { Left = 10, Top = 355, Width = 150, Placeholder = "Password", PasswordCharacter = (char)0x25CF });
            
            var padded = Controls.Add (new TextBox { Text = "With Padding", Left = 200, Top = 10, Width = 150, Padding = new Padding (5) });
            padded.Style.ForegroundColor = SKColors.Red;

            Controls.Add (new TextBox { Text = "Left aligned", Left = 380, Top = 10, Width = 160, TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add (new TextBox { Text = "Centered", Left = 380, Top = 50, Width = 160, TextAlign = ContentAlignment.MiddleCenter });
            Controls.Add (new TextBox { Text = "Right aligned", Left = 380, Top = 90, Width = 160, TextAlign = ContentAlignment.MiddleRight });
            Controls.Add (new TextBox { Text = "Top", Left = 560, Top = 10, Width = 150, Height = 56, TextAlign = ContentAlignment.TopLeft });
            Controls.Add (new TextBox { Text = "Middle", Left = 560, Top = 80, Width = 150, Height = 56, TextAlign = ContentAlignment.MiddleLeft });
            Controls.Add (new TextBox { Text = "Bottom", Left = 560, Top = 150, Width = 150, Height = 56, TextAlign = ContentAlignment.BottomLeft });

            Controls.Add (new Adorner { Left = 304, Top = 196, Width = 10, Height = 10 });
        }

        public class Adorner : Control
        {
            public Adorner ()
            {
                SetControlBehavior (ControlBehaviors.Transparent);
                SetControlBehavior (ControlBehaviors.Selectable, false);
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                e.Canvas.FillCircle (Width / 2, Height / 2, Width / 2, SKColors.Red);
            }
        }
    }
}
