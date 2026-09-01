using ModernFormsNext;
using SkiaSharp;
using System.Linq;

namespace ControlGallery.Panels;

public class FlowLayoutPanelPanel : Panel
{
    private readonly SKColor[] colors = new[] { 
        SKColors.CornflowerBlue, 
        SKColors.LightPink, 
        SKColors.LightSeaGreen, 
        SKColors.LightYellow, 
        SKColors.LightCoral,
        SKColors.LightGray,
        SKColors.LightGreen,
        SKColors.LightGoldenrodYellow
    };
    
    public FlowLayoutPanelPanel ()
    {
        var container = Controls.Add (new SplitContainer { Orientation = Orientation.Vertical, SplitterColor = SKColors.DarkGray });

        var ltr = container.Panel1.Controls.Add (new FlowLayoutPanel { Dock = DockStyle.Fill });
        var ttb = container.Panel2.Controls.Add (new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding (8)
        });
        var toolbar = container.Panel2.Controls.Add (new Panel { Dock = DockStyle.Top, Height = 48 });
        var add = toolbar.Controls.Add (new Button { Left = 8, Top = 6, Width = 96, Height = 36, Text = "Add item" });
        var remove = toolbar.Controls.Add (new Button { Left = 112, Top = 6, Width = 112, Height = 36, Text = "Remove item" });

        foreach (var color in colors)
            ltr.Controls.Add (CreatePanel (color));

        // Deliberately overflow the viewport. This is the manual regression for the
        // FlowLayoutPanel content extent and its dynamically managed vertical scrollbar.
        foreach (var color in colors.Concat (colors))
            ttb.Controls.Add (CreatePanel (color));

        var nextColor = 0;
        add.Click += (_, _) => ttb.Controls.Add (CreatePanel (colors[nextColor++ % colors.Length]));
        remove.Click += (_, _) => {
            if (ttb.Controls.Count == 0)
                return;

            var last = ttb.Controls[^1];
            ttb.Controls.Remove (last);
            last.Dispose ();
        };
    }

    private static Panel CreatePanel (SKColor color)
    {
        var panel = new Panel { Height = 100, Width = 100, Margin = new Padding (0, 0, 0, 8) };
        panel.Style.BackgroundColor = color;

        return panel;
    }
}
