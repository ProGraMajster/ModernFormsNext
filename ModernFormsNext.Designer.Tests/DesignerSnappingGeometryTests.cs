using System.Drawing;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerSnappingGeometryTests
{
    [Fact]
    public void AClippedAwayTargetEdgeDoesNotAttractThePointer()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 100, 30, 30));
        session.Document.Controls.AddNode("Button", "partlyClipped", new(-50, 20, 100, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(-58, context.Move(-58, 0, 1, false).X);
        Assert.DoesNotContain(context.Guides, guide => guide.Vertical && guide.Coordinate < 0);
    }

    [Fact]
    public void AGroupWithHiddenMembersKeepsItsMoveButDoesNotInventVisibleGroupGuides()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "first", new(10, 20, 40, 30));
        var hidden = session.Document.Controls.AddNode("Button", "hidden", new(200, 20, 40, 30));
        hidden.Properties["Visible"] = DesignPropertyValue.FromBoolean(false);
        session.Host.Selection.SelectNodes([first, hidden], first);
        Assert.Null(DesignerSnappingContext.Create(session, [first, hidden]));
        Assert.True(session.MoveSelectedNodes(15, 0));
        Assert.Equal(25, first.Bounds.X);
        Assert.Equal(215, hidden.Bounds.X);
    }

    [Fact]
    public void MixedParentGroupsUseTheirCommonContainerAndExcludeTheirOwnAncestors()
    {
        using var session = new DesignerSession();
        var left = session.Document.Controls.AddNode("Panel", "left", new(100, 100, 200, 200));
        var right = session.Document.Controls.AddNode("Panel", "right", new(400, 100, 200, 200));
        var first = left.Children.AddNode("Button", "first", new(10, 20, 40, 30));
        var second = right.Children.AddNode("Button", "second", new(20, 60, 50, 30));
        session.Document.Controls.AddNode("Panel", "target", new(650, 350, 100, 100));
        session.Host.Selection.SelectNodes([first, second], first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first, second]));
        Assert.Equal(180, context.Move(178, 0, 1, false).X);
        Assert.Contains(context.Guides, guide => guide.Vertical && guide.Coordinate == 650);
    }

    [Fact]
    public void CenterGuideMovesTheForestAsOneRectangleWithoutMutatingItDuringPlanning()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "first", new(10, 20, 50, 30));
        var second = session.Document.Controls.AddNode("Button", "second", new(80, 20, 50, 30));
        session.Document.Controls.AddNode("Button", "target", new(250, 120, 60, 30));
        session.Host.Selection.SelectNodes([first, second], first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first, second]));
        Assert.Equal(210, context.Move(208, 0, 1, false).X);
        Assert.Contains(context.Guides, guide => guide.Vertical && guide.Kind == DesignerSnapGuideKind.Center);
        Assert.Equal(10, first.Bounds.X);
        Assert.Equal(80, second.Bounds.X);
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void NestedPaddingAndMovingMarginShareTheProductionDocumentCoordinateSpace()
    {
        using var session = new DesignerSession();
        var panel = session.Document.Controls.AddNode("Panel", "panel", new(100, 100, 300, 200));
        panel.Properties["Padding"] = PaddingValue(new Padding(12, 14, 16, 18));
        var first = panel.Children.AddNode("Button", "first", new(30, 40, 50, 30));
        first.Properties["Margin"] = PaddingValue(new Padding(5, 6, 7, 8));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(-13, context.Move(-15, 0, 1, false).X);
        Assert.Contains(context.Guides, guide => guide.Vertical && guide.Kind == DesignerSnapGuideKind.Padding && guide.Coordinate == 117);
    }

    [Fact]
    public void AdjacentMarginGuideUsesBothControlsMargins()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "first", new(10, 20, 40, 30));
        var target = session.Document.Controls.AddNode("Button", "target", new(200, 20, 40, 30));
        first.Properties["Margin"] = PaddingValue(new Padding(5));
        target.Properties["Margin"] = PaddingValue(new Padding(9));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(244, context.Move(241, 0, 1, false).X);
        Assert.Contains(context.Guides, guide => guide.Vertical && guide.Kind == DesignerSnapGuideKind.Margin && guide.Coordinate == 254);
    }

    [Theory]
    [InlineData(180, 258, 260, 34, 34)]
    [InlineData(260, 178, 180, 34, 34)]
    [InlineData(261, 178, 180, 34, 35)]
    public void EqualMarginGapsCanBeRepeatedOrPlacedBetweenNeighbors(int secondX, int rawX, int expectedX, int firstGap, int secondGap)
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 40, 30));
        session.Document.Controls.AddNode("Button", "left", new(100, 20, 40, 30));
        session.Document.Controls.AddNode("Button", "right", new(secondX, 20, 40, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(expectedX - 10, context.Move(rawX - 10, 0, 1, false).X);
        Assert.Equal(new double?[] { firstGap, secondGap }, context.Guides.Where(guide => guide.Kind == DesignerSnapGuideKind.Spacing).Select(guide => guide.Gap));
    }

    [Fact]
    public void VerticalEqualSpacingUsesTheSameBoundedNeighborPolicy()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(20, 10, 40, 30));
        session.Document.Controls.AddNode("Button", "top", new(20, 100, 40, 30));
        session.Document.Controls.AddNode("Button", "bottom", new(20, 230, 40, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(155, context.Move(0, 153, 1, false).Y);
        Assert.Equal(new double?[] { 29, 29 }, context.Guides.Where(guide => guide.Kind == DesignerSnapGuideKind.Spacing).Select(guide => guide.Gap));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.4f)]
    public void AcquisitionAndReleaseTolerancesAreSurfacePixelsRatherThanDocumentPixels(float zoom)
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 100, 30));
        session.Document.Controls.AddNode("Button", "target", new(200, 90, 100, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        long distance = (long)Math.Floor(5 / zoom);
        Assert.Equal(190, context.Move(190 - distance, 0, zoom, false).X);
        distance = (long)Math.Floor(9 / zoom);
        Assert.Equal(190, context.Move(190 - distance, 0, zoom, false).X);
    }

    [Fact]
    public void ACloserCenterWinsOverAnEdgeWithinTheSameAcquisitionRadius()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 100, 30));
        session.Document.Controls.AddNode("Button", "target", new(200, 90, 60, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(170, context.Move(178, 0, 0.4f, false).X);
        Assert.Contains(context.Guides, guide => guide.Vertical && guide.Kind == DesignerSnapGuideKind.Center);
    }

    [Fact]
    public void ANewerSiblingPositionCannotBeOverwrittenByAFrozenGuide()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 100, 30));
        var target = session.Document.Controls.AddNode("Button", "target", new(200, 90, 60, 30));
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        target.Bounds = target.Bounds with { X = 400 };
        Assert.Equal(185, context.Move(185, 0, 1, false).X);
        Assert.Empty(context.Guides);
        Assert.Equal(400, target.Bounds.X);
    }

    [Fact]
    public void HiddenAndLazyTablePreviewsDoNotBecomeGuidesOrAllocateAFullGrid()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Button", "moving", new(10, 20, 100, 30));
        var hidden = session.Document.Controls.AddNode("Button", "hidden", new(200, -50, 60, 30));
        var table = session.Document.Controls.AddNode("TableLayoutPanel", "table", new(200, 90, 60, 30));
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(int.MaxValue);
        session.PreviewResourcePolicy = new DesignerPreviewResourcePolicy { MaximumTableStrips = 0, MaximumTableCells = 0 };
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        Assert.Equal(185, context.Move(185, 0, 1, false).X);
        Assert.Empty(context.Guides);
        Assert.Equal(int.MaxValue, table.Properties["RowCount"].Value);
        Assert.Equal(-50, hidden.Bounds.Y);
    }

    [Theory]
    [InlineData("FlowLayoutPanel")]
    [InlineData("TableLayoutPanel")]
    [InlineData("ContentPage")]
    public void ManagedPlacementDoesNotAcquireManualPositionGuides(string type)
    {
        using var session = new DesignerSession();
        var parent = session.Document.Controls.AddNode(type, "owner", new(0, 0, 300, 200));
        var first = parent.Children.AddNode("Button", "first", new(10, 20, 40, 30));
        session.SelectNode(first);
        Assert.Null(DesignerSnappingContext.Create(session, [first]));
    }

    private static DesignPropertyValue PaddingValue(Padding padding)
        => DesignerPropertyValueEditor.ToDesignPropertyValue(padding, typeof(Padding));
}
