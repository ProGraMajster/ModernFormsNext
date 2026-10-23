using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;
using System.Reflection;

namespace ModernFormsNext.Designer.Tests;

public sealed class PageDesignerEditingTests
{
    [Theory]
    [InlineData(nameof(TabControl))]
    [InlineData(nameof(SplitContainer))]
    public void ExistingSpecialContainersKeepTheirCanonicalInsertionTarget(string type)
    {
        using var session = Create(type, out var parent);
        var child = session.AddControl(nameof(Button));
        Assert.NotSame(parent, session.FindParent(child));
        Assert.Contains(session.FindParent(child), parent.Children);
        Assert.Null(DesignPageContainers.GetChildError(parent));
    }

    [Fact]
    public void CollectionDialogCancellationKeepsAuthoredDocumentAndHistoryUntouched()
    {
        using var session = Create(nameof(ContentPage), out var parent);
        using (var dialog = new DesignerTabPageCollectionDialog(session, parent))
            Invoke(dialog, "AddPage");
        Assert.Empty(parent.Children);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void FlyoutCollectionUsesExplicitSlotsAndOneUndoUnit()
    {
        using var session = Create(nameof(FlyoutPage), out var parent);
        using (var dialog = new DesignerTabPageCollectionDialog(session, parent))
        {
            Invoke(dialog, "AddPage");
            Invoke(dialog, "AddPage");
            var slot = Assert.Single(dialog.Controls.OfType<ComboBox>(), item => item.Items.Count == 2);
            slot.SelectedIndex = 1;
            Assert.Empty(parent.Children);
            Invoke(dialog, "CommitPages");
        }
        Assert.Equal(2, parent.Children.Count);
        Assert.Equal(new[] { "Detail", "Flyout" }, parent.Children.Select(child => DesignPageContainers.GetChildMember(parent, child)));
        Assert.Null(DesignPageContainers.GetChildError(parent));
        session.Transactions.Undo();
        Assert.Empty(parent.Children);
        Assert.False(session.Transactions.CanUndo);
        session.Transactions.Redo();
        Assert.Equal(2, parent.Children.Count);
    }

    private static void Invoke(object target, string method)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

    [Theory]
    [InlineData(nameof(ContentPage), nameof(Button))]
    [InlineData(nameof(PageHost), nameof(ContentPage))]
    [InlineData(nameof(NavigationPage), nameof(ContentPage))]
    [InlineData(nameof(TabbedPage), nameof(ContentPage))]
    public void ToolboxOwnershipUndoAndRedoKeepCanonicalNodes(string parentType, string childType)
    {
        using var session = Create(parentType, out var parent);
        var child = session.AddControl(childType);
        Assert.Same(child, Assert.Single(parent.Children));
        Assert.Same(parent, session.FindParent(child));
        session.Transactions.Undo();
        Assert.Empty(parent.Children);
        session.Transactions.Redo();
        Assert.Same(child, Assert.Single(parent.Children));
        session.SelectNode(child);
        Assert.Equal(parentType == nameof(TabbedPage), session.DuplicateSelectedNode());
    }

    [Theory]
    [InlineData(nameof(PageHost))]
    [InlineData(nameof(NavigationPage))]
    [InlineData(nameof(TabbedPage))]
    [InlineData(nameof(FlyoutPage))]
    public void RejectedToolboxInsertionDoesNotDirtyTheModel(string parentType)
    {
        using var session = Create(parentType, out var parent);
        Assert.Throws<InvalidOperationException>(() => session.AddControl(nameof(Button)));
        Assert.Empty(parent.Children);
        Assert.False(session.Transactions.CanUndo);
        Assert.Same(parent, session.SelectedNode);
    }

    [Fact]
    public void ReplacementRejectsSharingAndCyclesWithoutPublishingHistory()
    {
        using var session = Create(nameof(ContentPage), out var parent);
        Assert.Throws<InvalidOperationException>(() => session.ReplaceChildren(parent, [parent]));
        Assert.Empty(parent.Children);
        Assert.False(session.Transactions.CanUndo);
        var child = session.AddControl(nameof(Panel));
        session.SelectNode(parent);
        Assert.Throws<InvalidOperationException>(() => session.AddControl(nameof(Button)));
        Assert.Same(child, Assert.Single(parent.Children));
    }

    [Fact]
    public void TabRemovalAndReorderingPreserveAuthoredSelectionAndHistory()
    {
        using var session = Create(nameof(TabbedPage), out var parent);
        var first = session.AddControl(nameof(ContentPage));
        session.SelectNode(parent);
        var second = session.AddControl(nameof(ContentPage));
        session.SetPropertyValue(parent, "SelectedIndex", DesignPropertyValue.FromInt32(1));
        session.ReplaceChildren(parent, [second, first]);
        Assert.Equal(0, parent.Properties["SelectedIndex"].Value);
        Assert.True(session.DeleteNode(second));
        Assert.Equal(0, parent.Properties["SelectedIndex"].Value);
        session.Transactions.Undo();
        Assert.Same(second, parent.Children[0]);
        Assert.Equal(0, parent.Properties["SelectedIndex"].Value);
        session.Transactions.Undo();
        Assert.Same(second, parent.Children[1]);
        Assert.Equal(1, parent.Properties["SelectedIndex"].Value);
    }

    [Theory]
    [InlineData(nameof(FlyoutPage), "FlyoutWidth", "0")]
    [InlineData(nameof(FlyoutPage), "SplitBreakpoint", "-1")]
    [InlineData(nameof(FlyoutPage), "DisplayMode", "999")]
    [InlineData(nameof(FlyoutPage), "FlowDirection", "TopDown")]
    [InlineData(nameof(TabbedPage), "FlowDirection", "TopDown")]
    [InlineData(nameof(TabbedPage), "SelectedIndex", "0")]
    [InlineData(nameof(NavigationPage), "TransitionDuration", "-0.0001")]
    [InlineData(nameof(AppShell), "TransitionDuration", "-0.0001")]
    public void InvalidPagePropertiesRollbackWithoutUndoUnits(string type, string name, string value)
    {
        using var session = Create(type, out var node);
        var grid = new DesignerPropertyGridState(session);
        var property = Assert.Single(grid.Properties, item => item.Name == name);
        grid.SelectRow(new DesignerPropertyGridRow(property));
        Assert.False(grid.CommitSelectedValue(value));
        Assert.False(node.Properties.ContainsKey(name));
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData(nameof(ContentPage))]
    [InlineData(nameof(NavigationPage))]
    [InlineData(nameof(TabbedPage))]
    [InlineData(nameof(FlyoutPage))]
    [InlineData(nameof(AppShell))]
    public void MetadataKeepsOneTitleAndExcludesRuntimeObjects(string type)
    {
        using var session = Create(type, out var node);
        node.Properties["Text"] = DesignPropertyValue.FromString("Legacy title");
        var grid = new DesignerPropertyGridState(session);
        Assert.DoesNotContain(grid.Properties, item => item.Name is "Text" or "Routes" or "CurrentPage"
            or "CurrentRoute" or "NavigationStack" or "LifecycleState" or "IsPresented" or "CanGoBack"
            or "IsNavigating" or "PendingRequestCount" or "BackCommand" or "NavigateCommand"
            or "ToggleFlyoutCommand" or "CloseFlyoutCommand" or "IsFlyoutOpen" or "EffectiveDisplayMode");
        var title = Assert.Single(grid.Properties, item => item.Name == "Title");
        Assert.Equal("Legacy title", title.GetValue());
        grid.SelectRow(new DesignerPropertyGridRow(title));
        Assert.True(grid.CommitSelectedValue("Authored title"));
        Assert.False(node.Properties.ContainsKey("Text"));
        session.Transactions.Undo();
        Assert.Equal("Legacy title", node.Properties["Text"].Value);
        Assert.False(node.Properties.ContainsKey("Title"));
    }

    private static DesignerSession Create(string type, out DesignControlNode node)
    {
        var session = new DesignerSession();
        var document = new DesignDocument();
        node = new DesignControlNode { Name = "container", TypeName = type };
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        return session;
    }
}
