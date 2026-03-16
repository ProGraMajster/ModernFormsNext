using System;
using System.Threading.Tasks;

namespace ModernFormsNext.Animations
{
    /// <summary>
    /// Provides animation extension methods for controls.
    /// </summary>
    public static class ControlAnimationExtensions
    {
        /// <summary>
        /// Cancels all animations running on the control.
        /// </summary>
        /// <param name="control">The target control.</param>
        public static void CancelAnimations (this Control control)
        {
            AnimationManager.CancelAll (control);
        }

        /// <summary>
        /// Animates the control opacity.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="opacity">The target opacity in the range from 0 to 1.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task FadeToAsync (this Control control, float opacity, int duration = 250, Func<float, float>? easing = null)
        {
            opacity = Math.Clamp (opacity, 0f, 1f);

            var animation = new Animation (
                control,
                "Opacity",
                control.Opacity,
                opacity,
                duration,
                value => control.Opacity = value,
                easing);

            return AnimationManager.AddOrReplace (animation);
        }

        /// <summary>
        /// Animates the control translation.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="x">The target horizontal translation.</param>
        /// <param name="y">The target vertical translation.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task TranslateToAsync (this Control control, float x, float y, int duration = 250, Func<float, float>? easing = null)
        {
            var xAnimation = new Animation (
                control,
                "TranslationX",
                control.TranslationX,
                x,
                duration,
                value => control.TranslationX = value,
                easing);

            var yAnimation = new Animation (
                control,
                "TranslationY",
                control.TranslationY,
                y,
                duration,
                value => control.TranslationY = value,
                easing);

            return Task.WhenAll (
                AnimationManager.AddOrReplace (xAnimation),
                AnimationManager.AddOrReplace (yAnimation));
        }

        /// <summary>
        /// Animates the control scale uniformly.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="scale">The target uniform scale.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task ScaleToAsync (this Control control, float scale, int duration = 250, Func<float, float>? easing = null)
        {
            return control.ScaleToAsync (scale, scale, duration, easing);
        }

        /// <summary>
        /// Animates the control scale.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="scaleX">The target horizontal scale.</param>
        /// <param name="scaleY">The target vertical scale.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task ScaleToAsync (this Control control, float scaleX, float scaleY, int duration = 250, Func<float, float>? easing = null)
        {
            var xAnimation = new Animation (
                control,
                "ScaleX",
                control.ScaleX,
                scaleX,
                duration,
                value => control.ScaleX = value,
                easing);

            var yAnimation = new Animation (
                control,
                "ScaleY",
                control.ScaleY,
                scaleY,
                duration,
                value => control.ScaleY = value,
                easing);

            return Task.WhenAll (
                AnimationManager.AddOrReplace (xAnimation),
                AnimationManager.AddOrReplace (yAnimation));
        }

        /// <summary>
        /// Animates the control rotation in degrees.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="rotation">The target rotation in degrees.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="easing">The easing function.</param>
        /// <returns>A task that completes when the animation finishes.</returns>
        public static Task RotateToAsync (this Control control, float rotation, int duration = 250, Func<float, float>? easing = null)
        {
            var animation = new Animation (
                control,
                "Rotation",
                control.Rotation,
                rotation,
                duration,
                value => control.Rotation = value,
                easing);

            return AnimationManager.AddOrReplace (animation);
        }
    }
}
