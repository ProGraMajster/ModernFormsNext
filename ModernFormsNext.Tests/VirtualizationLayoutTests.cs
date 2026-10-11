using System.Drawing;
using ModernFormsNext.Layout;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VirtualizationLayoutTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(int.MaxValue, 1)]
    public void RangeRejectsInvalidAndOverflowingBounds(int start, int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RealizedRange(start, count));

    [Fact]
    public void RangeUsesHalfOpenOccurrenceIndices()
    {
        var range = new RealizedRange(7, 2);
        Assert.False(range.Contains(6)); Assert.True(range.Contains(7)); Assert.True(range.Contains(8)); Assert.False(range.Contains(9));
        Assert.Equal(9, range.EndIndex);
        Assert.False(RealizedRange.Empty.Contains(0));
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void StackPlansBounded100kRangeAndUnrealizedItemBounds(Orientation orientation)
    {
        var layout = new VirtualizingStackLayout(orientation);
        var viewport = orientation == Orientation.Vertical ? new RectangleF(0, 1_000_000, 200, 200) : new RectangleF(1_000_000, 0, 200, 200);
        var context = new VirtualizationContext(100_000, viewport, new(20, 20), 3);
        Assert.Equal(new RealizedRange(49_997, 16), layout.GetRealizedRange(in context));
        var bounds = layout.GetItemBounds(in context, 99_999);
        Assert.Equal(1_999_980, orientation == Orientation.Vertical ? bounds.Y : bounds.X);
        Assert.Equal(2_000_000, orientation == Orientation.Vertical ? layout.GetExtent(in context).Height : layout.GetExtent(in context).Width);
    }

    [Theory]
    [InlineData(0, 20, 0, 1)]
    [InlineData(20, 20, 1, 1)]
    [InlineData(19, 2, 0, 2)]
    [InlineData(-20, 20, 0, 0)]
    [InlineData(-5, 10, 0, 1)]
    [InlineData(200, 20, 0, 0)]
    public void StackUsesHalfOpenViewportAndClipsOutsideContent(float offset, float length, int start, int count)
    {
        var context = new VirtualizationContext(10, new(0, offset, 100, length), new(100, 20), 0);
        Assert.Equal(new RealizedRange(start, count), new VirtualizingStackLayout().GetRealizedRange(in context));
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(1, 0, 100)]
    [InlineData(1, 100, 0)]
    public void EmptySourceOrZeroAreaRealizesNothing(int count, float width, float height)
    {
        var context = new VirtualizationContext(count, new(0, 0, width, height), new(20, 20));
        Assert.Equal(RealizedRange.Empty, new VirtualizingStackLayout().GetRealizedRange(in context));
        Assert.Equal(RealizedRange.Empty, new VirtualizingWrapLayout().GetRealizedRange(in context));
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void WrapPlansRowsAndLastPartialGroupWithoutEnumeration(Orientation orientation)
    {
        var viewport = orientation == Orientation.Vertical ? new RectangleF(0, 20, 60, 20) : new RectangleF(20, 0, 20, 60);
        var context = new VirtualizationContext(100_001, viewport, new(20, 20), 0);
        var layout = new VirtualizingWrapLayout(orientation);
        Assert.Equal(new RealizedRange(3, 3), layout.GetRealizedRange(in context));
        var bounds = layout.GetItemBounds(in context, 100_000);
        Assert.Equal(666_660, orientation == Orientation.Vertical ? bounds.Y : bounds.X);
        Assert.Equal(20, orientation == Orientation.Vertical ? bounds.X : bounds.Y);
        var extent = layout.GetExtent(in context);
        Assert.Equal(666_680, orientation == Orientation.Vertical ? extent.Height : extent.Width);
    }

    [Fact]
    public void WrapReflowsOnCrossAxisResizeAndRejectsVariableMetrics()
    {
        var narrow = new VirtualizationContext(100, new(0, 20, 60, 20), new(20, 20), 0);
        var wide = new VirtualizationContext(100, new(0, 20, 100, 20), new(20, 20), 0);
        var layout = new VirtualizingWrapLayout();
        Assert.Equal(new RealizedRange(3, 3), layout.GetRealizedRange(in narrow));
        Assert.Equal(new RealizedRange(5, 5), layout.GetRealizedRange(in wide));
        var variable = new VirtualizationContext(100, new(0, 0, 100, 100), new(20, 20), metrics: new(100, 20));
        Assert.Throws<NotSupportedException>(() => layout.GetExtent(in variable));
    }

    [Fact]
    public void VariableStackUsesMeasurementsAndCorrectBoundaryOffsets()
    {
        var metrics = new VirtualizingItemMetrics(10, 20);
        Assert.True(metrics.SetMeasuredExtent(0, 40));
        Assert.True(metrics.SetMeasuredExtent(1, 10));
        Assert.False(metrics.SetMeasuredExtent(1, 10));
        var context = new VirtualizationContext(10, new(0, 40, 100, 10), new(100, 20), 0, metrics);
        var layout = new VirtualizingStackLayout();
        Assert.Equal(new RealizedRange(1, 1), layout.GetRealizedRange(in context));
        Assert.Equal(new RectangleF(0, 50, 100, 20), layout.GetItemBounds(in context, 2));
        Assert.Equal(210, metrics.TotalExtent);
        Assert.Equal(2, metrics.MeasuredCount);
        Assert.Equal(1, metrics.FindIndex(40));
        Assert.Equal(2, metrics.FindIndex(50));
        metrics.Reset(10, 30);
        Assert.Equal(0, metrics.MeasuredCount); Assert.Equal(300, metrics.TotalExtent);
    }

    [Fact]
    public void MeasurementsMatchNaivePrefixAndLookupForSeededMutations()
    {
        var random = new Random(55);
        var expected = Enumerable.Repeat(20d, 1_024).ToArray();
        var metrics = new VirtualizingItemMetrics(expected.Length, 20);
        for (int pass = 0; pass < 1_000; pass++)
        {
            int index = random.Next(expected.Length);
            expected[index] = random.Next(1, 100);
            metrics.SetMeasuredExtent(index, expected[index]);
            int boundary = random.Next(expected.Length + 1);
            double offset = expected.Take(boundary).Sum();
            Assert.Equal(offset, metrics.GetOffset(boundary));
            Assert.Equal(Math.Min(boundary, expected.Length - 1), metrics.FindIndex(offset));
        }
    }

    [Fact]
    public void LargeEstimateCorrectionsRecoverSmallExtentsAndOverflowRollsBack()
    {
        var metrics = new VirtualizingItemMetrics(2, 1e200);
        metrics.SetMeasuredExtent(0, 20);
        metrics.SetMeasuredExtent(1, 20);
        Assert.Equal(40, metrics.TotalExtent);
        Assert.Equal(20, metrics.GetOffset(1));
        Assert.Equal(1, metrics.FindIndex(20));
        metrics.SetMeasuredExtent(0, double.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => metrics.SetMeasuredExtent(1, double.MaxValue));
        Assert.Equal(20, metrics.GetExtent(1));
        metrics.SetMeasuredExtent(0, 20);
        Assert.Equal(40, metrics.TotalExtent);
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void ViewportOutsideCrossAxisDoesNotRealizeContainers(Orientation orientation)
    {
        var viewport = orientation == Orientation.Vertical ? new RectangleF(100, 0, 100, 100) : new RectangleF(0, 100, 100, 100);
        var context = new VirtualizationContext(10, viewport, new(20, 20));
        Assert.Equal(RealizedRange.Empty, new VirtualizingStackLayout(orientation).GetRealizedRange(in context));
        Assert.Equal(RealizedRange.Empty, new VirtualizingWrapLayout(orientation).GetRealizedRange(in context));
    }

    [Fact]
    public void MetricQueriesAndUpdatesOn100kItemsAllocateNothingAfterWarmup()
    {
        var metrics = new VirtualizingItemMetrics(100_000, 20);
        for (int i = 0; i < 2_000; i++) { metrics.SetMeasuredExtent(i, 25); metrics.FindIndex(i * 20); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) { metrics.SetMeasuredExtent(i % 2_000, 25); metrics.FindIndex(i * 20); metrics.GetOffset(i); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(100_000, metrics.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidMeasurementsDoNotMutateState(double value)
    {
        var metrics = new VirtualizingItemMetrics(2, 20);
        Assert.Throws<ArgumentOutOfRangeException>(() => metrics.SetMeasuredExtent(0, value));
        Assert.Equal(40, metrics.TotalExtent); Assert.Equal(0, metrics.MeasuredCount);
    }

    [Fact]
    public void LayoutUsesCanonicalLayoutEngineHostPath()
    {
        using var host = new LayoutHost();
        int before = host.Arrangements;
        host.PerformLayout();
        Assert.True(host.Arrangements > before);
        Assert.IsAssignableFrom<LayoutEngine>(host.LayoutEngine);
        using var unsupported = new Panel();
        Assert.Throws<NotSupportedException>(() => new VirtualizingStackLayout().Layout(unsupported, new(host, null)));
    }

    [Fact]
    public void InvalidContextsAndGeometryOverflowFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualizationContext(-1, new(0, 0, 1, 1), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualizationContext(1, new(float.NaN, 0, 1, 1), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualizationContext(1, new(0, 0, -1, 1), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualizationContext(1, new(0, 0, 1, 1), new(0, 1)));
        Assert.Throws<ArgumentException>(() => new VirtualizationContext(1, new(0, 0, 1, 1), new(1, 1), metrics: new(2, 1)));
        var tooLarge = new VirtualizationContext(10, new(0, 0, 1, 1), new(1, float.MaxValue));
        Assert.Throws<OverflowException>(() => new VirtualizingStackLayout().GetExtent(in tooLarge));
        var invalidDefault = default(VirtualizationContext);
        Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualizingStackLayout().GetExtent(in invalidDefault));
    }

    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void FiniteItemCoordinatesCannotProduceInfiniteFarEdges(Orientation orientation)
    {
        var cell = orientation == Orientation.Vertical ? new SizeF(1, float.MaxValue) : new SizeF(float.MaxValue, 1);
        var context = new VirtualizationContext(2, new(0, 0, 1, 1), cell);
        Assert.Throws<OverflowException>(() => new VirtualizingStackLayout(orientation).GetItemBounds(in context, 1));
        Assert.Throws<OverflowException>(() => new VirtualizingWrapLayout(orientation).GetItemBounds(in context, 1));
    }

    private sealed class LayoutHost : Panel, IVirtualizingLayoutHost
    {
        private readonly VirtualizingStackLayout layout = new();
        public int Arrangements;
        public override LayoutEngine LayoutEngine => layout ?? base.LayoutEngine;
        public VirtualizationContext GetVirtualizationContext() => new(10, new(0, 0, 100, 100), new(100, 20));
        public bool ArrangeVirtualizedItems(VirtualizingLayout strategy, in VirtualizationContext context)
        {
            Arrangements++;
            Assert.Equal(new RealizedRange(0, 7), strategy.GetRealizedRange(in context));
            return false;
        }
    }
}
