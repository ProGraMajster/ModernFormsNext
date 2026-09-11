namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

// One synchronous delivery boundary shared by the native View and InputConnection. This owns
// no state of its own. The view owns native lifetime pairing; command press suppression and
// resolution remain owned by the canonical surface.
internal static class AndroidKeyboardEventDispatch
{
    internal static bool Publish(object sender, AndroidInputKeyEvent? key,
        Func<AndroidInputKeyEvent, bool>? primary, EventHandler<AndroidInputKeyEvent>? legacy,
        Func<bool> isCurrent, Action notifyTextStateChanged, bool fromInputConnection = false,
        AndroidKeyboardPressState? pressState = null, Action? resetKeyboard = null)
    {
        if (key is not { } args) return false;
        if (!isCurrent()) return true; // A retired callback route must never resume native fallback.
        bool handled;
        if (primary is not null)
        {
            if (!fromInputConnection && args.IsHardwareKey && pressState is not null &&
                !pressState.TryPrepare(args, out args, out bool resetRequired))
            {
                if (resetRequired) resetKeyboard?.Invoke();
                return true;
            }
            handled = primary(args);
        }
        else if (AndroidInputKeyMapper.IsLegacyKey(args.Key))
        {
            legacy?.Invoke(sender, args);
            handled = true; // Preserve the original seven-key compatibility contract.
        }
        else
            return false;

        if (!isCurrent()) return true;
        notifyTextStateChanged();
        // BaseInputConnection redispatches asynchronously to View: a mapped IME transition
        // already delivered to the shared editor is accepted once, even if no command handled it.
        return fromInputConnection || !isCurrent() || handled;
    }
}
