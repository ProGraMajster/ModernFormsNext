using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class KeyGestureTests
{
    [Fact]
    public void EqualGesturesHaveValueEqualityHashAndDiagnosticText()
    {
        var first = new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift);
        var second = new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new KeyGesture(Keys.S, KeyModifiers.Control));
        Assert.Equal("Ctrl+Shift+S", first.ToString());
    }

    [Theory]
    [InlineData(Keys.Control | Keys.S, true)]
    [InlineData(Keys.Control | Keys.Shift | Keys.S, false)]
    [InlineData(Keys.Alt | Keys.S, false)]
    [InlineData(Keys.S, false)]
    [InlineData(Keys.Control | Keys.Z, false)]
    [InlineData(Keys.Control | Keys.Meta | Keys.S, false)]
    public void MatchingRequiresExactKeyAndModifiers(Keys keyData, bool expected)
        => Assert.Equal(expected, new KeyGesture(Keys.S, KeyModifiers.Control).Matches(new KeyEventArgs(keyData)));

    [Theory]
    [InlineData(Keys.F5, KeyModifiers.None, Keys.F5)]
    [InlineData(Keys.D1, KeyModifiers.Control, Keys.Control | Keys.D1)]
    [InlineData(Keys.Left, KeyModifiers.Shift, Keys.Shift | Keys.Left)]
    [InlineData(Keys.S, KeyModifiers.Meta, Keys.Meta | Keys.S)]
    [InlineData(Keys.S, KeyModifiers.Control | KeyModifiers.Alt, Keys.Control | Keys.Alt | Keys.S)]
    public void FunctionDigitNavigationAndPlatformModifiersMatch(Keys key, KeyModifiers modifiers, Keys keyData)
        => Assert.True(new KeyGesture(key, modifiers).Matches(new KeyEventArgs(keyData)));

    [Theory]
    [InlineData(Keys.A)]
    [InlineData(Keys.C)]
    [InlineData(Keys.E)]
    [InlineData(Keys.L)]
    [InlineData(Keys.N)]
    [InlineData(Keys.O)]
    [InlineData(Keys.S)]
    [InlineData(Keys.X)]
    [InlineData(Keys.Z)]
    public void PolishAltGraphNeverMatchesRealControlAltGesture(Keys key)
    {
        var gesture = new KeyGesture(key, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.True(gesture.Matches(new KeyEventArgs(key | Keys.Control | Keys.Alt)));
        Assert.False(gesture.Matches(new KeyEventArgs(key | Keys.Control | Keys.Alt | Keys.AltGraph)));
        Assert.False(gesture.Matches(new KeyEventArgs(key | Keys.Alt | Keys.AltGraph)));
    }

    [Theory]
    [InlineData(Keys.None, KeyModifiers.None)]
    [InlineData(Keys.Control | Keys.S, KeyModifiers.None)]
    [InlineData(Keys.LMenu, KeyModifiers.None)]
    [InlineData(Keys.LButton, KeyModifiers.Control)]
    [InlineData(Keys.S, KeyModifiers.AltGraph)]
    [InlineData(Keys.A, KeyModifiers.None)]
    [InlineData(Keys.A, KeyModifiers.Shift)]
    [InlineData(Keys.D1, KeyModifiers.Shift)]
    [InlineData(Keys.OemQuotes, KeyModifiers.None)]
    public void InvalidOrTextOnlyGesturesAreRejected(Keys key, KeyModifiers modifiers)
        => Assert.Throws<ArgumentException>(() => new KeyGesture(key, modifiers));

    [Fact]
    public void DefaultValueNeverMatchesAndNullEventsAreRejected()
    {
        Assert.False(default(KeyGesture).Matches(new KeyEventArgs(Keys.None)));
        Assert.Equal("<invalid>", default(KeyGesture).ToString());
        Assert.Throws<ArgumentNullException>(() => default(KeyGesture).Matches(null!));
    }

    [Fact]
    public void RawMetaSurvivesTheExistingWindowKitMapper()
    {
        var data = WindowKitExtensions.AddModifiers(Keys.S, RawInputModifiers.Control | RawInputModifiers.Meta);
        Assert.True(new KeyGesture(Keys.S, KeyModifiers.Control | KeyModifiers.Meta).Matches(new KeyEventArgs(data)));
        Assert.Equal(0x00080000, (int)Keys.AltGraph);
        Assert.Equal(0x00100000, (int)Keys.Meta);
    }
}
