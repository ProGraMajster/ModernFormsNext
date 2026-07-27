using System.Drawing;
using System.Numerics;

namespace ModernFormsNext.Animations;

internal static class AnimationInterpolatorResolver
{
    public static IAnimationInterpolator<T> Get<T>()
    {
        object? interpolator =
            typeof(T) == typeof(float) ? AnimationInterpolators.Float :
            typeof(T) == typeof(double) ? AnimationInterpolators.Double :
            typeof(T) == typeof(int) ? AnimationInterpolators.Int32 :
            typeof(T) == typeof(PointF) ? AnimationInterpolators.PointF :
            typeof(T) == typeof(SizeF) ? AnimationInterpolators.SizeF :
            typeof(T) == typeof(RectangleF) ? AnimationInterpolators.RectangleF :
            typeof(T) == typeof(Color) ? AnimationInterpolators.Color :
            typeof(T) == typeof(Matrix3x2) ? AnimationInterpolators.Matrix3x2 :
            null;

        return interpolator is IAnimationInterpolator<T> typed
            ? typed
            : throw new NotSupportedException(
                $"No built-in animation interpolator is registered for '{typeof(T).FullName}'.");
    }
}
