using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Layout;
using ModernFormsNext.Renderers;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class LayoutAwareVisualStateMetricTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void PaddingWithoutTransitionAppliesImmediately()
    {
        using var panel = CreatePanel();
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);

        panel.EnterForTest();

        Assert.Equal(new Padding(12), panel.PresentationPadding);
        Assert.Equal(new Padding(12), panel.StyleHover.Padding);
    }

    [Fact]
    public void PaddingInterpolatesEverySideAndEndsExactlyAtTarget()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(2, 4, 6, 8);
        panel.StyleHover.Padding = new Padding(10, 12, 14, 16);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(6, 8, 10, 12), panel.PresentationPadding);
        Assert.Equal(new Padding(10, 12, 14, 16), panel.StyleHover.Padding);

        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(10, 12, 14, 16), panel.PresentationPadding);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void RapidNormalHoverPressedRetargetsFromCurrentPresentationPadding()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        panel.StylePressed.Padding = new Padding(20);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Pressed);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Padding(8), panel.PresentationPadding);

        panel.DownForTest();
        Assert.Equal(new Padding(8), panel.PresentationPadding);
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(14), panel.PresentationPadding);
        Assert.Equal(new Padding(20), panel.StylePressed.Padding);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void LeavingHoverDuringTransitionDoesNotJump()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Normal);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        panel.LeaveForTest();

        Assert.Equal(new Padding(8), panel.PresentationPadding);
        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Padding(6), panel.PresentationPadding);
    }

    [Fact]
    public void ZeroDurationAndDisabledPolicyApplyTargetWithoutTicking()
    {
        using var zeroHarness = new AnimationSchedulerTestHarness();
        using var zeroPanel = CreatePanel(zeroHarness);
        zeroPanel.Style.Padding = new Padding(4);
        zeroPanel.StyleHover.Padding = new Padding(12);
        AddTransition(zeroPanel, VisualState.Normal, VisualState.Hover, TimeSpan.Zero);

        zeroPanel.EnterForTest();

        Assert.Equal(new Padding(12), zeroPanel.PresentationPadding);
        Assert.False(zeroHarness.TickSource.IsRunning);

        using var disabledHarness = new AnimationSchedulerTestHarness();
        disabledHarness.Policy.AnimationsEnabled = false;
        using var disabledPanel = CreatePanel(disabledHarness);
        disabledPanel.Style.Padding = new Padding(4);
        disabledPanel.StyleHover.Padding = new Padding(12);
        AddTransition(disabledPanel, VisualState.Normal, VisualState.Hover);

        disabledPanel.EnterForTest();

        Assert.Equal(new Padding(12), disabledPanel.PresentationPadding);
        Assert.False(disabledHarness.TickSource.IsRunning);
    }

    [Fact]
    public void ActiveTransitionUsesConfigurationSnapshot()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        var transition = new VisualStateTransition
        {
            Duration = Duration,
            Easing = Easings.Linear
        };
        panel.StyleTransitions.Add(VisualState.Normal, VisualState.Hover, transition);

        panel.EnterForTest();
        transition.Duration = TimeSpan.Zero;
        transition.Easing = static _ => throw new InvalidOperationException("future transitions only");
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(8), panel.PresentationPadding);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().FaultedCount);

        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Padding(12), panel.PresentationPadding);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void PaddingTransitionRearrangesDockFillChildWithoutDoubleAnimation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Size = new Size(200, 100);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        using var child = new Control
        {
            Dock = DockStyle.Fill,
            AnimationSchedulerOverride = harness.Scheduler,
            LayoutTransition = LinearLayoutTransition()
        };
        panel.Controls.Add(child);
        using var surface = new SkiaControlSurface(panel);
        panel.PerformLayout();

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Rectangle(8, 8, 184, 84), child.Bounds);
        Assert.Equal(child.Bounds, Rectangle.Round(child.PresentationBounds));
        Assert.False(child.HasActiveLayoutTransition);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Rectangle(12, 12, 176, 76), child.Bounds);
    }

    [Fact]
    public void PaddingTransitionKeepsExistingAnchorSemanticsWithoutRedundantAnimation()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Size = new Size(200, 100);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        using var child = new Control
        {
            Bounds = new Rectangle(10, 10, 100, 30),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            AnimationSchedulerOverride = harness.Scheduler,
            LayoutTransition = LinearLayoutTransition()
        };
        panel.Controls.Add(child);
        using var surface = new SkiaControlSurface(panel);
        panel.PerformLayout();
        Rectangle start = child.Bounds;

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        Rectangle middle = child.Bounds;
        Assert.Equal(AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, child.Anchor);
        Assert.Equal(new Rectangle(8, 8, 184, 84), panel.DisplayRectangle);
        harness.AdvanceAndTick(Duration / 2);
        Rectangle target = child.Bounds;

        Assert.Equal(start, middle);
        Assert.Equal(start, target);
        Assert.False(child.HasActiveLayoutTransition);
        Assert.Equal(new Padding(12), panel.PresentationPadding);
    }

    [Fact]
    public void ScrollableControlRecalculatesRangeAndPreservesOffsetFromPresentationPadding()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Size = new Size(100, 60);
        panel.AutoScroll = true;
        panel.Style.Padding = Padding.Empty;
        panel.StyleHover.Padding = new Padding(0, 0, 20, 0);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        using var child = new Control { Bounds = new Rectangle(0, 0, 120, 20) };
        panel.Controls.Add(child);
        using var surface = new SkiaControlSurface(panel);
        panel.PerformLayout();
        int startMaximum = panel.HorizontalScrollProperties.Maximum;

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        int middleMaximum = panel.HorizontalScrollProperties.Maximum;
        panel.HorizontalScrollProperties.Value = 10;

        Assert.True(startMaximum < middleMaximum);
        Assert.Equal(10, panel.TouchScrollPosition.X);
        Assert.Equal(-10, child.Left);
        Assert.Equal(90, panel.DisplayRectangle.Width);

        harness.AdvanceAndTick(Duration / 2);
        Assert.True(middleMaximum < panel.HorizontalScrollProperties.Maximum);
        Assert.Equal(10, panel.TouchScrollPosition.X);
    }

    [Fact]
    public void PaddingAndBorderThicknessUseOneCoherentProgress()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.Style.Border.Width = 2;
        panel.StyleHover.Padding = new Padding(12);
        panel.StyleHover.Border.Width = 6;
        AddTransition(panel, VisualState.Normal, VisualState.Hover);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(8), panel.PresentationPadding);
        Assert.Equal(4, panel.CurrentStyle.Border.GetWidth());
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Padding(12), panel.PresentationPadding);
        Assert.Equal(6, panel.CurrentStyle.Border.GetWidth());
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void BorderSidesInterpolateIndependently()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        SetBorderSides(panel.Style, 1, 2, 3, 4);
        SetBorderSides(panel.StyleHover, 5, 6, 7, 8);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(3, panel.CurrentStyle.Border.Left.GetWidth());
        Assert.Equal(4, panel.CurrentStyle.Border.Top.GetWidth());
        Assert.Equal(5, panel.CurrentStyle.Border.Right.GetWidth());
        Assert.Equal(6, panel.CurrentStyle.Border.Bottom.GetWidth());

        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(5, panel.CurrentStyle.Border.Left.GetWidth());
        Assert.Equal(6, panel.CurrentStyle.Border.Top.GetWidth());
        Assert.Equal(7, panel.CurrentStyle.Border.Right.GetWidth());
        Assert.Equal(8, panel.CurrentStyle.Border.Bottom.GetWidth());
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void BorderOnlyTransitionNotifiesInternalContentCaches()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new PresentationContentMetricsObserver
        {
            AnimationSchedulerOverride = harness.Scheduler
        };
        control.Style.Border.Width = 2;
        control.StyleHover.Border.Width = 6;
        AddTransition(control, VisualState.Normal, VisualState.Hover);

        control.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(1, control.PresentationContentMetricsChangeCount);
        Assert.Equal(4, control.CurrentStyle.Border.GetWidth());
    }

    [Fact]
    public void TextLayoutConsumesPresentationPaddingAndBorderThickness()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var button = new TestButton
        {
            Size = new Size(180, 60),
            AnimationSchedulerOverride = harness.Scheduler
        };
        button.Style.Padding = new Padding(4);
        button.Style.Border.Width = 2;
        button.StyleHover.Padding = new Padding(12);
        button.StyleHover.Border.Width = 6;
        AddTransition(button, VisualState.Normal, VisualState.Hover);
        Rectangle start = TextImageLayoutEngine.Layout(button).Field;

        button.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        Rectangle middle = TextImageLayoutEngine.Layout(button).Field;
        harness.AdvanceAndTick(Duration / 2);
        Rectangle target = TextImageLayoutEngine.Layout(button).Field;

        Assert.NotEqual(start, middle);
        Assert.NotEqual(middle, target);
        Assert.True(start.Left < middle.Left && middle.Left < target.Left);
    }

    [Fact]
    public void PresentationPaddingNotifiesInternalCachesWithoutRaisingPublicPaddingChanged()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new PresentationContentMetricsObserver
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Padding = new Padding(3)
        };
        control.Style.Padding = new Padding(4);
        control.StyleHover.Padding = new Padding(12);
        AddTransition(control, VisualState.Normal, VisualState.Hover);
        int publicChanges = 0;
        control.PaddingChanged += (_, _) => publicChanges++;

        control.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(1, control.PresentationContentMetricsChangeCount);
        Assert.Equal(0, publicChanges);
        Assert.Equal(new Padding(3), control.Padding);
    }

    [Fact]
    public void ScrollControlViewportAndTextCacheUsePresentationContentMetrics()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var textBox = new TestTextBox
        {
            AnimationSchedulerOverride = harness.Scheduler
        };
        textBox.Style.Padding = new Padding(2, 4, 6, 8);
        textBox.Style.Border.Width = 1;
        textBox.StyleHover.Padding = new Padding(10, 12, 14, 16);
        textBox.StyleHover.Border.Width = 5;
        AddTransition(textBox, VisualState.Normal, VisualState.Hover);
        textBox.Size = new Size(200, 60);

        textBox.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Rectangle(9, 11, 178, 34), textBox.PaddedClientRectangle);
        Assert.Equal(textBox.PaddedClientRectangle.Width, textBox.document.Width);
    }

    [Fact]
    public void ComboBoxGlyphUsesPresentationRightPadding()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var comboBox = new TestComboBox
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(100, 30)
        };
        comboBox.Style.Padding = new Padding(0, 0, 1, 0);
        comboBox.StyleHover.Padding = new Padding(0, 0, 9, 0);
        AddTransition(comboBox, VisualState.Normal, VisualState.Hover);
        var renderer = new ComboBoxRendererProbe();
        using var bitmap = new SKBitmap(100, 30);
        using var canvas = new SKCanvas(bitmap);
        var paintArgs = new PaintEventArgs(bitmap.Info, canvas, 1d);

        comboBox.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        Rectangle buttonArea = renderer.GetDropDownButtonAreaForTest(comboBox, paintArgs);

        Assert.Equal(new Rectangle(85, 0, 10, 30), buttonArea);
    }

    [Fact]
    public void GroupBoxDisplayAndPreferredSizeUsePresentationPadding()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var groupBox = new TestGroupBox
        {
            AnimationSchedulerOverride = harness.Scheduler,
            Size = new Size(200, 100),
            Text = "Metrics"
        };
        groupBox.Style.Padding = new Padding(4);
        groupBox.Style.Border.Width = 0;
        groupBox.StyleHover.Padding = new Padding(12);
        groupBox.StyleHover.Border.Width = 0;
        AddTransition(groupBox, VisualState.Normal, VisualState.Hover);
        Size startPreferred = groupBox.GetPreferredSize(Size.Empty);

        groupBox.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);
        Rectangle middleDisplay = groupBox.DisplayRectangle;
        Size middlePreferred = groupBox.GetPreferredSize(Size.Empty);
        harness.AdvanceAndTick(Duration / 2);
        Size targetPreferred = groupBox.GetPreferredSize(Size.Empty);

        Assert.Equal(8, middleDisplay.Left);
        Assert.Equal(184, middleDisplay.Width);
        Assert.True(startPreferred.Width < middlePreferred.Width);
        Assert.True(middlePreferred.Width < targetPreferred.Width);
        Assert.True(startPreferred.Height < middlePreferred.Height);
        Assert.True(middlePreferred.Height < targetPreferred.Height);
    }

    [Fact]
    public void PersistentPaddingAndTargetStylesNeverExposeIntermediateValues()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Padding = new Padding(3);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(3), panel.Padding);
        Assert.Equal(new Padding(4), panel.Style.Padding);
        Assert.Equal(new Padding(12), panel.StyleHover.Padding);
        Assert.Equal(new Padding(8), panel.PresentationPadding);
    }

    [Fact]
    public void ReentrantStateChangeDuringLayoutKeepsLatestStateAuthoritative()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        panel.StylePressed.Padding = new Padding(20);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Pressed);
        bool retargeted = false;
        panel.Layout += (_, _) =>
        {
            if (!retargeted && panel.VisualState == VisualState.Hover)
            {
                retargeted = true;
                panel.DownForTest();
            }
        };

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(VisualState.Pressed, panel.VisualState);
        Assert.Equal(new Padding(20), panel.StylePressed.Padding);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void RapidStateThrashingCompletesLatestTargetAndLeavesNoSchedulerEntry()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleFocused.Padding = new Padding(8);
        panel.StyleHover.Padding = new Padding(12);
        panel.StylePressed.Padding = new Padding(20);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Pressed);
        AddTransition(panel, VisualState.Pressed, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Focused);
        AddTransition(panel, VisualState.Focused, VisualState.Normal);
        AddTransition(panel, VisualState.Hover, VisualState.Normal);

        panel.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        Padding beforeRetarget = panel.PresentationPadding;
        panel.DownForTest();
        Assert.Equal(beforeRetarget, panel.PresentationPadding);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        beforeRetarget = panel.PresentationPadding;
        panel.UpForTest();
        Assert.Equal(beforeRetarget, panel.PresentationPadding);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        panel.FocusForTest();
        Assert.Equal(VisualState.Hover, panel.VisualState);
        beforeRetarget = panel.PresentationPadding;
        panel.LeaveForTest();
        Assert.Equal(VisualState.Focused, panel.VisualState);
        Assert.Equal(beforeRetarget, panel.PresentationPadding);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        beforeRetarget = panel.PresentationPadding;
        panel.BlurForTest();
        Assert.Equal(beforeRetarget, panel.PresentationPadding);

        Assert.Equal(1, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        harness.AdvanceAndTick(Duration);

        Assert.Equal(VisualState.Normal, panel.VisualState);
        Assert.Equal(new Padding(4), panel.PresentationPadding);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void DisabledRetargetsFromPressedAndReenablesIntoCurrentHoverState()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        panel.StylePressed.Padding = new Padding(20);
        panel.StyleDisabled.Padding = new Padding(28);
        AddTransition(panel, VisualState.Normal, VisualState.Hover);
        AddTransition(panel, VisualState.Hover, VisualState.Pressed);
        AddTransition(panel, VisualState.Pressed, VisualState.Disabled);
        AddTransition(panel, VisualState.Disabled, VisualState.Hover);

        panel.EnterForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        panel.DownForTest();
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        Padding beforeDisabled = panel.PresentationPadding;

        panel.Enabled = false;
        Assert.Equal(VisualState.Disabled, panel.VisualState);
        Assert.Equal(beforeDisabled, panel.PresentationPadding);
        harness.AdvanceAndTick(TimeSpan.FromMilliseconds(20));
        Padding beforeEnabled = panel.PresentationPadding;

        panel.Enabled = true;
        Assert.Equal(VisualState.Hover, panel.VisualState);
        Assert.Equal(beforeEnabled, panel.PresentationPadding);
        harness.AdvanceAndTick(Duration);

        Assert.Equal(new Padding(12), panel.PresentationPadding);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
    }

    [Fact]
    public void ResizingAnimatedAncestorAndChildMetricTransitionDoNotDoubleTransformDescendants()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(500, 260) };
        using var parent = new Panel
        {
            Bounds = new Rectangle(10, 20, 220, 120),
            AnimationSchedulerOverride = harness.Scheduler,
            LayoutTransition = LinearLayoutTransition()
        };
        using var child = CreatePanel(harness);
        child.Dock = DockStyle.Fill;
        child.Style.Padding = new Padding(4);
        child.StyleHover.Padding = new Padding(12);
        AddTransition(child, VisualState.Normal, VisualState.Hover);
        using var grandChild = new Control
        {
            Dock = DockStyle.Fill,
            AnimationSchedulerOverride = harness.Scheduler,
            LayoutTransition = LinearLayoutTransition()
        };
        child.Controls.Add(grandChild);
        parent.Controls.Add(child);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        root.PerformLayout();
        parent.PerformLayout();
        child.PerformLayout();

        parent.Size = new Size(320, 180);
        child.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new SizeF(270, 150), parent.PresentationBounds.Size);
        Assert.Equal(new Padding(8), child.PresentationPadding);
        Assert.False(child.HasActiveLayoutTransition);
        Assert.False(grandChild.HasActiveLayoutTransition);
        Assert.Equal(grandChild.Bounds, Rectangle.Round(grandChild.PresentationBounds));
        Assert.Equal(2, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        harness.AdvanceAndTick(Duration / 2);
        Assert.Equal(new Padding(12), child.PresentationPadding);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void FaultingEasingReleasesPresentationMetricsAndSchedulerEntry()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var panel = CreatePanel(harness);
        panel.Style.Padding = new Padding(4);
        panel.StyleHover.Padding = new Padding(12);
        panel.StyleTransitions.Add(
            VisualState.Normal,
            VisualState.Hover,
            new VisualStateTransition
            {
                Duration = Duration,
                Easing = static _ => throw new InvalidOperationException("test fault")
            });

        panel.EnterForTest();
        harness.AdvanceAndTick(Duration / 2);

        Assert.Equal(new Padding(12), panel.PresentationPadding);
        Assert.Same(panel.StyleHover, panel.CurrentStyle);
        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.Equal(1, harness.Scheduler.GetDiagnostics().FaultedCount);
    }

    [Fact]
    public void DisposeAndDetachSubtreeCancelMetricAnimations()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var root = new Panel { Size = new Size(400, 200) };
        var parent = CreatePanel(harness);
        var child = CreatePanel(harness);
        var grandChild = CreatePanel(harness);
        parent.Controls.Add(child);
        child.Controls.Add(grandChild);
        root.Controls.Add(parent);
        using var surface = new SkiaControlSurface(root);
        foreach (TestPanel item in new[] { parent, child, grandChild })
        {
            item.Style.Padding = new Padding(2);
            item.StyleHover.Padding = new Padding(10);
            AddTransition(item, VisualState.Normal, VisualState.Hover);
            item.EnterForTest();
        }

        Assert.Equal(3, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);

        root.Controls.Remove(parent);

        Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        Assert.False(harness.TickSource.IsRunning);
        Assert.Equal(new Padding(10), parent.PresentationPadding);
        Assert.Equal(new Padding(10), child.PresentationPadding);
        Assert.Equal(new Padding(10), grandChild.PresentationPadding);

        parent.Dispose();
    }

    [Fact]
    public void UserControlWithoutStateMetricsKeepsExistingPaddingSemantics()
    {
        using var userControl = new UserControl { Padding = new Padding(9) };

        Assert.Equal(userControl.Padding, userControl.PresentationPadding);
        Assert.Null(userControl.Style.Padding);
    }

    private static TestPanel CreatePanel(AnimationSchedulerTestHarness? harness = null)
        => new()
        {
            AnimationSchedulerOverride = harness?.Scheduler
        };

    private static void AddTransition(
        Control control,
        VisualState from,
        VisualState to,
        TimeSpan? duration = null)
        => control.StyleTransitions.Add(
            from,
            to,
            new VisualStateTransition
            {
                Duration = duration ?? Duration,
                Easing = Easings.Linear
            });

    private static LayoutTransition LinearLayoutTransition()
        => new()
        {
            Duration = Duration,
            Easing = Easings.Linear
        };

    private static void SetBorderSides(ControlStyle style, int left, int top, int right, int bottom)
    {
        style.Border.Left.Width = left;
        style.Border.Top.Width = top;
        style.Border.Right.Width = right;
        style.Border.Bottom.Width = bottom;
    }

    private sealed class TestPanel : Panel
    {
        public TestPanel()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));

        public void LeaveForTest()
            => OnMouseLeave(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));

        public void DownForTest()
            => RaiseMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));

        public void UpForTest()
            => RaiseMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, Point.Empty));

        public void FocusForTest()
            => OnGotFocus(EventArgs.Empty);

        public void BlurForTest()
            => OnLostFocus(EventArgs.Empty);
    }

    private sealed class TestButton : Button
    {
        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }

    private sealed class TestTextBox : TextBox
    {
        public TestTextBox()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }

    private sealed class TestComboBox : ComboBox
    {
        public TestComboBox()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }

    private sealed class ComboBoxRendererProbe : ComboBoxRenderer
    {
        public Rectangle GetDropDownButtonAreaForTest(ComboBox control, PaintEventArgs e)
            => GetDropDownButtonArea(control, e);
    }

    private sealed class TestGroupBox : GroupBox
    {
        public TestGroupBox()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));
    }

    private sealed class PresentationContentMetricsObserver : Panel
    {
        public PresentationContentMetricsObserver()
        {
            SetControlBehavior(ControlBehaviors.Hoverable);
        }

        public int PresentationContentMetricsChangeCount { get; private set; }

        public void EnterForTest()
            => OnMouseEnter(new MouseEventArgs(MouseButtons.None, 0, 0, 0, Point.Empty));

        internal override void OnPresentationContentMetricsChanged()
            => PresentationContentMetricsChangeCount++;
    }
}
