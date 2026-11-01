using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupLayoutTests
{
    [Fact]
    public void OutlineCommandsMoveTheWholeSiblingGroupAndKeepItsOrder()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(200, 100, 300, 200));
        var leading = session.Document.Controls.AddNode("Label", "leading", new(0, 250, 100, 20));
        session.Document.Controls.Remove(leading); session.Document.Controls.Insert(0, leading);
        session.Host.Selection.SelectNodes([second, first], first);
        Assert.True(session.MoveSelectedNodeUp());
        Assert.Equal(new[] { first, second, leading, panel }, session.Document.Controls);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { leading, first, second, panel }, session.Document.Controls);
        Assert.True(session.MoveSelectedNodeToNextContainer());
        Assert.Equal(new[] { first, second }, panel.Children);
        Assert.True(session.MoveSelectedNodeOutOfContainer());
        Assert.Empty(panel.Children);
        Assert.Equal(new[] { second, first }, session.SelectedNodes);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, panel.Children);
        Assert.Same(first, session.SelectedNode);
    }

    [Theory]
    [InlineData(DesignerGroupArrangement.AlignLeft, 10, 60, 120, 40)]
    [InlineData(DesignerGroupArrangement.AlignRight, -10, 60, 120, 40)]
    [InlineData(DesignerGroupArrangement.AlignTop, 80, 20, 120, 40)]
    [InlineData(DesignerGroupArrangement.AlignBottom, 80, 10, 120, 40)]
    [InlineData(DesignerGroupArrangement.SameWidth, 80, 60, 100, 40)]
    [InlineData(DesignerGroupArrangement.SameHeight, 80, 60, 120, 30)]
    [InlineData(DesignerGroupArrangement.SameSize, 80, 60, 100, 30)]
    public void ArrangementUsesPrimaryAndOneUndoUnit(DesignerGroupArrangement arrangement, int x, int y, int width, int height)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var before = second.Bounds;
        session.Host.Selection.SelectNodes([second, first], first);
        int notifications = 0;
        session.DocumentChanged += (_, _) => notifications++;
        Assert.True(session.ArrangeSelectedNodes(arrangement));
        Assert.Equal(new DesignBounds(x, y, width, height), second.Bounds);
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.Equal(1, notifications);
        Assert.Same(first, session.SelectedNode);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(before, second.Bounds);
        Assert.Equal(new[] { second, first }, session.SelectedNodes);
        Assert.True(session.Transactions.Redo());
        Assert.Equal(new DesignBounds(x, y, width, height), second.Bounds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DistributionRetainsOutsideEdgesAndDeterministicallySharesRemainder(bool vertical)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var third = session.Document.Controls.AddNode("Button", "button3", new(101, 101, 10, 10));
        first.Bounds = new(0, 0, 10, 10); second.Bounds = new(30, 30, 10, 10);
        session.Host.Selection.SelectNodes([third, first, second], second);
        Assert.True(session.ArrangeSelectedNodes(vertical ? DesignerGroupArrangement.DistributeVertically : DesignerGroupArrangement.DistributeHorizontally));
        Assert.Equal(vertical ? 50 : 30, second.Bounds.Y);
        Assert.Equal(vertical ? 30 : 50, second.Bounds.X);
        Assert.Equal(new DesignBounds(101, 101, 10, 10), third.Bounds);
        Assert.Equal(new DesignBounds(0, 0, 10, 10), first.Bounds);
        Assert.Same(second, session.SelectedNode);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
    }

    [Fact]
    public void GroupMoveNormalizesDescendantsAndKeepsOneDeltaAcrossDifferentParents()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(150, 100, 200, 100));
        session.Document.Controls.Remove(first); panel.Children.Add(first);
        session.Host.Selection.SelectNodes([panel, first, second], first);
        Assert.True(session.MoveSelectedNodes(15, -7));
        Assert.Equal(new DesignBounds(165, 93, 200, 100), panel.Bounds);
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(95, 53, 120, 40), second.Bounds);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new DesignBounds(150, 100, 200, 100), panel.Bounds);
        Assert.Equal(new[] { panel, first, second }, session.SelectedNodes);
    }

    [Fact]
    public void ManagedPositionAndSizeConstraintsRejectTheWholeOperation()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.Properties["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.Top));
        session.Host.Selection.SelectNodes([first, second], first);
        Assert.False(session.MoveSelectedNodes(1, 2));
        Assert.False(session.ArrangeSelectedNodes(DesignerGroupArrangement.AlignLeft));
        second.Properties.Remove("Dock");
        second.Properties["MinimumSize"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new System.Drawing.Size(110, 0), typeof(System.Drawing.Size));
        Assert.False(session.ArrangeSelectedNodes(DesignerGroupArrangement.SameWidth));
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 120, 40), second.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ReparentPreservesDocumentSpaceAndRestoresOriginalParentsAndPrimaryOnUndo()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(150, 100, 300, 250));
        session.Host.Selection.SelectNodes([second, first], first);
        var layout = new DesignerLayoutEngine().Layout(session.Document);
        var before = new[] { layout.GetEffectiveBounds(first), layout.GetEffectiveBounds(second) };
        Assert.True(session.ReparentSelectedNodes(panel));
        Assert.Equal(new[] { first, second }, panel.Children);
        layout = new DesignerLayoutEngine().Layout(session.Document);
        Assert.Equal(before, new[] { layout.GetEffectiveBounds(first), layout.GetEffectiveBounds(second) });
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Empty(panel.Children);
        Assert.Equal(new[] { first, second, panel }, session.Document.Controls);
        Assert.Equal(new[] { second, first }, session.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
    }

    [Fact]
    public void ReparentIntoSelectedDescendantOrAmbiguousPageSlotDoesNothing()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(150, 100, 300, 250));
        var nested = panel.Children.AddNode("Panel", "nested", new(0, 0, 100, 100));
        session.Host.Selection.SelectNodes([panel, first], first);
        Assert.False(session.ReparentSelectedNodes(nested));
        var page = session.Document.Controls.AddNode("ContentPage", "page1", new(0, 0, 200, 200));
        session.Host.Selection.SelectNodes([first, second]);
        Assert.False(session.ReparentSelectedNodes(page));
        Assert.Empty(page.Children);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
    }
}
