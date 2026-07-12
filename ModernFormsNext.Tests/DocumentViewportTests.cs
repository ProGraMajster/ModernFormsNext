using System.Drawing;
using ModernFormsNext.Renderers;
using Xunit;

namespace ModernFormsNext.Tests;

public class DocumentViewportTests
{
    [Fact]
    public void CullingKeepsPartiallyVisibleElementsAndSkipsOutsideElements()
    {
        var viewport = new Rectangle(0, 0, 100, 100);

        Assert.True(DocumentViewerRenderer.IsElementVisible(new Rectangle(0, 90, 20, 20), 0, viewport));
        Assert.False(DocumentViewerRenderer.IsElementVisible(new Rectangle(0, 101, 20, 20), 0, viewport));
        Assert.True(DocumentViewerRenderer.IsElementVisible(new Rectangle(0, 190, 20, 20), 100, viewport));
    }
}
