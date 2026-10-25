using System.Drawing;
using System.ComponentModel;
using SkiaSharp;
using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

/// <summary>Exercises policy ownership with the real scheduler and a deterministic clock.</summary>
public sealed class InheritedLayoutTransitionTests
{
    [Fact]
    public void ScopeIsOptInAndDoesNotMaterializeLocalValues()
    {
        using var f = new Fixture();
        f.Policy.ApplyToDescendants = false;
        f.Child.Left = 110;
        Assert.False(f.Child.HasActiveLayoutTransition);

        f.Policy.ApplyToDescendants = true;
        Assert.False(f.Child.HasActiveLayoutTransition); // No retroactive transition.
        f.Child.Left = 210;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Null(f.Child.LayoutTransition);
        Assert.Equal(160f, f.Child.PresentationBounds.X);
        Assert.Equal(1, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalDisabledConfigurationCanOptOutOneControlOrItsBranch(bool branch)
    {
        using var f = new Fixture();
        f.Parent.LayoutTransition = new LayoutTransition { Enabled = false, ApplyToDescendants = branch };
        f.Parent.Left = 30;
        f.Child.Left = 110;

        Assert.False(f.Parent.HasActiveLayoutTransition);
        Assert.Equal(!branch, f.Child.HasActiveLayoutTransition);
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(branch ? 110f : 60f, f.Child.PresentationBounds.X);
    }

    [Fact]
    public void NearestScopeWinsWhileLocalValuesOverrideIt()
    {
        using var f = new Fixture();
        f.Parent.LayoutTransition = Policy(200);
        f.Child.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(35f, f.Child.PresentationBounds.X);

        RectangleF current = f.Child.PresentationBounds;
        f.Child.LayoutTransition = Policy(100);
        Assert.Equal(current, f.Child.PresentationBounds);
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(72.5f, f.Child.PresentationBounds.X);

        f.Child.LayoutTransition = null;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(91.25f, f.Child.PresentationBounds.X);
    }

    [Fact]
    public void SharedConfigurationNotificationsRetargetOnceAndFinishOnTime()
    {
        using var f = new Fixture();
        f.Parent.LayoutTransition = f.Policy;
        f.Child.Left = 210;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        Assert.Equal(60f, f.Child.PresentationBounds.X);

        f.Policy.Duration = TimeSpan.FromMilliseconds(200);
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(135f, f.Child.PresentationBounds.X);
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void RemovingScopeSnapsInheritedRunsButPreservesLocalRuns()
    {
        using var f = new Fixture();
        f.Parent.LayoutTransition = Policy(100, scope: false);
        f.Parent.Left = 60;
        f.Child.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        f.Policy.ApplyToDescendants = false;

        Assert.True(f.Parent.HasActiveLayoutTransition);
        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(110f, f.Child.PresentationBounds.X);
        Assert.Equal(1, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ReparentUsesNewPolicyAndDetachRetiresTheWholeSubtree()
    {
        using var f = new Fixture();
        using var other = new Panel { Bounds = new Rectangle(300, 0, 200, 200),
            LayoutTransition = Policy(200), AnimationSchedulerOverride = f.Clock.Scheduler };
        f.Root.Controls.Add(other);
        f.Child.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        other.Controls.Add(f.Parent);
        Assert.False(f.Child.HasActiveLayoutTransition);
        f.Child.Left = 210;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(160f, f.Child.PresentationBounds.X);

        other.Controls.Remove(f.Parent);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
        f.Child.Left = 310;
        Assert.False(f.Child.HasActiveLayoutTransition);
    }

    [Fact]
    public void ParentResizeSuppressesRedundantDockEasingAndTicksNeverRunLayout()
    {
        using var f = new Fixture();
        f.Child.Dock = DockStyle.Fill;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        int layouts = 0;
        f.Root.Layout += (_, _) => layouts++;
        f.Parent.Layout += (_, _) => layouts++;
        f.Parent.Size = new Size(240, 180);
        Assert.True(f.Parent.HasActiveLayoutTransition);
        Assert.False(f.Child.HasActiveLayoutTransition);
        int committedLayouts = layouts;

        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(committedLayouts, layouts);
        Assert.Equal(f.Parent.ClientSize, f.Child.Size);
    }

    [Theory]
    [InlineData(0, 30, 40, 30)]
    [InlineData(40, 0, 40, 30)]
    [InlineData(40, 30, 0, 30)]
    [InlineData(40, 30, 40, 0)]
    public void ZeroAreaSourceOrTargetSnapsWithoutSchedulerDemand(int width, int height, int targetWidth, int targetHeight)
    {
        using var f = new Fixture();
        f.Child.LayoutTransition = new LayoutTransition { Enabled = false };
        f.Child.Size = new Size(width, height);
        f.Child.LayoutTransition = null;
        f.Child.Bounds = new Rectangle(110, 20, targetWidth, targetHeight);

        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(f.Child.Bounds, Rectangle.Round(f.Child.PresentationBounds));
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(f.Clock.TickSource.IsRunning);
    }

    [Fact]
    public void RetargetToZeroRetiresOnlyTheLayoutEntry()
    {
        using var f = new Fixture();
        f.Child.Left = 110;
        f.Parent.Left = 60;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));

        f.Child.Width = 0;

        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.False(f.Child.PresentationContains(new Point(110, 20)));
        Assert.True(f.Parent.HasActiveLayoutTransition);
        Assert.Equal(1, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void EasingThatChangesPolicyCannotPublishItsRetiredFrame()
    {
        using var f = new Fixture();
        bool changed = false;
        f.Policy.Easing = progress =>
        {
            if (!changed)
            {
                changed = true;
                f.Child.LayoutTransition = new LayoutTransition { Enabled = false };
            }
            return progress;
        };
        f.Child.Left = 110;

        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));

        Assert.True(changed);
        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(110f, f.Child.PresentationBounds.X);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void DisposingScopeRetiresAllInheritedOwnersAndUnsubscribesConfiguration()
    {
        using var f = new Fixture();
        f.Parent.Left = 60;
        f.Child.Left = 110;
        Assert.Equal(2, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);

        f.Parent.Dispose();
        f.Policy.Enabled = false;
        f.Policy.Duration = TimeSpan.FromMilliseconds(200);

        Assert.True(f.Child.IsDisposed);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(f.Clock.TickSource.IsRunning);
    }

    private static LayoutTransition Policy(int milliseconds, bool scope = true)
        => new() { Duration = TimeSpan.FromMilliseconds(milliseconds), Easing = Easings.Linear,
            ApplyToDescendants = scope };

    [Fact]
    public void SharedPolicyFailureStillRetiresOtherBranches()
    {
        using var f = new Fixture();
        using var other = new Panel { Bounds = new Rectangle(300, 0, 200, 200),
            AnimationSchedulerOverride = f.Clock.Scheduler, LayoutTransition = f.Policy };
        using var second = new Control { Bounds = new Rectangle(10, 10, 40, 30),
            AnimationSchedulerOverride = f.Clock.Scheduler };
        other.Controls.Add(second);
        f.Root.Controls.Add(other);
        f.Child.Left = 110;
        second.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        EventHandler<EventArgs<Rectangle>> reject = (_, _) => throw new InvalidOperationException("Expected invalidation failure.");
        f.Parent.Invalidated += reject;
        try
        {
            Assert.Throws<InvalidOperationException>(() => f.Policy.Enabled = false);
            Assert.False(f.Child.HasActiveLayoutTransition);
            Assert.False(second.HasActiveLayoutTransition);
            Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
        }
        finally { f.Parent.Invalidated -= reject; }
    }

    [Fact]
    public void ReparentFromInvalidationKeepsTheNewRoutePolicy()
    {
        using var f = new Fixture();
        using var other = new Panel { Bounds = new Rectangle(300, 0, 200, 200),
            AnimationSchedulerOverride = f.Clock.Scheduler, LayoutTransition = Policy(200) };
        f.Root.Controls.Add(other);
        f.Child.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(25));
        bool moved = false;
        f.Parent.Invalidated += (_, _) =>
        {
            if (moved) return;
            moved = true;
            other.Controls.Add(f.Child);
        };
        f.Policy.Enabled = false;
        Assert.Same(other, f.Child.Parent);
        f.Child.Left = 210;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(160f, f.Child.PresentationBounds.X);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReducedMotionAndZeroDurationLeaveNoInheritedTickDemand(bool reducedMotion)
    {
        using var f = new Fixture();
        if (reducedMotion) f.Clock.Policy.ReducedMotion = true;
        else f.Policy.Duration = TimeSpan.Zero;
        f.Child.Left = 110;
        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(110f, f.Child.PresentationBounds.X);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(f.Clock.TickSource.IsRunning);
    }

    [Fact]
    public void InheritedHitTestingTracksPresentationAndRejectsTheLogicalTargetUntilArrival()
    {
        using var f = new Fixture();
        f.Child.Left = 110;
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.True(f.Child.PresentationContains(new Point(65, 25)));
        Assert.False(f.Child.PresentationContains(new Point(115, 25)));
        f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.True(f.Child.PresentationContains(new Point(115, 25)));
        Assert.False(f.Child.PresentationContains(new Point(65, 25)));
    }

    [Fact]
    public void DesignModeAncestorPreventsInheritedAndLocalMotion()
    {
        using var f = new Fixture();
        f.Root.Site = new DesignSite(f.Root);
        f.Child.Left = 110;
        Assert.False(f.Child.HasActiveLayoutTransition);
        f.Child.LayoutTransition = Policy(100);
        f.Child.Left = 210;
        Assert.False(f.Child.HasActiveLayoutTransition);
        Assert.Equal(0, f.Clock.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void WarmInheritedFramesDoNotAddAllocationsComparedWithTheLocalPolicyPath()
    {
        long local = MeasureFrames(inherited: false);
        long inherited = MeasureFrames(inherited: true);
        Assert.Equal(local, inherited);
    }

    private static long MeasureFrames(bool inherited)
    {
        using var f = new Fixture();
        f.Policy.Duration = TimeSpan.FromSeconds(10);
        if (!inherited) f.Child.LayoutTransition = f.Policy;
        f.Child.Left = 110;
        for (int i = 0; i < 100; i++) f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) f.Clock.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class DesignSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void NestedInheritedMotionPreservesParentClippingAndZeroTargetErasesOldPixels()
    {
        using var clock = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(180, 100), BackColor = SKColors.White };
        using var parent = new Panel { Bounds = new Rectangle(10, 10, 100, 60), BackColor = SKColors.White,
            AnimationSchedulerOverride = clock.Scheduler };
        using var child = new Panel { Bounds = new Rectangle(20, 10, 40, 20), BackColor = SKColors.Blue,
            AnimationSchedulerOverride = clock.Scheduler };
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        surface.Resize(180, 100);
        root.LayoutTransition = Policy(100);
        using var image = new SKBitmap(180, 100);
        using var canvas = new SKCanvas(image);
        surface.Render(canvas);
        Assert.Equal(SKColors.Blue, image.GetPixel(40, 25));

        parent.Left = 30;
        child.Left = 140;
        clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        surface.Render(canvas);
        Assert.Equal(SKColors.Blue, image.GetPixel(110, 25));
        Assert.Equal(SKColors.White, image.GetPixel(130, 25));
        Assert.Equal(SKColors.White, image.GetPixel(40, 25));

        child.Width = 0;
        surface.Render(canvas);
        Assert.Equal(SKColors.White, image.GetPixel(110, 25));
        Assert.False(child.HasActiveLayoutTransition);
    }

    [Fact]
    public void InheritedAnchorWidthUsesTheComputedLogicalTargetWithoutFrameLayout()
    {
        using var clock = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(400, 200) };
        // Disable only the host's own resize motion, allowing its child to inherit the outer policy.
        using var host = new Panel { Size = new Size(200, 100), LayoutTransition = new LayoutTransition { Enabled = false } };
        using var child = new Control { Bounds = new Rectangle(10, 10, 100, 30),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            AnimationSchedulerOverride = clock.Scheduler };
        host.Controls.Add(child);
        root.Controls.Add(host);
        using var surface = new SkiaControlSurface(root);
        root.LayoutTransition = Policy(100);
        host.Width = 300;
        Assert.Equal(200, child.Width);
        Assert.Equal(100f, child.PresentationBounds.Width);
        int layouts = 0;
        host.Layout += (_, _) => layouts++;
        clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(150f, child.PresentationBounds.Width);
        Assert.Equal(0, layouts);
    }

    [Fact]
    public void NestedFlowAutoSizeCommitsPreferredSizeAndSiblingPositionBeforeInterpolation()
    {
        using var clock = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(400, 200) };
        using var flow = new FlowLayoutPanel { Bounds = new Rectangle(10, 10, 300, 150), WrapContents = false };
        using var child = new PreferredControl { AutoSize = true, Margin = Padding.Empty,
            AnimationSchedulerOverride = clock.Scheduler };
        using var sibling = new Control { Size = new Size(40, 30), Margin = Padding.Empty,
            AnimationSchedulerOverride = clock.Scheduler };
        flow.Controls.AddRange(child, sibling);
        root.Controls.Add(flow);
        using var surface = new SkiaControlSurface(root);
        flow.PerformLayout();
        Assert.Equal(new Size(40, 30), child.Size);
        Assert.Equal(40, sibling.Left);
        root.LayoutTransition = Policy(100);

        child.Preferred = new Size(120, 45);
        flow.PerformLayout();
        Assert.Equal(new Size(120, 45), child.Size);
        Assert.Equal(120, sibling.Left);
        Assert.Equal(40f, child.PresentationBounds.Width);
        Assert.Equal(40f, sibling.PresentationBounds.X);
        clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(80f, child.PresentationBounds.Width);
        Assert.Equal(80f, sibling.PresentationBounds.X);
    }

    private sealed class PreferredControl : Control
    {
        public Size Preferred { get; set; } = new(40, 30);
        public override Size GetPreferredSize(Size proposedSize) => Preferred;
    }

    [Fact]
    public void PolicyCancellationRestoresCanonicalIntegerCompositionAtFractionalDpi()
    {
        using var clock = new AnimationSchedulerTestHarness();
        var native = DispatchProxy.Create<IWindowBaseImpl, DpiWindowProxy>();
        using var window = new DpiWindow(native);
        using var root = new Panel { Bounds = new Rectangle(0, 0, 320, 200) };
        using var child = new Control { Bounds = new Rectangle(10, 20, 100, 40), AnimationSchedulerOverride = clock.Scheduler };
        root.Controls.Add(child);
        window.Controls.Add(root);
        var policy = Policy(100);
        root.LayoutTransition = policy;
        child.Left = 220;
        Assert.True(child.HasActiveLayoutTransition);
        clock.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        policy.Enabled = false;
        Assert.False(child.HasActiveLayoutTransition);
        Assert.Equal(root.PointToScreen(Point.Empty).X + child.ScaledLeft, child.PointToScreen(Point.Empty).X);
        Assert.NotEqual(root.PointToScreen(Point.Empty).X + root.LogicalToDeviceUnits(child.Left), child.PointToScreen(Point.Empty).X);
        Assert.Equal(child.ScaledWidth, child.AccessibilityObject.Bounds.Width);
        window.adapter.Dispose();
    }

    private sealed class DpiWindow(IWindowBaseImpl implementation) : WindowBase(implementation);

    public class DpiWindowProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            const double scale = 2.625;
            if (targetMethod?.Name is "get_RenderScaling" or "get_DesktopScaling") return scale;
            if (targetMethod?.Name == "PointToScreen" && args?[0] is WindowKit.Point point)
                return new WindowKit.PixelPoint((int)Math.Round(point.X * scale), (int)Math.Round(point.Y * scale));
            Type result = targetMethod?.ReturnType ?? typeof(void);
            return result == typeof(void) || !result.IsValueType ? null : Activator.CreateInstance(result);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public AnimationSchedulerTestHarness Clock { get; } = new();
        public Panel Root { get; }
        public Panel Parent { get; }
        public Control Child { get; }
        public LayoutTransition Policy { get; } = InheritedLayoutTransitionTests.Policy(100);
        private readonly SkiaControlSurface surface;

        public Fixture()
        {
            Root = new Panel { Size = new Size(600, 300), AnimationSchedulerOverride = Clock.Scheduler };
            Parent = new Panel { Bounds = new Rectangle(10, 10, 200, 150), AnimationSchedulerOverride = Clock.Scheduler };
            Child = new Control { Bounds = new Rectangle(10, 20, 40, 30), AnimationSchedulerOverride = Clock.Scheduler };
            Parent.Controls.Add(Child);
            Root.Controls.Add(Parent);
            surface = new SkiaControlSurface(Root);
            Root.LayoutTransition = Policy;
        }

        public void Dispose()
        {
            surface.Dispose();
            Root.Dispose();
            Clock.Dispose();
        }
    }
}
