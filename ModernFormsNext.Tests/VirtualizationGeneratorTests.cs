using System.Collections.Specialized;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VirtualizationGeneratorTests
{
    [Fact]
    public void LongScrollAndJumpKeep100kLogicalSourceBoundedAndReuseContainers()
    {
        var factory = new Factory(100_000);
        using var generator = new ItemContainerGenerator<Container>(factory, 32);
        generator.Realize(new(0, 20), factory.Source.Count);
        for (int i = 0; i < 10_000; i++)
        {
            int start = (i * 7_919) % 99_980;
            generator.Realize(new(start, 20), factory.Source.Count);
            Assert.Equal(20, generator.RealizedContainerCount);
            Assert.True(generator.RecycledContainerCount <= 32);
            Assert.Equal(20, generator.CreatedContainerCount);
            Assert.True(generator.TryGetContainer(start, out var container));
            Assert.Equal(factory.Source[start], container!.Value);
        }
        Assert.True(generator.ReusedContainerCount > 100_000);
        Assert.Equal(0, generator.DiscardedContainerCount);
    }

    [Fact]
    public void WarmedNormalScrollingAndStableRequestsAllocateNothing()
    {
        var factory = new Factory(100_000);
        using var generator = new ItemContainerGenerator<Container>(factory, 32);
        for (int i = 0; i < 2_000; i++) generator.Realize(new(i, 20), factory.Source.Count);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2_000; i < 12_000; i++) generator.Realize(new(i, 20), factory.Source.Count);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        int prepared = factory.Prepared;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) generator.Realize(new(11_999, 20), factory.Source.Count);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(prepared, factory.Prepared);
    }

    [Fact]
    public void PoolCapacityAndZeroCapacityDiscardDeterministically()
    {
        var factory = new Factory(100);
        var generator = new ItemContainerGenerator<Container>(factory, 2);
        generator.Realize(new(0, 10), 100);
        generator.Realize(RealizedRange.Empty, 100);
        Assert.Equal(2, generator.RecycledContainerCount);
        Assert.Equal(8, generator.DiscardedContainerCount);
        generator.Dispose(); generator.Dispose();
        Assert.Equal(10, generator.DiscardedContainerCount);
        Assert.All(factory.Created, c => Assert.Equal(1, c.Discards));
        Assert.All(factory.Created, c => Assert.Equal(-1, c.Value));
        Assert.Throws<ObjectDisposedException>(() => generator.Realize(new(0, 1), 100));
    }

    [Fact]
    public void InsertRemoveMoveReplaceAndResetPreserveOccurrenceBindings()
    {
        var factory = new Factory(30);
        using var generator = new ItemContainerGenerator<Container>(factory);
        generator.Realize(new(5, 10), 30);
        Assert.True(generator.TryGetContainer(8, out var selected));
        factory.Source.Insert(3, 99);
        var inserted = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Add, 99, 3), 31);
        Assert.Equal(9, inserted.MapIndex(8));
        Assert.True(generator.TryGetContainer(9, out var retained)); Assert.Same(selected, retained);
        int movedValue = factory.Source[9]; factory.Source.RemoveAt(9); factory.Source.Insert(12, movedValue);
        var moved = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Move, movedValue, 12, 9), 31);
        Assert.Equal(12, moved.MapIndex(9));
        Assert.True(generator.TryGetContainer(12, out retained)); Assert.Same(selected, retained);
        int removedValue = factory.Source[2]; factory.Source.RemoveAt(2);
        var removed = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Remove, removedValue, 2), 30);
        Assert.Equal(11, removed.MapIndex(12));
        Assert.True(generator.TryGetContainer(11, out retained)); Assert.Same(selected, retained);
        factory.Source[11] = 101;
        var replaced = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Replace, (object)101, (object)movedValue, 11), 30);
        Assert.Equal(-1, replaced.MapIndex(11));
        VerifyBindings(generator, factory);
        factory.Source.Clear();
        var reset = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Reset), 0);
        Assert.Equal(-1, reset.MapIndex(11)); Assert.Equal(0, generator.RealizedContainerCount);
    }

    [Fact]
    public void ReentrantNotificationsRetainOrderAndNewRangeWins()
    {
        var factory = new Factory(20);
        using var generator = new ItemContainerGenerator<Container>(factory);
        bool once = false;
        factory.OnPrepare = index =>
        {
            if (once) return;
            once = true;
            factory.Source.Insert(0, 999);
            generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Add, 999, 0), 21);
            int last = factory.Source[20]; factory.Source.RemoveAt(20); factory.Source.Insert(5, last);
            generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Move, last, 5, 20), 21);
            generator.Realize(new(5, 3), 21);
        };
        generator.Realize(new(0, 4), 20);
        Assert.Equal(new RealizedRange(5, 3), generator.RealizedRange);
        VerifyBindings(generator, factory);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("prepare")]
    [InlineData("clear")]
    public void ReentrantDisposalRetiresCurrentOwnershipWithoutResurrection(string phase)
    {
        var factory = new Factory(20);
        var generator = new ItemContainerGenerator<Container>(factory);
        if (phase == "create") factory.OnCreate = generator.Dispose;
        if (phase == "prepare") factory.OnPrepare = _ => generator.Dispose();
        generator.Realize(new(0, 4), 20);
        if (phase == "clear") { factory.OnClear = _ => generator.Dispose(); generator.Realize(new(10, 4), 20); }
        Assert.True(generator.IsDisposed); Assert.Equal(0, generator.RealizedContainerCount); Assert.Equal(0, generator.RecycledContainerCount);
        Assert.All(factory.Created, container => Assert.Equal(1, container.Discards));
        Assert.Equal(generator.CreatedContainerCount, generator.DiscardedContainerCount);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("prepare")]
    [InlineData("clear")]
    [InlineData("discard")]
    public void FactoryFailureFaultsGeneratorAndAttemptsEveryOwnedCleanup(string phase)
    {
        var factory = new Factory(20);
        var generator = new ItemContainerGenerator<Container>(factory, 0);
        generator.Realize(new(0, 4), 20);
        var error = new InvalidOperationException(phase);
        if (phase == "create") factory.OnCreate = () => throw error;
        if (phase == "prepare") factory.OnPrepare = _ => throw error;
        if (phase == "clear") factory.OnClear = _ => throw error;
        if (phase == "discard") factory.OnDiscard = () => throw error;
        Assert.ThrowsAny<Exception>(() => generator.Realize(new(10, 5), 20));
        Assert.True(generator.IsDisposed); Assert.Equal(0, generator.RealizedContainerCount);
        Assert.All(factory.Created, container => Assert.Equal(1, container.Discards));
        generator.Dispose();
        Assert.Equal(generator.CreatedContainerCount, generator.DiscardedContainerCount);
    }

    [Fact]
    public void IdenticalReentrantRequestDoesNotRestartPreparation()
    {
        var factory = new Factory(1_000);
        using var generator = new ItemContainerGenerator<Container>(factory);
        factory.OnPrepare = _ => generator.Realize(new(0, 1_000), 1_000);
        generator.Realize(new(0, 1_000), 1_000);
        Assert.Equal(1_000, factory.Prepared);
        Assert.Equal(new RealizedRange(0, 1_000), generator.RealizedRange);
    }

    [Fact]
    public void UnnotifiedCountChangeAndUnindexedNotificationResetStaleMappings()
    {
        var factory = new Factory(10);
        using var generator = new ItemContainerGenerator<Container>(factory);
        generator.Realize(new(0, 4), 10);
        factory.Source.Insert(0, 999);
        var change = generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Add, 999), 11);
        Assert.Equal(NotifyCollectionChangedAction.Reset, change.Action);
        VerifyBindings(generator, factory);
        factory.Source.Add(100);
        generator.Realize(new(0, 4), 12);
        VerifyBindings(generator, factory);
    }

    [Fact]
    public void InvalidExternalRequestLeavesExistingGeneratorUsable()
    {
        var factory = new Factory(10);
        using var generator = new ItemContainerGenerator<Container>(factory);
        generator.Realize(new(0, 4), 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Realize(new(9, 2), 10));
        Assert.Throws<ArgumentException>(() => generator.NotifyItemsChanged(new(NotifyCollectionChangedAction.Add, 9, 0), 20));
        Assert.False(generator.IsDisposed); Assert.Equal(4, generator.RealizedContainerCount);
        VerifyBindings(generator, factory);
    }

    [Fact]
    public void GeneratorIsUsableWithANonControlContainerType()
    {
        using var generator = new ItemContainerGenerator<System.Text.StringBuilder>(new BuilderFactory());
        generator.Realize(new(9, 1), 20);
        Assert.True(generator.TryGetContainer(9, out var builder)); Assert.Equal("9", builder!.ToString());
    }

    [Fact]
    public void NonsettlingCallbacksFailBoundedlyAndCleanUpEveryContainer()
    {
        var factory = new Factory(20);
        var generator = new ItemContainerGenerator<Container>(factory);
        factory.OnPrepare = index => generator.Realize(new(index == 0 ? 10 : 0, 1), 20);
        var error = Assert.Throws<InvalidOperationException>(() => generator.Realize(new(0, 1), 20));
        Assert.Contains("64", error.Message);
        Assert.True(generator.IsDisposed);
        Assert.Equal(0, generator.RealizedContainerCount);
        Assert.All(factory.Created, container => Assert.Equal(1, container.Discards));
    }

    [Fact]
    public void InvalidFactoryIdentityDoesNotDoubleDiscardTheOwnedInstance()
    {
        var factory = new RepeatingFactory();
        var generator = new ItemContainerGenerator<Container>(factory);
        Assert.Throws<InvalidOperationException>(() => generator.Realize(new(0, 2), 2));
        Assert.True(generator.IsDisposed);
        Assert.Equal(1, factory.Container.Discards);
    }

    private static void VerifyBindings(ItemContainerGenerator<Container> generator, Factory factory)
    {
        var range = generator.RealizedRange;
        for (int i = range.StartIndex; i < range.EndIndex; i++)
        {
            Assert.True(generator.TryGetContainer(i, out var container));
            Assert.Equal(factory.Source[i], container!.Value);
        }
    }

    private sealed class Container { public int Value = -1; public int Discards; }
    private sealed class Factory(int count) : IItemContainerFactory<Container>
    {
        public readonly List<int> Source = Enumerable.Range(0, count).ToList();
        public readonly List<Container> Created = new();
        public int Prepared;
        public Action? OnCreate, OnDiscard;
        public Action<int>? OnPrepare, OnClear;
        public Container CreateContainer() { OnCreate?.Invoke(); var container = new Container(); Created.Add(container); return container; }
        public void PrepareContainer(Container container, int index) { Prepared++; container.Value = Source[index]; OnPrepare?.Invoke(index); }
        public void ClearContainer(Container container, int previousIndex) { container.Value = -1; OnClear?.Invoke(previousIndex); }
        public void DiscardContainer(Container container) { container.Discards++; OnDiscard?.Invoke(); }
    }
    private sealed class BuilderFactory : IItemContainerFactory<System.Text.StringBuilder>
    {
        public System.Text.StringBuilder CreateContainer() => new();
        public void PrepareContainer(System.Text.StringBuilder container, int index) => container.Append(index);
        public void ClearContainer(System.Text.StringBuilder container, int previousIndex) => container.Clear();
        public void DiscardContainer(System.Text.StringBuilder container) { }
    }
    private sealed class RepeatingFactory : IItemContainerFactory<Container>
    {
        public readonly Container Container = new();
        public Container CreateContainer() => Container;
        public void PrepareContainer(Container container, int index) { }
        public void ClearContainer(Container container, int previousIndex) { }
        public void DiscardContainer(Container container) => container.Discards++;
    }
}
