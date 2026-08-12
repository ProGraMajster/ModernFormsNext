using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ControlGallery.Panels;

/// <summary>
/// Provides a compact manual check for compatible visual-state brush transitions.
/// </summary>
public sealed class BrushInterpolationPanel : BasePanel
{
    /// <summary>
    /// Initializes the brush-interpolation demonstration.
    /// </summary>
    public BrushInterpolationPanel()
    {
        AutoScroll = true;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "Brush interpolation compatibility",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 54,
            Width = 760,
            Height = 52,
            Multiline = true,
            Text = "Hover and press both cards. The left card stays Solid; the right moves from Solid through three- and two-stop linear gradients. Rapidly retarget the states and watch for jumps."
        });

        var stage = Controls.Add(new Panel
        {
            Left = 24,
            Top = 122,
            Width = 720,
            Height = 320,
            BackColor = new SKColor(241, 244, 249)
        });
        stage.Style.Border.Width = 1;
        stage.Style.Border.Color = Theme.BorderMidColor;

        var solidCard = stage.Controls.Add(new HoverPanel
        {
            Bounds = new Rectangle(20, 68, 330, 184)
        });
        solidCard.Style.BackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 78, 70, 180));
        solidCard.Style.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 45, 38, 120));
        solidCard.Style.Border.Width = 4;
        solidCard.StyleHover.BackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 28, 166, 154));
        solidCard.StyleHover.BorderBrush = new SolidColorBrush(Color.White);
        solidCard.StylePressed.BackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 225, 72, 118));
        solidCard.StylePressed.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 224, 130));
        AddCardTransitions(solidCard);
        solidCard.Controls.Add(new PassiveLabel
        {
            Dock = DockStyle.Fill,
            Text = "Solid  <->  Solid",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White,
            BackColor = SKColors.Transparent
        });

        var mixedCard = stage.Controls.Add(new HoverPanel
        {
            Bounds = new Rectangle(370, 68, 330, 184)
        });
        mixedCard.Style.BackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 78, 70, 180));
        mixedCard.Style.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 45, 38, 120));
        mixedCard.Style.Border.Width = 4;
        mixedCard.StyleHover.BackgroundBrush = Linear(
            (Color.FromArgb(255, 28, 126, 214), 0f),
            (Color.FromArgb(255, 53, 196, 154), 0.52f),
            (Color.FromArgb(255, 247, 193, 72), 1f));
        mixedCard.StyleHover.BorderBrush = Linear(
            (Color.White, 0f),
            (Color.FromArgb(255, 28, 126, 214), 1f));
        mixedCard.StylePressed.BackgroundBrush = Linear(
            (Color.FromArgb(255, 225, 72, 118), 0f),
            (Color.FromArgb(255, 111, 65, 190), 1f));
        mixedCard.StylePressed.BorderBrush = new SolidColorBrush(Color.White);

        AddCardTransitions(mixedCard);

        mixedCard.Controls.Add(new PassiveLabel
        {
            Dock = DockStyle.Fill,
            Text = "Solid  <->  3 stops  <->  2 stops",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White,
            BackColor = SKColors.Transparent
        });

        Controls.Add(new Label
        {
            Left = 24,
            Top = 462,
            Width = 720,
            Height = 58,
            Multiline = true,
            Text = "Expected: color, alpha, stop structure and border paint move smoothly; releasing or leaving lands exactly on the authored state and the scheduler returns to idle."
        });
    }

    private static LinearGradientBrush Linear(params (Color Color, float Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            Start = new PointF(0f, 0f),
            End = new PointF(1f, 1f)
        };
        foreach ((Color color, float offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
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

    private static void AddCardTransitions(Control control)
    {
        AddTransition(control, VisualState.Normal, VisualState.Hover, 320);
        AddTransition(control, VisualState.Hover, VisualState.Normal, 260);
        AddTransition(control, VisualState.Hover, VisualState.Pressed, 140);
        AddTransition(control, VisualState.Pressed, VisualState.Hover, 180);
    }

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
}
