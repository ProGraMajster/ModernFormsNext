using System;
using ModernFormsNext;

namespace ControlGallery.Panels
{
    public class TitleBarPanel : Panel
    {
        public TitleBarPanel ()
        {
            var titlebar = Controls.Add (new FormTitleBar {
                Text = "Test Text!",
                // This cached 32px bitmap has enough source detail for the 16-logical-pixel
                // title icon through 200% DPI. The titlebar borrows it from ImageLoader.
                Image = ImageLoader.Get ("swatches.png")
            });

            titlebar.Controls.Add (
                new TextBox {
                    Placeholder = "Search",
                    Dock = DockStyle.Left
                }
            );

            var toggle = Controls.Add(new Button { Text = "Hide title-bar image", Left = 20, Top = 80, Width = 220 });
            toggle.Click += (_, _) => {
                titlebar.ShowImage = !titlebar.ShowImage;
                toggle.Text = titlebar.ShowImage ? "Hide title-bar image" : "Show title-bar image";
            };
            Controls.Add(new Label {
                Left = 20, Top = 125, Width = 600, Height = 72, Multiline = true,
                Text = "32 px source, centered in a 16 logical px target. Move this window between monitors to check DPI sizing. " +
                    "Small source bitmaps cannot regain missing detail when enlarged."
            });
        }
    }
}
