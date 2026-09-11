using ModernFormsNext.WindowKit.Backend.Android.Rendering;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidInputKeyMapperTests
{
    [Theory]
    [InlineData(29, AndroidInputKey.A, Key.A)]
    [InlineData(54, AndroidInputKey.Z, Key.Z)]
    [InlineData(7, AndroidInputKey.D0, Key.D0)]
    [InlineData(16, AndroidInputKey.D9, Key.D9)]
    [InlineData(131, AndroidInputKey.F1, Key.F1)]
    [InlineData(142, AndroidInputKey.F12, Key.F12)]
    [InlineData(144, AndroidInputKey.NumPad0, Key.NumPad0)]
    [InlineData(153, AndroidInputKey.NumPad9, Key.NumPad9)]
    [InlineData(154, AndroidInputKey.Divide, Key.Divide)]
    [InlineData(157, AndroidInputKey.Add, Key.Add)]
    [InlineData(159, AndroidInputKey.Separator, Key.Separator)]
    [InlineData(160, AndroidInputKey.Enter, Key.Enter)]
    [InlineData(61, AndroidInputKey.Tab, Key.Tab)]
    [InlineData(111, AndroidInputKey.Escape, Key.Escape)]
    [InlineData(62, AndroidInputKey.Space, Key.Space)]
    [InlineData(122, AndroidInputKey.Home, Key.Home)]
    [InlineData(123, AndroidInputKey.End, Key.End)]
    [InlineData(92, AndroidInputKey.PageUp, Key.PageUp)]
    [InlineData(93, AndroidInputKey.PageDown, Key.PageDown)]
    [InlineData(124, AndroidInputKey.Insert, Key.Insert)]
    [InlineData(117, AndroidInputKey.LeftMeta, Key.LWin)]
    [InlineData(118, AndroidInputKey.RightMeta, Key.RWin)]
    [InlineData(113, AndroidInputKey.LeftCtrl, Key.LeftCtrl)]
    [InlineData(58, AndroidInputKey.RightAlt, Key.RightAlt)]
    [InlineData(68, AndroidInputKey.OemTilde, Key.OemTilde)]
    [InlineData(75, AndroidInputKey.OemQuotes, Key.OemQuotes)]
    [InlineData(76, AndroidInputKey.OemQuestion, Key.OemQuestion)]
    public void NativeIdentitiesReachExistingWindowKitKeys(int native, AndroidInputKey expected, Key platform)
    {
        var result = Assert.IsType<AndroidInputKeyEvent>(
            AndroidInputKeyMapper.FromNative(native, true, 0, 3));
        Assert.Equal(expected, result.Key);
        Assert.Equal(platform, result.PlatformKey);
    }

    [Theory]
    [InlineData(3)] // Android Home.
    [InlineData(4)] // Android Back.
    [InlineData(24)] // Volume up.
    [InlineData(26)] // Power.
    [InlineData(85)] // Media play/pause.
    [InlineData(130)] // Media record, just below F1.
    [InlineData(143)] // NumLock, between F12 and numpad digits.
    [InlineData(161)] // Unsupported numpad Equals.
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void UnsupportedSystemOrOemKeysAreNotCaptured(int native)
        => Assert.Null(AndroidInputKeyMapper.FromNative(native, true, 0, 3));

    [Fact]
    public void EveryDeclaredIdentityHasOneExistingPlatformIdentityAndLegacyValuesRemainStable()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, new[]
        {
            (int)AndroidInputKey.Backspace, (int)AndroidInputKey.Delete, (int)AndroidInputKey.Enter,
            (int)AndroidInputKey.Left, (int)AndroidInputKey.Up, (int)AndroidInputKey.Right, (int)AndroidInputKey.Down
        });
        foreach (var key in Enum.GetValues<AndroidInputKey>())
            Assert.NotEqual(Key.None, new AndroidInputKeyEvent(key, false).PlatformKey);
        Assert.Equal(Key.None, new AndroidInputKeyEvent((AndroidInputKey)int.MaxValue, false).PlatformKey);
    }

    [Theory]
    [InlineData(0x1000, KeyModifiers.Control)]
    [InlineData(0x2000, KeyModifiers.Control)]
    [InlineData(0x4000, KeyModifiers.Control)]
    [InlineData(0x1, KeyModifiers.Shift)]
    [InlineData(0x40, KeyModifiers.Shift)]
    [InlineData(0x80, KeyModifiers.Shift)]
    [InlineData(0x2, KeyModifiers.Alt)]
    [InlineData(0x10, KeyModifiers.Alt)]
    [InlineData(0x20, KeyModifiers.Alt | KeyModifiers.AltGraph)]
    [InlineData(0x10000, KeyModifiers.Meta)]
    [InlineData(0x20000, KeyModifiers.Meta)]
    [InlineData(0x40000, KeyModifiers.Meta)]
    [InlineData(0x310000, KeyModifiers.Meta)] // Lock states do not become shortcut modifiers.
    [InlineData(0x24040, KeyModifiers.Meta | KeyModifiers.Control | KeyModifiers.Shift)]
    public void SideAndAggregateNativeBitsNormalizeWithoutLosingRightAlt(int native, KeyModifiers expected)
        => Assert.Equal(expected, AndroidInputKeyMapper.FromNative(47, true, native, 1)!.Value.Modifiers);

    [Theory]
    [InlineData(0, false, false, true)]
    [InlineData(14, false, false, true)]
    [InlineData(-1, false, false, false)]
    [InlineData(14, true, false, false)]
    [InlineData(14, false, true, false)]
    public void SourceAndRepeatMetadataSurviveMappingWithoutGrantingImeShortcutEligibility(
        int device, bool soft, bool connection, bool expected)
    {
        var mapped = AndroidInputKeyMapper.FromNative(47, false, 0x2040, device, soft, connection,
            repeatCount: 8, isCanceled: true, isDeadKey: true)!.Value;
        Assert.Equal(expected, mapped.IsHardwareKey);
        Assert.False(mapped.IsDown);
        Assert.Equal(8, mapped.RepeatCount);
        Assert.True(mapped.IsCanceled);
        Assert.True(mapped.IsDeadKey);
        Assert.Equal(device, mapped.DeviceId);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, mapped.Modifiers);
        Assert.Equal(Key.S, mapped.PlatformKey);
    }

    [Fact]
    public void CompatibilityConstructionRetainsEditingDefaultsAndDerivedKey()
    {
        var original = new AndroidInputKeyEvent(AndroidInputKey.Left, false);
        var (key, down) = original;
        Assert.Equal(AndroidInputKey.Left, key);
        Assert.False(down);
        Assert.Equal(Key.Left, original.PlatformKey);
        Assert.Equal(-1, original.DeviceId);
        Assert.Equal(0, original.RepeatCount);
        Assert.False(original.IsHardwareKey);
        Assert.False(original.IsCanceled);
        Assert.False(original.IsDeadKey);
    }
}
