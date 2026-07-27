using System.Drawing;
using System.Numerics;
using ModernFormsNext.Animations;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class BrushAnimationTests
{
    [Fact]
    public void SolidBrushAnimationMutatesObservableStateInPlace()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var brush = new SolidColorBrush(Color.Red)
        {
            Opacity = 0.2f,
            Transform = Matrix3x2.Identity
        };
        var target = new SolidColorBrush(Color.Blue)
        {
            Opacity = 0.8f,
            Transform = Matrix3x2.CreateTranslation(20f, 10f)
        };
        int changes = 0;
        brush.Changed += (_, _) => changes++;

        AnimationHandle handle = brush.AnimateTo(
            target,
            TimeSpan.FromMilliseconds(100),
            scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(AnimationState.Running, handle.State);
        Assert.Equal(0.5f, brush.Opacity, 3);
        Assert.Equal(Matrix3x2.CreateTranslation(10f, 5f), brush.Transform);
        Assert.Equal(Color.FromArgb(128, 0, 128).ToArgb(), brush.PaintColor.ToArgb());
        Assert.True(changes >= 3);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(Color.Blue.ToArgb(), brush.PaintColor.ToArgb());
    }

    [Fact]
    public void GradientStopAnimationClampsOvershootingOffsetsAndPropagatesChanges()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var stop = new GradientStop(Color.Black, 0.25f);
        var target = new GradientStop(Color.White, 0.75f);
        var brush = new LinearGradientBrush();
        brush.GradientStops.Add(stop);
        int brushChanges = 0;
        brush.Changed += (_, _) => brushChanges++;

        stop.AnimateTo(
            target,
            TimeSpan.FromMilliseconds(100),
            easing: _ => 2f,
            scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1f, stop.Offset);
        Assert.Equal(Color.White.ToArgb(), stop.PaintColor.ToArgb());
        Assert.True(brushChanges >= 2);
    }

    [Fact]
    public void AnimatedResourceBrushInvalidatesRenderingWithoutRequestingLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var brush = new SolidColorBrush(Color.Red);
        var target = new SolidColorBrush(Color.Blue);
        using var control = new InvalidationProbeControl { BackgroundBrush = brush };
        using var surface = new SkiaControlSurface(control);
        control.ResetCounters();

        brush.AnimateTo(target, TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.True(control.Invalidations > 0);
        Assert.Equal(0, control.LayoutPasses);
    }

    [Fact]
    public void CancellationCausesNoFurtherBrushMutationOrRepaint()
    {
        using var harness = new AnimationSchedulerTestHarness();
        var brush = new SolidColorBrush(Color.Red);
        using var control = new InvalidationProbeControl { BackgroundBrush = brush };
        using var surface = new SkiaControlSurface(control);
        AnimationHandle handle = brush.AnimateTo(
            new SolidColorBrush(Color.Blue),
            TimeSpan.FromMilliseconds(100),
            scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        Color canceledColor = brush.PaintColor;
        control.ResetCounters();

        handle.Cancel();
        harness.AdvanceAndTick(TimeSpan.FromSeconds(1));

        Assert.Equal(canceledColor.ToArgb(), brush.PaintColor.ToArgb());
        Assert.Equal(0, control.Invalidations);
        Assert.Equal(AnimationState.Canceled, handle.State);
    }

    [Fact]
    public void AnimatingBoundsUsesLayoutInvalidationWhileRenderTransformDoesNot()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var parent = new InvalidationProbeControl();
        using var child = new Control { Width = 20, Height = 20 };
        parent.Controls.Add(child);
        using var surface = new SkiaControlSurface(parent);
        parent.ResetCounters();

        harness.Scheduler.Animate(
            child,
            "Width",
            child.Width,
            80,
            AnimationInterpolators.Int32,
            value => child.Width = value,
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) });
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.True(parent.LayoutPasses > 0);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        int layoutPasses = parent.LayoutPasses;

        harness.Scheduler.Animate(
            child,
            "TranslationX",
            child.TranslationX,
            40f,
            AnimationInterpolators.Float,
            value => child.TranslationX = value,
            new AnimationOptions { Duration = TimeSpan.FromMilliseconds(100) });
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        Assert.Equal(layoutPasses, parent.LayoutPasses);
    }

    [Fact]
    public void ReplacingDynamicResourceDetachesControlFromStillAnimatingOldBrush()
    {
        using var harness = new AnimationSchedulerTestHarness();
        object key = $"BrushAnimationTests.{Guid.NewGuid():N}";
        var oldBrush = new SolidColorBrush(Color.Red);
        var target = new SolidColorBrush(Color.Blue);
        var replacement = new SolidColorBrush(Color.Green);
        using var control = new InvalidationProbeControl();
        using var surface = new SkiaControlSurface(control);
        control.Resources[key] = oldBrush;
        control.SetResourceReference(nameof(Control.BackgroundBrush), key);
        oldBrush.AnimateTo(target, TimeSpan.FromMilliseconds(100), scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        control.Resources[key] = replacement;
        control.ResetCounters();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        Assert.Same(replacement, control.BackgroundBrush);
        Assert.Equal(Color.Green.ToArgb(), replacement.PaintColor.ToArgb());
        Assert.Equal(0, control.Invalidations);
        Assert.NotEqual(Color.Red.ToArgb(), oldBrush.PaintColor.ToArgb());
    }

    [Fact]
    public void LinearGradientAnimationUpdatesGeometryStopsAndSpreadAtCompletion()
    {
        using var harness = new AnimationSchedulerTestHarness();
        LinearGradientBrush brush = Linear(Color.Red, Color.Blue, new PointF(0f, 0f), new PointF(1f, 0f));
        LinearGradientBrush target = Linear(Color.Green, Color.White, new PointF(0.2f, 0.4f), new PointF(0.8f, 1f));
        target.GradientStops[0].Offset = 0.2f;
        target.GradientStops[1].Offset = 0.8f;
        target.SpreadMode = GradientSpreadMode.Repeat;

        AnimationHandle handle = brush.AnimateTo(
            target,
            TimeSpan.FromMilliseconds(100),
            scheduler: harness.Scheduler);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(new PointF(0.1f, 0.2f), brush.Start);
        Assert.Equal(new PointF(0.9f, 0.5f), brush.End);
        Assert.Equal(GradientSpreadMode.Pad, brush.SpreadMode);
        Assert.Equal(0.9f, brush.GradientStops[1].Offset, 3);

        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(AnimationState.Completed, handle.State);
        Assert.Equal(GradientSpreadMode.Repeat, brush.SpreadMode);
        Assert.Equal(target.GradientStops[1].PaintColor.ToArgb(), brush.GradientStops[1].PaintColor.ToArgb());
    }

    private static LinearGradientBrush Linear(Color first, Color second, PointF start, PointF end)
    {
        var brush = new LinearGradientBrush { Start = start, End = end };
        brush.GradientStops.AddRange([
            new GradientStop(first, 0f),
            new GradientStop(second, 1f)
        ]);
        return brush;
    }

    private sealed class InvalidationProbeControl : Control
    {
        public int Invalidations { get; private set; }

        public int LayoutPasses { get; private set; }

        public void ResetCounters()
        {
            Invalidations = 0;
            LayoutPasses = 0;
        }

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            LayoutPasses++;
            base.OnLayout(e);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DefaultAnimationSchedulerCollection
{
    public const string Name = "Default animation scheduler";
}

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class ControlAnimationLifecycleTests
{
    [Fact]
    public async Task LegacyAsyncHelperStillClampsNegativeDurationAndNullCancellationIsANoOp()
    {
        using var control = new Control();

        Task animation = control.FadeToAsync(0.5f, duration: -10);
        ((Control)null!).CancelAnimations();
        control.CancelAnimations();

        await animation;
    }

    [Fact]
    public async Task DisposingControlCancelsItsOwnedDefaultAnimation()
    {
        var control = new Control();
        AnimationHandle handle = AnimationScheduler.Default.Start(
            control,
            "Dispose",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromHours(1) });

        control.Dispose();

        Assert.Equal(AnimationState.Canceled, handle.State);
        Assert.Equal(AnimationState.Canceled, await handle.Completion);
    }

    [Fact]
    public async Task DetachingControlCancelsItsOwnedDefaultAnimation()
    {
        using var parent = new Control();
        using var child = new Control();
        parent.Controls.Add(child);
        AnimationHandle handle = AnimationScheduler.Default.Start(
            child,
            "Detach",
            _ => { },
            new AnimationOptions { Duration = TimeSpan.FromHours(1) });

        parent.Controls.Remove(child);

        Assert.Equal(AnimationState.Canceled, handle.State);
        Assert.Equal(AnimationState.Canceled, await handle.Completion);
    }
}
