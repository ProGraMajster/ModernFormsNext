using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidTextInputStateTests
{
    [Fact]
    public void SurroundingTextUsesSelectionEdges()
    {
        var state = new AndroidTextInputState("Zażółć 👋 tekst", 7, 9);

        Assert.Equal("żółć ", state.GetTextBeforeCursor(5));
        Assert.Equal(" te", state.GetTextAfterCursor(3));
        Assert.Equal("👋", state.GetSelectedText());
    }

    [Fact]
    public void CodePointDeletionKeepsSurrogatePairsIntact()
    {
        var state = new AndroidTextInputState("A👋B😀C", 3, 3);

        Assert.Equal(new AndroidTextDeletionRequest(2, 1), state.GetUtf16DeletionForCodePoints(1, 1));

        state = new AndroidTextInputState("A👋B😀C", 4, 4);
        Assert.Equal(new AndroidTextDeletionRequest(1, 2), state.GetUtf16DeletionForCodePoints(1, 1));
    }

    [Fact]
    public void ConstructorRejectsInvalidSelectionOrComposition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AndroidTextInputState("abc", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AndroidTextInputState("abc", 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AndroidTextInputState("abc", 0, 0, 0, -1));
    }
}
