using System;
using System.Threading.Tasks;

namespace ModernFormsNext.Animations
{
    /// <summary>
    /// Provides higher-level animation effects for controls.
    /// </summary>
    public static class ControlAnimationEffects
    {
        /// <summary>
        /// Animates the control scale relative to the current value.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="scale">The relative scale multiplier.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task RelScaleToAsync (
            this Control control,
            float scale,
            int duration = 250,
            Func<float, float>? easing = null)
        {
            return control.ScaleToAsync (
                control.ScaleX * scale,
                control.ScaleY * scale,
                duration,
                easing);
        }

        /// <summary>
        /// Animates the control rotation relative to the current rotation.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="rotation">The rotation delta in degrees.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task RelRotateToAsync (
            this Control control,
            float rotation,
            int duration = 250,
            Func<float, float>? easing = null)
        {
            return control.RotateToAsync (
                control.Rotation + rotation,
                duration,
                easing);
        }

        /// <summary>
        /// Performs a shake animation typically used for error feedback.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="amplitude">Shake amplitude in pixels.</param>
        /// <param name="duration">Total animation duration.</param>
        public static async Task ShakeAsync (
            this Control control,
            float amplitude = 8f,
            int duration = 350)
        {
            var step = duration / 6f;

            await control.TranslateToAsync (-amplitude, 0, (int)step, Easings.EaseOut);
            await control.TranslateToAsync (amplitude, 0, (int)step, Easings.EaseOut);
            await control.TranslateToAsync (-amplitude * 0.6f, 0, (int)step, Easings.EaseOut);
            await control.TranslateToAsync (amplitude * 0.6f, 0, (int)step, Easings.EaseOut);
            await control.TranslateToAsync (-amplitude * 0.3f, 0, (int)step, Easings.EaseOut);
            await control.TranslateToAsync (0, 0, (int)step, Easings.EaseOut);
        }

        /// <summary>
        /// Performs a pulse animation (grow and shrink).
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="scale">Pulse scale multiplier.</param>
        /// <param name="duration">Animation duration.</param>
        public static async Task PulseAsync (
            this Control control,
            float scale = 1.08f,
            int duration = 250)
        {
            var half = duration / 2;

            await control.ScaleToAsync (scale, half, Easings.EaseOutCubic);
            await control.ScaleToAsync (1f, half, Easings.EaseInOutCubic);
        }

        /// <summary>
        /// Adds a hover animation to the control.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="hoverScale">The hover scale multiplier.</param>
        /// <param name="duration">Animation duration.</param>
        public static void AddHoverTransition (
            this Control control,
            float hoverScale = 1.04f,
            int duration = 140)
        {
            control.MouseEnter += async (_, __) => {
                await control.ScaleToAsync (hoverScale, duration, Easings.EaseOutCubic);
            };

            control.MouseLeave += async (_, __) => {
                await control.ScaleToAsync (1f, duration, Easings.EaseOutCubic);
            };
        }
    }
}
