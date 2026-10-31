using ModernFormsNext;
using SkiaSharp;

namespace ControlGallery.Panels;

public class TableLayoutPanelPanel : TableLayoutPanel
{
    private readonly SKColor[] colors = new[] { 
        SKColors.CornflowerBlue, 
        SKColors.LightPink, 
        SKColors.LightSeaGreen, 
        SKColors.LightYellow, 
        SKColors.LightCoral,
        SKColors.LightGray,
        SKColors.LightGreen,
        SKColors.LightGoldenrodYellow,
        SKColors.Violet,
        SKColors.Orange
    };
    
    public TableLayoutPanelPanel ()
    {
        ColumnCount = 2;
        RowCount = 5;

        ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));
        ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));

        RowStyles.Add (new RowStyle (SizeType.Percent, 20));
        RowStyles.Add (new RowStyle (SizeType.Percent, 20));
        RowStyles.Add (new RowStyle (SizeType.Percent, 20));
        RowStyles.Add (new RowStyle (SizeType.Percent, 20));
        RowStyles.Add (new RowStyle (SizeType.Percent, 20));

        for (var row = 0; row < RowCount; row++)
            for (var col = 0; col < ColumnCount; col++) {
                if (row == RowCount - 1 && col == 1) continue;
                var cell = Controls.Add(CreatePanel(colors[(row * ColumnCount) + col], row, col));
                if (row == RowCount - 1) SetColumnSpan(cell, 2);
            }
    }

    private Panel CreatePanel (SKColor color, int row, int col)
    {
        var panel = new Panel { Height = 100, Width = 100, Dock = DockStyle.Fill, Margin = new Padding(3, 5, 9, 7) };
        panel.Style.BackgroundColor = color;
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Multiline = true,
            Text = $"logical ({row}, {col}) · مرحبا · שלום" + (row == RowCount - 1 ? "\nColumnSpan = 2" : ""),
            TextAlign = ContentAlignment.MiddleCenter });
        if (row == 0 && col == 0) {
            var direction = panel.Controls.Add(new Button { Dock = DockStyle.Top, Height = 36,
                Text = "RTL / LTR", Name = "ToggleTableRtl" });
            direction.Click += (_, _) => RightToLeft = RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;
        }

        LayoutSettings.SetColumn (panel, col);
        LayoutSettings.SetRow (panel, row);

        return panel;
    }
}
