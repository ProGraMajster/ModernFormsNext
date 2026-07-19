using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class EasingTests
{
    public static TheoryData<Func<float, float>> BuiltInEasings => new()
    {
        Easings.Linear,
        Easings.EaseIn,
        Easings.EaseOut,
        Easings.EaseInOut,
        Easings.EaseOutCubic,
        Easings.EaseInOutCubic
    };

    [Theory]
    [MemberData(nameof(BuiltInEasings))]
    public void BuiltInEasingsPreserveExactEndpoints(Func<float, float> easing)
    {
        Assert.Equal(0f, easing(0f));
        Assert.Equal(1f, easing(1f));
        Assert.InRange(easing(0.5f), 0f, 1f);
    }

    [Theory]
    [MemberData(nameof(BuiltInEasings))]
    public void BuiltInEasingsRejectProgressOutsideFiniteUnitRange(Func<float, float> easing)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => easing(-0.001f));
        Assert.Throws<ArgumentOutOfRangeException>(() => easing(1.001f));
        Assert.Throws<ArgumentOutOfRangeException>(() => easing(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => easing(float.PositiveInfinity));
    }
}
