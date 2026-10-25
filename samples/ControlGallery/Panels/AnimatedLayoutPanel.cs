using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ControlGallery.Panels;

/// <summary>
/// Provides manual checks for opt-in logical-to-presentation layout transitions.
/// </summary>
public sealed class AnimatedLayoutPanel : BasePanel
{
    private static readonly Rectangle StartBounds = new(24, 34, 190, 100);
    private static readonly Rectangle EndBounds = new(430, 142, 260, 150);

    private readonly Panel card;
    private readonly LayoutTransition transition;
    private readonly Label status;
    private readonly Button transitionButton;
    private bool atEnd;

    /// <summary>
    /// Initializes the animated-layout demonstration.
    /// </summary>
    public AnimatedLayoutPanel()
    {
        AutoScroll = true;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "Animated layout",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 760,
            Height = 42,
            Multiline = true,
            Text = "Bounds changes remain logical and immediate to code, while the card is composed and hit-tested at an interpolated presentation rectangle. Its child content moves with it as one visual subtree."
        });

        var stage = Controls.Add(new Panel
        {
            Left = 24,
            Top = 106,
            Width = 720,
            Height = 326,
            BackColor = new SKColor(242, 245, 249)
        });
        stage.Style.Border.Width = 1;
        stage.Style.Border.Color = Theme.BorderMidColor;

        transition = new LayoutTransition
        {
            Duration = TimeSpan.FromMilliseconds(550),
            Easing = Easings.EaseOut,
            ApplyToDescendants = true
        };
        // Borrowed container configuration; card.LayoutTransition intentionally remains null.
        stage.LayoutTransition = transition;
        card = stage.Controls.Add(new Panel
        {
            Bounds = StartBounds,
            BackgroundBrush = new SolidColorBrush(Color.MediumPurple)
        });
        card.Style.Border.Width = 1;
        card.Style.Border.Color = new SKColor(75, 0, 130);
        Label cardContent = card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Click while moving",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter,
            ForeColor = SKColors.White
        });

        AddButton(24, 450, 158, "Move + resize", ToggleTarget);
        AddButton(190, 450, 158, "Rapid retarget", RapidRetarget);
        transitionButton = AddButton(356, 450, 178, string.Empty, ToggleTransition);
        AddButton(542, 450, 158, "Reset", ResetCard);
        AddButton(24, 630, 220, "Zero width / restore", () =>
        {
            if (card.Width == 0)
            {
                // Restore the logical toggle state as well as its rectangle, so the next move
                // always selects the alternate target, including while motion is disabled.
                ResetCard();
                return;
            }

            card.Width = 0;
            UpdateStatus();
        });

        status = Controls.Add(new Label
        {
            Left = 24,
            Top = 500,
            Width = 720,
            Height = 54,
            Multiline = true
        });
        card.Click += (_, _) => ReportCardClick();
        cardContent.Click += (_, _) => ReportCardClick();
        Controls.Add(new Label
        {
            Left = 24,
            Top = 562,
            Width = 720,
            Height = 58,
            Multiline = true,
            Text = "The stage supplies an inherited policy; the card has no local value. Click while moving, retarget, or disable mid-flight. Zero width and restoring from zero must snap, with no old content left behind."
        });

        UpdateStatus();
    }

    private Button AddButton(int left, int top, int width, string text, Action action)
    {
        var button = Controls.Add(new Button
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 36,
            Text = text
        });
        button.Click += (_, _) => action();
        return button;
    }

    private void ToggleTarget()
    {
        atEnd = !atEnd;
        card.Bounds = atEnd ? EndBounds : StartBounds;
        UpdateStatus();
    }

    private void RapidRetarget()
    {
        card.Bounds = new Rectangle(110, 62, 220, 118);
        card.Bounds = new Rectangle(300, 188, 170, 112);
        card.Bounds = atEnd ? StartBounds : EndBounds;
        atEnd = !atEnd;
        UpdateStatus();
    }

    private void ToggleTransition()
    {
        transition.Enabled = !transition.Enabled;
        UpdateStatus();
    }

    private void ResetCard()
    {
        atEnd = false;
        card.Bounds = StartBounds;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        transitionButton.Text = transition.Enabled ? "Transition: enabled" : "Transition: disabled";
        status.Text = $"Logical Bounds = {card.Bounds} | Duration = {transition.Duration.TotalMilliseconds:0} ms | {(transition.Enabled ? "EaseOut" : "immediate")}";
    }

    private void ReportCardClick()
    {
        status.Text = $"Card clicked. Logical Bounds = {card.Bounds}";
    }
}
