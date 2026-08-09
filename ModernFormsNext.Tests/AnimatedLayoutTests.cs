using System.Drawing;
using System.Reflection;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AnimatedLayoutTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void BoundsChangeWithoutTransitionIsImmediate()
    {
        using var root = new Panel { Size = new Size(500, 300) };
        using var child = new Control { Bounds = new Rectangle(10, 20, 40, 30) };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);

        child.Bounds = new Rectangle(100, 80, 120, 90);

        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
    }

    [Fact]
    public void TransitionStartsAtPreviousPresentationBounds()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        Rectangle previous = fixture.Child.Bounds;

        fixture.Child.Bounds = new Rectangle(100, 80, 120, 90);

        Assert.Equal(new RectangleF(previous.X, previous.Y, previous.Width, previous.Height), fixture.Child.PresentationBounds);
        Assert.True(fixture.Child.HasActiveLayoutTransition);
    }

    [Fact]
    public void TransitionFinishesExactlyAtLogicalTarget()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        var target = new Rectangle(101, 83, 127, 91);

        fixture.Child.Bounds = target;
        harness.AdvanceAndTick(Duration);

        Assert.Equal(target, fixture.Child.Bounds);
        Assert.Equal(target, Rectangle.Round(fixture.Child.PresentationBounds));
        Assert.False(fixture.Child.HasActiveLayoutTransition);
    }

    [Fact]
    public void TransitionInterpolatesX()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Bounds = new Rectangle(110, 20, 40, 30);
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(60f, fixture.Child.PresentationBounds.X);
    }

    [Fact]
    public void TransitionInterpolatesY()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Bounds = new Rectangle(10, 120, 40, 30);
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(70f, fixture.Child.PresentationBounds.Y);
    }

    [Fact]
    public void TransitionInterpolatesWidth()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Bounds = new Rectangle(10, 20, 140, 30);
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(90f, fixture.Child.PresentationBounds.Width);
    }

    [Fact]
    public void TransitionInterpolatesHeight()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Bounds = new Rectangle(10, 20, 40, 130);
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(80f, fixture.Child.PresentationBounds.Height);
    }

    [Fact]
    public void RetargetStartsAtCurrentPresentationBounds()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Left = 110;
        harness.AdvanceAndTick(Duration / 2);
        RectangleF current = fixture.Child.PresentationBounds;

        fixture.Child.Left = 310;

        Assert.Equal(current, fixture.Child.PresentationBounds);
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(185f, fixture.Child.PresentationBounds.X);
    }

    [Fact]
    public void RepeatedRetargetsStartAtEachCurrentPresentationBounds()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Left = 110;
        harness.AdvanceAndTick(Duration / 4);
        RectangleF presentationTowardB = fixture.Child.PresentationBounds;

        fixture.Child.Left = 210;
        Assert.Equal(presentationTowardB, fixture.Child.PresentationBounds);
        harness.AdvanceAndTick(Duration / 4);
        RectangleF presentationTowardC = fixture.Child.PresentationBounds;

        fixture.Child.Left = 410;
        Assert.Equal(presentationTowardC, fixture.Child.PresentationBounds);
        harness.AdvanceAndTick(Duration);

        Assert.Equal(fixture.Child.Bounds, Rectangle.Round(fixture.Child.PresentationBounds));
        Assert.False(fixture.Child.HasActiveLayoutTransition);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveDurationAppliesTargetWithoutStartingTicker(int milliseconds)
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness, TimeSpan.FromMilliseconds(milliseconds));
        var target = new Rectangle(100, 80, 120, 90);

        fixture.Child.Bounds = target;

        Assert.Equal(target, Rectangle.Round(fixture.Child.PresentationBounds));
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void DisablingTransitionSnapsToTargetAndUnregisters()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Child.Left = 210;
        harness.AdvanceAndTick(Duration / 4);

        fixture.Transition.Enabled = false;

        Assert.Equal(fixture.Child.Bounds, Rectangle.Round(fixture.Child.PresentationBounds));
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void CompletedTransitionStopsSharedScheduler()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);

        fixture.Child.Left = 210;
        harness.AdvanceAndTick(Duration);

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1, harness.TickSource.StopTransitions);
    }

    [Fact]
    public void FaultedEasingReleasesPresentationStateAndStopsScheduler()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Transition.Easing = _ => throw new InvalidOperationException("Expected test fault.");
        fixture.Child.Left = 210;

        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(fixture.Child.Bounds, Rectangle.Round(fixture.Child.PresentationBounds));
        Assert.False(fixture.Child.HasActiveLayoutTransition);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().FaultedCount);
    }

    [Fact]
    public void DisposedControlDoesNotRemainScheduled()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var parent = new Panel { Bounds = new Rectangle(10, 20, 200, 120) };
        using var child = new Panel { Bounds = new Rectangle(15, 25, 80, 60) };
        using var grandChild = new Control { Bounds = new Rectangle(5, 10, 40, 30) };
        child.Controls.Add(grandChild);
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        ConfigureTransition(parent, harness);
        ConfigureTransition(child, harness);
        ConfigureTransition(grandChild, harness);
        parent.Left = 210;
        child.Top = 75;
        grandChild.Left = 35;

        Assert.Equal(3, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        parent.Dispose();

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(parent.Bounds, Rectangle.Round(parent.PresentationBounds));
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
        Assert.Equal(grandChild.Bounds, Rectangle.Round(grandChild.PresentationBounds));
    }

    [Fact]
    public void RemovedControlDoesNotRemainScheduled()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Child.Left = 210;

        fixture.Root.Controls.Remove(fixture.Child);

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(fixture.Child.Bounds, Rectangle.Round(fixture.Child.PresentationBounds));
    }

    [Fact]
    public void RemovingSubtreeCancelsNestedLayoutTransitions()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var parent = new Panel { Bounds = new Rectangle(10, 20, 200, 120) };
        using var child = new Panel { Bounds = new Rectangle(15, 25, 80, 60) };
        using var grandChild = new Control { Bounds = new Rectangle(5, 10, 40, 30) };
        child.Controls.Add(grandChild);
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        ConfigureTransition(parent, harness);
        ConfigureTransition(child, harness);
        ConfigureTransition(grandChild, harness);
        parent.Left = 210;
        child.Top = 75;
        grandChild.Left = 35;

        Assert.Equal(3, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        root.Controls.Remove(parent);

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(parent.Bounds, Rectangle.Round(parent.PresentationBounds));
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
        Assert.Equal(grandChild.Bounds, Rectangle.Round(grandChild.PresentationBounds));
    }

    [Fact]
    public void MultipleControlsShareOneSchedulerTickSource()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var first = CreateAnimatedControl(harness, new Rectangle(10, 20, 40, 30));
        using var second = CreateAnimatedControl(harness, new Rectangle(10, 80, 40, 30));
        root.Controls.Add(first);
        root.Controls.Add(second);
        using var surface = new SkiaControlSurface(root);

        first.Left = 210;
        second.Left = 310;

        Assert.Equal(2, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1, harness.TickSource.StartTransitions);
    }

    [Fact]
    public void DockLayoutCommitsTargetBeforeAnimatingPresentation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(200, 100) };
        using var child = new Control { Size = new Size(50, 30), Dock = DockStyle.Right };
        root.Controls.Add(child);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(200, 100);
        child.AnimationSchedulerOverride = harness.Scheduler;
        child.LayoutTransition = LinearTransition();
        Rectangle oldPresentation = Rectangle.Round(child.PresentationBounds);

        surface.Resize(300, 100);

        Assert.Equal(250, child.Left);
        Assert.Equal(oldPresentation, Rectangle.Round(child.PresentationBounds));
        harness.AdvanceAndTick(Duration);
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
    }

    [Fact]
    public void AnchorLayoutAnimatesComputedWidthWithoutChangingAnchorSemantics()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Control { Size = new Size(200, 100) };
        using var child = new Control
        {
            Bounds = new Rectangle(10, 10, 100, 30)
        };
        root.Controls.Add(child);
        child.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        using var surface = new SkiaControlSurface(root);
        surface.Resize(200, 100);
        child.AnimationSchedulerOverride = harness.Scheduler;
        child.LayoutTransition = LinearTransition();

        surface.Resize(300, 100);

        Assert.Equal(200, child.Width);
        Assert.Equal(100f, child.PresentationBounds.Width);
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(150f, child.PresentationBounds.Width);
    }

    [Fact]
    public void ConstraintsApplyToLogicalTargetBeforeTransition()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Child.MinimumSize = new Size(50, 40);
        fixture.Child.MaximumSize = new Size(120, 100);

        fixture.Child.SetBounds(10, 20, 500, 1);

        Assert.Equal(new Size(120, 40), fixture.Child.Size);
        harness.AdvanceAndTick(Duration);
        Assert.Equal(new SizeF(120, 40), fixture.Child.PresentationBounds.Size);
    }

    [Fact]
    public void MovingAnimatedParentDoesNotScheduleUnchangedChild()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var parent = CreateAnimatedControl(harness, new Rectangle(10, 20, 200, 120));
        using var child = CreateAnimatedControl(harness, new Rectangle(15, 25, 40, 30));
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        Rectangle childPresentation = Rectangle.Round(child.PresentationBounds);

        parent.Location = new Point(210, 120);

        Assert.True(parent.HasActiveLayoutTransition);
        Assert.False(child.HasActiveLayoutTransition);
        Assert.Equal(childPresentation, Rectangle.Round(child.PresentationBounds));
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ResizingAnimatedParentDoesNotDoubleAnimateDockedDescendants()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var parent = new Panel { Bounds = new Rectangle(10, 20, 200, 120) };
        using var child = new Panel { Dock = DockStyle.Fill };
        using var grandChild = new Control { Dock = DockStyle.Fill };
        child.Controls.Add(grandChild);
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(500, 300);
        parent.PerformLayout();
        child.PerformLayout();

        foreach (Control control in new Control[] { parent, child, grandChild })
        {
            control.AnimationSchedulerOverride = harness.Scheduler;
            control.LayoutTransition = LinearTransition();
        }

        parent.Size = new Size(300, 200);

        Assert.True(parent.HasActiveLayoutTransition);
        Assert.False(child.HasActiveLayoutTransition);
        Assert.False(grandChild.HasActiveLayoutTransition);
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
        Assert.Equal(grandChild.Bounds, Rectangle.Round(grandChild.PresentationBounds));
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void HitTestingUsesPresentationPositionDuringTransition()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        int clicks = 0;
        fixture.Child.Click += (_, _) => clicks++;
        fixture.Child.Left = 110;
        harness.AdvanceAndTick(Duration / 2);

        fixture.Surface.ProcessPointer(1, ControlSurfacePointerAction.Down, 65, 25);
        fixture.Surface.ProcessPointer(1, ControlSurfacePointerAction.Up, 65, 25);
        fixture.Surface.ProcessPointer(2, ControlSurfacePointerAction.Down, 115, 25);
        fixture.Surface.ProcessPointer(2, ControlSurfacePointerAction.Up, 115, 25);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void PresentationHitTestingMapsScaledWidthBackToLogicalClientCoordinates()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        Point received = Point.Empty;
        fixture.Child.MouseDown += (_, e) => received = e.Location;
        fixture.Child.Width = 80;
        harness.AdvanceAndTick(Duration / 2);

        fixture.Surface.ProcessPointer(3, ControlSurfacePointerAction.Down, 40, 25);

        Assert.Equal(40, received.X);
    }

    [Fact]
    public void AccessibilityBoundsIncludeAncestorPresentationScaling()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 300) };
        using var parent = new Panel { Bounds = new Rectangle(10, 20, 100, 80) };
        using var child = new Control { Dock = DockStyle.Fill };
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(500, 300);
        parent.PerformLayout();
        ConfigureTransition(parent, harness);
        ConfigureTransition(child, harness);

        parent.Size = new Size(200, 160);

        Assert.Equal(new Size(200, 160), child.Size);
        Assert.Equal(new Rectangle(10, 20, 100, 80), child.AccessibilityObject.Bounds);
    }

    [Fact]
    public void CompletionHasNoAccumulatedRoundingDrift()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        var target = new Rectangle(103, 87, 131, 97);
        fixture.Child.Bounds = target;

        for (int i = 0; i < 10; i++)
            harness.AdvanceAndTick(TimeSpan.FromMilliseconds(10));

        Assert.Equal(target, Rectangle.Round(fixture.Child.PresentationBounds));
    }

    [Fact]
    public void RapidRetargetingNeverJumpsBeforeNextFrame()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Child.Left = 110;
        harness.AdvanceAndTick(Duration / 4);
        RectangleF beforeRetargets = fixture.Child.PresentationBounds;

        fixture.Child.Left = 210;
        Assert.Equal(beforeRetargets, fixture.Child.PresentationBounds);
        fixture.Child.Left = 310;
        Assert.Equal(beforeRetargets, fixture.Child.PresentationBounds);
        fixture.Child.Left = 410;

        Assert.Equal(beforeRetargets, fixture.Child.PresentationBounds);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FlowLayoutUsesOneComputedChildRectangleAsTransitionTarget()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new FlowLayoutPanel { Size = new Size(400, 100), WrapContents = false };
        using var first = new Control { Size = new Size(50, 30), Margin = Padding.Empty };
        using var second = new Control { Size = new Size(50, 30), Margin = Padding.Empty };
        root.Controls.Add(first);
        root.Controls.Add(second);
        using var surface = new SkiaControlSurface(root);
        root.PerformLayout();
        second.AnimationSchedulerOverride = harness.Scheduler;
        second.LayoutTransition = LinearTransition();
        int oldLeft = second.Left;

        first.Width = 100;

        Assert.True(second.Left > oldLeft);
        Assert.Equal(oldLeft, Rectangle.Round(second.PresentationBounds).Left);
    }

    [Fact]
    public void TableLayoutUsesOneComputedChildRectangleAsTransitionTarget()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new TableLayoutPanel { Size = new Size(200, 100), ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        using var first = new Control { Dock = DockStyle.Fill, Margin = Padding.Empty };
        using var second = new Control { Dock = DockStyle.Fill, Margin = Padding.Empty };
        root.Controls.Add(first, 0, 0);
        root.Controls.Add(second, 1, 0);
        using var surface = new SkiaControlSurface(root);
        root.PerformLayout();
        second.AnimationSchedulerOverride = harness.Scheduler;
        second.LayoutTransition = LinearTransition();
        Rectangle oldPresentation = Rectangle.Round(second.PresentationBounds);

        root.Width = 300;

        Assert.NotEqual(oldPresentation, second.Bounds);
        Assert.Equal(oldPresentation, Rectangle.Round(second.PresentationBounds));
    }

    [Fact]
    public void WindowHostedControlsWithoutTransitionsKeepImmediateFormPathSemantics()
    {
        using var window = new TestWindow();
        using var child = new Control { Bounds = new Rectangle(10, 20, 40, 30) };
        window.Controls.Add(child);
        var target = new Rectangle(100, 80, 120, 90);

        child.Bounds = target;

        Assert.Equal(target, child.Bounds);
        Assert.Equal(target, Rectangle.Round(child.PresentationBounds));
    }

    [Fact]
    public void ClosingWindowCancelsNestedLayoutTransitions()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var window = new TestWindow();
        using var parent = new Panel { Bounds = new Rectangle(10, 20, 200, 120) };
        using var child = new Panel { Bounds = new Rectangle(15, 25, 80, 60) };
        using var grandChild = new Control { Bounds = new Rectangle(5, 10, 40, 30) };
        child.Controls.Add(grandChild);
        parent.Controls.Add(child);
        window.Controls.Add(parent);
        ConfigureTransition(parent, harness);
        ConfigureTransition(child, harness);
        ConfigureTransition(grandChild, harness);
        parent.Left = 210;
        child.Top = 75;
        grandChild.Left = 35;

        Assert.Equal(3, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        window.Close();

        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(parent.Bounds, Rectangle.Round(parent.PresentationBounds));
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
        Assert.Equal(grandChild.Bounds, Rectangle.Round(grandChild.PresentationBounds));
    }

    [Fact]
    public void UserControlWithoutTransitionKeepsImmediateBoundsSemantics()
    {
        using var userControl = new UserControl { Size = new Size(300, 200) };
        using var child = new Control { Bounds = new Rectangle(10, 20, 40, 30) };
        userControl.Controls.Add(child);
        using var surface = new SkiaControlSurface(userControl);
        var target = new Rectangle(100, 80, 120, 90);

        child.Bounds = target;

        Assert.Equal(target, child.Bounds);
        Assert.Equal(target, Rectangle.Round(child.PresentationBounds));
    }

    [Fact]
    public void HidingControlCancelsActiveLayoutTransition()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var fixture = new AnimatedFixture(harness);
        fixture.Child.Left = 210;

        fixture.Child.Visible = false;

        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(fixture.Child.Bounds, Rectangle.Round(fixture.Child.PresentationBounds));
    }

    private static Control CreateAnimatedControl(AnimationSchedulerTestHarness harness, Rectangle bounds)
        => new()
        {
            Bounds = bounds,
            AnimationSchedulerOverride = harness.Scheduler,
            LayoutTransition = LinearTransition()
        };

    private static void ConfigureTransition(Control control, AnimationSchedulerTestHarness harness)
    {
        control.AnimationSchedulerOverride = harness.Scheduler;
        control.LayoutTransition = LinearTransition();
    }

    private static LayoutTransition LinearTransition(TimeSpan? duration = null)
        => new()
        {
            Duration = duration ?? Duration,
            Easing = Easings.Linear
        };

    private sealed class AnimatedFixture : IDisposable
    {
        public AnimatedFixture(AnimationSchedulerTestHarness harness, TimeSpan? duration = null)
        {
            Root = new Panel { Size = new Size(500, 300) };
            Child = new Control { Bounds = new Rectangle(10, 20, 40, 30) };
            Root.Controls.Add(Child);
            Surface = new SkiaControlSurface(Root);
            Child.AnimationSchedulerOverride = harness.Scheduler;
            Transition = LinearTransition(duration);
            Child.LayoutTransition = Transition;
        }

        public Panel Root { get; }
        public Control Child { get; }
        public SkiaControlSurface Surface { get; }
        public LayoutTransition Transition { get; }

        public void Dispose()
        {
            Surface.Dispose();
            Root.Dispose();
        }
    }

    private sealed class TestWindow : WindowBase
    {
        public TestWindow()
            : base(DispatchProxy.Create<IWindowBaseImpl, NoopWindowProxy>())
        {
        }
    }

    private class NoopWindowProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Type? returnType = targetMethod?.ReturnType;
            if (returnType is null || returnType == typeof(void))
                return null;
            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
