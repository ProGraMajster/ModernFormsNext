using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;

namespace ControlGallery.Panels;

/// <summary>Exercises logical layout, text, scrolling and local paint while moving the gallery between monitors.</summary>
public sealed class HighDpiPanel : BasePanel
{
    private readonly Panel content;
    private readonly Panel card;
    private readonly Label status;
    private bool alternate;

    /// <summary>Creates the interactive DPI scenario without changing any display settings.</summary>
    public HighDpiPanel()
    {
        content = Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) });
        var sidebar = Controls.Add(new Panel { Dock = DockStyle.Left, Width = 220 });
        var action = sidebar.Controls.Add(new Button { Bounds = new Rectangle(12, 20, 196, 44), Text = "Icon and text",
            Padding = new Padding(48, 0, 10, 0), TextAlign = ModernFormsNext.ContentAlignment.MiddleLeft });
        action.Controls.Add(new Panel { Bounds = new Rectangle(15, 10, 24, 24) });
        status = sidebar.Controls.Add(new Label { Bounds = new Rectangle(12, 80, 196, 180), Multiline = true,
            Text = "Move the window between monitors. Resize, maximize, hover and press the button. Scroll the list; open the popup." });
        card = content.Controls.Add(new Panel { Size = new Size(460, 290), Padding = new Padding(16) });
        card.Style.Border.Width = 1;
        card.Controls.Add(new Label { Text = "Centered setup overlay", AutoSize = true, Location = new Point(16, 12) });
        var combo = card.Controls.Add(new ComboBox { Bounds = new Rectangle(16, 50, 260, 34) });
        combo.Items.Add("Popup placement"); combo.Items.Add("Second choice"); combo.SelectedIndex = 0;
        var scroll = card.Controls.Add(new FlowLayoutPanel { Bounds = new Rectangle(16, 94, 280, 172), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left });
        for (int index = 0; index < 12; index++) scroll.Controls.Add(new Button { Size = new Size(240, 34), Text = $"Scrollable item {index + 1}" });
        var anchor = card.Controls.Add(new Button { Bounds = new Rectangle(310, 230, 134, 42), Text = "Right / bottom",
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom });
        anchor.LayoutTransition = new LayoutTransition { Duration = TimeSpan.FromMilliseconds(250), Easing = Easings.EaseOut };
        action.Click += (_, _) => { alternate = !alternate; card.Size = alternate ? new Size(500, 330) : new Size(460, 290); ArrangeCard(); };
        content.Layout += (_, _) => ArrangeCard();
        ArrangeCard();
    }

    private void ArrangeCard()
    {
        var area = content.LogicalPaddedClientRectangle;
        card.Location = new Point(Math.Max(area.Left, area.Left + (area.Width - card.Width) / 2),
            Math.Max(area.Top, area.Top + (area.Height - card.Height) / 2));
    }

    /// <inheritdoc/>
    protected override void OnDpiChanged(EventArgs e)
    {
        base.OnDpiChanged(e);
        status.Text = $"Scale: {Scaling:P0}. Layout remains logical.\nMove, resize, maximize; hover, press, scroll and open the popup.";
        ArrangeCard();
    }
}
