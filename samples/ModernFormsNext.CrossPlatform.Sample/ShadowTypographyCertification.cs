using System;
using System.Drawing;
using ModernFormsNext.Drawing;

namespace ModernFormsNext.Validation;

/// <summary>A finite shared appearance example and assertion fixture for native platform runners.</summary>
/// <remarks>Uses only production controls, shaping, resources and the compositor. UI thread only.</remarks>
internal sealed class ShadowTypographyCertification : IDisposable
{
    private readonly Action<string> output;
    private readonly Control.ControlCollection owner;
    private readonly Shadow low = new(Color.FromArgb(80, Color.Black), 3, offsetY: 2);
    private readonly Shadow high = new(Color.FromArgb(112, Color.Black), 8, offsetX: 3, offsetY: 6, spread: 1);
    private readonly Panel card;
    private readonly Label caption;
    private readonly TextBox editor;
    private readonly Button action;
    private readonly Label status;
    private int assertions;

    internal Panel Root { get; }
    internal int PaintCount => ((AppearanceRootPanel)Root).PaintCount;

    internal ShadowTypographyCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new AppearanceRootPanel { Dock = DockStyle.Fill, Name = "ShadowTypography" });
        Root.Resources["CardElevation"] = low;
        card = Root.Controls.Add(new Panel { Bounds = new(24, 24, 280, 176) });
        card.Style.Border.Radius = 12;
        card.SetResourceReference(nameof(Control.Shadow), "CardElevation");
        caption = card.Controls.Add(new Label
        {
            Bounds = new(16, 12, 248, 84), Text = "Shared typography\nZażółć · مرحبا · שלום", Multiline = true,
            LineHeight = 1.5, LetterSpacing = 0.75, TextAlign = ContentAlignment.TopLeft
        });
        action = card.Controls.Add(new Button { Bounds = new(16, 112, 232, 40), Text = "Toggle appearance" });
        editor = Root.Controls.Add(new TextBox
        {
            Bounds = new(24, 224, 280, 44), Text = "Selectable spaced text", LetterSpacing = 0.75,
            AccessibleName = "Typography editor"
        });
        status = Root.Controls.Add(new Label { Bounds = new(24, 288, 280, 80), Multiline = true,
            Text = "Logical-pixel shadow and spacing\nLine height is a multiplier." });
        action.Click += (_, _) => PrepareAlternative(!ReferenceEquals(card.Shadow, high));
        Root.BringToFront();
    }

    internal void Exercise()
    {
        Check(Root.FindForm() is not null && Root.Scaling > 0, "attached-production-window");
        Check(Root.Created && card.Created && caption.Created && action.Created && editor.Created,
            "created-production-controls");
        Rectangle bounds = card.Bounds;
        caption.LineHeight = 1;
        caption.LetterSpacing = 0;
        // Measure a short unwrapped probe so font fallback or wrapping of the visible multilingual
        // caption cannot turn a spacing increase into a smaller measured longest wrapped line.
        const string probe = "First line\nSecond line";
        var unconstrained = new Size(int.MaxValue, int.MaxValue);
        var natural = TextMeasurer.MeasureText(probe, caption, unconstrained);
        caption.LineHeight = 1.5;
        caption.LetterSpacing = 0.75;
        var spaced = TextMeasurer.MeasureText(probe, caption, unconstrained);
        Check(spaced.Width > natural.Width, "tracking-affects-shaped-measurement");
        Check(spaced.Height > natural.Height, "line-height-affects-shaped-measurement");
        for (int i = 0; i < 100; i++)
        {
            Root.Resources["CardElevation"] = i % 2 == 0 ? high : low;
            Check(ReferenceEquals(card.Shadow, Root.Resources["CardElevation"]), "resource-replacement-" + i);
        }
        Check(card.Bounds == bounds && Root.Controls.Count == 3 && card.Controls.Count == 2,
            "appearance-preserves-layout-and-owned-tree");
        Check(!card.Bounds.Contains(card.Right + 3, card.Top + 10), "outer-shadow-does-not-expand-input-bounds");
        PrepareAlternative(false);
        output($"SHADOW_TYPOGRAPHY assertions={assertions} scale={Root.Scaling} natural={natural} spaced={spaced}");
        status.Text = $"Shared checks: {assertions}\nDPI scale: {Root.Scaling:0.##}";
    }

    internal void PrepareAlternative(bool alternative)
    {
        Root.Resources["CardElevation"] = alternative ? high : low;
        caption.LineHeight = alternative ? 1.8 : 1.5;
        caption.LetterSpacing = alternative ? 1.25 : 0.75;
        editor.LetterSpacing = alternative ? 1.25 : 0.75;
        card.Opacity = alternative ? 0.8f : 1;
        caption.Enabled = !alternative;
        action.Text = alternative ? "Restore appearance" : "Toggle appearance";
        Check(ReferenceEquals(card.Shadow, alternative ? high : low) && caption.Enabled == !alternative &&
            action.Text == (alternative ? "Restore appearance" : "Toggle appearance") &&
            caption.LineHeight == (alternative ? 1.8 : 1.5) && caption.LetterSpacing == (alternative ? 1.25 : 0.75),
            "appearance-model-state-" + alternative);
        output($"APPEARANCE_STATE alternative={alternative} text={action.Text} enabled={caption.Enabled} opacity={card.Opacity} " +
            $"rootCreated={Root.Created} cardCreated={card.Created} actionCreated={action.Created} paints={PaintCount}");
    }

    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException(category);
        assertions++;
        output("PASS " + category);
    }

    public void Dispose()
    {
        owner.Remove(Root);
        Root.Dispose();
    }

    private sealed class AppearanceRootPanel : Panel
    {
        internal int PaintCount { get; private set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintCount++;
        }
    }
}
