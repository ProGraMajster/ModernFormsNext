using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using SkiaSharp;

namespace ControlGallery.Panels;

/// <summary>
/// Provides a manual hover and press check for layout-aware visual-state metrics.
/// </summary>
public sealed class LayoutAwareVisualStatesPanel : BasePanel
{
    /// <summary>
    /// Initializes the layout-aware visual-state demonstration.
    /// </summary>
    public LayoutAwareVisualStatesPanel()
    {
        AutoScroll = true;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "Layout-aware visual states",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 54,
            Width = 760,
            Height = 48,
            Multiline = true,
            Text = "Hover and press the card or caption. Padding, border thickness, corner radius and compatible text metrics share the state transition; the Dock.Fill child follows the content rectangle."
        });

        var stage = Controls.Add(new Panel
        {
            Left = 24,
            Top = 118,
            Width = 720,
            Height = 330,
            BackColor = new SKColor(242, 245, 249)
        });
        stage.Style.Border.Width = 1;
        stage.Style.Border.Color = Theme.BorderMidColor;

        var card = stage.Controls.Add(new HoverPanel
        {
            Bounds = new Rectangle(170, 72, 380, 180),
            BackColor = new SKColor(92, 64, 171)
        });
        card.Style.Padding = new Padding(12);
        card.Style.Border.Width = 2;
        card.Style.Border.Color = new SKColor(55, 35, 120);
        card.Style.Border.Radius = 2;
        card.StyleHover.Padding = new Padding(28);
        card.StyleHover.Border.Width = 6;
        card.StyleHover.Border.Radius = 18;
        card.StylePressed.Padding = new Padding(36);
        card.StylePressed.Border.Width = 8;
        card.StylePressed.Border.Radius = 10;

        AddTransition(card, VisualState.Normal, VisualState.Hover, 260);
        AddTransition(card, VisualState.Hover, VisualState.Normal, 220);
        AddTransition(card, VisualState.Hover, VisualState.Pressed, 120);
        AddTransition(card, VisualState.Pressed, VisualState.Hover, 160);

        card.Controls.Add(new PassiveLabel
        {
            Dock = DockStyle.Fill,
            Text = "Normal 12 / Hover 28 / Pressed 36",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White,
            BackColor = new SKColor(126, 94, 205)
        });

        var caption = stage.Controls.Add(new HoverMetricLabel
        {
            Name = "VisualStateMetricCaption",
            AccessibleName = "Visual state metric caption",
            Bounds = new Rectangle(28, 268, 664, 54),
            Text = "Hover text: size, height, spacing, corners",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White,
            BackColor = new SKColor(41, 98, 160)
        });
        caption.Style.FontSize = 12;
        caption.Style.LineHeight = 1;
        caption.Style.LetterSpacing = 0;
        caption.Style.Border.Radius = 2;
        caption.StyleHover.FontSize = 20;
        caption.StyleHover.LineHeight = 1.5;
        caption.StyleHover.LetterSpacing = 0.75;
        caption.StyleHover.Border.Radius = 18;
        caption.StylePressed.FontSize = 18;
        caption.StylePressed.LineHeight = 1.25;
        caption.StylePressed.LetterSpacing = 0.25;
        caption.StylePressed.Border.Radius = 10;
        AddTransition(caption, VisualState.Normal, VisualState.Hover, 300);
        AddTransition(caption, VisualState.Hover, VisualState.Normal, 220);
        AddTransition(caption, VisualState.Hover, VisualState.Pressed, 120);
        AddTransition(caption, VisualState.Pressed, VisualState.Hover, 180);

        Controls.Add(new Label
        {
            Left = 24,
            Top = 470,
            Width = 720,
            Height = 54,
            Multiline = true,
            Text = "Manual check: quickly move in/out and press repeatedly. Text and corners must settle exactly. Padding must follow the card smoothly, with no delayed child motion after the state settles."
        });
    }

    private static void AddTransition(
        Control control,
        VisualState from,
        VisualState to,
        int durationMilliseconds)
        => control.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                Easing = Easings.CubicOut
            });

    private sealed class HoverPanel : Panel
    {
        public HoverPanel()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }
    }

    private sealed class PassiveLabel : Label
    {
        public PassiveLabel()
        {
            SetControlBehavior(ControlBehaviors.ReceivesMouseEvents, false);
        }
    }

    private sealed class HoverMetricLabel : Label
    {
        public HoverMetricLabel() => SetControlBehavior(ControlBehaviors.Hoverable);
    }
}
