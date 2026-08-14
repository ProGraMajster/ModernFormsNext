namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>
/// Identifies the Android MotionEvent action needed by the platform-neutral translation plan.
/// </summary>
internal enum AndroidMotionEventAction
{
    Other,
    Down,
    PointerDown,
    Move,
    PointerUp,
    Up,
    Cancel
}

/// <summary>
/// Describes which MotionEvent pointer indexes must be translated for one native event.
/// </summary>
/// <remarks>
/// Pointer indexes are ephemeral positions inside one MotionEvent. The native view uses the
/// returned index only to read Android's stable pointer ID and coordinates for that event.
/// </remarks>
internal readonly record struct AndroidMotionEventPlan
{
    private AndroidMotionEventPlan(
        AndroidPointerAction? pointerAction,
        int pointerCount,
        int actionIndex,
        bool cancelAll)
    {
        PointerAction = pointerAction;
        PointerCount = pointerCount;
        ActionIndex = actionIndex;
        CancelAll = cancelAll;
    }

    public AndroidPointerAction? PointerAction { get; }

    public int PointerCount { get; }

    public int ActionIndex { get; }

    public bool CancelAll { get; }

    public int EventCount => PointerAction == AndroidPointerAction.Move
        ? PointerCount
        : PointerAction is null ? 0 : 1;

    public int GetPointerIndex(int translatedEventIndex)
    {
        if ((uint)translatedEventIndex >= (uint)EventCount)
            throw new ArgumentOutOfRangeException(nameof(translatedEventIndex));
        return PointerAction == AndroidPointerAction.Move ? translatedEventIndex : ActionIndex;
    }

    public static AndroidMotionEventPlan Create(
        AndroidMotionEventAction action,
        int pointerCount,
        int actionIndex)
    {
        if (pointerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(pointerCount));

        AndroidPointerAction? translated = action switch
        {
            AndroidMotionEventAction.Down or AndroidMotionEventAction.PointerDown => AndroidPointerAction.Down,
            AndroidMotionEventAction.Move => AndroidPointerAction.Move,
            AndroidMotionEventAction.Up or AndroidMotionEventAction.PointerUp => AndroidPointerAction.Up,
            _ => null
        };

        if (translated is not null &&
            (pointerCount == 0 || actionIndex < 0 || actionIndex >= pointerCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionIndex),
                actionIndex,
                "The MotionEvent action index must identify an available pointer.");
        }

        return new AndroidMotionEventPlan(
            translated,
            pointerCount,
            actionIndex,
            cancelAll: action == AndroidMotionEventAction.Cancel);
    }
}
