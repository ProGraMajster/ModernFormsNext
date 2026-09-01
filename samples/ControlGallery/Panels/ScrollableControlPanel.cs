using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class ScrollableControlPanel : Panel
    {
        public ScrollableControlPanel ()
        {
            var sc = new ScrollableControl {
                Left = 100,
                Top = 100,
                Width = 200,
                Height = 200,
                AutoScroll = true
            };

            sc.Controls.AddRange (
                new Label { Left = 10, Top = 10, Width = 160, Height = 30, Text = "Wheel over label" },
                new TextBox { Left = 10, Top = 55, Width = 160, Height = 36, Text = "Wheel over input" },
                new CheckBox { Left = 10, Top = 110, Width = 160, Height = 36, Text = "Wheel over option" },
                CreateButton ("Wheel over button", 10, 165),
                CreateButton ("Overflow", 10, 300));

            Controls.Add (sc);

            var sc2 = new ScrollableControl {
                Left = 350,
                Top = 100,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            var nested = new ScrollableControl {
                Left = 10,
                Top = 10,
                Width = 185,
                Height = 110,
                AutoScroll = true
            };
            nested.Controls.AddRange (
                CreateButton ("Inner 1", 5, 5),
                CreateButton ("Inner 2", 5, 140),
                CreateButton ("Inner 3", 5, 275));
            sc2.Controls.AddRange (
                nested,
                new Label {
                    Left = 10,
                    Top = 340,
                    Width = 180,
                    Height = 50,
                    Multiline = true,
                    Text = "At the inner limit, keep wheeling to scroll the outer panel."
                });

            Controls.Add (sc2);

            var sc3 = new ScrollableControl {
                Left = 100,
                Top = 350,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            sc3.Controls.AddRange (
                CreateButton ("1", 0, 0),
                CreateButton ("2", 100, 0),
                CreateButton ("3", 200, 0),
                CreateButton ("4", 300, 0));

            Controls.Add (sc3);

            var sc4 = new ScrollableControl {
                Left = 350,
                Top = 350,
                Width = 225,
                Height = 200,
                AutoScroll = true
            };

            sc4.Controls.Add (CreateButton ("1", 0, 0));

            Controls.Add (sc4);
        }

        private Button CreateButton (string text, int x, int y)
        {
            return new Button {
                Left = x,
                Top = y,
                Width = 160,
                Height = 110,
                Text = text
            };
        }
    }
}
