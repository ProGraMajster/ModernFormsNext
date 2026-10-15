using System.Collections.Specialized;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VirtualizationMetricMutationTests
{
    [Fact]
    public void FixedSeedIndexedMutationsRetainExactOccurrenceMeasurements()
    {
        var random = new Random(55_202);
        var expected = Enumerable.Range(0, 100).Select(i => (double)(i + 1)).ToList();
        var metrics = new VirtualizingItemMetrics(100, 20);
        for (int i = 0; i < expected.Count; i++) metrics.SetMeasuredExtent(i, expected[i]);
        for (int step = 0; step < 1_000; step++)
        {
            int before = expected.Count;
            int index = random.Next(expected.Count);
            NotifyCollectionChangedEventArgs notification;
            switch (step % 4)
            {
                case 0:
                    notification = new(NotifyCollectionChangedAction.Add, (object)"new", index);
                    expected.Insert(index, 20); break;
                case 1:
                    notification = new(NotifyCollectionChangedAction.Remove, (object)"old", index);
                    expected.RemoveAt(index); break;
                case 2:
                    int target = random.Next(expected.Count);
                    notification = new(NotifyCollectionChangedAction.Move, (object)"move", target, index);
                    double moved = expected[index]; expected.RemoveAt(index); expected.Insert(target, moved); break;
                default:
                    notification = new(NotifyCollectionChangedAction.Replace, (object)"new", (object)"old", index);
                    expected[index] = 20; break;
            }
            metrics.ApplyChange(ItemIndexChange.FromNotification(notification, before, expected.Count));
            Assert.Equal(expected.Count, metrics.Count);
            Assert.Equal(expected.Sum(), metrics.TotalExtent);
            double prefix = 0;
            for (int i = 0; i < expected.Count; i++)
            { Assert.Equal(prefix, metrics.GetOffset(i)); Assert.Equal(expected[i], metrics.GetExtent(i)); prefix += expected[i]; }
        }
    }

    [Fact]
    public void AppendsWithinReservedCapacityAllocateNothingAndPreserveMeasurements()
    {
        var metrics = new VirtualizingItemMetrics(100_000, 20);
        metrics.SetMeasuredExtent(123, 42);
        var added = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (object)"new", 100_000);
        var change = ItemIndexChange.FromNotification(added, 100_000, 100_001);
        long before = GC.GetAllocatedBytesForCurrentThread();
        metrics.ApplyChange(change);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(42, metrics.GetExtent(123));
        Assert.Equal(1, metrics.MeasuredCount);
        Assert.Equal(2_000_042, metrics.TotalExtent);
    }
}
