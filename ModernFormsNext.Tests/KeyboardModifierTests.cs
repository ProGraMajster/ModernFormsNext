using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.Tests;

public class KeyboardModifierTests
{
    [Fact]
    public void AltGraphPreservesPhysicalModifiersButIsNotShortcutControl()
    {
        var modifiers = RawInputModifiers.Control | RawInputModifiers.Alt | RawInputModifiers.AltGraph;
        var keyData = WindowKitExtensions.AddModifiers(Keys.A, modifiers);
        var args = new KeyEventArgs(keyData);

        Assert.True(args.Control);
        Assert.True(args.Alt);
        Assert.True(args.AltGraph);
        Assert.False(args.IsShortcutControlPressed);
        Assert.Equal(Keys.A, args.KeyCode);
        Assert.True((args.Modifiers & Keys.AltGraph) != 0);
    }

    [Fact]
    public void RealControlAltWithoutAltGraphRemainsShortcutControl()
    {
        var args = new KeyEventArgs(Keys.Control | Keys.Alt | Keys.A);

        Assert.True(args.Control);
        Assert.True(args.Alt);
        Assert.False(args.AltGraph);
        Assert.True(args.IsShortcutControlPressed);
    }

    [Fact]
    public void LeftAltAloneIsNotAltGraphOrShortcutControl()
    {
        var args = new KeyEventArgs(Keys.Alt | Keys.A);

        Assert.True(args.Alt);
        Assert.False(args.AltGraph);
        Assert.False(args.IsShortcutControlPressed);
    }

    [Fact]
    public void TextInputCarriesAltGraphWithoutChangingPlatformText()
    {
        var args = new KeyPressEventArgs("ą", Keys.Control | Keys.Alt | Keys.AltGraph);

        Assert.Equal("ą", args.Text);
        Assert.Equal('ą', args.KeyChar);
        Assert.True(args.Control);
        Assert.True(args.Alt);
        Assert.True(args.AltGraph);
    }

    [Fact]
    public void KeyboardMaskIncludesAltGraph()
    {
        Assert.Equal(
            RawInputModifiers.AltGraph,
            RawInputModifiers.AltGraph & RawInputModifiers.KeyboardMask);
    }

    [Fact]
    public void MouseInputAlsoDistinguishesAltGraphFromShortcutControl()
    {
        var args = new MouseEventArgs(
            MouseButtons.None,
            0,
            0,
            0,
            System.Drawing.Point.Empty,
            keyData: Keys.Control | Keys.Alt | Keys.AltGraph);

        Assert.True(args.Control);
        Assert.True(args.Alt);
        Assert.True(args.AltGraph);
        Assert.False(args.IsShortcutControlPressed);
    }
}
