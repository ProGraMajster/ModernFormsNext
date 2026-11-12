using System;
using System.Collections.Generic;
using System.Text;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class TabControlPanel : Panel
    {
        public TabControlPanel ()
        {
            var tc = new TabControl {
                Dock = DockStyle.Fill
            };

            Controls.Add (tc);

            var typography = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 44 });
            var scaleButton = typography.Controls.Add(new Button { Left = 8, Top = 6, Width = 170, Height = 32, Text = "Text size: 100%" });
            typography.Controls.Add(new Label { Left = 190, Top = 10, Width = 430, Text = "Change caption size, then select or close a tab." });
            float[] textScales = [1f, 1.25f, 1.5f, 1.75f, 2f];
            int scaleIndex = 0;
            Font originalFont = tc.Font;
            scaleButton.Click += (_, _) => {
                scaleIndex = (scaleIndex + 1) % textScales.Length;
                tc.Font = new Font(originalFont.FamilyName, originalFont.SizeInPoints * textScales[scaleIndex]);
                scaleButton.Text = $"Text size: {textScales[scaleIndex] * 100:0}%";
            };
            tc.ShowCloseButtons = true;

            var tp1 = tc.TabPages.Add ("Tab X");
            var tp2 = tc.TabPages.Add ("Tab Y");
            var tp3 = tc.TabPages.Add ("Tab 3");

            tp1.Controls.Add (new Label { Text = "Tab X Contents!", Left = 3, Top = 3 });
            tp2.Controls.Add (new Label { Text = "Tab Y Contents!", Left = 3, Top = 3 });
            tp3.Controls.Add (new Label { Text = "Tab 3 Contents!", Left = 3, Top = 3 });

            var tp4 = new TabPage { Text = "Tab 1" };
            tp4.Controls.Add (new Label { Text = "Tab 1 Contents!", Left = 3, Top = 3 });
            tc.TabPages.Insert (1, tp4);

            var tp5 = new TabPage { Text = "Tab 2" };
            tp5.Controls.Add (new Label { Text = "Tab 2 Contents!", Left = 3, Top = 3 });
            tc.TabPages[2] = tp5;

            tc.TabPages.RemoveAt (0);
            tc.TabPages[0].Pinned = true;
            tc.TabPages[1].Closable = true;
        }
    }
}
