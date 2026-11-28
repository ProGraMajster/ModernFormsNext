using ModernFormsNext.Rendering;
using ModernFormsNext.WindowKit;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class FrameRequestPacerTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void BurstKeepsOneWakeAndRetainsLatestDamage(double hz)
    {
        TimeSpan now = TimeSpan.Zero;
        List<TimeSpan?> wakes = [];
        List<Rect> signals = [];
        using var gate = new FrameRequestPacer(() => now, wakes.Add, signals.Add);
        gate.SetRefreshRate(hz);
        TimeSpan budget = TimeSpan.FromTicks((long)Math.Ceiling(TimeSpan.TicksPerSecond / hz));
        gate.Request(new Rect(0, 0, 4, 4));
        gate.Request(new Rect(8, 8, 2, 2));
        gate.Request(new Rect(20, 10, 2, 3));
        Assert.Equal(budget, Assert.Single(wakes));
        Assert.Single(signals);

        now = budget - TimeSpan.FromTicks(1);
        gate.Wake();
        Assert.Single(signals);
        Assert.Equal(TimeSpan.FromTicks(1), wakes[^1]);

        now += TimeSpan.FromMilliseconds(300);
        gate.Wake();
        Assert.Equal(new Rect(8, 8, 14, 5), signals[1]);
        Assert.Equal(new FrameRequestCounts(3, 2, 1, 0, 2), gate.Counts);
        gate.Wake();
        Assert.Equal(2, signals.Count);
    }

    [Fact]
    public void FasterDisplayRetimesPendingDamageAndMissingRateFallsBackImmediately()
    {
        TimeSpan now = TimeSpan.Zero;
        List<TimeSpan?> wakes = [];
        List<Rect> signals = [];
        using var gate = new FrameRequestPacer(() => now, wakes.Add, signals.Add);
        gate.SetRefreshRate(60);
        gate.Request(new Rect(1, 1, 2, 2));
        now = TimeSpan.FromMilliseconds(4);
        gate.Request(new Rect(2, 2, 2, 2));

        gate.SetRefreshRate(144);
        Assert.InRange(wakes[^1]!.Value.TotalMilliseconds, 2.94, 2.95);
        gate.SetRefreshRate(null);
        Assert.Null(wakes[^1]);
        Assert.Equal(2, signals.Count);
        gate.Wake();
        Assert.Equal(2, signals.Count);
    }

    [Fact]
    public void ResumeAndResizeNeverReplayRetiredDamage()
    {
        TimeSpan now = TimeSpan.Zero;
        List<TimeSpan?> wakes = [];
        List<Rect> signals = [];
        using var gate = new FrameRequestPacer(() => now, wakes.Add, signals.Add);
        gate.SetRefreshRate(60);
        var oldBacking = new Rect(40, 44, 20, 20);
        gate.Request(oldBacking);
        gate.Request(oldBacking);
        Assert.True(gate.RetireDamage());
        var fullNewBacking = new Rect(0, 0, 15, 18);
        gate.Request(fullNewBacking);
        Assert.Equal(fullNewBacking, signals[^1]);
        gate.Request(oldBacking);
        gate.SetActive(false);
        now += TimeSpan.FromSeconds(2);
        gate.Wake();
        gate.Request(oldBacking);
        Assert.Equal(2, signals.Count);
        Assert.Null(wakes[^1]);
        gate.SetActive(true);
        gate.Request(fullNewBacking);
        gate.Request(fullNewBacking);
        gate.Dispose();
        gate.Wake();
        Assert.Equal(3, signals.Count);
        Assert.Null(wakes[^1]);
    }

    [Fact]
    public void ReentrantWorkBelongsToTheNextBudget()
    {
        TimeSpan now = TimeSpan.Zero;
        List<Rect> signals = [];
        FrameRequestPacer? gateForCallback = null;
        using var gate = new FrameRequestPacer(() => now, _ => { }, Invalidate);
        gateForCallback = gate;
        gate.SetRefreshRate(120);
        gate.Request(new Rect(0, 0, 2, 2));
        Assert.Single(signals);
        now += TimeSpan.FromMilliseconds(9);
        gate.Wake();
        Assert.Equal(2, signals.Count);

        void Invalidate(Rect damage)
        {
            signals.Add(damage);
            if (signals.Count == 1)
                gateForCallback!.Request(new Rect(1, 0, 2, 2));
        }
    }

    [Fact]
    public void HeadlessHostUsesCurrentGeometryAndCancelsWakeOnClose()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form();
        using var display = host.Show(form);
        TimeSpan? wake = null;
        var gate = form.AttachFrameRequestPacer(() => host.Clock.CurrentTime, value => wake = value);
        gate.SetRefreshRate(60);
        form.Invalidate(new System.Drawing.Rectangle(1, 1, 10, 10));
        form.Invalidate(new System.Drawing.Rectangle(22, 22, 8, 8));
        Assert.NotNull(wake);

        display.Resize(420, 240);
        Assert.True(gate.Counts.Retired > 0);
        form.Invalidate();
        form.Invalidate();
        long retiredBeforeDpi = gate.Counts.Retired;
        display.SetRenderScale(2.25);
        Assert.True(gate.Counts.Retired > retiredBeforeDpi);
        Assert.Equal(2.25, form.Scaling);
        form.Invalidate();

        form.Close();
        Assert.Null(wake);
        var completed = gate.Counts;
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        gate.Wake();
        Assert.Equal(completed, gate.Counts);
    }
}
