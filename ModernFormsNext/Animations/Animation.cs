using System;
using System.Threading.Tasks;

namespace ModernFormsNext.Animations
{
    /// <summary>
    /// Represents a single running animation.
    /// </summary>
    internal sealed class Animation
    {
        private readonly TaskCompletionSource<bool> completionSource = new (TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Initializes a new instance of the <see cref="Animation"/> class.
        /// </summary>
        /// <param name="target">The target control.</param>
        /// <param name="key">The animation key used to replace an existing animation for the same property.</param>
        /// <param name="startValue">The start value.</param>
        /// <param name="endValue">The end value.</param>
        /// <param name="duration">The animation duration in milliseconds.</param>
        /// <param name="apply">The callback used to apply the interpolated value.</param>
        /// <param name="easing">The easing function.</param>
        public Animation (
            Control target,
            string key,
            float startValue,
            float endValue,
            int duration,
            Action<float> apply,
            Func<float, float>? easing = null)
        {
            Target = target ?? throw new ArgumentNullException (nameof (target));
            Key = key ?? throw new ArgumentNullException (nameof (key));
            StartValue = startValue;
            EndValue = endValue;
            Duration = Math.Max (1, duration);
            Apply = apply ?? throw new ArgumentNullException (nameof (apply));
            Easing = easing ?? Easings.Linear;
        }

        /// <summary>
        /// Gets the target control.
        /// </summary>
        public Control Target { get; }

        /// <summary>
        /// Gets the animation key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the start value.
        /// </summary>
        public float StartValue { get; }

        /// <summary>
        /// Gets the end value.
        /// </summary>
        public float EndValue { get; }

        /// <summary>
        /// Gets the duration of the animation in milliseconds.
        /// </summary>
        public int Duration { get; }

        /// <summary>
        /// Gets the easing function.
        /// </summary>
        public Func<float, float> Easing { get; }

        /// <summary>
        /// Gets the callback used to apply interpolated values.
        /// </summary>
        public Action<float> Apply { get; }

        /// <summary>
        /// Gets or sets the animation start time in milliseconds.
        /// </summary>
        public long StartTimeMs { get; set; }

        /// <summary>
        /// Gets a value indicating whether the animation has been cancelled.
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// Gets the completion task for the animation.
        /// </summary>
        public Task Completion => completionSource.Task;

        /// <summary>
        /// Cancels the animation.
        /// </summary>
        public void Cancel ()
        {
            if (IsCancelled)
                return;

            IsCancelled = true;
            completionSource.TrySetResult (false);
        }

        /// <summary>
        /// Completes the animation.
        /// </summary>
        public void Complete ()
        {
            completionSource.TrySetResult (true);
        }
    }
}
