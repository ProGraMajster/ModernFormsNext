using System.Drawing;
using ModernFormsNext.Extensions;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Tests;

public class AnimationPublicApiCompatibilityTests
{
    [Fact]
    public void MouseEventArgsRetainsLegacyConstructorSignature()
    {
        var constructor = typeof(MouseEventArgs).GetConstructor(
            [
                typeof(MouseButtons),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(Point),
                typeof(int?),
                typeof(int?),
                typeof(Keys)
            ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void DrawBorderRetainsLegacyExtensionSignature()
    {
        var method = typeof(SkiaExtensions).GetMethod(
            nameof(SkiaExtensions.DrawBorder),
            [typeof(SKCanvas), typeof(Rectangle), typeof(ControlStyle)]);

        Assert.NotNull(method);
    }
}
