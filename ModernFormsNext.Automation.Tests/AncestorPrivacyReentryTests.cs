using System.Text.Json;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Privacy")]
public sealed class AncestorPrivacyReentryTests
{
    private const string Secret = "PRIVATE_ANCESTOR_GRID_PAYLOAD_59";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetterProtectingLiveAncestorDiscardsEarlierPayload(bool gridGetter)
    {
        using var f = new AutomationFixture();
        var parent = f.Add(new PrivacyPanel());
        var child = parent.Controls.Add(new GridProbe());
        child.OnPayload = stage => { if (stage == (gridGetter ? "grid" : "name")) parent.Protected = true; };
        var snapshot = f.Inspect(child).Value!;
        Assert.NotEqual(AutomationRedaction.None, snapshot.Redaction);
        Assert.Null(snapshot.Name);
        Assert.Null(snapshot.Value);
        Assert.Null(snapshot.RangeValue);
        Assert.Null(snapshot.GridInfo);
        Assert.Null(snapshot.GridCell);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        Assert.Equal(gridGetter ? 1 : 0, child.ValueReads);
        Assert.Equal(0, child.CellReads);
    }

    [Fact]
    public void FartherAncestorsPrivacyGetterCannotProtectAlreadyCheckedParentUnnoticed()
    {
        using var f = new AutomationFixture();
        var outer = f.Add(new PrivacyPanel());
        var parent = outer.Controls.Add(new PrivacyPanel());
        var child = parent.Controls.Add(new GridProbe());
        bool armed = false;
        child.OnPayload = stage => armed |= stage == "name";
        outer.OnPrivacyRead = () => { if (armed) parent.Protected = true; };
        var snapshot = f.Inspect(child).Value!;
        Assert.NotEqual(AutomationRedaction.None, snapshot.Redaction);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        Assert.Equal(0, child.ValueReads);
        Assert.Equal(0, child.CellReads);
    }

    [Fact]
    public void ParentReplacementDuringPrivacyVerificationFailsClosed()
    {
        using var f = new AutomationFixture();
        var outer = f.Add(new PrivacyPanel());
        var replacement = f.Add(new PrivacyPanel { Protected = true });
        var parent = outer.Controls.Add(new PrivacyPanel());
        var child = parent.Controls.Add(new GridProbe());
        bool armed = false;
        child.OnPayload = stage => armed |= stage == "name";
        outer.OnPrivacyRead = () =>
        {
            if (!armed) return;
            armed = false;
            outer.Controls.Remove(parent);
            replacement.Controls.Add(parent);
        };
        var snapshot = f.Inspect(child).Value!;
        Assert.True(snapshot.Redaction.HasFlag(AutomationRedaction.PrivacyUnknown));
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        Assert.Equal(0, child.ValueReads);
    }

    [Fact]
    public void AlreadyProtectedAncestorSkipsAllPayloadGetters()
    {
        using var f = new AutomationFixture();
        var parent = f.Add(new PrivacyPanel { Protected = true });
        var child = parent.Controls.Add(new GridProbe());
        int reads = 0;
        child.OnPayload = _ => reads++;
        var snapshot = f.Inspect(child).Value!;
        Assert.NotEqual(AutomationRedaction.None, snapshot.Redaction);
        Assert.Equal(0, reads);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
    }

    [Fact]
    public void CyclicParentIntroducedByPayloadGetterFailsClosedWithoutMorePayloadReads()
    {
        using var f = new AutomationFixture();
        var child = f.Add(new GridProbe());
        child.OnPayload = stage => { if (stage == "name") child.SelfCycle = true; };
        var result = f.Inspect(child);
        Assert.Contains(result.Issues, issue => issue.Code == AutomationErrorCode.CycleDetected);
        Assert.True(result.Value!.Redaction.HasFlag(AutomationRedaction.PrivacyUnknown));
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.Equal(0, child.ValueReads);
    }

    private sealed class PrivacyPanel : Panel
    {
        internal bool Protected;
        internal Action? OnPrivacyRead;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(PrivacyPanel owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleStates State
            {
                get
                {
                    owner.OnPrivacyRead?.Invoke();
                    return base.State | (owner.Protected ? AccessibleStates.Protected : 0);
                }
            }
        }
    }

    private sealed class GridProbe : Control
    {
        internal Action<string>? OnPayload;
        internal int ValueReads, CellReads;
        internal bool SelfCycle;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(GridProbe owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleObject? Parent => owner.SelfCycle ? this : base.Parent;
            public override string? Name { get { owner.OnPayload?.Invoke("name"); return Secret; } set { } }
            public override string? Value { get { owner.ValueReads++; owner.OnPayload?.Invoke("value"); return Secret; } set { } }
            public override AccessibleRangeValue? RangeValue => new(7, 0, 10, 1, 2, false);
            public override AccessibleGridProvider? GridProvider { get { owner.OnPayload?.Invoke("grid"); return new Grid(); } }
            public override AccessibleGridCellInfo? GridCell { get { owner.CellReads++; return null; } }
        }
        private sealed class Grid : AccessibleGridProvider
        {
            public override int RowCount => 1;
            public override int ColumnCount => 1;
            public override AccessibleObject? GetItem(int row, int column) => null;
        }
    }
}
