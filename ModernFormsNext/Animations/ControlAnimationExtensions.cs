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
        /// Runs a custom eased-progress animation owned by a control.
        /// </summary>
        /// <param name="control">The control that owns the animation lifecycle and replacement key.</param>
        /// <param name="key">The non-empty owner-local animation channel.</param>
        /// <param name="duration">The non-negative unscaled duration.</param>
        /// <param name="update">The UI-thread callback receiving finite eased progress.</param>
        /// <param name="easing">Optional easing function. The default is <see cref="Easings.Linear"/>.</param>
        /// <returns>A handle for cancellation, pause/resume, state, and completion.</returns>
        /// <remarks>
        /// Starting a second animation with the same control and key cancels the first. The control
        /// automatically cancels owned animations when disposed or detached from an established
        /// parent. Use a property setter that performs the correct render or layout invalidation.
        /// This API uses the same scheduler and UI dispatcher on Windows and Android.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> or <paramref name="update"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
        public static AnimationHandle Animate(
            this Control control,
            string key,
            TimeSpan duration,
            Action<float> update,
            Func<float, float>? easing = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            return AnimationScheduler.Default.Start(
                control,
                key,
                update,
                new AnimationOptions
                {
                    Duration = duration,
                    Easing = easing ?? Easings.Linear
                });
        }

        /// <summary>
        /// Cancels all animations running on the control.
        /// </summary>
        /// <param name="control">The target control.</param>
        public static void CancelAnimations (this Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            AnimationScheduler.Default.CancelAll(control);
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

            return AnimationScheduler.Default.Animate(
                control,
                "Opacity",
                control.Opacity,
                opacity,
                AnimationInterpolators.Float,
                value => control.Opacity = value,
                CreateOptions(duration, easing)).Completion;
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
            AnimationHandle xAnimation = AnimationScheduler.Default.Animate(
                control,
                "TranslationX",
                control.TranslationX,
                x,
                AnimationInterpolators.Float,
                value => control.TranslationX = value,
                CreateOptions(duration, easing));

            AnimationHandle yAnimation = AnimationScheduler.Default.Animate(
                control,
                "TranslationY",
                control.TranslationY,
                y,
                AnimationInterpolators.Float,
                value => control.TranslationY = value,
                CreateOptions(duration, easing));

            return Task.WhenAll(xAnimation.Completion, yAnimation.Completion);
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
            AnimationHandle xAnimation = AnimationScheduler.Default.Animate(
                control,
                "ScaleX",
                control.ScaleX,
                scaleX,
                AnimationInterpolators.Float,
                value => control.ScaleX = value,
                CreateOptions(duration, easing));

            AnimationHandle yAnimation = AnimationScheduler.Default.Animate(
                control,
                "ScaleY",
                control.ScaleY,
                scaleY,
                AnimationInterpolators.Float,
                value => control.ScaleY = value,
                CreateOptions(duration, easing));

            return Task.WhenAll(xAnimation.Completion, yAnimation.Completion);
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
            return AnimationScheduler.Default.Animate(
                control,
                "Rotation",
                control.Rotation,
                rotation,
                AnimationInterpolators.Float,
                value => control.Rotation = value,
                CreateOptions(duration, easing)).Completion;
        }

        private static AnimationOptions CreateOptions(int duration, Func<float, float>? easing)
        {
            if (duration < 0)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Animation duration cannot be negative.");

            return new AnimationOptions
            {
                Duration = TimeSpan.FromMilliseconds(duration),
                Easing = easing ?? Easings.Linear
            };
        }
    }
}
