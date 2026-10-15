using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ModernFormsNext.Tests;

public sealed class ItemsControlAllocationTests(ITestOutputHelper output)
{
    [Fact]
    public void WarmedControlScrollCostDependsOnViewportRatherThan100kSourceSize()
    {
        var small = Observe(1_000);
        var large = Observe(100_000);
        output.WriteLine($"1000: {small}; 100000: {large}");
        // Control callbacks, canonical scroll displacement and layout events allocate. Compare the
        // same warmed viewport/workload, rather than borrowing the pure generator's zero-byte contract.
        Assert.True(large.Bytes <= small.Bytes * 1.25 + 16_384,
            $"100k source changed warmed viewport allocation cost: small={small.Bytes}, large={large.Bytes}");
        Assert.True(large.Created <= 16);
        Assert.True(large.Reused > 5_000);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Observation Observe(int count)
    {
        using var view = new Probe { Size = new(200, 200), ItemSize = new(100, 20) };
        var source = new object[count];
        Array.Fill(source, "same borrowed value");
        view.SetSource(source);
        var scroll = view.VerticalScrollProperties;
        for (int step = 0; step < 500; step++) scroll.Value = step % 400 * 20;
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int step = 0; step < 5_000; step++) scroll.Value = step % 400 * 20;
        long elapsed = Stopwatch.GetTimestamp() - started;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        var snapshot = view.GetVirtualizationSnapshot();
        Assert.InRange(view.Controls.Count, 1, 16);
        Assert.Equal(snapshot.RealizedContainerCount, view.Controls.Count);
        return new(bytes, snapshot.CreatedContainerCount, snapshot.ReusedContainerCount,
            TimeSpan.FromSeconds((double)elapsed / Stopwatch.Frequency), retainedAfter - retainedBefore);
    }

    private readonly record struct Observation(long Bytes, long Created, long Reused, TimeSpan Duration, long ProcessRetainedDelta);

    private sealed class Probe : ItemsControl
    {
        internal void SetSource(IList source) => SetItemsSource(source);
        public override bool Visible { get => true; set => base.Visible = value; }
    }
}
