using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidKeyboardEventDispatchTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrimaryHandlerRunsOnceAndExcludesLegacyObservers(bool handled)
    {
        int primary = 0, legacy = 0, notifications = 0;
        var mapped = AndroidInputKeyMapper.FromNative(21, true, 0, 1);
        var result = AndroidKeyboardEventDispatch.Publish(this, mapped,
            _ => { primary++; return handled; }, (_, _) => legacy++,
            () => true, () => notifications++);
        Assert.Equal(handled, result);
        Assert.Equal((1, 0, 1), (primary, legacy, notifications));
    }

    [Theory]
    [InlineData(67, true)]
    [InlineData(112, true)]
    [InlineData(66, true)]
    [InlineData(21, true)]
    [InlineData(19, true)]
    [InlineData(22, true)]
    [InlineData(20, true)]
    [InlineData(29, false)]
    [InlineData(131, false)]
    [InlineData(61, false)]
    public void LegacySubscribersOnlyReceiveOriginalSevenKeys(int key, bool delivered)
    {
        int calls = 0, notifications = 0;
        var result = AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(key, true, 0, 1),
            null, (_, _) => calls++, () => true, () => notifications++);
        Assert.Equal(delivered, result);
        Assert.Equal(delivered ? 1 : 0, calls);
        Assert.Equal(calls, notifications);
    }

    [Fact]
    public void CompatibilitySevenKeysRemainConsumedWithoutSubscribers()
        => Assert.True(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(21, false, 0, 1),
            null, null, () => true, () => { }));

    [Theory]
    [InlineData(21)]
    [InlineData(29)]
    public void UnhandledImeTransitionIsAcceptedOnceWithoutBaseRedispatch(int key)
    {
        int shared = 0, fallback = 0;
        var result = AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(key, true, 0x40, 14, fromInputConnection: true),
            e => { Assert.False(e.IsHardwareKey); shared++; return false; },
            null, () => true, () => { }, fromInputConnection: true);
        if (!result) fallback++;
        Assert.Equal(1, shared);
        Assert.Equal(0, fallback);
        Assert.True(result);
    }

    [Fact]
    public void UnmappedImeKeyCanFallBackWithoutBeingDeliveredToPrimary()
    {
        int calls = 0;
        Assert.False(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(4, true, 0, 14, fromInputConnection: true),
            _ => { calls++; return false; }, null, () => true, () => { }, fromInputConnection: true));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void CallbackThatRetiresRouteCannotNotifyReplacementOrResumeNativeFallback()
    {
        bool current = true;
        int notifications = 0;
        var result = AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(47, true, 0x1000, 1),
            _ => { current = false; return false; }, null,
            () => current, () => notifications++);
        Assert.True(result);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void EditorNotificationCanRetireRouteBeforeNativeFallback()
    {
        bool current = true;
        Assert.True(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(29, true, 0, 1),
            _ => false, null, () => current, () => current = false));
    }

    [Fact]
    public void RetiredEntryDoesNotInvokeHandlers()
    {
        Assert.True(AndroidKeyboardEventDispatch.Publish(this,
            AndroidInputKeyMapper.FromNative(47, true, 0, 1),
            _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(),
            () => false, () => throw new InvalidOperationException()));
    }

    [Fact]
    public void HandlerFailurePropagatesWithoutLegacyDeliveryOrTextNotification()
    {
        int later = 0;
        var failure = new InvalidOperationException("command");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            AndroidKeyboardEventDispatch.Publish(this,
                AndroidInputKeyMapper.FromNative(21, true, 0, 1),
                _ => throw failure, (_, _) => later++, () => true, () => later++)));
        Assert.Equal(0, later);
    }

    [Fact]
    public void KeyboardResetFailureDoesNotSkipLaterSubscriberOrOtherMandatoryCleanup()
    {
        int resets = 0;
        bool released = false;
        EventHandler subscribers = (_, _) => throw new InvalidOperationException("reset");
        subscribers += (_, e) => { Assert.Same(EventArgs.Empty, e); resets++; };
        Assert.Throws<InvalidOperationException>(() => AndroidSurfaceCleanup.Complete(
            () => AndroidSurfaceCleanup.ResetKeyboard(this, subscribers),
            () => released = true));
        Assert.Equal(1, resets);
        Assert.True(released);
    }
}
