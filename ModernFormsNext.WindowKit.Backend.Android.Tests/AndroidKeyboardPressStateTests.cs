using ModernFormsNext.CrossPlatform.Sample;
using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

[Collection(AndroidTextInputUiCollection.Name)]
public sealed class AndroidKeyboardPressStateTests
{
    [Theory]
    [InlineData(62)] // Space.
    [InlineData(66)] // Enter.
    public void ResetThenLateHardwareReleaseCannotActivateRealButton(int key)
    {
        using var root = new Panel();
        var button = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        surface.Resize(480, 160);
        button.Select();
        int clicks = 0, releases = 0;
        button.Click += (_, _) => clicks++;
        button.KeyUp += (_, _) => releases++;
        var presses = new AndroidKeyboardPressState();
        bool Send(bool down, int repeat = 0) => AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(key, down, 0, 14, repeatCount: repeat),
            input => AndroidKeyboardInput.Process(surface, input), null, () => true, () => { },
            pressState: presses, resetKeyboard: surface.ResetKeyboardState);

        Send(true);
        presses.Reset(); // The production native reset commits pairing before shared callbacks.
        surface.ResetKeyboardState();
        Assert.True(Send(false));
        Assert.Equal(0, clicks);
        Assert.Equal(0, releases);
        Assert.Equal(0, presses.Count);

        Assert.True(Send(true, repeat: 2)); // A held key's resumed repeat cannot start a new gesture.
        Assert.Equal(0, presses.Count);
        Assert.Equal(0, clicks);
        Send(true);
        Send(true, repeat: 1);
        Assert.True(Send(false));
        Assert.Equal(1, clicks);
        Assert.Equal(0, presses.Count);
    }

    [Fact]
    public void OrphanRepeatDoesNotStartCommandButNewInitialPressAndRepeatsWork()
    {
        using var root = new Panel();
        var editor = root.Controls.Add(new TextBox { Text = "seed" });
        using var surface = new SkiaControlSurface(root);
        surface.Resize(480, 160);
        editor.Select();
        int calls = 0;
        root.InputBindings.Add(new KeyBinding(new DelegateCommand(() => calls++),
            new KeyGesture(Keys.S, KeyModifiers.Control)));
        var presses = new AndroidKeyboardPressState();
        bool Send(bool down, int repeat = 0) => AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(47, down, 0x1000, 14, repeatCount: repeat),
            input => AndroidKeyboardInput.Process(surface, input), null, () => true, () => { },
            pressState: presses, resetKeyboard: surface.ResetKeyboardState);

        Assert.True(Send(true));
        presses.Reset();
        surface.ResetKeyboardState();
        Assert.True(Send(true, 5));
        Assert.True(Send(false));
        Assert.Equal(1, calls);
        Assert.True(Send(true));
        Assert.True(Send(true, 1));
        Assert.True(Send(true, 2));
        Assert.True(Send(false));
        Assert.Equal(4, calls);
        Assert.Equal("seed", editor.Text);
    }

    [Fact]
    public void AnotherDevicesReleaseCannotRetireTheMatchingNativePair()
    {
        var presses = new AndroidKeyboardPressState();
        var down = AndroidInputKeyMapper.FromNative(62, true, 0, 3)!.Value;
        Assert.True(presses.TryPrepare(down, out _, out _));
        Assert.True(presses.TryPrepare(down with { IsDown = false, DeviceId = 4 }, out var other, out _));
        Assert.True(other.IsCanceled);
        Assert.Equal(1, presses.Count);
        Assert.True(presses.TryPrepare(down with { IsDown = false }, out var matching, out _));
        Assert.False(matching.IsCanceled);
        Assert.Equal(0, presses.Count);
    }

    [Fact]
    public void NativeCanceledReleaseRemovesPairAndCannotBecomeAnOrdinarySecondRelease()
    {
        var presses = new AndroidKeyboardPressState();
        var down = AndroidInputKeyMapper.FromNative(62, true, 0, 3)!.Value;
        presses.TryPrepare(down, out _, out _);
        Assert.True(presses.TryPrepare(down with { IsDown = false, IsCanceled = true }, out var canceled, out _));
        Assert.True(canceled.IsCanceled);
        Assert.Equal(0, presses.Count);
        presses.TryPrepare(down with { IsDown = false }, out var duplicate, out _);
        Assert.True(duplicate.IsCanceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImeAndLegacyDeliveryRetainUnpairedCompatibility(bool legacy)
    {
        var presses = new AndroidKeyboardPressState();
        var release = AndroidInputKeyMapper.FromNative(66, false, 0, 3,
            fromInputConnection: !legacy)!.Value;
        int calls = 0;
        bool Primary(AndroidInputKeyEvent input) { Assert.False(input.IsCanceled); calls++; return false; }
        void Legacy(object? sender, AndroidInputKeyEvent input) { Assert.False(input.IsCanceled); calls++; }
        Assert.True(AndroidKeyboardEventDispatch.Publish(this, release, legacy ? null : Primary,
            legacy ? Legacy : null, () => true, () => { }, fromInputConnection: !legacy,
            pressState: presses));
        Assert.Equal(1, calls);
        Assert.Equal(0, presses.Count);
    }

    [Fact]
    public void PrimaryCallbackResetCannotRecreateItsRetiredPress()
    {
        var presses = new AndroidKeyboardPressState();
        bool current = true;
        Assert.True(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(47, true, 0, 3),
            _ => { presses.Reset(); current = false; return false; },
            null, () => current, () => { }, pressState: presses));
        Assert.Equal(0, presses.Count);
    }

    [Fact]
    public void CapacityOverflowClearsNativeAndSharedStateThenVetoesWithoutRetry()
    {
        using var root = new Panel();
        var button = root.Controls.Add(new Button());
        using var surface = new SkiaControlSurface(root);
        surface.Resize(480, 160);
        button.Select();
        int calls = 0, resets = 0;
        var presses = new AndroidKeyboardPressState();
        for (var device = 0; device < AndroidKeyboardPressState.MaximumPressedKeys; device++)
            Assert.True(presses.TryPrepare(
                AndroidInputKeyMapper.FromNative(62, true, 0, device)!.Value, out _, out _));
        Assert.True(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(62, true, 0, 999),
            input => { calls++; return AndroidKeyboardInput.Process(surface, input); },
            null, () => true, () => { }, pressState: presses,
            resetKeyboard: () =>
            {
                Assert.Equal(0, presses.Count);
                surface.ResetKeyboardState();
                resets++;
            }));
        Assert.Equal(0, calls);
        Assert.Equal(1, resets);
        Assert.Equal(0, presses.Count);
    }
}
