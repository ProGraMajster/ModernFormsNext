using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels
{
    public class ColorDialogPanel : Panel
    {
        public ColorDialogPanel ()
        {
            var button = Controls.Add (new Button {
                Text = "Show Color Dialog",
                AutoSize = true,
            });

            var color_panel = Controls.Add (new Panel {
                Width = 100,
                Height = 100
            });
            color_panel.Style.BackgroundColor = SKColors.DarkGray;

            var label = Controls.Add (new Label {
                AutoSize = true,
                Width = 200,
                Height = 100
            });

            button.Click += async (s, e) => {
                var owner = FindForm ();
                if (owner is null)
                    return;

                var dlg = new ColorDialog ();
                var result = await dlg.ShowDialog (owner);

                if (result == DialogResult.OK) {
                    color_panel.Style.BackgroundColor = dlg.Color;
                    label.Text = $"Selected Color: R={dlg.Color.Red}, G={dlg.Color.Green}, B={dlg.Color.Blue}";
                }
            };

            button.Dock = DockStyle.Top;
            color_panel.Dock = DockStyle.Top;
            label.Dock = DockStyle.Top;

            Controls.Add (button);
            Controls.Add (color_panel);
            Controls.Add (label);
        }
    }
}
