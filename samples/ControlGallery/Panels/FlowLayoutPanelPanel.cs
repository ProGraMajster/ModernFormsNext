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

        var ltr = container.Panel1.Controls.Add (new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(7, 11, 13, 17) });
        ltr.Controls.Add(new Button { Size = new System.Drawing.Size(100, 40), Text = "مرحبا", TabIndex = 0 });
        ltr.Controls.Add(new Button { Size = new System.Drawing.Size(100, 40), Text = "שלום", TabIndex = 1 });
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
        var direction = toolbar.Controls.Add(new Button { Left = 232, Top = 6, Width = 150, Height = 36,
            Text = "RTL / LTR", Name = "ToggleFlowRtl" });
        direction.Click += (_, _) => container.RightToLeft = container.RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;

        foreach (var color in colors)
            ltr.Controls.Add (CreatePanel (color));

        var mixed = ltr.Controls.Add(new FlowLayoutPanel { Width = 160, Height = 100, RightToLeft = RightToLeft.No });
        mixed.Controls.Add(new Button { Width = 70, Height = 40, Text = "LTR 1" });
        mixed.Controls.Add(new Button { Width = 70, Height = 40, Text = "LTR 2" });

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
