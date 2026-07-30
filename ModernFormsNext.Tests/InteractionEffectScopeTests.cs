using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(DefaultAnimationSchedulerCollection.Name)]
public sealed class InteractionEffectScopeTests
{
    [Fact]
    public void PanelWithoutEffectsDoesNotEnterPressedStateOrStartRipple()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = new VisiblePanel
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(160, 80)
        };

        panel.RaiseMouseDown(Mouse(30, 20));

        Assert.Equal(VisualState.Normal, panel.VisualState);
        Assert.Empty(panel.InteractionEffects);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ExplicitPanelRippleRunsAndRemainsClippedToPanel()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = new VisiblePanel
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(100, 50),
            Ripple = new RippleEffect
            {
                Color = Color.FromArgb(160, 20, 80, 160),
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = Easings.Linear
            }
        };
        panel.Style.Border.Radius = 16;

        panel.RaiseMouseDown(Mouse(50, 25));
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(50));
        using SKBitmap bitmap = RenderEffect(panel);

        Assert.Equal(1, panel.Ripple.ActiveRippleCount);
        Assert.Equal(VisualState.Normal, panel.VisualState);
        Assert.NotEqual(0, bitmap.GetPixel(50, 25).Alpha);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void ChildClickRunsOnlyChildRippleAndDoesNotBubbleToParent()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = new VisiblePanel
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(200, 100),
            Ripple = CreateRipple()
        };
        var button = new Button
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Bounds = new Rectangle(20, 20, 100, 40),
            Ripple = CreateRipple()
        };
        panel.Controls.Add(button);
        int panelDown = 0;
        int buttonDown = 0;
        panel.MouseDown += (_, _) => panelDown++;
        button.MouseDown += (_, _) => buttonDown++;
        Assert.True(button.ScaledBounds.Contains(new Point(40, 35)));

        panel.RaiseMouseDown(Mouse(40, 35));

        Assert.Equal(0, panelDown);
        Assert.Equal(1, buttonDown);
        Assert.Equal(0, panel.Ripple.ActiveRippleCount);
        Assert.Equal(1, button.Ripple.ActiveRippleCount);
        Assert.Equal(VisualState.Normal, panel.VisualState);
        Assert.Equal(VisualState.Pressed, button.VisualState);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void ExplicitPressedTransitionOptsNonHoverableControlIntoVisualStateAnimation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = new Panel
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(100, 50)
        };
        panel.StylePressed.BackgroundColor = SKColors.Red;
        panel.StyleTransitions.Add(
            VisualState.Normal,
            VisualState.Pressed,
            new VisualStateTransition
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Easing = Easings.Linear
            });

        panel.RaiseMouseDown(Mouse(20, 20));

        Assert.Equal(VisualState.Pressed, panel.VisualState);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.True(harness.TickSource.IsRunning);
    }

    [Fact]
    public void DataGridViewWithoutEffectsKeepsCellHeaderAndDragInputAnimationFree()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using DataGridView grid = CreateGrid(harness);
        Rectangle first = grid.GetCellBounds(0, 0);
        Rectangle second = grid.GetCellBounds(1, 1);

        grid.RaiseMouseDown(Mouse(first.Left + 5, first.Top + 5));
        AssertGridIdle(grid, harness);
        grid.RaiseMouseUp(Mouse(first.Left + 5, first.Top + 5));

        grid.RaiseMouseDown(Mouse(first.Left + 5, 5));
        AssertGridIdle(grid, harness);
        grid.RaiseMouseUp(Mouse(first.Left + 5, 5));

        grid.RaiseMouseDown(Mouse(first.Left + 5, first.Top + 5));
        AssertGridIdle(grid, harness);
        grid.RaiseMouseMove(Mouse(second.Left + 5, second.Top + 5));
        grid.RaiseMouseUp(Mouse(second.Left + 5, second.Top + 5));
        AssertGridIdle(grid, harness);
    }

    [Fact]
    public void DataGridViewScrollbarInputDoesNotStartControlOrScrollbarEffect()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using DataGridView grid = CreateGrid(harness);
        VerticalScrollBar scrollbar = Assert.Single(
            grid.Controls.GetAllControls(true).OfType<VerticalScrollBar>());
        scrollbar.Visible = true;
        scrollbar.Bounds = new Rectangle(grid.Width - 15, 0, 15, grid.Height);
        scrollbar.AnimationSchedulerOverride = harness.Scheduler;
        Point location = new(
            scrollbar.ScaledBounds.Left + (scrollbar.ScaledBounds.Width / 2),
            scrollbar.ScaledBounds.Top + Math.Max(1, scrollbar.ScaledBounds.Height / 2));

        grid.RaiseMouseDown(Mouse(location.X, location.Y));

        Assert.Equal(VisualState.Normal, grid.VisualState);
        Assert.NotEqual(VisualState.Pressed, scrollbar.VisualState);
        Assert.Empty(grid.InteractionEffects);
        Assert.Empty(scrollbar.InteractionEffects);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RapidClicksAcrossDataGridViewCellsNeverCreateGlobalWaves()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using DataGridView grid = CreateGrid(harness);

        for (int index = 0; index < 20; index++)
        {
            Rectangle cell = grid.GetCellBounds(index % 10, index % 2);
            grid.RaiseMouseDown(Mouse(cell.Left + 4, cell.Top + 4, index));
            AssertGridIdle(grid, harness);
            grid.RaiseMouseUp(Mouse(cell.Left + 4, cell.Top + 4, index));
        }

        AssertGridIdle(grid, harness);
    }

    [Fact]
    public void EditingChildEffectDoesNotAlsoRunExplicitGridEffect()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using DataGridView grid = CreateGrid(harness);
        grid.Ripple = CreateRipple();
        grid.BeginEdit(0, 0);
        TextBox editor = Assert.Single(
            grid.Controls.GetAllControls(true).OfType<TextBox>());
        editor.AnimationSchedulerOverride = harness.Scheduler;
        editor.Ripple = CreateRipple();
        int gridDown = 0;
        int editorDown = 0;
        grid.MouseDown += (_, _) => gridDown++;
        editor.MouseDown += (_, _) => editorDown++;
        Point location = new(
            editor.ScaledBounds.Left + Math.Max(1, editor.ScaledBounds.Width / 2),
            editor.ScaledBounds.Top + Math.Max(1, editor.ScaledBounds.Height / 2));

        grid.RaiseMouseDown(Mouse(location.X, location.Y));

        Assert.Equal(0, gridDown);
        Assert.Equal(1, editorDown);
        Assert.Equal(0, grid.Ripple.ActiveRippleCount);
        Assert.Equal(1, editor.Ripple.ActiveRippleCount);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    private static DataGridView CreateGrid(AnimationSchedulerTestHarness harness)
    {
        var grid = new VisibleDataGridView
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(220, 120)
        };
        grid.Columns.Add("First", 120);
        grid.Columns.Add("Second", 120);
        for (int index = 0; index < 20; index++)
            grid.Rows.Add($"A{index}", $"B{index}");
        return grid;
    }

    private static void AssertGridIdle(DataGridView grid, AnimationSchedulerTestHarness harness)
    {
        Assert.NotEqual(VisualState.Pressed, grid.VisualState);
        Assert.Empty(grid.InteractionEffects);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    private static RippleEffect CreateRipple()
        => new()
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = Easings.Linear
        };

    private static MouseEventArgs Mouse(int x, int y, int pointerId = 0)
        => new(
            MouseButtons.Left,
            1,
            x,
            y,
            Point.Empty,
            null,
            null,
            Keys.None,
            pointerId,
            PointerDeviceKind.Mouse);

    private static SKBitmap RenderEffect(Control control)
    {
        var bitmap = new SKBitmap(control.Width, control.Height);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);
        control.RenderInteractionEffects(
            InteractionEffectLayer.AboveBackgroundBelowContent,
            new PaintEventArgs(bitmap.Info, canvas, 1d));
        canvas.Flush();
        return bitmap;
    }

    private sealed class VisiblePanel : Panel
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }

    private sealed class VisibleDataGridView : DataGridView
    {
        public override bool Visible
        {
            get => true;
            set => base.Visible = value;
        }
    }
}
