using System.Drawing;
using System.Text.Json;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

public sealed class GridSnapshotTests
{
    [Fact]
    public void GridSnapshotsContainDetachedCoordinatesAndSameRootHeaderIds()
    {
        using var f = new AutomationFixture();
        var grid = f.Add(new DataGridView { Bounds = new Rectangle(0, 0, 400, 200), RowHeadersVisible = true });
        grid.Columns.Add("Name", 140); grid.Columns.Add("Value", 140);
        grid.Rows.Add("a", "first"); grid.Rows.Add("z", "last");
        var peer = grid.AccessibilityObject.GridProvider!.GetItem(0, 1)!;
        var snapshot = f.Session.InspectAsync(f.Root.RootId, f.Handle(peer)).Completed().Value!;
        var metadata = snapshot.GridCell!;
        Assert.Equal(f.Handle(grid).RuntimeId, metadata.GridRuntimeId);
        Assert.Equal(0, metadata.Row); Assert.Equal(1, metadata.Column);
        Assert.Single(metadata.ColumnHeaderRuntimeIds);
        Assert.Single(metadata.RowHeaderRuntimeIds);
        var root = f.Inspect(grid).Value!;
        Assert.Equal(new AutomationGridInfo(2, 2, true, AccessibleTableTraversal.RowMajor), root.GridInfo);
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.Equal(0, metadata.Row);
        Assert.Equal(1, f.Session.InspectAsync(f.Root.RootId, f.Handle(peer)).Completed().Value!.GridCell!.Row);
        Assert.DoesNotContain("BoundItem", JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void GridCellActionsUseTheExistingSessionAndCanonicalEditRoute()
    {
        using var f = new AutomationFixture();
        var grid = f.Add(new DataGridView { Size = new Size(300, 150) });
        grid.Columns.Add("Name"); grid.Rows.Add("before");
        var peer = grid.AccessibilityObject.GridProvider!.GetItem(0, 0)!;
        var result = f.Session.PerformActionAsync(f.Root.RootId, f.Handle(peer), AccessibleActions.SetValue,
            AutomationActionValue.FromText("after")).Completed();
        Assert.Equal(AutomationActionStatus.Accepted, result.Status);
        Assert.Equal("after", grid.Rows[0].Cells[0].Value);
        Assert.False(grid.IsCurrentCellInEditMode);
        grid.Rows.Clear();
        Assert.NotEqual(AutomationActionStatus.Accepted, f.Session.PerformActionAsync(f.Root.RootId, f.Handle(peer),
            AccessibleActions.SetValue, AutomationActionValue.FromText("unsafe")).Completed().Status);
    }

    [Fact]
    public void PasswordInputScopeIsAFrameworkPrivacySafeguardEvenForCustomPeer()
    {
        using var f = new AutomationFixture();
        var editor = f.Add(new PasswordScopeEditor { Text = "private-value" });
        editor.TextInputOptions = editor.TextInputOptions with { Scope = TextInputScope.Password };
        var result = f.Inspect(editor).Value!;
        Assert.NotEqual(AutomationRedaction.None, result.Redaction);
        Assert.Null(result.Name); Assert.Null(result.Value); Assert.Null(result.GridInfo); Assert.Null(result.GridCell);
        Assert.DoesNotContain("private-value", JsonSerializer.Serialize(result));
    }

    private sealed class PasswordScopeEditor : TextBox
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new UnsafePeer(this);
        private sealed class UnsafePeer(TextBox owner) : ControlAccessibleObject(owner)
        {
            public override bool IsSensitive => false;
            public override AccessibleStates State => AccessibleStates.None;
            public override string? Name { get => "private-value"; set { } }
            public override string? Value { get => "private-value"; set { } }
        }
    }
}
