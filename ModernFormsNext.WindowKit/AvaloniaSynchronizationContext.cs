using System;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using ModernFormsNext.WindowKit.Utilities;

namespace ModernFormsNext.WindowKit.Threading
{
    /// <summary>
    /// Provides the synchronization context installed on the dispatcher thread.
    /// </summary>
    public class AvaloniaSynchronizationContext : SynchronizationContext
    {
        internal readonly DispatcherPriority Priority;
        private readonly NonPumpingLockHelper.IHelperImpl? _nonPumpingHelper =
            AvaloniaLocator.Current.GetService<NonPumpingLockHelper.IHelperImpl>();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaSynchronizationContext"/> class for the current thread.
        /// </summary>
        public AvaloniaSynchronizationContext():  this(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            
        }
        
        // This constructor is here to enforce STA behavior for unit tests
        internal AvaloniaSynchronizationContext(bool isStaThread)
        {
            if (_nonPumpingHelper != null 
                && isStaThread)
                SetWaitNotificationRequired();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaSynchronizationContext"/> class.
        /// </summary>
        /// <param name="priority">The dispatcher priority used when posting callbacks.</param>
        public AvaloniaSynchronizationContext(DispatcherPriority priority)
        {
            Priority = priority;
        }
        
        /// <summary>
        /// Controls if SynchronizationContext should be installed in InstallIfNeeded. Used by Designer.
        /// </summary>
        public static bool AutoInstall { get; set; } = true;

        /// <summary>
        /// Installs synchronization context in current thread
        /// </summary>
        public static void InstallIfNeeded()
        {
            if (!AutoInstall || Current is AvaloniaSynchronizationContext)
            {
                return;
            }

            SetSynchronizationContext(Dispatcher.UIThread.GetContextWithPriority(DispatcherPriority.Normal));
        }

        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state)
        {
            Dispatcher.UIThread.Post(d, state, Priority);
        }

        /// <inheritdoc/>
        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Dispatcher.UIThread.CheckAccess())
                d(state);
            else
                Dispatcher.UIThread.InvokeAsync(() => d(state), DispatcherPriority.Send).GetAwaiter().GetResult();
        }
        
        /// <inheritdoc />
#if !NET6_0_OR_GREATER
        [PrePrepareMethod]
#endif
        public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            if (
                _nonPumpingHelper != null
                && Dispatcher.UIThread.CheckAccess() 
                && Dispatcher.UIThread.DisabledProcessingCount > 0)
                return _nonPumpingHelper.Wait(waitHandles, waitAll, millisecondsTimeout);
            return base.Wait(waitHandles, waitAll, millisecondsTimeout);
        }

        /// <summary>
        /// Restores the previous synchronization context when disposed.
        /// </summary>
        public record struct RestoreContext : IDisposable
        {
            private readonly SynchronizationContext? _oldContext;
            private bool _needRestore;

            internal RestoreContext(SynchronizationContext? oldContext)
            {
                _oldContext = oldContext;
                _needRestore = true;
            }
            
            /// <inheritdoc />
            public void Dispose()
            {
                if (_needRestore)
                {
                    SetSynchronizationContext(_oldContext);
                    _needRestore = false;
                }
            }
        }

        /// <summary>
        /// Ensures that the current synchronization context posts to the dispatcher at the requested priority.
        /// </summary>
        /// <param name="priority">The dispatcher priority to install for asynchronous continuations.</param>
        /// <returns>A disposable context that restores the previous synchronization context.</returns>
        public static RestoreContext Ensure(DispatcherPriority priority)
        {
            if (Current is AvaloniaSynchronizationContext avaloniaContext 
                && avaloniaContext.Priority == priority)
                return default;
            var oldContext = Current;
            Dispatcher.UIThread.VerifyAccess();
            SetSynchronizationContext(Dispatcher.UIThread.GetContextWithPriority(priority));
            return new RestoreContext(oldContext);
        }
    }
}
