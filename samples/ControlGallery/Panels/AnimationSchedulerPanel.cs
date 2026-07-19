using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;

namespace ControlGallery.Panels;

/// <summary>
/// Provides opt-in manual checks for the shared UI animation scheduler.
/// </summary>
public sealed class AnimationSchedulerPanel : BasePanel
{
    private readonly AnimationScheduler scheduler = AnimationScheduler.Default;
    private readonly List<AnimationHandle> handles = [];
    private readonly Panel opacityCard;
    private readonly Panel easingCard;
    private readonly SolidColorBrush colorBrush;
    private readonly LinearGradientBrush gradientBrush;
    private readonly Label diagnosticsLabel;
    private readonly Button animationsButton;
    private readonly Button reducedMotionButton;
    private readonly bool originalAnimationsEnabled;
    private readonly bool originalReducedMotion;
    private readonly double originalDurationScale;
    private bool alternate;
    private bool unloaded;

    /// <summary>
    /// Initializes a new animation scheduler demonstration panel.
    /// </summary>
    public AnimationSchedulerPanel()
    {
        AutoScroll = true;
        originalAnimationsEnabled = scheduler.Policy.AnimationsEnabled;
        originalReducedMotion = scheduler.Policy.ReducedMotion;
        originalDurationScale = scheduler.Policy.DurationScale;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "Shared UI animation scheduler",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 780,
            Height = 42,
            Multiline = true,
            Text = "Each action uses the same monotonic, UI-thread scheduler. Start several effects, replace or cancel them, then verify that diagnostics return to zero active animations."
        });

        opacityCard = AddCard(24, 108, "Opacity", new SolidColorBrush(Color.CornflowerBlue));
        colorBrush = new SolidColorBrush(Color.MediumPurple);
        AddCard(280, 108, "Color", colorBrush);
        easingCard = AddCard(536, 108, "EaseInOutCubic", new SolidColorBrush(Color.MediumSeaGreen));

        gradientBrush = CreateGradient();
        AddCard(24, 250, "Gradient stops", gradientBrush);
        AddCard(280, 250, "Brush transform", gradientBrush);
        AddCard(536, 250, "Parallel channels", new SolidColorBrush(Color.Orange));

        AddButton(24, 392, 118, "Opacity", RunOpacity);
        AddButton(150, 392, 118, "Color", RunColor);
        AddButton(276, 392, 118, "Easing", RunEasing);
        AddButton(402, 392, 118, "Gradient", RunGradient);
        AddButton(528, 392, 118, "Transform", RunBrushTransform);
        AddButton(654, 392, 118, "Run parallel", RunParallel);

        AddButton(24, 434, 118, "Replace", RunReplacement);
        AddButton(150, 434, 118, "Cancel all", CancelAllAnimations);
        animationsButton = AddButton(276, 434, 164, string.Empty, ToggleAnimations);
        reducedMotionButton = AddButton(448, 434, 164, string.Empty, ToggleReducedMotion);
        AddButton(620, 434, 152, "Refresh status", UpdateDiagnostics);

        diagnosticsLabel = Controls.Add(new Label
        {
            Left = 24,
            Top = 486,
            Width = 748,
            Height = 58,
            Multiline = true
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 552,
            Width = 748,
            Height = 66,
            Multiline = true,
            Text = "Manual checks: rapidly press Replace, cancel mid-flight, and toggle both motion policies. Leaving this page cancels all handles and restores the policy values that were active before the page opened. No animation runs automatically."
        });

        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    /// <inheritdoc />
    public override void UnloadPanel()
    {
        if (unloaded)
            return;

        unloaded = true;
        CancelAllAnimations();
        scheduler.Policy.AnimationsEnabled = originalAnimationsEnabled;
        scheduler.Policy.ReducedMotion = originalReducedMotion;
        scheduler.Policy.DurationScale = originalDurationScale;
        base.UnloadPanel();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            UnloadPanel();
        base.Dispose(disposing);
    }

    private Panel AddCard(int left, int top, string caption, ModernFormsNext.Drawing.Brush brush)
    {
        var card = Controls.Add(new Panel
        {
            Left = left,
            Top = top,
            Width = 232,
            Height = 112,
            BackgroundBrush = brush
        });
        card.Style.Border.Width = 1;
        card.Style.Border.Color = Theme.BorderMidColor;
        card.Controls.Add(new Label
        {
            Left = 10,
            Top = 76,
            Width = 210,
            Height = 25,
            Text = caption,
            BackColor = new SkiaSharp.SKColor(255, 255, 255, 215)
        });
        return card;
    }

    private Button AddButton(int left, int top, int width, string text, Action action)
    {
        var button = Controls.Add(new Button
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 32,
            Text = text
        });
        button.Click += (_, _) => action();
        return button;
    }

    private void RunOpacity()
    {
        alternate = !alternate;
        float target = alternate ? 0.2f : 1f;
        Track(scheduler.Animate(
            this,
            "Opacity",
            opacityCard.Opacity,
            target,
            AnimationInterpolators.Float,
            value =>
            {
                opacityCard.Opacity = value;
                UpdateDiagnostics();
            },
            Options(Easings.EaseInOut)));
    }

    private void RunColor()
    {
        Color target = alternate ? Color.DeepSkyBlue : Color.OrangeRed;
        Track(scheduler.Animate(
            this,
            "Color",
            colorBrush.PaintColor,
            target,
            AnimationInterpolators.Color,
            value =>
            {
                colorBrush.PaintColor = value;
                UpdateDiagnostics();
            },
            Options(Easings.EaseOut)));
    }

    private void RunEasing()
    {
        float target = easingCard.TranslationX < 50f ? 150f : 0f;
        Track(scheduler.Animate(
            this,
            "EasingTranslation",
            easingCard.TranslationX,
            target,
            AnimationInterpolators.Float,
            value =>
            {
                easingCard.TranslationX = value;
                UpdateDiagnostics();
            },
            Options(Easings.EaseInOutCubic)));
    }

    private void RunGradient()
    {
        var target = new GradientStop(
            alternate ? Color.Gold : Color.HotPink,
            alternate ? 0.72f : 0.35f);
        Track(gradientBrush.GradientStops[1].AnimateTo(
            target,
            TimeSpan.FromMilliseconds(800),
            easing: Easings.EaseInOut,
            scheduler: scheduler));
        UpdateDiagnostics();
    }

    private void RunBrushTransform()
    {
        Matrix3x2 target = gradientBrush.Transform == Matrix3x2.Identity
            ? Matrix3x2.CreateRotation(0.3f, new Vector2(116f, 56f))
            : Matrix3x2.Identity;
        Track(scheduler.Animate(
            this,
            "BrushTransform",
            gradientBrush.Transform,
            target,
            AnimationInterpolators.Matrix3x2,
            value =>
            {
                gradientBrush.Transform = value;
                UpdateDiagnostics();
            },
            Options(Easings.EaseInOut)));
    }

    private void RunParallel()
    {
        RunOpacity();
        RunColor();
        RunEasing();
        RunGradient();
        RunBrushTransform();
    }

    private void RunReplacement()
    {
        AnimationOptions slow = Options(Easings.Linear);
        slow.Duration = TimeSpan.FromSeconds(2);
        Track(scheduler.Animate(
            this,
            "Replacement",
            easingCard.TranslationY,
            120f,
            AnimationInterpolators.Float,
            value => easingCard.TranslationY = value,
            slow));
        Track(scheduler.Animate(
            this,
            "Replacement",
            easingCard.TranslationY,
            easingCard.TranslationY < 20f ? 60f : 0f,
            AnimationInterpolators.Float,
            value =>
            {
                easingCard.TranslationY = value;
                UpdateDiagnostics();
            },
            Options(Easings.EaseOut)));
        UpdateDiagnostics();
    }

    private void CancelAllAnimations()
    {
        scheduler.CancelAll(this);
        foreach (AnimationHandle handle in handles)
            handle.Cancel();
        handles.Clear();
        UpdateDiagnostics();
    }

    private void ToggleAnimations()
    {
        scheduler.Policy.AnimationsEnabled = !scheduler.Policy.AnimationsEnabled;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void ToggleReducedMotion()
    {
        scheduler.Policy.ReducedMotion = !scheduler.Policy.ReducedMotion;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void Track(AnimationHandle handle)
    {
        handles.RemoveAll(static item => item.State is
            AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted);
        handles.Add(handle);
        UpdateDiagnostics();
    }

    private void UpdatePolicyButtons()
    {
        animationsButton.Text = scheduler.Policy.AnimationsEnabled
            ? "Animations: enabled"
            : "Animations: disabled";
        reducedMotionButton.Text = scheduler.Policy.ReducedMotion
            ? "Reduced motion: on"
            : "Reduced motion: off";
    }

    private void UpdateDiagnostics()
    {
        AnimationSchedulerDiagnostics diagnostics = scheduler.GetDiagnostics();
        diagnosticsLabel.Text =
            $"Active: {diagnostics.ActiveAnimationCount} | ticks: {diagnostics.TickCount} | completed: {diagnostics.CompletedCount} | canceled: {diagnostics.CanceledCount} | faulted: {diagnostics.FaultedCount}\n" +
            $"Tick source: {(diagnostics.IsTickSourceRunning ? "running" : "stopped")} | scheduler: {(diagnostics.IsPaused ? "paused" : "active")} | average tick: {diagnostics.AverageTickDuration.TotalMilliseconds:F3} ms";
    }

    private static AnimationOptions Options(Func<float, float> easing)
        => new()
        {
            Duration = TimeSpan.FromMilliseconds(800),
            Easing = easing
        };

    private static LinearGradientBrush CreateGradient()
    {
        var brush = new LinearGradientBrush
        {
            Start = new PointF(0f, 0f),
            End = new PointF(1f, 1f)
        };
        brush.GradientStops.AddRange([
            new GradientStop(Color.MediumPurple, 0f),
            new GradientStop(Color.HotPink, 0.5f),
            new GradientStop(Color.Orange, 1f)
        ]);
        return brush;
    }
}
