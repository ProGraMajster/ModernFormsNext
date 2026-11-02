using System.Drawing;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerTextBaselineTests
{
    [Theory]
    [InlineData(ContentAlignment.TopLeft, 12)]
    [InlineData(ContentAlignment.MiddleLeft, 30)]
    [InlineData(ContentAlignment.BottomRight, 18)]
    public void InertProjectionMatchesTheProductionTextAndImageLayout(ContentAlignment alignment, int fontSize)
    {
        var node = new DesignControlNode { TypeName = "Label", Name = "label", Bounds = new(0, 0, 220, 70) };
        node.Properties["Text"] = DesignPropertyValue.FromString("Baseline sample");
        node.Properties["TextAlign"] = DesignerPropertyValueEditor.ToDesignPropertyValue(alignment, typeof(ContentAlignment));
        node.Properties["Padding"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new Padding(5, 7, 9, 11), typeof(Padding));
        node.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(fontSize);
        using var reference = new Label { Text = "Baseline sample", Bounds = new Rectangle(0, 0, 220, 70),
            TextAlign = alignment, Padding = new Padding(5, 7, 9, 11) };
        reference.Style.FontSize = fontSize;
        // Compare neutral-property admission with an independently configured production control.
        Assert.Equal(Assert.IsType<double>(DesignerTextBaseline.MeasureControl(reference)),
            Assert.IsType<double>(DesignerTextBaseline.Measure(node, DesignerSnapBounds.From(node.Bounds))), precision: 5);
    }

    [Fact]
    public void DifferentFontSizesSnapTheirProductionBaselinesInsteadOfTheirRectangles()
    {
        using var session = new DesignerSession();
        var first = session.Document.Controls.AddNode("Label", "moving", new(10, 20, 160, 70));
        var second = session.Document.Controls.AddNode("Label", "target", new(300, 100, 160, 70));
        foreach (var node in new[] { first, second })
        {
            node.Properties["Text"] = DesignPropertyValue.FromString("Baseline");
            node.Properties["TextAlign"] = DesignerPropertyValueEditor.ToDesignPropertyValue(ContentAlignment.TopLeft, typeof(ContentAlignment));
        }
        first.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(12);
        second.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(30);
        double start = first.Bounds.Y + Assert.IsType<double>(DesignerTextBaseline.Measure(first, DesignerSnapBounds.From(first.Bounds)));
        double target = second.Bounds.Y + Assert.IsType<double>(DesignerTextBaseline.Measure(second, DesignerSnapBounds.From(second.Bounds)));
        long expected = (long)Math.Round(target - start, MidpointRounding.AwayFromZero);
        session.SelectNode(first);
        var context = Assert.IsType<DesignerSnappingContext>(DesignerSnappingContext.Create(session, [first]));
        var result = context.Move(0, expected - 2, 1, false);
        Assert.Equal(expected, result.Y);
        Assert.Contains(context.Guides, guide => guide.Kind == DesignerSnapGuideKind.Baseline);
        Assert.InRange(Math.Abs(start + result.Y - target), 0, 0.5);
    }

    [Theory]
    [InlineData("Example.Controls.CustomLabel", "short text", false)]
    [InlineData("Label", "first\nsecond", false)]
    [InlineData("Label", "short text", true)]
    public void UnsupportedOrMultilineDataDoesNotInventABaseline(string type, string text, bool multiline)
    {
        var node = new DesignControlNode { TypeName = type, Name = "sample", Bounds = new(0, 0, 200, 60) };
        node.Properties["Text"] = DesignPropertyValue.FromString(text);
        node.Properties["Multiline"] = DesignPropertyValue.FromBoolean(multiline);
        Assert.Null(DesignerTextBaseline.Measure(node, DesignerSnapBounds.From(node.Bounds)));
    }

    [Fact]
    public void LongBenignTextIsExcludedBeforeShaping()
    {
        var node = new DesignControlNode { TypeName = "Label", Name = "sample", Bounds = new(0, 0, 200, 60) };
        node.Properties["Text"] = DesignPropertyValue.FromString(new string('a', DesignerTextBaseline.MaximumTextLength + 1));
        Assert.Null(DesignerTextBaseline.Measure(node, DesignerSnapBounds.From(node.Bounds)));
    }
}
