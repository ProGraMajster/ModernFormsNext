using System.Collections;
using System.Collections.Specialized;
using ModernFormsNext.Virtualization;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VirtualizationIndexChangeTests
{
    [Theory]
    [InlineData(2, 6, 2)]
    [InlineData(6, 2, 2)]
    [InlineData(2, 2, 2)]
    [InlineData(0, 8, 2)]
    [InlineData(8, 0, 2)]
    public void BlockMoveMappingMatchesSourceOccurrences(int oldIndex, int newIndex, int length)
    {
        var old = Enumerable.Range(0, 10).ToList();
        var changed = old.ToList();
        var block = changed.GetRange(oldIndex, length);
        changed.RemoveRange(oldIndex, length); changed.InsertRange(newIndex, block);
        var notification = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, (IList)block, newIndex, oldIndex);
        var mapping = ItemIndexChange.FromNotification(notification, 10, 10);
        for (int index = 0; index < old.Count; index++) Assert.Equal(changed.IndexOf(old[index]), mapping.MapIndex(index));
    }

    [Fact]
    public void DuplicateValuesAreSeparateOccurrencesAndReplaceRetiresOnlyAffectedBlock()
    {
        var mapping = ItemIndexChange.FromNotification(new(NotifyCollectionChangedAction.Replace,
            (IList)new[] { 7, 7 }, (IList)new[] { 7, 7 }, 2), 5, 5);
        Assert.Equal(0, mapping.MapIndex(0)); Assert.Equal(1, mapping.MapIndex(1));
        Assert.Equal(-1, mapping.MapIndex(2)); Assert.Equal(-1, mapping.MapIndex(3)); Assert.Equal(4, mapping.MapIndex(4));
        Assert.Equal(-1, mapping.MapIndex(-1));
    }

    [Fact]
    public void AddAndRemoveBlockMappingsMatchOccurrencePositions()
    {
        var added = ItemIndexChange.FromNotification(new(NotifyCollectionChangedAction.Add, (IList)new[] { 1, 1, 1 }, 2), 5, 8);
        Assert.Equal(1, added.MapIndex(1)); Assert.Equal(5, added.MapIndex(2)); Assert.Equal(7, added.MapIndex(4));
        var removed = ItemIndexChange.FromNotification(new(NotifyCollectionChangedAction.Remove, (IList)new[] { 1, 1, 1 }, 2), 8, 5);
        Assert.Equal(-1, removed.MapIndex(2)); Assert.Equal(-1, removed.MapIndex(4)); Assert.Equal(2, removed.MapIndex(5));
    }

    [Fact]
    public void InvalidMetadataFailsBeforePublishingAMapping()
    {
        Assert.Throws<ArgumentException>(() => ItemIndexChange.FromNotification(new(NotifyCollectionChangedAction.Remove, 1, 9), 5, 4));
        Assert.Throws<ArgumentException>(() => ItemIndexChange.FromNotification(new(NotifyCollectionChangedAction.Add, 1, 2), 5, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ItemIndexChange.Reset(2, 0).MapIndex(2));
        Assert.Equal(-1, ItemIndexChange.Reset(2, 3).MapIndex(1));
    }
}
