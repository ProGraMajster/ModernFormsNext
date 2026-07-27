using System;
using System.Threading;
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

        /// <summary>Runs a typed control animation on the shared scheduler.</summary>
        /// <typeparam name="T">The value type produced on each UI-dispatcher frame.</typeparam>
        /// <param name="control">The target and owner of the animation.</param>
        /// <param name="key">The owner-local replacement channel.</param>
        /// <param name="from">The captured start value.</param>
        /// <param name="to">The target value.</param>
        /// <param name="interpolator">The typed value interpolator.</param>
        /// <param name="options">Optional duration, delay, easing, and replacement settings.</param>
        /// <param name="update">The UI-thread callback that applies the value.</param>
        /// <param name="cancellationToken">A token that cancels only this scheduled handle.</param>
        /// <returns>The terminal scheduler state.</returns>
        public static async Task<AnimationState> AnimateAsync<T>(
            this Control control,
            string key,
            T from,
            T to,
            IAnimationInterpolator<T> interpolator,
            AnimationOptions? options,
            Action<T> update,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(control);
            AnimationHandle handle = AnimationScheduler.Default.Animate(
                control,
                key,
                from,
                to,
                interpolator,
                update,
                options);
            using CancellationTokenRegistration registration =
                cancellationToken.CanBeCanceled ? cancellationToken.Register(handle.Cancel) : default;
            return await handle.Completion.ConfigureAwait(false);
        }

        /// <summary>Creates a reusable opacity animation bound to the control.</summary>
        public static PropertyAnimation<float> FadeTo(
            this Control control,
            float opacity,
            AnimationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            var definition = new PropertyAnimation<float>(
                control,
                "Opacity",
                () => control.Opacity,
                Math.Clamp(opacity, 0f, 1f),
                AnimationInterpolators.Float,
                value => control.Opacity = value);
            ApplyOptions(definition, options);
            return definition;
        }

        /// <summary>Creates a reusable translation animation bound to the control.</summary>
        public static AnimationDefinition TranslateTo(
            this Control control,
            float x,
            float y,
            AnimationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            var xAnimation = CreateFloatProperty(
                control, "TranslationX", () => control.TranslationX, x, value => control.TranslationX = value, options);
            var yAnimation = CreateFloatProperty(
                control, "TranslationY", () => control.TranslationY, y, value => control.TranslationY = value, options);
            // Preserve the established TranslationX/TranslationY replacement channels at the
            // root. Generic parallel groups intentionally isolate child keys, but doing that here
            // lets an older convenience-helper run overwrite a newer target value.
            return Animation.ParallelPreservingChildChannels(xAnimation, yAnimation);
        }

        /// <summary>Creates a reusable uniform scale animation bound to the control.</summary>
        public static AnimationDefinition ScaleTo(
            this Control control,
            float scale,
            AnimationOptions? options = null)
            => ScaleTo(control, scale, scale, options);

        /// <summary>Creates a reusable two-axis scale animation bound to the control.</summary>
        public static AnimationDefinition ScaleTo(
            this Control control,
            float scaleX,
            float scaleY,
            AnimationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            var xAnimation = CreateFloatProperty(
                control, "ScaleX", () => control.ScaleX, scaleX, value => control.ScaleX = value, options);
            var yAnimation = CreateFloatProperty(
                control, "ScaleY", () => control.ScaleY, scaleY, value => control.ScaleY = value, options);
            return Animation.ParallelPreservingChildChannels(xAnimation, yAnimation);
        }

        /// <summary>Creates a reusable rotation animation bound to the control.</summary>
        public static PropertyAnimation<float> RotateTo(
            this Control control,
            float rotation,
            AnimationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            return CreateFloatProperty(
                control, "Rotation", () => control.Rotation, rotation, value => control.Rotation = value, options);
        }

        /// <summary>
        /// Cancels all animations running on the control.
        /// </summary>
        /// <param name="control">The target control.</param>
        public static void CancelAnimations (this Control control)
        {
            // Preserve the original helper's null-tolerant cancellation behavior.
            if (control is null)
                return;
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
            return control.FadeTo(opacity, CreateOptions(duration, easing)).RunAsync();
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
            return control.TranslateTo(x, y, CreateOptions(duration, easing)).RunAsync();
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
            return control.ScaleTo(scaleX, scaleY, CreateOptions(duration, easing)).RunAsync();
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
            return control.RotateTo(rotation, CreateOptions(duration, easing)).RunAsync();
        }

        private static PropertyAnimation<float> CreateFloatProperty(
            Control control,
            string key,
            Func<float> from,
            float to,
            Action<float> update,
            AnimationOptions? options)
        {
            var definition = new PropertyAnimation<float>(
                control,
                key,
                from,
                to,
                AnimationInterpolators.Float,
                update);
            ApplyOptions(definition, options);
            return definition;
        }

        private static void ApplyOptions(AnimationDefinition definition, AnimationOptions? options)
        {
            if (options is null)
                return;
            definition.Duration = options.Duration;
            definition.Delay = options.Delay;
            definition.Easing = options.Easing;
            definition.ReplacementMode = options.ReplacementMode;
        }

        private static AnimationOptions CreateOptions(int duration, Func<float, float>? easing)
        {
            return new AnimationOptions
            {
                // The legacy helpers accepted every integer and used a one-millisecond minimum.
                // Keep that behavior while the composable TimeSpan APIs retain strict validation.
                Duration = TimeSpan.FromMilliseconds(Math.Max(1, duration)),
                Easing = easing ?? Easings.Linear
            };
        }
    }
}
