using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class AccessibleGridMetadataTests
{
    [Fact]
    public void InfiniteHeaderSequenceStopsAfterTheDocumentedAssociationLimit()
    {
        var peer = new AccessibleObject();
        int reads = 0;
        IEnumerable<AccessibleObject> Headers() { while (true) { reads++; yield return peer; } }
        Assert.Throws<ArgumentException>(() => new AccessibleGridCellInfo(peer, 0, 0, rowHeaders: Headers()));
        Assert.Equal(65, reads);
    }

    [Fact]
    public void HeaderAssociationsAreCopiedAndRejectNull()
    {
        var grid = new AccessibleObject();
        var header = new AccessibleObject();
        var source = new[] { header };
        var info = new AccessibleGridCellInfo(grid, 0, 0, columnHeaders: source);
        source[0] = new AccessibleObject();
        Assert.Same(header, Assert.Single(info.ColumnHeaders));
        Assert.Throws<ArgumentException>(() => new AccessibleGridCellInfo(grid, 0, 0, rowHeaders: new AccessibleObject[] { null! }));
    }
}
