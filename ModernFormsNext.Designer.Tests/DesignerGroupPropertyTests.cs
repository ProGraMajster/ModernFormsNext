using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerGroupPropertyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstTextInputCommitReplacesInitialSelectionWithoutRevokingItsOwnEdit(bool mixed)
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["Text"] = DesignPropertyValue.FromString("First");
        second.Properties["Text"] = DesignPropertyValue.FromString("Second");
        if (mixed) session.Host.Selection.SelectNodes([first, second], first);
        else session.SelectNode(first);
        using var grid = new DesignerPropertyGrid(session) { Width = 330, Height = 600 };
        using var surface = new SkiaControlSurface(grid);
        var state = (DesignerPropertyGridState)typeof(DesignerPropertyGrid)
            .GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(grid)!;
        var row = new DesignerPropertyGridRow(Assert.Single(state.Properties, property => property.Name == "Text"));
        grid.BeginEdit(row, new System.Drawing.Rectangle(140, 150, 170, 24));
        var editor = Assert.Single(grid.Controls.OfType<TextBox>());
        Assert.Equal(mixed ? string.Empty : "First", editor.Text);
        var client = Assert.IsAssignableFrom<ModernFormsNext.WindowKit.Input.ITextInputClient>(surface.TextInputClient);
        Assert.True(client.CommitText("Shared"));
        Assert.Equal("Shared", editor.Text);
        Assert.Equal("Shared", state.EditingText);
        Assert.Equal("First", first.Properties["Text"].Value);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MixedTextIsExplicitAndUntouchedEditorDoesNotWriteButExplicitClearDoes()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["Text"] = DesignPropertyValue.FromString("First");
        second.Properties["Text"] = DesignPropertyValue.FromString("Second");
        session.Host.Selection.SelectNodes([first, second], first);
        var grid = new DesignerPropertyGridState(session);
        Select(grid, "Text");
        Assert.True(grid.SelectedProperty!.IsMixed);
        grid.BeginEditing();
        Assert.Equal(string.Empty, grid.EditingText);
        Assert.True(grid.CommitEditing());
        Assert.Equal("First", first.Properties["Text"].Value);
        Assert.Equal("Second", second.Properties["Text"].Value);
        Assert.False(session.Transactions.CanUndo);
        grid.BeginEditing();
        grid.UpdateEditingText(string.Empty); // An explicit delete of the empty mixed editor.
        Assert.True(grid.CommitEditing());
        Assert.Equal(string.Empty, first.Properties["Text"].Value);
        Assert.Equal(string.Empty, second.Properties["Text"].Value);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal("First", first.Properties["Text"].Value);
        Assert.Equal("Second", second.Properties["Text"].Value);
        Assert.Same(first, session.SelectedNode);
    }

    [Fact]
    public void AuthoredMixedLabelIsAnOrdinarySharedValue()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["Text"] = second.Properties["Text"] = DesignPropertyValue.FromString("(multiple values)");
        session.Host.Selection.SelectNodes([first, second]);
        var grid = new DesignerPropertyGridState(session);
        Select(grid, "Text");
        Assert.False(grid.SelectedProperty!.IsMixed);
        Assert.Equal("(multiple values)", grid.SelectedProperty.GetValueText());
        Assert.True(grid.CommitSelectedValue("Shared"));
        Assert.Equal("Shared", first.Properties["Text"].Value);
        Assert.Equal("Shared", second.Properties["Text"].Value);
    }

    [Fact]
    public void SharedBoundsTargetEveryMemberWithoutChangingPrimaryOrOtherDimensions()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second], first);
        var grid = new DesignerPropertyGridState(session);
        Select(grid, "Width");
        Assert.True(grid.SelectedProperty!.IsMixed);
        Assert.True(grid.CommitSelectedValue("130"));
        Assert.Equal(new DesignBounds(10, 20, 130, 30), first.Bounds);
        Assert.Equal(new DesignBounds(80, 60, 130, 40), second.Bounds);
        Assert.Same(first, session.SelectedNode);
        Assert.Equal(1, session.CurrentHistory.UndoCount);
        Assert.True(session.Transactions.Undo());
        Assert.Equal(100, first.Bounds.Width);
        Assert.Equal(120, second.Bounds.Width);
    }

    [Fact]
    public void ValidationFailureOnNonPrimaryMemberRollsBackEveryValue()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        first.Properties["Text"] = DesignPropertyValue.FromString("First");
        second.Properties["Text"] = DesignPropertyValue.FromString("Second");
        second.Properties["LineHeight"] = DesignPropertyValue.FromDouble(-1);
        session.Host.Selection.SelectNodes([first, second], first);
        var grid = new DesignerPropertyGridState(session);
        Select(grid, "Text");
        Assert.False(grid.CommitSelectedValue("Changed"));
        Assert.Equal("First", first.Properties["Text"].Value);
        Assert.Equal("Second", second.Properties["Text"].Value);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void OverflowRollsBackBoundsForTheWholeGroup()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        session.Host.Selection.SelectNodes([first, second]);
        var grid = new DesignerPropertyGridState(session);
        Select(grid, "X");
        Assert.False(grid.CommitSelectedValue(int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(10, first.Bounds.X);
        Assert.Equal(80, second.Bounds.X);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void CommonIntersectionAndMembershipChangesKeepEditingSafe()
    {
        using var session = DesignerGroupTestModel.Create(out var first, out var second);
        second.TypeName = "TextBox";
        session.Host.Selection.SelectNodes([first, second], first);
        var grid = new DesignerPropertyGridState(session);
        Assert.DoesNotContain(grid.Properties, property => property.Name == "PasswordChar");
        Assert.True(Assert.Single(grid.Properties, property => property.Name == "Name").IsReadOnly);
        Assert.False(grid.SupportsEvents);
        Assert.Empty(grid.Events);
        Select(grid, "Text");
        grid.BeginEditing();
        Assert.True(grid.IsEditing);
        session.Host.Selection.SelectNodes([first], first); // Same primary; changed membership.
        Assert.False(grid.IsEditing);
        Assert.True(grid.SupportsEvents);
        Assert.False(session.Transactions.CanUndo);
    }

    private static void Select(DesignerPropertyGridState state, string name)
        => state.SelectRow(new DesignerPropertyGridRow(Assert.Single(state.Properties, property => property.Name == name)));
}
