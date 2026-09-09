using System.Collections.Immutable;
using System.Reflection;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Query")]
public sealed class QueryTests
{
    [Fact]
    public void RootEnumerationAndInspectionUseCanonicalRoot()
    {
        using var f = new AutomationFixture();
        var roots = f.Session.GetRootsAsync().Completed();
        Assert.Equal(AutomationErrorCode.None, roots.Error);
        var root = Assert.Single(roots.Value);
        Assert.Equal(f.Root.RootId, root.RootId);
        Assert.Equal(f.Form.AccessibilityObject.RuntimeId.ToString(), root.Handle.RuntimeId);
        var snapshot = f.Session.InspectAsync(root.RootId, root.Handle).Completed();
        Assert.Equal(AutomationErrorCode.None, snapshot.Error);
        Assert.Equal(AccessibleControlType.Window, snapshot.Value!.ControlType);
        Assert.Null(snapshot.Value.ParentRuntimeId);
    }

    [Fact]
    public void ChildrenAreDirectCanonicalEdges()
    {
        using var f = new AutomationFixture();
        var panel = f.Add(new Panel { Name = "panel" });
        var first = panel.Controls.Add(new Button { Name = "a" });
        var second = panel.Controls.Add(new TextBox { Name = "b" });
        var result = f.Session.GetChildrenAsync(f.Root.RootId, f.Handle(panel)).Completed();
        Assert.Equal(AutomationErrorCode.None, result.Error);
        Assert.Equal(new[] { f.Handle(first).RuntimeId, f.Handle(second).RuntimeId }, result.Value.Select(n => n.RuntimeId));
        Assert.All(result.Value, n => Assert.Equal(f.Handle(panel).RuntimeId, n.ParentRuntimeId));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("state")]
    public void FiltersReadSemanticMetadata(string filter)
    {
        using var f = new AutomationFixture();
        var button = f.Add(new Button { Name = "fallback", Text = "Visible label", AccessibleName = "Semantic label", AccessibleAutomationId = "save", Enabled = false });
        f.Add(new TextBox { Name = "other" });
        var query = filter switch
        {
            "id" => new AutomationQuery { AutomationId = "save" },
            "name" => new AutomationQuery { Name = "Semantic label" },
            "type" => new AutomationQuery { ControlType = AccessibleControlType.Button },
            _ => new AutomationQuery { RequiredStates = AccessibleStates.Unavailable, ExcludedStates = AccessibleStates.Checked }
        };
        var result = f.Session.FindOneAsync(f.Root.RootId, query).Completed();
        Assert.Equal(AutomationErrorCode.None, result.Error);
        Assert.Equal(f.Handle(button), result.Value!.Handle);
    }

    [Fact]
    public void AutomationIdFallsBackToNameAndMatchingIsOrdinal()
    {
        using var f = new AutomationFixture();
        f.Add(new Button { Name = "Save" });
        Assert.Single(f.Find(new() { AutomationId = "Save" }).Value);
        Assert.Empty(f.Find(new() { AutomationId = "save" }).Value);
    }

    [Theory]
    [InlineData(0, AutomationErrorCode.NodeNotFound)]
    [InlineData(1, AutomationErrorCode.None)]
    [InlineData(2, AutomationErrorCode.AmbiguousMatch)]
    public void FindOneDistinguishesZeroOneAndMultipleEvenWithOneResultBudget(int count, AutomationErrorCode expected)
    {
        using var f = new AutomationFixture(new() { MaxResults = 1 });
        for (int i = 0; i < count; i++) f.Add(new Button { AccessibleAutomationId = "same" });
        var result = f.Session.FindOneAsync(f.Root.RootId, new() { AutomationId = "same" }).Completed();
        Assert.Equal(expected, result.Error);
        Assert.Equal(expected == AutomationErrorCode.None, result.Value is not null);
    }

    [Fact]
    public void TraversalIsDepthFirstAndFindAllReportsResultTruncation()
    {
        using var f = new AutomationFixture(new() { MaxResults = 2 });
        var parent = f.Add(new Panel { AccessibleName = "match" });
        var child = parent.Controls.Add(new Button { AccessibleName = "match" });
        f.Add(new Button { AccessibleName = "match" });
        var result = f.Find(new() { Name = "match" });
        Assert.True(result.Truncated);
        Assert.Equal(AutomationErrorCode.LimitExceeded, result.Error);
        Assert.Equal(new[] { f.Handle(parent), f.Handle(child) }, result.Value.Select(n => n.Handle));
    }

    [Fact]
    public void LogicalListAndTreeItemsAndCustomChildrenAreQueryable()
    {
        using var f = new AutomationFixture();
        var list = f.Add(new ListBox()); list.Items.Add("list item");
        var tree = f.Add(new TreeView()); tree.Items.Add(new TreeViewItem { Text = "tree item" });
        var custom = f.Add(new SemanticControl()); custom.Child.Name = "custom item";
        foreach (var name in new[] { "list item", "tree item", "custom item" })
            Assert.Equal(AutomationErrorCode.None, f.Session.FindOneAsync(f.Root.RootId, new() { Name = name }).Completed().Error);
        Assert.Single(f.Find(new() { ControlType = AccessibleControlType.ListItem }).Value);
        Assert.Single(f.Find(new() { ControlType = AccessibleControlType.TreeItem }).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenAndInvisibleControlsAreNotExposed(bool hiddenView)
    {
        using var f = new AutomationFixture();
        var button = f.Add(new Button { Name = "hidden" });
        bool disposed = false;
        button.Disposed += (_, _) => disposed = true;
        if (hiddenView) button.AccessibilityView = AccessibilityView.Hidden; else button.Visible = false;
        Assert.Empty(f.Find(new() { AutomationId = "hidden" }).Value);
        Assert.Equal(AutomationErrorCode.NodeUnavailable, f.Inspect(button).Error);
        Assert.False(disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DepthAndNodeLimitsCannotCertifyAbsenceOrUniqueness(bool depth)
    {
        using var f = new AutomationFixture(depth ? new() { MaxDepth = 0 } : new() { MaxNodes = 1 });
        f.Add(new Button { Name = "target" });
        var result = f.Session.FindOneAsync(f.Root.RootId, new() { AutomationId = "target" }).Completed();
        Assert.Equal(AutomationErrorCode.LimitExceeded, result.Error);
        Assert.True(result.Truncated);
        Assert.Null(result.Value);
        var root = f.Session.GetRootsAsync().Completed().Value[0];
        Assert.True(f.Session.InspectAsync(root.RootId, root.Handle).Completed().Value!.Truncated);
    }

    [Fact]
    public void ChildCycleIsReportedWithoutRecursion()
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl()); c.Child.Children.Add(c.Child);
        var result = f.Find(new());
        Assert.Contains(result.Issues, i => i.Code == AutomationErrorCode.CycleDetected);
    }

    [Fact]
    public void ParentCycleIsReportedWithoutFollowingIt()
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl()); _ = c.AccessibilityObject; c.Child.ParentValue = c.Child;
        Assert.Contains(f.Find(new()).Issues, i => i.Code == AutomationErrorCode.CycleDetected);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("null")]
    [InlineData("parent")]
    [InlineData("getter")]
    public void MalformedPeersProduceStructuredErrorsAndSessionRemainsUsable(string kind)
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl());
        switch (kind)
        {
            case "count": c.Child.CountGetter = () => -1; break;
            case "null": c.Child.CountGetter = () => 1; break;
            case "parent": c.Child.Add(new ScriptPeer()).ParentValue = null; break;
            case "getter": c.Child.NameGetter = () => throw new Exception("private"); break;
        }
        Assert.NotEqual(AutomationErrorCode.None, f.Find(new()).Error);
        c.Dispose();
        Assert.Equal(AutomationErrorCode.None, f.Find(new()).Error);
        Assert.False(f.Session.IsStopped);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedObjectAndCorruptDuplicateRuntimeIdAreRejected(bool distinctObject)
    {
        using var f = new AutomationFixture();
        var c = f.Add(new SemanticControl());
        var child = c.Child.Add(new ScriptPeer());
        var duplicate = distinctObject ? c.Child.Add(new ScriptPeer()) : child;
        if (distinctObject)
        {
            // RuntimeId is nonvirtual and unique in normal #59 code. Corrupt only a test peer's
            // private readonly field to prove the consumer fails closed on duplicate identity.
            typeof(AccessibleObject).GetField("runtime_id", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(duplicate, child.RuntimeId);
        }
        else c.Child.Children.Add(duplicate);
        Assert.Contains(f.Find(new()).Issues, i => i.Code == AutomationErrorCode.DuplicateRuntimeId);
        Assert.NotEqual(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, f.Handle(child), AccessibleActions.Invoke).Completed().Status);
    }

    [Fact]
    public void ExplodingChildCountIsBoundedEvenWhenEveryChildIsNull()
    {
        using var f = new AutomationFixture(new() { MaxNodes = 16 });
        var c = f.Add(new SemanticControl());
        int calls = 0;
        c.Child.CountGetter = () => int.MaxValue;
        c.Child.ChildGetter = _ => { calls++; return null; };
        var result = f.Find(new());
        Assert.True(result.Truncated);
        Assert.InRange(calls, 1, 16);
        Assert.InRange(result.Issues.Length, 1, 16);
    }

    [Fact]
    public void SnapshotIsDetachedAndChildrenAreImmutable()
    {
        using var f = new AutomationFixture();
        var b = f.Add(new Button { Text = "before" });
        var first = f.Inspect(b).Value!;
        b.Text = "after";
        var second = f.Inspect(b).Value!;
        Assert.Equal("before", first.Name);
        Assert.Equal("after", second.Name);
        Assert.NotEqual(first.CaptureId, second.CaptureId);
        Assert.IsType<ImmutableArray<string>>(first.ChildRuntimeIds);
        Assert.All(typeof(AutomationNodeSnapshot).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void InvalidQueryAndHandlePayloadsAreStructured()
    {
        using var f = new AutomationFixture();
        Assert.Equal(AutomationErrorCode.InvalidArgument,
            f.Find(new() { RequiredStates = AccessibleStates.Checked, ExcludedStates = AccessibleStates.Checked }).Error);
        Assert.Equal(AutomationErrorCode.InvalidArgument,
            f.Session.InspectAsync(f.Root.RootId, new(f.Session.SessionId, "900719925474099300000")).Completed().Error);
        Assert.Equal(AutomationErrorCode.InvalidArgument,
            f.Session.InspectAsync(f.Root.RootId, new(f.Session.SessionId, "01")).Completed().Error);
    }

    [Fact]
    public void RootResultLimitIsExplicit()
    {
        using var f = new AutomationFixture(new() { MaxResults = 1 });
        var other = new Form(); f.Host.Show(other); using var root = f.Session.RegisterRoot(other);
        var result = f.Session.GetRootsAsync().Completed();
        Assert.Single(result.Value);
        Assert.True(result.Truncated);
        Assert.Equal(AutomationErrorCode.LimitExceeded, result.Error);
    }

    [Fact]
    public void ErrorCollectionsAreInitializedAndSafeToSerialize()
    {
        using var f = new AutomationFixture(); var handle = f.Handle(f.Add(new Button()));
        var children = f.Session.GetChildrenAsync("missing root", handle).Completed();
        Assert.False(children.Value.IsDefault);
        Assert.Contains("\"Value\":[]", System.Text.Json.JsonSerializer.Serialize(children));
        f.Session.Stop();
        var roots = f.Session.GetRootsAsync().Completed();
        Assert.False(roots.Value.IsDefault);
        Assert.Contains("\"Value\":[]", System.Text.Json.JsonSerializer.Serialize(roots));
    }

    [Fact]
    public void FormClientAreaIsValidatedButNeverProjected()
    {
        using var f = new AutomationFixture(); var b = f.Add(new Button());
        var canonicalParent = b.AccessibilityObject.Parent!;
        Assert.NotSame(f.Form.AccessibilityObject, canonicalParent);
        Assert.Same(f.Form.AccessibilityObject, canonicalParent.Parent);
        var result = f.Find(new());
        Assert.Equal(AutomationErrorCode.None, result.Error);
        Assert.DoesNotContain(result.Value, n => n.RuntimeId == canonicalParent.RuntimeId.ToString());
        Assert.Equal(f.Handle(f.Form.AccessibilityObject).RuntimeId,
            Assert.Single(result.Value, n => n.Handle == f.Handle(b)).ParentRuntimeId);
    }

    [Theory]
    [InlineData("detached", AutomationErrorCode.MalformedTree)]
    [InlineData("cycle", AutomationErrorCode.CycleDetected)]
    [InlineData("depth", AutomationErrorCode.LimitExceeded)]
    public void OmittedParentChainsMustReachTraversedParentWithinBudget(string kind, AutomationErrorCode expected)
    {
        using var f = new AutomationFixture(new() { MaxDepth = 4 }); var c = f.Add(new SemanticControl());
        _ = c.AccessibilityObject;
        var omitted = new ScriptPeer(); c.Child.ParentValue = omitted;
        if (kind == "cycle") omitted.ParentValue = c.Child;
        if (kind == "depth")
        {
            var current = omitted;
            for (int i = 0; i < 6; i++) { var next = new ScriptPeer(); current.ParentValue = next; current = next; }
            current.ParentValue = c.AccessibilityObject;
        }
        var result = f.Find(new());
        Assert.Contains(result.Issues, i => i.Code == expected && i.Property == AutomationProperty.Parent);
        Assert.DoesNotContain(result.Value, n => n.Handle == f.Handle(c.Child));
        int actions = 0; c.Child.Action = (_, _) => { actions++; return true; };
        Assert.NotEqual(AutomationActionStatus.Accepted,
            f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.Invoke).Completed().Status);
        Assert.Equal(0, actions);
    }

}
