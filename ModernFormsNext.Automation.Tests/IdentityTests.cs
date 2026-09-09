using System.Reflection;
using System.Text.Json;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Identity")]
public sealed class IdentityTests
{
    [Fact]
    public void SameControlRemoveReinsertAndReorderRetainCanonicalHandle()
    {
        using var f = new AutomationFixture();
        var first = f.Add(new Button { Name = "first" }); var second = f.Add(new Button { Name = "second" });
        var handle = f.Handle(first);
        f.Form.Controls.Remove(first);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.InspectAsync(f.Root.RootId, handle).Completed().Error);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.PerformActionAsync(f.Root.RootId, handle, AccessibleActions.Invoke).Completed().Error);
        f.Form.Controls.Add(first);
        Assert.Equal(AutomationErrorCode.None, f.Session.InspectAsync(f.Root.RootId, handle).Completed().Error);
        Assert.Equal(handle, f.Handle(first));
        Assert.NotEqual(handle, f.Handle(second));
    }

    [Fact]
    public void RecreatedSameNameNeverRetargetsOldHandle()
    {
        using var f = new AutomationFixture();
        var first = f.Add(new Button { Name = "same" }); var old = f.Handle(first); first.Dispose();
        var replacement = f.Add(new Button { Name = "same" });
        Assert.NotEqual(old, f.Handle(replacement));
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.InspectAsync(f.Root.RootId, old).Completed().Error);
    }

    [Fact]
    public void DuplicateNamesHaveDistinctHandles()
    {
        using var f = new AutomationFixture();
        f.Add(new Button { Name = "same" }); f.Add(new Button { Name = "same" });
        var nodes = f.Find(new() { AutomationId = "same" }).Value;
        Assert.Equal(2, nodes.Length);
        Assert.NotEqual(nodes[0].Handle, nodes[1].Handle);
    }

    [Fact]
    public void DuplicateListTextKeepsOccurrenceIdentityOnMove()
    {
        using var f = new AutomationFixture();
        var list = f.Add(new ListBox()); list.Items.Add("same"); list.Items.Add("same");
        var children = f.Session.GetChildrenAsync(f.Root.RootId, f.Handle(list)).Completed().Value;
        Assert.NotEqual(children[0].Handle, children[1].Handle);
        list.Items.Move(0, 1);
        var moved = f.Session.GetChildrenAsync(f.Root.RootId, f.Handle(list)).Completed().Value;
        Assert.Equal(children[0].Handle, moved[1].Handle);
        Assert.Equal(children[1].Handle, moved[0].Handle);
    }

    [Fact]
    public void ListRemoveAddCreatesNewOccurrenceWhileTreeReinsertKeepsCachedPeer()
    {
        using var f = new AutomationFixture();
        var list = f.Add(new ListBox()); list.Items.Add("item");
        var oldList = f.Handle(list.AccessibilityObject.GetChild(0)!);
        list.Items.RemoveAt(0); _ = f.Find(new()); list.Items.Add("item");
        Assert.NotEqual(oldList, f.Handle(list.AccessibilityObject.GetChild(0)!));
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.InspectAsync(f.Root.RootId, oldList).Completed().Error);
        var tree = f.Add(new TreeView()); var item = tree.Items.Add(new TreeViewItem("item"));
        var oldTree = f.Handle(tree.AccessibilityObject.GetChild(0)!);
        tree.Items.Remove(item);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.InspectAsync(f.Root.RootId, oldTree).Completed().Error);
        tree.Items.Add(item);
        Assert.Equal(AutomationErrorCode.None, f.Session.InspectAsync(f.Root.RootId, oldTree).Completed().Error);
        Assert.Equal(oldTree, f.Handle(tree.AccessibilityObject.GetChild(0)!));
    }

    [Fact]
    public void HiddenHandleBecomesResolvableWhenCanonicalPeerIsExposedAgain()
    {
        using var f = new AutomationFixture();
        var b = f.Add(new Button()); var handle = f.Handle(b);
        b.AccessibilityView = AccessibilityView.Hidden;
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Inspect(b).Error);
        b.AccessibilityView = AccessibilityView.Control;
        Assert.Equal(handle, f.Inspect(b).Value!.Handle);
    }

    [Fact]
    public void SessionsRejectEachOthersHandlesEvenForSameRoot()
    {
        using var f = new AutomationFixture();
        using var other = new AutomationSession(); using var root = other.RegisterRoot(f.Form);
        var handle = f.Handle(f.Add(new Button()));
        Assert.NotEqual(f.Session.SessionId, other.SessionId);
        Assert.Equal(AutomationErrorCode.StaleNode, other.InspectAsync(root.RootId, handle).Completed().Error);
        Assert.Equal(AutomationErrorCode.StaleNode, other.PerformActionAsync(root.RootId, handle, AccessibleActions.Invoke).Completed().Error);
    }

    [Fact]
    public void ExplicitRootScopesQueriesAndRejectsCrossRootActions()
    {
        using var f = new AutomationFixture();
        var a = f.Add(new Button { Name = "same" });
        var formB = new Form(); var b = formB.Controls.Add(new Button { Name = "same" }); f.Host.Show(formB);
        using var rootB = f.Session.RegisterRoot(formB);
        Assert.Equal(f.Handle(a), f.Session.FindOneAsync(f.Root.RootId, new() { AutomationId = "same" }).Completed().Value!.Handle);
        Assert.Equal(f.Handle(b), f.Session.FindOneAsync(rootB.RootId, new() { AutomationId = "same" }).Completed().Value!.Handle);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Session.PerformActionAsync(rootB.RootId, f.Handle(a), AccessibleActions.Invoke).Completed().Error);
    }

    [Fact]
    public void RuntimeIdBeyondJavaScriptSafeIntegerIsPreservedAsString()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        const long id = 9007199254740993;
        typeof(AccessibleObject).GetField("runtime_id", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c.Child, id);
        var snapshot = f.Session.InspectAsync(f.Root.RootId, f.Handle(c.Child)).Completed().Value!;
        Assert.Equal("9007199254740993", snapshot.RuntimeId);
        Assert.Contains("\"RuntimeId\":\"9007199254740993\"", JsonSerializer.Serialize(snapshot));
    }
}
