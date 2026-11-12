using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class TabStripPanel : Panel
    {
        public TabStripPanel ()
        {
            var tb1 = Controls.Add (new TabStrip { Left = 10, Top = 10, Width = 600 });

            tb1.Tabs.Add (new TabStripItem { Text = "File" });
            tb1.Tabs.Add (new TabStripItem { Text = "Share" });
            tb1.Tabs.Add ("View");
            tb1.Tabs.Add ("Disabled").Enabled = false;

            tb1.SelectedIndex = 1;

            Controls.Add (new Label { Text = "Disabled", Left = 10, Top = 50, Width = 200 });
            var tb2 = Controls.Add (new TabStrip { Left = 10, Top = 75, Width = 600, Enabled = false });

            tb2.Tabs.Add (new TabStripItem { Text = "File" });
            tb2.Tabs.Add (new TabStripItem { Text = "Share" });
            tb2.Tabs.Add ("View");

            Controls.Add(new Label { Left = 10, Top = 125, Width = 600, Text = "Standalone strip with its own 200% italic font" });
            var large = Controls.Add(new TabStrip {
                Left = 10, Top = 155, Width = 700,
                Font = new Font(Theme.UIFont.FamilyName, Theme.FontSize * 2, FontStyle.Italic)
            });
            large.Tabs.Add("General");
            large.Tabs.Add("Advanced");
        }
    }
}
