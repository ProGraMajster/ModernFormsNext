using System.Text.Json.Nodes;
using ModernFormsNext.Designer.Clipboard;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerClipboardForestTests
{
    [Fact]
    public void MultipleRootsRoundTripAsDetachedOrderedData()
    {
        var first = Node("button1", 10); var second = Node("button2", 80);
        first.Properties["Text"] = DesignPropertyValue.FromString("First");
        Assert.True(DesignerClipboardSerializer.TrySerializeSelection([first, second], out var text, out var error), error);
        Assert.Contains("\"version\":2", text, StringComparison.Ordinal);
        Assert.Contains("\"roots\":[", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"root\":", text, StringComparison.Ordinal);
        Assert.True(DesignerClipboardSerializer.TryDeserializeSelection(text, out var copies, out error), error);
        Assert.Equal(new[] { "button1", "button2" }, copies!.Select(node => node.Name));
        Assert.Equal(first.Bounds, copies![0].Bounds);
        Assert.Equal(second.Bounds, copies[1].Bounds);
        Assert.NotSame(first, copies[0]);
        copies[0].Properties["Text"] = DesignPropertyValue.FromString("Changed copy");
        Assert.Equal("First", first.Properties["Text"].GetString());
        Assert.False(DesignerClipboardSerializer.TryDeserialize(text, out var singular, out _));
        Assert.Null(singular);
    }

    [Fact]
    public void LegacySingleRootStillWritesVersionOneAndReadsThroughBothPaths()
    {
        var first = Node("button1", 10);
        Assert.True(DesignerClipboardSerializer.TrySerialize(first, out var text, out var error), error);
        Assert.Contains("\"version\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"root\":", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"roots\":", text, StringComparison.Ordinal);
        Assert.True(DesignerClipboardSerializer.TryDeserialize(text, out var singular, out error), error);
        Assert.Equal(first.Name, singular!.Name);
        Assert.True(DesignerClipboardSerializer.TryDeserializeSelection(text, out var selection, out error), error);
        Assert.Equal(first.Name, Assert.Single(selection!).Name);
    }

    [Fact]
    public void AggregatePropertyBudgetIsSharedAcrossRootsBeforeEncoding()
    {
        var first = Node("button1", 10); var second = Node("button2", 80);
        var value = DesignPropertyValue.FromInt32(1);
        int perRoot = DesignDocumentValidator.MaximumPropertyValueCount / 2 + 1;
        foreach (var node in new[] { first, second })
            for (int index = 0; index < perRoot; index++) node.Properties["Value" + index] = value;
        Assert.False(DesignerClipboardSerializer.TrySerializeSelection([first, second], out var text, out var error));
        Assert.Null(text);
        Assert.Contains("budget", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AggregateTextBudgetCountsRepeatedValuesAtEachRoot()
    {
        var first = Node("button1", 10); var second = Node("button2", 80);
        // One bounded benign string is borrowed twice; admission must count both occurrences.
        var value = DesignPropertyValue.FromString(new string('a', DesignDocumentValidator.MaximumTextLength / 2 + 1));
        first.Properties["Text"] = second.Properties["Text"] = value;
        Assert.False(DesignerClipboardSerializer.TrySerializeSelection([first, second], out var text, out var error));
        Assert.Null(text);
        Assert.Contains("budget", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoordinateProjectionDoesNotMutateBorrowedSourceNodes()
    {
        var first = Node("button1", 10); var second = Node("button2", 80);
        var bounds = new[] { first.Bounds with { X = 30 }, second.Bounds with { X = 100 } };
        Assert.True(DesignerClipboardSerializer.TrySerializeSelection([first, second], out var text, out var error, bounds), error);
        Assert.Equal(10, first.Bounds.X);
        Assert.Equal(80, second.Bounds.X);
        Assert.True(DesignerClipboardSerializer.TryDeserializeSelection(text, out var copies, out error), error);
        Assert.Equal(bounds, copies!.Select(node => node.Bounds));
    }

    [Fact]
    public void InvalidProjectedEdgesAreRejectedBeforePublishingClipboardContent()
    {
        var first = Node("button1", 10); var second = Node("button2", 80);
        var bounds = new[] { first.Bounds, second.Bounds with { X = int.MaxValue } };
        Assert.False(DesignerClipboardSerializer.TrySerializeSelection([first, second], out var text, out _, bounds));
        Assert.Null(text);
        Assert.Equal(80, second.Bounds.X);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbiguousRootsOrUnsupportedVersionAreRejectedAsData(bool unsupportedVersion)
    {
        Assert.True(DesignerClipboardSerializer.TrySerializeSelection([Node("button1", 10), Node("button2", 80)], out var text, out _));
        var data = JsonNode.Parse(text!)!.AsObject();
        if (unsupportedVersion) data["version"] = 3;
        else data["root"] = data["roots"]![0]!.DeepClone();
        Assert.False(DesignerClipboardSerializer.TryDeserializeSelection(data.ToJsonString(), out var nodes, out _));
        Assert.Null(nodes);
    }

    [Fact]
    public void EmptyOrSharedRootSelectionCannotBeEncoded()
    {
        Assert.False(DesignerClipboardSerializer.TrySerializeSelection([], out var empty, out _));
        Assert.Null(empty);
        var node = Node("button1", 10);
        Assert.False(DesignerClipboardSerializer.TrySerializeSelection([node, node], out var shared, out _));
        Assert.Null(shared);
    }

    private static DesignControlNode Node(string name, int x)
        => new() { TypeName = "Button", Name = name, Bounds = new(x, 20, 100, 30) };
}
