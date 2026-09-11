using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[Collection(AndroidTextInputUiCollection.Name)]
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
        Assert.Equal(modifiers, e.Modifiers);
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

    [Theory]
    [InlineData(-1, true, true)]
    [InlineData(2, false, true)]
    [InlineData(2, true, false)]
    public void ImeShiftLeftExtendsRealEditorSelectionWithoutExecutingMatchingShortcut(
        int deviceId, bool softKeyboard, bool inputConnection)
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { Text = "abcdef" });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(480, 160);
        editor.Select();
        var client = Assert.IsAssignableFrom<ITextInputClient>(surface.TextInputClient);
        Assert.True(client.SetSelection(4, 4));
        var shortcutCalls = 0;
        root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => shortcutCalls++),
            new KeyGesture(Keys.Left, KeyModifiers.Shift)));

        for (var count = 0; count < 2; count++)
        {
            var down = AndroidInputKeyEvent.FromSource(AndroidInputKey.Left, true, KeyModifiers.Shift,
                deviceId, softKeyboard, inputConnection);
            var up = AndroidInputKeyEvent.FromSource(AndroidInputKey.Left, false, KeyModifiers.Shift,
                deviceId, softKeyboard, inputConnection);
            // Match the host's existing event bridge: preserve editing modifiers separately
            // from its decision to bypass the shortcut resolver for an IME event.
            var key = Keys.Left | (down.Modifiers.HasFlag(KeyModifiers.Shift) ? Keys.Shift : Keys.None);
            surface.ProcessKeyDown(key, isTextInput: !down.IsHardwareKey);
            surface.ProcessKeyUp(key, isTextInput: !up.IsHardwareKey);
        }

        var state = client.GetState()!;
        Assert.Equal((4, 2), (state.SelectionStart, state.SelectionEnd));
        Assert.Equal("abcdef", editor.Text);
        Assert.Equal(0, shortcutCalls);
    }
}
