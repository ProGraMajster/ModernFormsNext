using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public class AndroidKeyboardBindingTests
{
    [Theory]
    [InlineData(2, false, false, true)]
    [InlineData(0, false, false, true)]
    [InlineData(-1, false, false, false)]
    [InlineData(2, true, false, false)]
    [InlineData(2, false, true, false)]
    [InlineData(-1, true, true, false)]
    public void SourceClassificationKeepsImeOutOfBindings(int device, bool soft, bool connection, bool hardware)
    {
        var modifiers = KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta;
        var e = AndroidInputKeyEvent.FromSource(AndroidInputKey.Left, true, modifiers, device, soft, connection);
        Assert.Equal(hardware, e.IsHardwareKey);
        Assert.Equal(hardware ? modifiers : KeyModifiers.None, e.Modifiers);
        Assert.Equal(AndroidInputKey.Left, e.Key);
        Assert.True(e.IsDown);
    }

    [Fact]
    public void ExistingConstructorRetainsEditingDefaultsAndDeconstruction()
    {
        var e = new AndroidInputKeyEvent(AndroidInputKey.Enter, false);
        var (key, down) = e;
        Assert.Equal(AndroidInputKey.Enter, key);
        Assert.False(down);
        Assert.False(e.IsHardwareKey);
        Assert.Equal(KeyModifiers.None, e.Modifiers);
    }

    [Fact]
    public void HardwareRightAltMarkerCannotMatchControlAltGesture()
    {
        var e = AndroidInputKeyEvent.FromSource(AndroidInputKey.Left, true,
            KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.AltGraph, 2, false, false);
        Assert.True(e.IsHardwareKey);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.AltGraph, e.Modifiers);
        Assert.False(new KeyGesture(Keys.Left, KeyModifiers.Control | KeyModifiers.Alt)
            .Matches(new KeyEventArgs(Keys.Left | Keys.Control | Keys.Alt | Keys.AltGraph)));
    }
}
