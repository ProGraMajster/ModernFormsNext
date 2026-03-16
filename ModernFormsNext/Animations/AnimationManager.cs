using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ModernFormsNext.Animations
{
    /// <summary>
    /// Manages running animations for controls.
    /// </summary>
    internal static class AnimationManager
    {
        private static readonly object sync = new ();
        private static readonly List<Animation> animations = new ();
        private static readonly Stopwatch stopwatch = Stopwatch.StartNew ();

        private static bool loopRunning;

        /// <summary>
        /// Adds an animation or replaces an existing animation for the same control and key.
        /// </summary>
        /// <param name="animation">The animation to add.</param>
        /// <returns>A task that completes when the animation finishes or is cancelled.</returns>
        public static Task AddOrReplace (Animation animation)
        {
            if (animation is null)
                throw new ArgumentNullException (nameof (animation));

            lock (sync) {
                for (var i = animations.Count - 1; i >= 0; i--) {
                    if (animations[i].Target == animation.Target && animations[i].Key == animation.Key) {
                        animations[i].Cancel ();
                        animations.RemoveAt (i);
                    }
                }

                animation.StartTimeMs = stopwatch.ElapsedMilliseconds;
                animations.Add (animation);

                if (!loopRunning) {
                    loopRunning = true;
                    _ = RunLoopAsync ();
                }
            }

            return animation.Completion;
        }

        /// <summary>
        /// Cancels all animations running on the specified control.
        /// </summary>
        /// <param name="control">The target control.</param>
        public static void CancelAll (Control control)
        {
            if (control is null)
                return;

            lock (sync) {
                for (var i = animations.Count - 1; i >= 0; i--) {
                    if (animations[i].Target == control) {
                        animations[i].Cancel ();
                        animations.RemoveAt (i);
                    }
                }
            }
        }

        /// <summary>
        /// Cancels a specific animation running on the specified control.
        /// </summary>
        /// <param name="control">The target control.</param>
        /// <param name="key">The animation key.</param>
        public static void Cancel (Control control, string key)
        {
            if (control is null || string.IsNullOrEmpty (key))
                return;

            lock (sync) {
                for (var i = animations.Count - 1; i >= 0; i--) {
                    if (animations[i].Target == control && animations[i].Key == key) {
                        animations[i].Cancel ();
                        animations.RemoveAt (i);
                    }
                }
            }
        }

        private static async Task RunLoopAsync ()
        {
            try {
                while (true) {
                    Animation[] current;

                    lock (sync) {
                        if (animations.Count == 0) {
                            loopRunning = false;
                            return;
                        }

                        current = animations.ToArray ();
                    }

                    var now = stopwatch.ElapsedMilliseconds;

                    foreach (var animation in current) {
                        if (animation.IsCancelled)
                            continue;

                        var elapsed = now - animation.StartTimeMs;
                        var rawProgress = Math.Clamp ((float)elapsed / animation.Duration, 0f, 1f);
                        var easedProgress = animation.Easing (rawProgress);
                        var value = Lerp (animation.StartValue, animation.EndValue, easedProgress);

                        animation.Apply (value);

                        if (rawProgress >= 1f) {
                            lock (sync) {
                                animations.Remove (animation);
                            }

                            animation.Complete ();
                        }
                    }

                    await Task.Delay (16).ConfigureAwait (false);
                }
            } finally {
                lock (sync) {
                    if (animations.Count == 0)
                        loopRunning = false;
                }
            }
        }

        private static float Lerp (float from, float to, float t)
            => from + ((to - from) * t);
    }
}
