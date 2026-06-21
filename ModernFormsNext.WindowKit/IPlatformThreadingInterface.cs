using System;
using System.Threading;
using ModernFormsNext.WindowKit.Metadata;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Provides platform-specific services relating to threading.
    /// </summary>
    [PrivateApi]
    public interface IPlatformThreadingInterface
    {
        /// <summary>
        /// Starts a timer.
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="interval">The interval.</param>
        /// <param name="tick">The action to call on each tick.</param>
        /// <returns>An <see cref="IDisposable"/> used to stop the timer.</returns>
        IDisposable StartTimer(DispatcherPriority priority, TimeSpan interval, Action tick);

        /// <summary>
        /// Signals the dispatcher that work is available at the specified priority.
        /// </summary>
        /// <param name="priority">The priority of the queued dispatcher work.</param>
        void Signal(DispatcherPriority priority);

        /// <summary>
        /// Gets a value indicating whether the current thread owns the platform dispatcher loop.
        /// </summary>
        bool CurrentThreadIsLoopThread { get; }

        /// <summary>
        /// Raised when the platform dispatcher has been signaled.
        /// </summary>
        event Action<DispatcherPriority?>? Signaled;
    }
}
