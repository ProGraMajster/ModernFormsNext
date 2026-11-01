using ModernFormsNext.Designer.Clipboard;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupClipboardTests
{
    [Fact]
    public void CutDoesNotRemoveANewerSelectionPublishedByAClipboardObserver()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        var other = session.Document.Controls.AddNode("Label", "other", new(0, 200, 100, 20));
        session.Host.Selection.SelectNodes([first, second]);
        session.Clipboard.Changed += (_, _) => session.SelectNode(other);
        Assert.False(session.CutSelectedNode());
        Assert.Equal(new[] { first, second, other }, session.Document.Controls);
        Assert.Same(other, session.SelectedNode);
        Assert.False(session.Transactions.CanUndo);
        Assert.True(DesignerClipboardSerializer.TryDeserializeSelection(session.Clipboard.Content, out var copies, out _));
        Assert.Equal(2, copies!.Count);
    }

    [Fact]
    public void ClipboardAndExistingDocumentShareOneTextBudgetAtInsertion()
    {
        using var source = DesignerGroupTestModel.Create(out var first, out var second);
        var text = DesignPropertyValue.FromString(new string('a', DesignDocumentValidator.MaximumTextLength / 4));
        first.Properties["Text"] = second.Properties["Text"] = text;
        source.Host.Selection.SelectNodes([first, second]);
        Assert.True(source.CopySelectedNode());
        using var destination = DesignerGroupTestModel.Create(out var retainedFirst, out var retainedSecond);
        destination.Document.Properties["Text"] = DesignPropertyValue.FromString(new string('b', DesignDocumentValidator.MaximumTextLength / 2 + 1));
        Assert.True(new DesignDocumentValidator().ValidateStructure(destination.Document).IsValid);
        destination.Clipboard.SetContent(source.Clipboard.Content!);
        Assert.False(destination.PasteCopiedNode());
        Assert.Equal(new[] { retainedFirst, retainedSecond }, destination.Document.Controls);
        Assert.False(destination.Transactions.CanUndo);
        Assert.False(destination.IsDirty);
    }

    [Fact]
    public void CutAndDuplicateAreAtomicAndPreserveAllSelectionHistory()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        Assert.True(session.DuplicateSelectedNode());
        var copies = session.SelectedNodes.ToArray();
        Assert.Equal(2, copies.Length);
        Assert.Equal(first.Bounds with { X = first.Bounds.X + 16, Y = first.Bounds.Y + 16 }, copies[0].Bounds);
        Assert.Equal(second.Bounds with { X = second.Bounds.X + 16, Y = second.Bounds.Y + 16 }, copies[1].Bounds);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, session.SelectedNodes);
        Assert.Same(first, session.SelectedNode);
        Assert.True(session.CutSelectedNode());
        Assert.Empty(session.Document.Controls);
        Assert.True(DesignerClipboardSerializer.TryDeserializeSelection(session.Clipboard.Content, out var cut, out _));
        Assert.Equal(2, cut!.Count);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(new[] { first, second }, session.Document.Controls);
        Assert.Same(first, session.SelectedNode);
    }

    [Fact]
    public void CopyFromDifferentParentsUsesOneDocumentCoordinateSpace()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Document.Controls.Remove(first);
        var panel = session.Document.Controls.AddNode("Panel", "panel1", new(100, 100, 300, 200));
        panel.Children.Add(first);
        var layout = new DesignerLayoutEngine().Layout(session.Document);
        var a = layout.GetEffectiveBounds(first); var b = layout.GetEffectiveBounds(second);
        session.Host.Selection.SelectNodes([first, second]);
        Assert.True(session.CopySelectedNode());
        Assert.Equal(new DesignBounds(10, 20, 100, 30), first.Bounds);
        session.SelectForm();
        Assert.True(session.PasteCopiedNode());
        var copies = session.SelectedNodes;
        // Forest order follows the outline, independent of selection click order.
        Assert.Equal(a.X - b.X, copies[1].Bounds.X - copies[0].Bounds.X);
        Assert.Equal(a.Y - b.Y, copies[1].Bounds.Y - copies[0].Bounds.Y);
    }

    [Fact]
    public void APageSlotRejectsTheEntireForestBeforeAnyMutation()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second]);
        Assert.True(session.CopySelectedNode());
        var page = session.Document.Controls.AddNode("ContentPage", "page1", new(0, 0, 400, 300));
        session.SelectNode(page);
        session.MarkSaved();
        Assert.False(session.CanPasteCopiedNode);
        Assert.False(session.PasteCopiedNode());
        Assert.Empty(page.Children);
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void OverflowInOneMemberRejectsTheWholeDuplicateAndKeepsClipboard()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Bounds = new(int.MaxValue - first.Bounds.Width, 20, first.Bounds.Width, first.Bounds.Height);
        session.Host.Selection.SelectNodes([first, second]);
        Assert.True(session.CopySelectedNode());
        string? content = session.Clipboard.Content;
        Assert.False(session.DuplicateSelectedNode());
        Assert.Equal(new[] { first, second }, session.Document.Controls);
        Assert.Equal(content, session.Clipboard.Content);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void TablePasteKeepsSpansAndDelegatesAutomaticPlacementToProductionLayout()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["TableColumnSpan"] = DesignPropertyValue.FromInt32(2);
        session.Host.Selection.SelectNodes([first, second]);
        Assert.True(session.CopySelectedNode());
        var table = session.Document.Controls.AddNode("TableLayoutPanel", "table1", new(0, 0, 400, 300));
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(3);
        table.Properties["RowCount"] = DesignPropertyValue.FromInt32(2);
        session.SelectNode(table);
        Assert.True(session.PasteCopiedNode());
        Assert.Equal(2, table.Children.Count);
        Assert.Equal(2, table.Children[0].Properties["TableColumnSpan"].Value);
        Assert.All(table.Children, child => Assert.Equal(-1, child.Properties["TableColumn"].Value));
        var layout = new DesignerLayoutEngine().Layout(session.Document);
        Assert.Empty(layout.PreviewDiagnostics);
        Assert.NotEqual(layout.GetEffectiveBounds(table.Children[0]).X, layout.GetEffectiveBounds(table.Children[1]).X);
    }

    [Fact]
    public void ATableBeyondHardSafetyRemainsAuthoredButRejectsGroupPaste()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second]);
        Assert.True(session.CopySelectedNode());
        var table = session.Document.Controls.AddNode("TableLayoutPanel", "table1", new(0, 0, 400, 300));
        table.Properties["ColumnCount"] = DesignPropertyValue.FromInt32(int.MaxValue);
        session.PreviewResourcePolicy = new DesignerPreviewResourcePolicy { MaximumTableCells = 0, MaximumTableStrips = 0 };
        session.SelectNode(table);
        Assert.False(session.PasteCopiedNode());
        Assert.Empty(table.Children);
        Assert.Equal(int.MaxValue, table.Properties["ColumnCount"].Value);
        Assert.False(session.Transactions.CanUndo);
    }
}
