using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupOperationTests
{
    [Fact]
    public void ForeignSelectionRejectsTheWholeDeleteWithoutDirtyingOrHistory()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var foreign = new DesignControlNode { TypeName = "Button", Name = "foreign" };
        session.Host.Selection.SelectNodes([first, foreign], first);
        Assert.False(session.DeleteSelectedNode());
        Assert.Equal(new[] { first, second }, session.Document.Controls);
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
        Assert.Equal(new[] { first, foreign }, session.SelectedNodes);
    }

    [Fact]
    public void GroupDeletePublishesOneCompleteModelAndSelectionBoundary()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        int notifications = 0;
        session.SelectionChanged += (_, _) =>
        {
            notifications++;
            if (notifications == 1)
            {
                Assert.Empty(session.Document.Controls);
                Assert.Empty(session.SelectedNodes);
            }
            else
            {
                Assert.Equal(new[] { first, second }, session.Document.Controls);
                Assert.Equal(new[] { first, second }, session.SelectedNodes);
            }
        };
        Assert.True(session.DeleteSelectedNode());
        Assert.Equal(1, notifications);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void AReentrantSelectionAtDeleteCompletionRemainsTheNewestSelection()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var retained = session.Document.Controls.AddNode("Label", "retained", new DesignBounds(10, 200, 90, 20));
        session.Host.Selection.SelectNodes([first, second], first);
        bool replaced = false;
        session.Host.Selection.SelectionChanged += (_, _) =>
        {
            if (!replaced && session.SelectedNodes.Count == 0)
            {
                replaced = true;
                session.SelectNode(retained);
            }
        };
        Assert.True(session.DeleteSelectedNode());
        Assert.True(replaced);
        Assert.Same(retained, session.SelectedNode);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, session.SelectedNodes);
        Assert.True(session.Transactions.Redo());
        Assert.Same(retained, session.SelectedNode);
    }

    [Fact]
    public void DeleteNormalizesSelectedAncestorAndChildAndRestoresAllOnUndo()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Document.Controls.Remove(first);
        var panel = new DesignControlNode { TypeName = "Panel", Name = "panel1", Bounds = new(0, 0, 240, 160) };
        panel.Children.Add(first);
        session.Document.Controls.Insert(0, panel);
        session.Host.Selection.SelectNodes([panel, first, second], first);
        Assert.True(session.DeleteSelectedNode());
        Assert.Empty(session.Document.Controls);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { panel, second }, session.Document.Controls);
        Assert.Same(first, Assert.Single(panel.Children));
        Assert.Equal(new[] { panel, first, second }, session.Host.Selection.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
    }

    [Fact]
    public void CopyPastePreservesTheGroupDeltaAndCommitsOneUndoUnit()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        Assert.True(session.CopySelectedNode());
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
        session.SelectForm();
        Assert.True(session.PasteCopiedNode());
        Assert.Equal(4, session.Document.Controls.Count);
        var copies = session.Document.Controls.Skip(2).ToArray();
        Assert.Equal(second.Bounds.X - first.Bounds.X, copies[1].Bounds.X - copies[0].Bounds.X);
        Assert.Equal(second.Bounds.Y - first.Bounds.Y, copies[1].Bounds.Y - copies[0].Bounds.Y);
        Assert.Equal(4, session.Document.Controls.Select(node => node.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(copies, session.Host.Selection.SelectedNodes);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, session.Document.Controls);
        Assert.Empty(session.Host.Selection.SelectedNodes);
        Assert.True(session.Transactions.Redo());
        Assert.Equal(copies, session.Host.Selection.SelectedNodes);
    }
}
