using System.Drawing;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerRtlPlaceholderTests
{
    [Fact]
    public void FlowUsesAuthoredCustomBoundsWithoutResolvingOrRunningApplicationTypes()
    {
        var flow = Node("flow", "FlowLayoutPanel", 260, 100);
        var external = Node("badge", "Example.Badge", 70, 35);
        external.Properties["UserSetting"] = DesignPropertyValue.FromString("representative metadata");
        external.Properties["AutoSize"] = DesignPropertyValue.FromBoolean(true);
        flow.Children.Add(external);
        using var preview = DesignerPagePreview.TryCreate(flow, new Size(260, 100), out var error, RightToLeft.Yes);
        Assert.Null(error);
        Assert.NotNull(preview);
        var placeholder = preview.Controls[external];
        Assert.IsType<Control>(placeholder);
        Assert.Equal(new Size(70, 35), placeholder.Size);
        Assert.False(placeholder.AutoSize);
        Assert.Equal(RightToLeft.Yes, placeholder.RightToLeft);
        Assert.True(placeholder.Left > 150);
        Assert.Equal("representative metadata", external.Properties["UserSetting"].Value);
    }

    [Theory]
    [InlineData("ModernFormsNext.RightToLeft", "Sideways")]
    [InlineData("ModernFormsNext.RightToLeft", "1")]
    [InlineData("Example.Direction", "Yes")]
    public void KnownBasePropertiesOnAnAuthoredPlaceholderStillRejectInvalidValues(string enumType, string value)
    {
        var table = Node("table", "TableLayoutPanel", 260, 100);
        var external = Node("badge", "Example.Badge", 70, 35);
        external.Properties[nameof(Control.RightToLeft)] = DesignPropertyValue.FromEnum(enumType, value);
        table.Children.Add(external);
        using var preview = DesignerPagePreview.TryCreate(table, new Size(260, 100), out var error);
        Assert.Null(preview);
        Assert.NotNull(error);
    }

    [Fact]
    public void FrameworkMetadataKeepsTheAuthoredInheritDefault()
    {
        var metadata = new DesignMetadataReader().ReadProperty(typeof(Control).GetProperty(nameof(Control.RightToLeft))!);
        Assert.True(metadata.HasDefaultValue);
        Assert.Equal(RightToLeft.Inherit, metadata.DefaultValue);
    }

    [Fact]
    public void FlowProjectionRetainsTheExistingRuntimePreviewNodeBudget()
    {
        var flow = Node("flow", "FlowLayoutPanel", 260, 100);
        for (int i = 0; i < DesignerPagePreview.MaximumNodes; i++)
            flow.Children.Add(Node("child" + i, "Panel", 10, 10));
        using var preview = DesignerPagePreview.TryCreate(flow, new Size(260, 100), out var error);
        Assert.Null(preview);
        Assert.Contains("256", error);
        Assert.Equal(DesignerPagePreview.MaximumNodes, flow.Children.Count);
    }

    private static DesignControlNode Node(string name, string type, int width, int height)
        => new() { Name = name, TypeName = type, Bounds = new DesignBounds(0, 0, width, height) };
}
