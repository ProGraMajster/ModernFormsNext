using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Threading;

    /// <summary>
    /// Provides services for managing work items on a thread.
    /// </summary>
    /// <remarks>
    /// In Avalonia, there is usually only a single <see cref="Dispatcher"/> in the application -
    /// the one for the UI thread, retrieved via the <see cref="UIThread"/> property.
    /// </remarks>
public partial class Dispatcher : IDispatcher
    {
    private IDispatcherImpl _impl;
    internal object InstanceLock { get; } = new();
    private IControlledDispatcherImpl? _controlledImpl;
    private static Dispatcher? s_uiThread;
    private static readonly object s_testingScopeLock = new();
    private static bool s_testingScopeActive;
    private IDispatcherImplWithPendingInput? _pendingInputImpl;
    private readonly IDispatcherImplWithExplicitBackgroundProcessing? _backgroundProcessingImpl;

    private readonly AvaloniaSynchronizationContext?[] _priorityContexts =
        new AvaloniaSynchronizationContext?[DispatcherPriority.MaxValue - DispatcherPriority.MinValue + 1];

    internal Dispatcher(IDispatcherImpl impl)
    {
        _impl = impl;
        impl.Timer += OnOSTimer;
        impl.Signaled += Signaled;
        _controlledImpl = _impl as IControlledDispatcherImpl;
        _pendingInputImpl = _impl as IDispatcherImplWithPendingInput;
        _backgroundProcessingImpl = _impl as IDispatcherImplWithExplicitBackgroundProcessing;
        if (_backgroundProcessingImpl != null)
            _backgroundProcessingImpl.ReadyForBackgroundProcessing += OnReadyForExplicitBackgroundProcessing;
    }

    /// <summary>
    /// Gets the dispatcher associated with the UI thread.
    /// </summary>
    public static Dispatcher UIThread => s_uiThread ??= CreateUIThreadDispatcher();

    /// <summary>
    /// Gets a value indicating whether this dispatcher supports nested run loops.
    /// </summary>
    public bool SupportsRunLoops => _controlledImpl != null;

    private static Dispatcher CreateUIThreadDispatcher()
    {
        var impl = AvaloniaLocator.Current.GetService<IDispatcherImpl>();
        if (impl == null)
        {
            var platformThreading = AvaloniaGlobals.GetService<IPlatformThreadingInterface>();
            if (platformThreading != null)
                impl = new LegacyDispatcherImpl(platformThreading);
            else
                impl = new NullDispatcherImpl();
        }
        return new Dispatcher(impl);
    }

    internal static IDisposable PushUIThreadForTesting(IDispatcherImpl implementation)
    {
        ArgumentNullException.ThrowIfNull(implementation);

        lock (s_testingScopeLock)
        {
            if (s_testingScopeActive)
                throw new InvalidOperationException("A deterministic ModernFormsNext UI dispatcher is already active in this process.");

            Dispatcher? previous = s_uiThread;
            Dispatcher installed = new(implementation);
            s_uiThread = installed;
            s_testingScopeActive = true;
            return new TestingDispatcherScope(previous, installed);
        }
    }

    private sealed class TestingDispatcherScope(Dispatcher? previous, Dispatcher installed) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            lock (s_testingScopeLock)
            {
                if (disposed)
                    return;
                if (!ReferenceEquals(s_uiThread, installed))
                    throw new InvalidOperationException("The deterministic ModernFormsNext UI dispatcher scope was replaced before disposal.");

                s_uiThread = previous;
                s_testingScopeActive = false;
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Checks that the current thread is the UI thread.
    /// </summary>
    public bool CheckAccess() => _impl?.CurrentThreadIsLoopThread ?? true;

    /// <summary>
    /// Checks that the current thread is the UI thread and throws if not.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The current thread is not the UI thread.
    /// </exception>
    public void VerifyAccess()
        {
        if (!CheckAccess())
        {
            // Used to inline VerifyAccess.
            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            static void ThrowVerifyAccess()
                => throw new InvalidOperationException("Call from invalid thread");
            ThrowVerifyAccess();
        }
    }

    internal AvaloniaSynchronizationContext GetContextWithPriority(DispatcherPriority priority)
    {
        DispatcherPriority.Validate(priority, nameof(priority));
        var index = priority - DispatcherPriority.MinValue;
        return _priorityContexts[index] ??= new(priority);
    }
}
