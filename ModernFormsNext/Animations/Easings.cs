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
        public static float Linear (float t) => t;

        /// <summary>
        /// Represents quadratic ease-in.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseIn (float t) => t * t;

        /// <summary>
        /// Represents quadratic ease-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseOut (float t) => 1f - ((1f - t) * (1f - t));

        /// <summary>
        /// Represents quadratic ease-in-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseInOut (float t)
        {
            if (t < 0.5f)
                return 2f * t * t;

            return 1f - (MathF.Pow (-2f * t + 2f, 2f) / 2f);
        }

        /// <summary>
        /// Represents cubic ease-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseOutCubic (float t) => 1f - MathF.Pow (1f - t, 3f);

        /// <summary>
        /// Represents cubic ease-in-out.
        /// </summary>
        /// <param name="t">The normalized progress in the range from 0 to 1.</param>
        /// <returns>The eased value.</returns>
        public static float EaseInOutCubic (float t)
        {
            if (t < 0.5f)
                return 4f * t * t * t;

            return 1f - (MathF.Pow (-2f * t + 2f, 3f) / 2f);
        }
    }
}
