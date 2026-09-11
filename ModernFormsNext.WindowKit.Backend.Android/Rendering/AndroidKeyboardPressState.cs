namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

// Native transport pairing only: no gestures, commands, focus traversal or character conversion.
// Pairing is limited to primary-handler hardware routes, preserving legacy and IME semantics.
internal sealed class AndroidKeyboardPressState
{
    internal const int MaximumPressedKeys = 256;
    private readonly HashSet<(AndroidInputKey Key, int DeviceId)> pressed = [];

    internal int Count => pressed.Count;

    internal void Reset() => pressed.Clear();

    internal bool TryPrepare(AndroidInputKeyEvent input, out AndroidInputKeyEvent result,
        out bool resetRequired)
    {
        result = input;
        resetRequired = false;
        var identity = (input.Key, input.DeviceId);
        if (!input.IsDown)
        {
            var paired = pressed.Remove(identity);
            result = input with { IsCanceled = input.IsCanceled || !paired };
            return true;
        }
        if (input.IsCanceled)
        {
            pressed.Remove(identity);
            return false;
        }
        if (pressed.Contains(identity)) return true;
        // A repeat delivered after suspension cannot start a new command or button gesture.
        if (input.RepeatCount > 0) return false;
        if (pressed.Count == MaximumPressedKeys)
        {
            // Saturation fails closed. The caller also resets the shared host, then vetoes
            // this event without retrying it against state changed by reset subscribers.
            Reset();
            resetRequired = true;
            return false;
        }
        pressed.Add(identity); // Commit before the primary callback can reenter lifecycle.
        return true;
    }
}
