using System;

namespace ModernFormsNext.Animations
{
    /// <summary>
    /// Provides common easing functions for animations.
    /// </summary>
    public static class Easings
    {
        /// <summary>
        /// Represents linear easing.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float Linear (float t)
        {
            ValidateProgress(t);
            return t;
        }

        /// <summary>
        /// Represents quadratic ease-in.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseIn (float t)
        {
            ValidateProgress(t);
            return t * t;
        }

        /// <summary>
        /// Represents quadratic ease-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseOut (float t)
        {
            ValidateProgress(t);
            return 1f - ((1f - t) * (1f - t));
        }

        /// <summary>
        /// Represents quadratic ease-in-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseInOut (float t)
        {
            ValidateProgress(t);
            if (t < 0.5f)
                return 2f * t * t;

            return 1f - (MathF.Pow (-2f * t + 2f, 2f) / 2f);
        }

        /// <summary>
        /// Represents cubic ease-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseOutCubic (float t)
        {
            ValidateProgress(t);
            return 1f - MathF.Pow (1f - t, 3f);
        }

        /// <summary>
        /// Represents cubic ease-in. This naming pairs with <see cref="CubicOut"/>.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float CubicIn(float t)
        {
            ValidateProgress(t);
            return t * t * t;
        }

        /// <summary>
        /// Represents cubic ease-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float CubicOut(float t) => EaseOutCubic(t);

        /// <summary>
        /// Represents cubic ease-in-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float CubicInOut(float t) => EaseInOutCubic(t);

        /// <summary>
        /// Represents a bounded ease-out bounce curve.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float BounceOut(float t)
        {
            ValidateProgress(t);
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1)
                return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>
        /// Represents cubic ease-in-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseInOutCubic (float t)
        {
            ValidateProgress(t);
            if (t < 0.5f)
                return 4f * t * t * t;

            return 1f - (MathF.Pow (-2f * t + 2f, 3f) / 2f);
        }

        private static void ValidateProgress(float value)
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Easing progress must be finite and in the inclusive range 0 through 1.");
        }
    }
}
