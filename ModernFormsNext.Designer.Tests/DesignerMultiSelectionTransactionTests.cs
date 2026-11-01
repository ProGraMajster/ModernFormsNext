using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerMultiSelectionTransactionTests
{
    [Fact]
    public void UndoAndRedoRestoreOrderedMembershipAndPrimaryWithOneModelUnit()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        using (var transaction = session.Transactions.Begin("Edit selected controls"))
        {
            session.SetPropertyValue(first, "Text", DesignPropertyValue.FromString("First value"));
            session.SetPropertyValue(second, "Text", DesignPropertyValue.FromString("Second value"));
            session.Host.Selection.SelectNodes([second, first], second);
            transaction.Commit();
        }
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.False(first.Properties.ContainsKey("Text"));
        Assert.False(second.Properties.ContainsKey("Text"));
        Assert.Equal(new[] { first, second }, session.Host.Selection.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
        Assert.True(session.Transactions.Redo());
        Assert.Equal("First value", first.Properties["Text"].GetString());
        Assert.Equal("Second value", second.Properties["Text"].GetString());
        Assert.Equal(new[] { second, first }, session.Host.Selection.SelectedNodes);
        Assert.Same(second, session.SelectedNode);
    }

    [Fact]
    public void RollbackRestoresMembershipEvenWhenPrimaryNeverChanges()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        using (var transaction = session.Transactions.Begin("Cancelled selection"))
        {
            session.Host.Selection.SelectNodes([first], first);
            transaction.Rollback();
        }
        Assert.Equal(new[] { first, second }, session.Host.Selection.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.HasActiveTransaction);
    }

    [Fact]
    public void NestedRollbackRestoresItsSelectionWithoutLosingOuterChanges()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        using (var outer = session.Transactions.Begin("Outer group edit"))
        {
            session.SetPropertyValue(first, "Text", DesignPropertyValue.FromString("Keep"));
            session.Host.Selection.SelectNodes([second, first], second);
            using (var nested = session.Transactions.Begin("Cancelled nested edit"))
            {
                session.SetPropertyValue(second, "Text", DesignPropertyValue.FromString("Discard"));
                session.SelectNode(first);
                nested.Rollback();
            }
            Assert.Equal(new[] { second, first }, session.Host.Selection.SelectedNodes);
            Assert.Same(second, session.SelectedNode);
            Assert.False(second.Properties.ContainsKey("Text"));
            Assert.Equal("Keep", first.Properties["Text"].GetString());
            outer.Commit();
        }
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, session.Host.Selection.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
    }
}

internal static class DesignerGroupTestModel
{
    internal static DesignerSession Create(out DesignControlNode first, out DesignControlNode second)
    {
        var document = new DesignDocument
        {
            Namespace = "DesignerTests", ClassName = "GroupForm", FormName = "groupForm",
            Size = new DesignSize(640, 480)
        };
        first = document.Controls.AddNode("Button", "button1", new DesignBounds(10, 20, 100, 30));
        second = document.Controls.AddNode("Button", "button2", new DesignBounds(80, 60, 120, 40));
        var session = new DesignerSession(initialRenderMode: DesignerControlRenderMode.Placeholder);
        session.OpenDocument(document, path: null, markDirty: false);
        return session;
    }
}
