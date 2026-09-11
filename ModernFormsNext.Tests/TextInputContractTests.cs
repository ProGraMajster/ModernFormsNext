using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TextInputContractTests
{
    [Fact]
    public void BoundedSnapshotKeepsAbsoluteRangesOutsideTheReturnedTextWindow()
    {
        var state = new TextInputState("slice", 100, 1_000_000, 500_000, 90,
            10, 900_000, 17, new Rect(2, 3, 1, 20), new(), 19);
        Assert.Equal(100, state.TextStart);
        Assert.Equal(1_000_000, state.DocumentLength);
        Assert.Equal(500_000, state.SelectionStart);
        Assert.Equal(90, state.SelectionEnd);
        Assert.True(state.HasComposition);
        var transformed = state.WithCaretRectangle(new Rect(20, 30, 2, 40), 64);
        Assert.Same(state.Text, transformed.Text);
        Assert.Same(state.Options, transformed.Options);
        Assert.Equal(64, transformed.CaretBaseline);
        Assert.Equal(19, state.CaretBaseline);
    }

    [Fact]
    public void SnapshotRejectsOversizedPayloadsInvalidOffsetsAndNonfiniteGeometry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(new string('x', TextInputState.MaximumTextLength + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("ab", int.MaxValue,
            int.MaxValue, 0, 0, -1, -1, 0, default, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 1, 2, 0, -1, -1, 0, default, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 1, 0, 0, -1, 0, 0, default, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 1, 0, 0, 1, 0, 0, default, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 1, 0, 0, -1, -1, -1, default, new()));
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create("", new Rect(invalid, 0, 1, 20)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 0, 0, 0, -1, -1, 0, default, new(), invalid));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Create("", new Rect(0, 0, -1, 20)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextInputState("", 0, 0, 0, 0, -1, -1, 0,
            default, new() { Scope = (TextInputScope)999 }));
    }

    [Fact]
    public void CompositionMetadataRejectsInvalidStageAndRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextCompositionEventArgs((TextCompositionStage)999, -1, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextCompositionEventArgs(TextCompositionStage.Updated, 4, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextCompositionEventArgs(TextCompositionStage.Updated, -1, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextCompositionEventArgs(TextCompositionStage.Finished, -1, -1, -1));
    }

    private static TextInputState Create(string text, Rect rectangle = default)
        => new(text, 0, text.Length, 0, 0, -1, -1, 0, rectangle, new());
}
