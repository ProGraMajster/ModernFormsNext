using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext.Animations;
using ModernFormsNext.Diagnostics;
using SkiaSharp;

namespace ModernFormsNext.PerformanceLab;

internal sealed class PacingScenario(PacingWorkload workload) : BenchmarkScenario
{
    private const int Steps = 96;
    private Control content = null!;
    private PaintPulse? pulse;
    private AnimationHandle? animation;

    internal override string Id => "pacing-" + workload.ToString().ToLowerInvariant();
    internal override string WorkloadVersion => "s02d-ui-thread-" + Id + "-v1";
    internal override bool AllowsEmptyMeasurement => workload == PacingWorkload.Idle;

    internal override Control CreateContent()
    {
        if (workload == PacingWorkload.Tree)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            panel.SuspendLayout();
            for (int index = 0; index < 320; index++)
                panel.Controls.Add(new Label { Text = $"Row {index:D3}", Width = 70, Height = 24 });
            panel.ResumeLayout();
            return content = panel;
        }

        pulse = new PaintPulse { Dock = DockStyle.Fill };
        return content = pulse;
    }

    internal override void Begin(BenchmarkRun run)
    {
        if (workload == PacingWorkload.Idle)
        {
            run.BeginMeasurement();
            run.Schedule(1400, () => run.Complete());
            return;
        }

        int steps = 0;
        int startingPaints = 0;
        int animationUpdates = 0;
        pulse?.SetProfiler(run.Profiler);
        if (workload == PacingWorkload.Animation) animation = StartAnimation();
        // The handle has scenario lifetime. Complete and native Close both retire it.
        run.Window.Closed += (_, _) => animation?.Dispose();
        run.Schedule(10, Warm);

        void Warm()
        {
            if (!run.CanStartMeasuredWork)
            {
                Update(steps++);
                return;
            }

            if (workload == PacingWorkload.Animation)
            {
                animation?.Dispose();
                animationUpdates = 0;
                animation = StartAnimation();
            }

            pulse?.ResetInput();
            steps = 0;
            startingPaints = run.PaintPasses;
            run.BeginMeasurement();
            run.Schedule(10, Measured);
        }

        AnimationHandle StartAnimation() => AnimationScheduler.Default.Start(
            content, "S02-d pacing lab", progress =>
            {
                animationUpdates++;
                pulse!.Fraction = progress;
                pulse.Invalidate();
            }, new AnimationOptions { Duration = TimeSpan.FromSeconds(2) });

        void Measured()
        {
            Update(steps++);
            if (steps < Steps) return;
            animation?.Dispose();
            // Allow the final scheduled repaint to enter the native callback and finish.
            run.Schedule(80, () =>
            {
                if (run.PaintPasses == startingPaints)
                    throw new InvalidOperationException("The pacing workload produced no control paints.");
                run.SetWorkloadUpdates(workload == PacingWorkload.Animation ? animationUpdates : Steps);
                run.SetSyntheticInput(pulse?.GetInput());
                run.Complete();
            });
        }

        void Update(int index)
        {
            switch (workload)
            {
                case PacingWorkload.Tree:
                    // Independent damage requests within a single dispatcher turn.
                    for (int i = 0; i < 48; i++)
                    {
                        Control child = content.Controls[(index * 29 + i) % 320];
                        child.ForeColor = child.ForeColor == SKColors.Maroon ? SKColors.Navy : SKColors.Maroon;
                    }
                    break;
                case PacingWorkload.Resize:
                    run.Window.ClientSize = new Size(920 + index % 5 * 12, 610 + index % 4 * 10);
                    pulse!.Fraction = (index % 17) / 16d;
                    pulse.Invalidate();
                    break;
                case PacingWorkload.Input:
                    pulse!.PostPointer(run.Window, index);
                    break;
                case PacingWorkload.Animation:
                    if (animation is null || animation.Completion.IsCompleted)
                        animation = StartAnimation();
                    break;
            }
        }
    }

    private sealed class PaintPulse : Control
    {
        private PerformanceProfiler profiler = null!;
        private readonly List<SyntheticInputSample> samples = new(Steps);
        private TimeSpan? postedAt;
        private TimeSpan? handledAt;
        private int posted, skipped;

        internal double Fraction { get; set; }

        internal void SetProfiler(PerformanceProfiler owner) => profiler = owner;

        internal void ResetInput()
        {
            postedAt = handledAt = null;
            samples.Clear();
            posted = skipped = 0;
        }

        internal SyntheticInputObservation? GetInput()
            => posted == 0 ? null : new(posted, skipped, samples.ToArray());

        internal void PostPointer(Form form, int step)
        {
            if (postedAt.HasValue)
            {
                skipped++;
                return;
            }
            // Only the explicitly synthetic message sent to this lab-owned HWND is timed.
            const uint wmMouseMove = 0x0200;
            int x = (int)(178 * form.Scaling) + step % 97;
            int y = (int)(176 * form.Scaling);
            postedAt = profiler.ReadElapsedMark();
            posted++;
            if (!PostMessage(form.PlatformHandle.Handle, wmMouseMove, 0, x | (y << 16)))
                throw new InvalidOperationException("Posting the lab pointer message failed.");
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);
            if (!postedAt.HasValue || handledAt.HasValue) return;
            handledAt = profiler.ReadElapsedMark();
            Fraction = (args.X % 120) / 120d;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            using var brush = new SKPaint { Color = SKColors.CornflowerBlue };
            args.Canvas.DrawRect(120, 120, (float)(30 + 230 * Fraction), 30, brush);
            if (postedAt is { } queued && handledAt is { } handled)
            {
                samples.Add(new(queued, handled, profiler.ReadElapsedMark()));
                postedAt = handledAt = null;
            }
        }

        [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    }
}

internal enum PacingWorkload { Idle, Tree, Animation, Resize, Input }
internal sealed record SyntheticInputSample(TimeSpan PostedAt, TimeSpan HandledAt, TimeSpan DrewAt);
internal sealed record SyntheticInputObservation(int Posted, int SkippedWhilePending, SyntheticInputSample[] Samples);
