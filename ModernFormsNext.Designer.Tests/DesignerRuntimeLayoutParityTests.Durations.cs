using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(9_007_199_254_740_993L)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    public void DurationTicksSurviveEditorJsonParserAndCompiledRuntimeWithoutRounding(long ticks)
    {
        var duration = TimeSpan.FromTicks(ticks);
        Assert.True(TimeSpanDesignValue.TryRead(DesignPropertyValue.FromObject(duration), out var direct, out _));
        Assert.Equal(duration, direct);
        string text = DesignerPropertyValueEditor.ToDisplayString(duration);
        Assert.True(DesignerPropertyValueEditor.TryConvert(text, typeof(TimeSpan), out var edited, out var error), error);
        Assert.Equal(duration, Assert.IsType<TimeSpan>(edited));
        var document = BasicDocument();
        var panel = Node("durationData", nameof(Panel), 0, 0, 100, 100);
        panel.Properties["Tag"] = DesignerPropertyValueEditor.ToDesignPropertyValue(edited, typeof(TimeSpan));
        document.Controls.Add(panel);

        var parsed = RoundTripPageDocument(document);
        var stored = Assert.Single(parsed.Controls).Properties["Tag"];
        Assert.True(TimeSpanDesignValue.TryRead(stored, out var decoded, out error), error);
        Assert.Equal(ticks, decoded.Ticks);
        using var root = CompileAndCreateGeneratedRoot(parsed);
        var runtime = Assert.IsType<Panel>(Assert.Single(Assert.IsAssignableFrom<Control>(root.Instance).Controls));
        Assert.Equal(ticks, Assert.IsType<TimeSpan>(runtime.Tag).Ticks);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0.00001")]
    [InlineData("922337203685477.5808")]
    [InlineData("-922337203685477.5809")]
    public void InvalidOrSubTickMillisecondsCannotBecomeADuration(string text)
        => Assert.False(DesignerPropertyValueEditor.TryConvert(text, typeof(TimeSpan), out _, out _));
}
