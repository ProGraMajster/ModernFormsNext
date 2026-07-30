using System;
using System.Collections.Generic;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;
using SkiaSharp;

namespace ControlGallery.Panels;

/// <summary>
/// Provides manual demonstrations for composable animations, visual-state transitions,
/// and scheduler-backed interaction effects.
/// </summary>
public sealed class AnimationsAndInteractionEffectsPanel : BasePanel
{
    private readonly AnimationScheduler scheduler = AnimationScheduler.Default;
    private readonly List<AnimationRun> runs = [];
    private readonly RippleDemoButton pointerRipple;
    private readonly RippleDemoButton centerRipple;
    private readonly RippleDemoButton rapidRipple;
    private readonly Button transitionButton;
    private readonly Panel animationTarget;
    private readonly Label diagnosticsLabel;
    private readonly Button animationsButton;
    private readonly Button reducedMotionButton;
    private readonly ComboBox ripplePolicyCombo;
    private readonly bool originalAnimationsEnabled;
    private readonly bool originalReducedMotion;
    private readonly double originalDurationScale;
    private bool alternate;
    private bool unloaded;

    /// <summary>Initializes the opt-in animation and interaction-effects gallery page.</summary>
    public AnimationsAndInteractionEffectsPanel()
    {
        AutoScroll = true;
        originalAnimationsEnabled = scheduler.Policy.AnimationsEnabled;
        originalReducedMotion = scheduler.Policy.ApplicationReducedMotion;
        originalDurationScale = scheduler.Policy.DurationScale;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 820,
            Height = 30,
            Text = "Animations and Interaction Effects",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 820,
            Height = 42,
            Multiline = true,
            Text = "All demos share AnimationScheduler.Default. Hover, focus, click, resize, cancel, and rapidly repeat actions while watching active work return to idle."
        });

        pointerRipple = AddEffectButton(24, 108, "Pointer ripple", startFromPointer: true);
        centerRipple = AddEffectButton(218, 108, "Center ripple", startFromPointer: false);
        rapidRipple = AddEffectButton(412, 108, "Rapid ripple target", startFromPointer: true);
        rapidRipple.Ripple!.MaxConcurrentRipples = 4;

        transitionButton = AddEffectButton(606, 108, "Hover / focus / press", startFromPointer: true);
        transitionButton.PressEffect = new PressScaleEffect();
        transitionButton.Style.BackgroundColor = SKColors.SlateBlue;
        transitionButton.Style.ForegroundColor = SKColors.White;
        transitionButton.StyleHover.BackgroundColor = SKColors.MediumPurple;
        transitionButton.StyleHover.ScaleX = 1.04f;
        transitionButton.StyleHover.ScaleY = 1.04f;
        transitionButton.StyleFocused.Border.Color = SKColors.Gold;
        transitionButton.StyleFocused.Border.Width = 2;
        transitionButton.StylePressed.BackgroundColor = SKColors.DarkSlateBlue;
        transitionButton.StyleDisabled.Opacity = 0.45f;
        AddStateTransition(transitionButton, VisualState.Normal, VisualState.Hover);
        AddStateTransition(transitionButton, VisualState.Hover, VisualState.Normal);
        AddStateTransition(transitionButton, VisualState.Normal, VisualState.Focused);
        AddStateTransition(transitionButton, VisualState.Focused, VisualState.Normal);
        AddStateTransition(transitionButton, VisualState.Hover, VisualState.Pressed);
        AddStateTransition(transitionButton, VisualState.Pressed, VisualState.Hover);

        animationTarget = Controls.Add(new Panel
        {
            Left = 24,
            Top = 202,
            Width = 150,
            Height = 72,
            BackColor = SKColors.CornflowerBlue
        });
        animationTarget.Style.Border.Width = 1;
        animationTarget.Style.Border.Color = Theme.BorderMidColor;
        animationTarget.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Animation target",
            TextAlign = ModernFormsNext.ContentAlignment.MiddleCenter
        });

        AddButton(194, 202, 110, "Sequence", RunSequence);
        AddButton(312, 202, 110, "Parallel", RunParallel);
        AddButton(430, 202, 110, "Timeline", RunTimeline);
        AddButton(548, 202, 110, "Keyframes", RunKeyframes);
        AddButton(666, 202, 110, "Repeat", RunRepeat);

        AddButton(194, 244, 110, "Auto-reverse", RunAutoReverse);
        AddButton(312, 244, 110, "Shake", RunShake);
        AddButton(430, 244, 110, "Pulse", RunPulse);
        AddButton(548, 244, 110, "Interpolator", RunCustomInterpolator);
        AddButton(666, 244, 110, "Disabled state", ToggleTransitionDisabled);

        AddButton(24, 304, 110, "Start", RunSequence);
        AddButton(142, 304, 110, "Cancel", CancelAll);
        AddButton(260, 304, 110, "Rapid x5", RunRapidRipples);
        AddButton(378, 304, 110, "Replace", RunReplace);
        AddButton(496, 304, 110, "IgnoreNew", RunIgnoreNew);
        reducedMotionButton = AddButton(614, 304, 164, string.Empty, ToggleReducedMotion);

        animationsButton = AddButton(24, 346, 180, string.Empty, ToggleAnimations);
        AddButton(212, 346, 180, "Refresh platform policy", RefreshPlatformPolicy);
        AddButton(400, 346, 180, "Reset transforms", ResetTarget);
        AddButton(588, 346, 190, "Resize ripple targets", ResizeRippleTargets);

        Controls.Add(new Label
        {
            Left = 24,
            Top = 396,
            Width = 164,
            Height = 28,
            Text = "Rapid ripple overflow:"
        });
        ripplePolicyCombo = Controls.Add(new ComboBox
        {
            Left = 194,
            Top = 392,
            Width = 190,
            Height = 32
        });
        foreach (string policy in Enum.GetNames<RippleOverflowPolicy>())
            ripplePolicyCombo.Items.Add(policy);
        ripplePolicyCombo.SelectedIndexChanged += (_, _) => ApplyRipplePolicy();
        AddButton(400, 392, 180, "Rapid input x20", RunRapidRipples20);
        AddButton(588, 392, 190, "Refresh diagnostics", UpdateDiagnostics);

        diagnosticsLabel = Controls.Add(new Label
        {
            Left = 24,
            Top = 442,
            Width = 754,
            Height = 96,
            Multiline = true
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 548,
            Width = 754,
            Height = 118,
            Multiline = true,
            Text = "Manual checklist: change Windows animation preference while this page is open; verify immediate reduced-motion behavior and return to enabled; select each ripple overflow policy and run rapid input; resize during a wave; test pointer/keyboard activation, disabled state, cancel, Designer save/reload, and diagnostics returning to Active 0 / Tick source stopped."
        });

        ripplePolicyCombo.SelectedIndex = 0;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    /// <inheritdoc/>
    public override void UnloadPanel()
    {
        if (unloaded)
            return;
        unloaded = true;
        CancelAll();
        scheduler.Policy.AnimationsEnabled = originalAnimationsEnabled;
        scheduler.Policy.ReducedMotion = originalReducedMotion;
        scheduler.Policy.DurationScale = originalDurationScale;
        base.UnloadPanel();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            UnloadPanel();
        base.Dispose(disposing);
    }

    private RippleDemoButton AddEffectButton(
        int left,
        int top,
        string text,
        bool startFromPointer)
    {
        var button = Controls.Add(new RippleDemoButton
        {
            Left = left,
            Top = top,
            Width = 176,
            Height = 58,
            Text = text,
            Ripple = new RippleEffect
            {
                Color = Color.FromArgb(105, 255, 255, 255),
                Duration = TimeSpan.FromMilliseconds(500),
                StartFromPointer = startFromPointer,
                RadiusMode = RippleRadiusMode.CoverControl,
                Layer = RippleLayer.AboveBackgroundBelowContent,
                MaxConcurrentRipples = 4
            }
        });
        button.Style.BackgroundColor = SKColors.Teal;
        button.Style.ForegroundColor = SKColors.White;
        button.Style.Border.Radius = 12;
        return button;
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

    private void RunSequence()
    {
        ResetTarget();
        AnimationDefinition animation = Animation.Sequence(
            animationTarget.FadeTo(0.35f, Options(220)),
            animationTarget.ScaleTo(1.18f, Options(220)),
            Animation.Parallel(
                animationTarget.FadeTo(1f, Options(220)),
                animationTarget.ScaleTo(1f, Options(220))));
        Track(animation.Start(scheduler));
    }

    private void RunParallel()
    {
        alternate = !alternate;
        float direction = alternate ? 1f : -1f;
        Track(Animation.Parallel(
            animationTarget.TranslateTo(120f * direction, 22f, Options(650)),
            animationTarget.RotateTo(18f * direction, Options(650)),
            animationTarget.FadeTo(0.55f, Options(650)))
            .Start(scheduler));
    }

    private void RunTimeline()
    {
        var timeline = new AnimationTimeline()
            .At(TimeSpan.Zero, animationTarget.FadeTo(0.35f, Options(300)))
            .At(TimeSpan.FromMilliseconds(120), animationTarget.ScaleTo(1.2f, Options(300)))
            .At(TimeSpan.FromMilliseconds(300), Animation.Parallel(
                animationTarget.FadeTo(1f, Options(300)),
                animationTarget.ScaleTo(1f, Options(300)),
                animationTarget.TranslateTo(0f, 0f, Options(300))));
        Track(timeline.Start(scheduler));
    }

    private void RunKeyframes()
    {
        var keyframes = KeyframeAnimation<float>
            .Create(
                animationTarget,
                value =>
                {
                    animationTarget.ScaleX = value;
                    animationTarget.ScaleY = value;
                })
            .Keyframe(0f, 1f)
            .Keyframe(0.4f, 1.25f, Easings.CubicOut)
            .Keyframe(0.72f, 0.94f, Easings.CubicOut)
            .Keyframe(1f, 1f, Easings.BounceOut);
        keyframes.Duration = TimeSpan.FromMilliseconds(800);
        Track(keyframes.Start(scheduler));
    }

    private void RunRepeat()
    {
        AnimationDefinition animation = animationTarget
            .TranslateTo(70f, 0f, Options(220))
            .Repeat(3);
        Track(animation.Start(scheduler));
    }

    private void RunAutoReverse()
    {
        AnimationDefinition animation = animationTarget
            .RotateTo(16f, Options(260))
            .Repeat(2)
            .AutoReverse();
        Track(animation.Start(scheduler));
    }

    private void RunShake()
    {
        var shake = new ShakeAnimation
        {
            Duration = TimeSpan.FromMilliseconds(700),
            Distance = 14f,
            Oscillations = 5
        };
        Track(shake.Start(animationTarget, scheduler));
    }

    private void RunPulse()
    {
        var pulse = KeyframeAnimation<float>
            .Create(
                animationTarget,
                value =>
                {
                    animationTarget.ScaleX = value;
                    animationTarget.ScaleY = value;
                })
            .Keyframe(0f, 1f)
            .Keyframe(0.5f, 1.14f, Easings.CubicOut)
            .Keyframe(1f, 1f, Easings.CubicOut);
        pulse.Duration = TimeSpan.FromMilliseconds(400);
        pulse.Repeat(3);
        Track(pulse.Start(scheduler));
    }

    private void RunCustomInterpolator()
    {
        var animation = new PropertyAnimation<float>(
            animationTarget,
            "Gallery.CustomInterpolator",
            animationTarget.TranslationY,
            animationTarget.TranslationY < 35f ? 70f : 0f,
            new OvershootInterpolator(),
            value => animationTarget.TranslationY = value)
        {
            Duration = TimeSpan.FromMilliseconds(700),
            Easing = Easings.CubicOut
        };
        Track(animation.Start(scheduler));
    }

    private void RunReplace()
    {
        var first = animationTarget.RotateTo(180f, new AnimationOptions
        {
            Duration = TimeSpan.FromSeconds(2),
            Easing = Easings.Linear,
            ReplacementMode = AnimationReplacementMode.Replace
        });
        var replacement = animationTarget.RotateTo(0f, Options(500));
        Track(first.Start(scheduler));
        Track(replacement.Start(scheduler));
    }

    private void RunIgnoreNew()
    {
        var options = new AnimationOptions
        {
            Duration = TimeSpan.FromMilliseconds(1200),
            Easing = Easings.CubicOut,
            ReplacementMode = AnimationReplacementMode.IgnoreNew
        };
        Track(animationTarget.RotateTo(120f, options).Start(scheduler));
        Track(animationTarget.RotateTo(-120f, options).Start(scheduler));
    }

    private void RunRapidRipples()
    {
        for (int index = 0; index < 5; index++)
            rapidRipple.TriggerRipple(18 + (index * 28), 16 + ((index % 2) * 18), index + 1);
        UpdateDiagnostics();
    }

    private void RunRapidRipples20()
    {
        for (int index = 0; index < 20; index++)
            rapidRipple.TriggerRipple(12 + ((index * 17) % Math.Max(1, rapidRipple.Width - 24)), 12 + ((index % 2) * 22), index + 100);
        UpdateDiagnostics();
    }

    private void ApplyRipplePolicy()
    {
        if (rapidRipple?.Ripple is null
            || ripplePolicyCombo.SelectedItem is not { } selected
            || !Enum.TryParse(selected.ToString(), out RippleOverflowPolicy policy))
        {
            return;
        }

        rapidRipple.Ripple.OverflowPolicy = policy;
        UpdateDiagnostics();
    }

    private void ToggleTransitionDisabled()
        => transitionButton.Enabled = !transitionButton.Enabled;

    private void ToggleAnimations()
    {
        scheduler.Policy.AnimationsEnabled = !scheduler.Policy.AnimationsEnabled;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void ToggleReducedMotion()
    {
        scheduler.Policy.ReducedMotion = !scheduler.Policy.ApplicationReducedMotion;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void RefreshPlatformPolicy()
    {
        scheduler.RefreshPlatformPolicy();
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void ResizeRippleTargets()
    {
        int height = pointerRipple.Height < 70 ? 78 : 58;
        pointerRipple.Height = height;
        centerRipple.Height = height;
        rapidRipple.Height = height;
    }

    private void ResetTarget()
    {
        animationTarget.Opacity = 1f;
        animationTarget.TranslationX = 0f;
        animationTarget.TranslationY = 0f;
        animationTarget.ScaleX = 1f;
        animationTarget.ScaleY = 1f;
        animationTarget.Rotation = 0f;
        UpdateDiagnostics();
    }

    private void CancelAll()
    {
        foreach (AnimationRun run in runs)
            run.Cancel();
        runs.Clear();
        scheduler.CancelAll(animationTarget);
        CancelRipples(pointerRipple, centerRipple, rapidRipple, transitionButton);
        UpdateDiagnostics();
    }

    private void Track(AnimationRun run)
    {
        runs.RemoveAll(static item => item.State is
            AnimationState.Completed or AnimationState.Canceled or AnimationState.Faulted);
        runs.Add(run);
        UpdateDiagnostics();
    }

    private void UpdatePolicyButtons()
    {
        animationsButton.Text = scheduler.Policy.AnimationsEnabled
            ? "Animations: enabled"
            : "Animations: disabled";
        reducedMotionButton.Text = scheduler.Policy.ReducedMotion
            ? $"Reduced motion: on{(scheduler.Policy.ApplicationReducedMotion ? " (app)" : " (platform)")}"
            : "Reduced motion: off";
    }

    private void UpdateDiagnostics()
    {
        AnimationSchedulerDiagnostics diagnostics = scheduler.GetDiagnostics();
        AnimationPlatformDiagnostics platform = scheduler.GetPlatformDiagnostics();
        int activeRipples =
            (pointerRipple?.Ripple?.ActiveRippleCount ?? 0) +
            (centerRipple?.Ripple?.ActiveRippleCount ?? 0) +
            (rapidRipple?.Ripple?.ActiveRippleCount ?? 0) +
            (transitionButton?.Ripple?.ActiveRippleCount ?? 0);
        diagnosticsLabel.Text =
            $"Active: {diagnostics.ActiveAnimationCount} | completed: {diagnostics.CompletedCount} | canceled: {diagnostics.CanceledCount} | faulted: {diagnostics.FaultedCount} | active ripples: {activeRipples}\n" +
            $"Ticks: {diagnostics.TickCount} | tick source: {(diagnostics.IsTickSourceRunning ? "running" : "stopped")} | policy: {(scheduler.Policy.AnimationsEnabled ? "enabled" : "disabled")}, reduced motion {(scheduler.Policy.ReducedMotion ? "on" : "off")}\n" +
            $"Platform: {platform.Source} | state: {platform.ProviderState} | animations: {(platform.AnimationsEnabled ? "enabled" : "disabled")} | fallback: {platform.FallbackUsed} | last update: {platform.LastPlatformUpdate?.ToString("O") ?? "never"}" +
            (string.IsNullOrWhiteSpace(platform.LastError) ? string.Empty : $" | error: {platform.LastError}");
    }

    private static void CancelRipples(params Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            if (button.Ripple is not { } ripple)
                continue;
            ripple.Enabled = false;
            ripple.Enabled = true;
        }
    }

    private static void AddStateTransition(Button button, VisualState from, VisualState to)
        => button.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = Easings.CubicOut
            });

    private static AnimationOptions Options(int milliseconds)
        => new()
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            Easing = Easings.CubicOut
        };

    private sealed class RippleDemoButton : Button
    {
        public void TriggerRipple(int x, int y, int pointerId)
        {
            var args = new MouseEventArgs(
                MouseButtons.Left,
                1,
                x,
                y,
                Point.Empty,
                null,
                null,
                Keys.None,
                pointerId,
                PointerDeviceKind.Touch);
            OnMouseDown(args);
            OnMouseUp(args);
        }
    }

    private sealed class ShakeAnimation : AnimationDefinition
    {
        public float Distance { get; init; } = 8f;
        public int Oscillations { get; init; } = 4;

        protected override void Update(AnimationContext context, float progress)
        {
            context.Target.TranslationX =
                MathF.Sin(progress * MathF.PI * 2f * Oscillations)
                * Distance
                * (1f - progress);
        }
    }

    private sealed class OvershootInterpolator : IAnimationInterpolator<float>
    {
        public float Interpolate(float from, float to, float progress)
        {
            float overshoot = MathF.Sin(progress * MathF.PI) * 0.18f;
            return from + ((to - from) * (progress + overshoot));
        }
    }
}
