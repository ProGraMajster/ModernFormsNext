using System.Diagnostics;
using ModernFormsNext.Animations;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.Tests;

[Collection(FontResolutionMetricsCollection.Name)]
public sealed partial class ExpandedVisualStateMetricTests
{
    private readonly ITestOutputHelper output;
    public ExpandedVisualStateMetricTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void WarmResolvedFontFlagsDoNotAllocatePerPresentationQuery()
    {
        var style = new ControlStyle(null) { Font = Theme.UIFont };
        for (int i = 0; i < 1000; i++) _ = style.GetFontStyle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) _ = style.GetFontStyle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"FONT_FLAGS queries=1000 allocatedBytes={allocated}");
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void WarmRadiusPlannerFramesAllocateNothingAndNeverRequestTextLayout()
    {
        using var harness = new AnimationSchedulerTestHarness();
        using var control = CreatePanel(harness);
        control.Style.Border.Radius = 0;
        control.StyleHover.Border.Radius = 1000;
        AddTransition(control, VisualState.Normal, VisualState.Hover, TimeSpan.FromSeconds(4));
        control.Enter();
        for (int i = 0; i < 1000; i++) harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) harness.AdvanceAndTick(TimeSpan.FromMilliseconds(1));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(500, control.CurrentStyle.Border.GetRadius());
        Assert.Equal(0, control.MetricNotifications);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void BoundedTextReflowHasOneMetricNotificationPerFrameAndNoTypefaceSelection()
    {
        const int frames = 64;
        // A predeclared linear budget for <=512 UTF-16 units, including normal accessibility
        // notifications and the existing shaping engine; this is not allocation-free painting.
        const long maximumBytesPerFrame = 256 * 1024;
        using var harness = new AnimationSchedulerTestHarness();
        using var control = new MetricTextBox { Width = 240, MultiLine = true,
            Text = string.Join(" ", Enumerable.Repeat("Zażółć spacing and wrapping", 12)),
            AnimationSchedulerOverride = harness.Scheduler };
        Assert.InRange(control.Text.Length, 1, 512);
        control.Style.FontSize = 12;
        control.Style.LineHeight = 1;
        control.Style.LetterSpacing = 0;
        control.StyleHover.FontSize = 20;
        control.StyleHover.LineHeight = 1.5;
        control.StyleHover.LetterSpacing = 1;
        TimeSpan run = TimeSpan.FromSeconds(1);
        TimeSpan step = run / frames;
        AddTransition(control, VisualState.Normal, VisualState.Hover, run);
        control.Enter();
        for (int i = 0; i < frames; i++) { harness.AdvanceAndTick(step); _ = control.document.GetTextBlock(); }
        control.Leave();
        control.Enter();
        int notifications = control.MetricNotifications;
        FontResolutionDiagnostics.ResetCounters();
        FontResolutionDiagnostics.SetEnabled(true);
        try
        {
            long started = Stopwatch.GetTimestamp();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < frames; i++) { harness.AdvanceAndTick(step); _ = control.document.GetTextBlock(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double microseconds = Stopwatch.GetElapsedTime(started).TotalMicroseconds / frames;
            var font = FontResolutionDiagnostics.Snapshot();
            int changed = control.MetricNotifications - notifications;
            output.WriteLine($"REFLOW textUnits={control.Text.Length} frames={frames} allocatedBytes={allocated} bytesPerFrame={allocated / frames} microsecondsPerFrame={microseconds:F2} metricNotifications={changed} typefaceSelections={font.FromFamilyNameCalls}");
            Assert.Equal(frames, changed);
            Assert.Equal(0, font.FromFamilyNameCalls);
            Assert.InRange(allocated, 1, frames * maximumBytesPerFrame);
            Assert.Equal(0, harness.Scheduler.GetDiagnostics().ActiveAnimationCount);
        }
        finally { FontResolutionDiagnostics.SetEnabled(false); }
    }
}
