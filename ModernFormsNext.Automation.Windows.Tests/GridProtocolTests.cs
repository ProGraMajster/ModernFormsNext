using System.Collections.Immutable;
using System.Text.Json.Nodes;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

public sealed class GridProtocolTests
{
    [Fact]
    public void PreviousSnapshotWithoutGridFieldsRemainsReadableAndOmittedOnWrite()
    {
        var snapshot = Protocol.Read<AutomationNodeSnapshot>(BaseSnapshot());
        Assert.Null(snapshot.GridInfo); Assert.Null(snapshot.GridCell);
        var wire = Protocol.Element(snapshot);
        Assert.False(wire.TryGetProperty("gridInfo", out _));
        Assert.False(wire.TryGetProperty("gridCell", out _));
    }

    [Fact]
    public void GridMetadataRoundTripsAsDetachedNumbersAndDecimalIds()
    {
        var wire = JsonNode.Parse(BaseSnapshot().GetRawText())!.AsObject();
        wire["gridInfo"] = JsonNode.Parse(Protocol.Element(new AutomationGridInfo(10, 2, true, AccessibleTableTraversal.RowMajor)).GetRawText());
        wire["gridCell"] = JsonNode.Parse(Protocol.Element(new AutomationGridCellInfo("9223372036854775806", 3, 1, 1, 1,
            ImmutableArray.Create("17"), ImmutableArray.Create("19"))).GetRawText());
        var snapshot = Protocol.Read<AutomationNodeSnapshot>(System.Text.Json.JsonSerializer.SerializeToElement(wire));
        var roundTrip = Protocol.Read<AutomationNodeSnapshot>(Protocol.Element(snapshot));
        Assert.Equal(10, roundTrip.GridInfo!.Rows);
        Assert.Equal(2, roundTrip.GridInfo.Columns);
        Assert.Equal("9223372036854775806", roundTrip.GridCell!.GridRuntimeId);
        Assert.Equal(3, roundTrip.GridCell.Row);
        Assert.Equal("17", Assert.Single(roundTrip.GridCell.RowHeaderRuntimeIds));
        Assert.Equal("19", Assert.Single(roundTrip.GridCell.ColumnHeaderRuntimeIds));
        Assert.Equal(1, Protocol.Version);
    }

    private static System.Text.Json.JsonElement BaseSnapshot() => Protocol.Element(new
    {
        handle = new AutomationNodeHandle("session", "1"), rootId = "root", automationId = (string?)null,
        name = "grid", role = AccessibleRole.Table, controlType = AccessibleControlType.DataGrid,
        states = AccessibleStates.None, supportedActions = AccessibleActions.None, value = (string?)null,
        rangeValue = (AccessibleRangeValue?)null,
        bounds = new AutomationBounds(0, 0, 100, 100, AutomationCoordinateSpace.Surface), parentRuntimeId = (string?)null,
        childRuntimeIds = Array.Empty<string>(), redaction = AutomationRedaction.None, captureId = "capture", truncated = false
    });
}
