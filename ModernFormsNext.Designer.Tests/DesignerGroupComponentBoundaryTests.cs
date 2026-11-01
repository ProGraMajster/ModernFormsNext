using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupComponentBoundaryTests
{
    [Theory]
    [InlineData("Copy")]
    [InlineData("Move")]
    [InlineData("Delete")]
    public void DirectServiceMembershipCannotEditInsideAnAtomicComponent(string operation)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var component = session.Document.Controls.AddNode("Example.ComponentCard", "component1", new(250, 200, 200, 100));
        var embedded = component.Children.AddNode("Label", "embedded", new(10, 10, 100, 20));
        var original = DesignDocumentSerializer.Default.Serialize(session.Document);
        session.Host.Selection.SelectNodes([first, embedded], first);
        bool success = operation switch
        {
            "Copy" => session.CopySelectedNode(),
            "Move" => session.MoveSelectedNodes(10, 10),
            _ => session.DeleteSelectedNode()
        };
        Assert.False(success);
        Assert.Equal(original, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
        Assert.Equal(new[] { first, embedded }, session.SelectedNodes);
    }

    [Fact]
    public void SharedPropertiesDoNotExposeEmbeddedComponentInternalsFromRawMembership()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out _);
        var component = session.Document.Controls.AddNode("Example.ComponentCard", "component1", new(250, 200, 200, 100));
        var embedded = component.Children.AddNode("Label", "embedded", new(10, 10, 100, 20));
        session.Host.Selection.SelectNodes([first, embedded], first);
        var grid = new DesignerPropertyGridState(session);
        Assert.Empty(grid.Properties);
        Assert.False(grid.CommitSelectedValue("Changed"));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void AGroupCannotBeReparentedIntoAnEmbeddedComponentContainer()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var component = session.Document.Controls.AddNode("Example.ComponentCard", "component1", new(250, 200, 200, 100));
        var embedded = component.Children.AddNode("Panel", "embedded", new(10, 10, 100, 80));
        session.Host.Selection.SelectNodes([first, second], first);
        Assert.False(session.ReparentSelectedNodes(embedded));
        Assert.Empty(embedded.Children);
        Assert.Equal(new[] { first, second, component }, session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MarqueeKeepsTheComponentOwnerAsOneAtomicSelectionCandidate()
    {
        using var session = DesignerGroupTestModel.Create(out _, out _);
        var component = session.Document.Controls.AddNode("Example.ComponentCard", "component1", new(250, 200, 200, 100));
        var embedded = component.Children.AddNode("Panel", "embedded", new(10, 10, 100, 80));
        var hit = new DesignerHitTestService(new DesignerCoordinateMapper())
            .HitTestMarquee(session, new(0, 0, 600, 400));
        Assert.Contains(component, hit);
        Assert.DoesNotContain(embedded, hit);
    }
}
