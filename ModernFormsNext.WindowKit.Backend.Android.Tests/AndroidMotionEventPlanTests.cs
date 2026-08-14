using ModernFormsNext.WindowKit.Backend.Android.Rendering;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidMotionEventPlanTests
{
    [Fact]
    public void ChangedPointerActionsUseAndroidActionIndex()
    {
        (AndroidMotionEventAction Native, AndroidPointerAction Expected)[] cases =
        [
            (AndroidMotionEventAction.Down, AndroidPointerAction.Down),
            (AndroidMotionEventAction.PointerDown, AndroidPointerAction.Down),
            (AndroidMotionEventAction.Up, AndroidPointerAction.Up),
            (AndroidMotionEventAction.PointerUp, AndroidPointerAction.Up)
        ];

        foreach ((AndroidMotionEventAction nativeAction, AndroidPointerAction expectedAction) in cases)
        {
            AndroidMotionEventPlan plan = AndroidMotionEventPlan.Create(
                nativeAction,
                pointerCount: 3,
                actionIndex: 1);

            Assert.Equal(expectedAction, plan.PointerAction);
            Assert.Equal(1, plan.EventCount);
            Assert.Equal(1, plan.GetPointerIndex(0));
        }
    }

    [Fact]
    public void PointerReorderDoesNotChangeStableIdSelectedByActionIndex()
    {
        int[] beforeReorder = [7, 3];
        int[] afterReorder = [3];
        AndroidMotionEventPlan pointerUp = AndroidMotionEventPlan.Create(
            AndroidMotionEventAction.PointerUp,
            beforeReorder.Length,
            actionIndex: 0);
        AndroidMotionEventPlan move = AndroidMotionEventPlan.Create(
            AndroidMotionEventAction.Move,
            afterReorder.Length,
            actionIndex: 0);

        int releasedPointerId = beforeReorder[pointerUp.GetPointerIndex(0)];
        int remainingPointerId = afterReorder[move.GetPointerIndex(0)];

        Assert.Equal(7, releasedPointerId);
        Assert.Equal(3, remainingPointerId);
    }

    [Fact]
    public void MoveTranslatesEveryCurrentPointerIndexExactlyOnce()
    {
        int[] pointerIds = [9, 2, 14];
        AndroidMotionEventPlan plan = AndroidMotionEventPlan.Create(
            AndroidMotionEventAction.Move,
            pointerIds.Length,
            actionIndex: 0);

        int[] translatedIds = Enumerable.Range(0, plan.EventCount)
            .Select(index => pointerIds[plan.GetPointerIndex(index)])
            .ToArray();

        Assert.Equal(pointerIds, translatedIds);
    }

    [Fact]
    public void CancelRequestsOneGlobalCancellationInsteadOfIndexRouting()
    {
        AndroidMotionEventPlan plan = AndroidMotionEventPlan.Create(
            AndroidMotionEventAction.Cancel,
            pointerCount: 2,
            actionIndex: 0);

        Assert.True(plan.CancelAll);
        Assert.Null(plan.PointerAction);
        Assert.Equal(0, plan.EventCount);
    }
}
