using ModernFormsNext.Layout;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class PropertyStoreTests
{
    [Fact]
    public void IntegerStoreCanBeEmptiedAndRepopulatedAfterRemovingItsLastBlock()
    {
        var store = new PropertyStore();
        // Keys 0 and 1 share a packed block; key 4 occupies the following block.
        store.SetInteger(0, 10);
        store.SetInteger(1, 20);
        store.SetInteger(4, 30);
        store.RemoveInteger(0);
        Assert.Equal(20, store.GetInteger(1));
        store.RemoveInteger(4);
        store.RemoveInteger(1);

        Assert.Equal(0, store.GetInteger(1, out bool found));
        Assert.False(found);
        Assert.False(store.ContainsInteger(4));
        Assert.False(store.ContainsKey(0));
        store.RemoveInteger(1); // Removing an already absent key remains harmless.

        store.SetInteger(1, -7);
        store.SetInteger(8, int.MaxValue);
        Assert.Equal(-7, store.GetInteger(1));
        Assert.Equal(int.MaxValue, store.GetInteger(8));
        Assert.False(store.ContainsInteger(0));
        store.RemoveInteger(1);
        store.RemoveInteger(8);
        Assert.Equal(42, store.GetInteger(8, defaultValue: 42));
    }
}
