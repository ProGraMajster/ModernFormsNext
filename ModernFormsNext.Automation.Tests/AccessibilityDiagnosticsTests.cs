using System.Drawing;
using System.Text.Json;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

public sealed class AccessibilityDiagnosticsTests
{
    [Fact]
    public void DetachedAnalysisPreservesCanonicalIdentityAndNeverReadsOrInvokesLiveControls()
    {
        using var f = new AutomationFixture();
        var button = f.Add(new DiagnosticButton { AccessibleName = "", Text = "private-display-text" });
        var capture = f.Find(new());
        button.RejectReads = true;
        AccessibilityDiagnosticReport? report = null;
        CompletedTaskAssertions.Worker(() => report = AccessibilityDiagnostics.Analyze(capture));
        var diagnostic = Assert.Single(report!.Diagnostics);
        Assert.Equal(AccessibilityDiagnosticCode.MissingInteractiveName, diagnostic.Code);
        Assert.Equal(f.Handle(button), diagnostic.Handle);
        Assert.Equal(f.Root.RootId, diagnostic.RootId);
        Assert.Equal(capture.CaptureId, diagnostic.CaptureId);
        Assert.False(report.CoverageIncomplete);
        Assert.Equal(0, button.Actions);
        Assert.DoesNotContain("private-display-text", JsonSerializer.Serialize(report));
        button.RejectReads = false;
    }

    [Fact]
    public void CanonicalNameFallbackIsAcceptedAndExplicitEmptyNameIsReported()
    {
        using var f = new AutomationFixture();
        f.Add(new Button { Text = "Save" });
        f.Add(new TextBox { Name = "search", Text = "" });
        var empty = f.Add(new Button { AccessibleName = " \t", Text = "ignored fallback" });
        var report = AccessibilityDiagnostics.Analyze(f.Find(new()));
        Assert.Equal(f.Handle(empty), Assert.Single(report.Diagnostics).Handle);
    }

    [Fact]
    public void RedactedAndGetterFailedNodesDoNotBecomeMissingNameFindings()
    {
        using var f = new AutomationFixture();
        var password = f.Add(new TextBox { AccessibleName = "", Text = "secret-value" });
        password.TextInputOptions = password.TextInputOptions with { Scope = TextInputScope.Password };
        var failed = f.Add(new DiagnosticButton { AccessibleName = "", RejectReads = true });
        var capture = f.Find(new());
        Assert.NotEmpty(capture.Issues);
        var report = AccessibilityDiagnostics.Analyze(capture);
        Assert.Empty(report.Diagnostics);
        Assert.True(report.CoverageIncomplete);
        Assert.DoesNotContain("secret-value", JsonSerializer.Serialize(report));
        failed.RejectReads = false;
    }

    [Fact]
    public void CaptureAndDiagnosticLimitsRemainExplicit()
    {
        using var f = new AutomationFixture();
        for (int i = 0; i < 5; i++) f.Add(new Button { AccessibleName = "" });
        var capture = f.Find(new());
        var report = AccessibilityDiagnostics.Analyze(capture, 2);
        Assert.Equal(2, report.Diagnostics.Length);
        Assert.True(report.Truncated);
        Assert.True(report.CoverageIncomplete);
        Assert.Throws<ArgumentOutOfRangeException>(() => AccessibilityDiagnostics.Analyze(capture, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AccessibilityDiagnostics.Analyze(capture, 4097));
    }

    [Fact]
    public void TruncatedNodeDoesNotBecomeDefiniteMissingName()
    {
        using var f = new AutomationFixture(new() { MaxDepth = 1 });
        var parent = f.Add(new Button { AccessibleName = "" });
        parent.Controls.Add(new Button { AccessibleName = "" });
        var capture = f.Find(new());
        Assert.True(capture.Truncated);
        var report = AccessibilityDiagnostics.Analyze(capture);
        Assert.Empty(report.Diagnostics);
        Assert.True(report.CoverageIncomplete);
        Assert.False(report.Truncated);
    }

    [Fact]
    public void ProvableStateBoundsAndReadOnlyActionConflictsAreReportedWithoutActions()
    {
        using var f = new AutomationFixture();
        var button = f.Add(new DiagnosticButton { AccessibleName = "Action", Malformed = true });
        var report = AccessibilityDiagnostics.Analyze(f.Find(new()));
        Assert.Equal(new[] { AccessibilityDiagnosticCode.ContradictoryExpansionState,
            AccessibilityDiagnosticCode.InvalidBounds, AccessibilityDiagnosticCode.InvalidRange },
            report.Diagnostics.Select(d => d.Code));
        Assert.All(report.Diagnostics, d => Assert.Equal(f.Handle(button), d.Handle));
        Assert.Equal(0, button.Actions);
    }

    [Fact]
    public void GridCoordinatesAreCheckedOnlyAgainstACapturedContainingGrid()
    {
        using var f = new AutomationFixture();
        var grid = f.Add(new DiagnosticGrid());
        var all = AccessibilityDiagnostics.Analyze(f.Find(new()));
        var diagnostic = Assert.Single(all.Diagnostics);
        Assert.Equal(AccessibilityDiagnosticCode.InvalidGridCoordinates, diagnostic.Code);
        Assert.Equal(f.Handle(grid.Cell), diagnostic.Handle);
        var filtered = AccessibilityDiagnostics.Analyze(f.Find(new() { Name = "Cell" }));
        Assert.Empty(filtered.Diagnostics); // The omitted grid is not a broken association.
    }

    private sealed class DiagnosticButton : Button
    {
        internal bool RejectReads, Malformed;
        internal int Actions;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(DiagnosticButton owner) : ControlAccessibleObject(owner)
        {
            public override string? Name { get => owner.RejectReads ? throw new InvalidOperationException("private exception") : base.Name; set => base.Name = value; }
            public override AccessibleStates State => base.State | (owner.Malformed ? AccessibleStates.Expanded | AccessibleStates.Collapsed : 0);
            public override Rectangle Bounds => owner.Malformed ? new(0, 0, -1, 10) : base.Bounds;
            public override AccessibleRangeValue? RangeValue => owner.Malformed ? new(2, 0, 10, 1, 1, true) : null;
            public override AccessibleActions SupportedActions => owner.Malformed ? AccessibleActions.SetValue : base.SupportedActions;
            public override bool PerformAction(AccessibleActions action, object? parameter = null) { owner.Actions++; return base.PerformAction(action, parameter); }
        }
    }

    private sealed class DiagnosticGrid : Control
    {
        internal AccessibleObject Cell => AccessibilityObject.GetChild(0)!;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer : ControlAccessibleObject
        {
            private readonly CellPeer cell;
            internal Peer(Control owner) : base(owner) { cell = new(this); }
            public override AccessibleGridProvider GridProvider => new Provider(cell);
            public override int GetChildCount() => 1;
            public override AccessibleObject GetChild(int index) => cell;
        }
        private sealed class CellPeer(AccessibleObject parent) : AccessibleObject
        {
            public override string? Name { get => "Cell"; set { } }
            public override AccessibleObject Parent => parent;
            public override AccessibleGridCellInfo GridCell => new(parent, 4, 0);
        }
        private sealed class Provider(AccessibleObject cell) : AccessibleGridProvider
        {
            public override int RowCount => 1;
            public override int ColumnCount => 1;
            public override AccessibleObject? GetItem(int row, int column) => row == 0 && column == 0 ? cell : null;
        }
    }
}
