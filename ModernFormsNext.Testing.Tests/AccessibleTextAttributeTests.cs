using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class AccessibleTextAttributeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FractionalDpiFontSizeSurvivesNativePointRoundtripWithoutBroadApproximation(bool rich)
    {
        using var host = ModernFormsTestHost.Create(new TestViewport(400, 180, 1.25));
        TextBox editor = rich ? new RichTextBox() : new TextBox();
        editor.Style.FontSize = 10;
        editor.Text = "fractional size";
        host.Show(editor);
        var range = editor.AccessibilityObject.TextProvider!.DocumentRange;
        double size = Assert.IsType<double>(range.GetAttributeValue(AccessibleTextAttribute.FontSize));
        Assert.Equal(9.6d, size); // The real renderer rounded 10 * 1.25 to twelve device pixels.
        double nativePoints = size * (72d / 96d);
        double roundtrip = nativePoints * (96d / 72d);
        Assert.NotEqual(size, roundtrip); // This is actual conversion roundoff, not an exact integer case.
        var found = range.FindAttribute(AccessibleTextAttribute.FontSize, roundtrip);
        Assert.NotNull(found);
        Assert.Equal(editor.Text, found.GetText());

        double different = size;
        for (int i = 0; i < 8; i++) different = Math.BitIncrement(different);
        Assert.Null(range.FindAttribute(AccessibleTextAttribute.FontSize, different));
        Assert.Null(range.FindAttribute(AccessibleTextAttribute.FontSize, double.NaN));
        Assert.Null(range.FindAttribute(AccessibleTextAttribute.FontSize, double.PositiveInfinity));
    }
}
