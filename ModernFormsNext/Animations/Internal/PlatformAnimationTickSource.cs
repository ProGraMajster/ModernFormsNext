using System.Diagnostics;
using ModernFormsNext.WindowKit.Backend;

namespace ModernFormsNext.Animations;

/// <summary>
/// Selects a registered platform frame source without making default-scheduler initialization
/// order observable to framework applications.
/// </summary>
/// <remarks>
/// The shared scheduler can be initialized by a static control or theme reference before the
/// platform backend starts. In that case this source temporarily uses the process fallback and
/// promotes active demand to the native frame source as soon as the backend registers it. The
/// registry owns the platform source; this adapter only stops it when scheduler demand ends.
/// </remarks>
internal sealed class PlatformAnimationTickSource : IAnimationTickSource
{
    private readonly object sync = new();
    private readonly IAnimationTickSource fallback;
    private readonly Func<IPlatformAnimationFrameSource?> resolvePlatformSource;
    private IPlatformAnimationFrameSource? platformSource;
    private IPlatformAnimationFrameSource? rejectedPlatformSource;
    private Action? tickRequested;
    private bool isRunning;
    private bool disposed;

    public PlatformAnimationTickSource()
        : this(
            new ThreadPoolAnimationTickSource(),
            static () => PlatformServiceRegistry.GetService<IPlatformAnimationFrameSource>())
    {
    }

    internal PlatformAnimationTickSource(
        IAnimationTickSource fallback,
        Func<IPlatformAnimationFrameSource?> resolvePlatformSource)
    {
        this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        this.resolvePlatformSource = resolvePlatformSource
            ?? throw new ArgumentNullException(nameof(resolvePlatformSource));
    }

    public bool IsRunning
    {
        get
        {
            lock (sync)
                return isRunning;
        }
    }

    public void Start(Action tickRequested)
    {
        ArgumentNullException.ThrowIfNull(tickRequested);

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            this.tickRequested = tickRequested;
            isRunning = true;

            if (platformSource is { } existing)
            {
                // Start is also the callback-update operation defined by the public frame-source
                // contract. Android keeps this idempotent and never adds a duplicate callback.
                if (TryUsePlatformSourceLocked(existing, tickRequested))
                    return;
            }

            IPlatformAnimationFrameSource? candidate = ResolvePlatformSourceLocked();
            if (candidate is not null && TryUsePlatformSourceLocked(candidate, tickRequested))
                return;

            fallback.Start(HandleFallbackTick);
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            if (disposed)
                return;

            isRunning = false;
            tickRequested = null;
            fallback.Stop();
            StopPlatformSourceLocked();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;

            disposed = true;
            isRunning = false;
            tickRequested = null;
            fallback.Dispose();
            StopPlatformSourceLocked();
            platformSource = null;
            rejectedPlatformSource = null;
        }
    }

    private void HandleFallbackTick()
    {
        Action? callback;
        lock (sync)
        {
            if (disposed || !isRunning || platformSource is not null)
                return;

            callback = tickRequested;
            IPlatformAnimationFrameSource? candidate = ResolvePlatformSourceLocked();
            if (candidate is not null && callback is not null &&
                TryUsePlatformSourceLocked(candidate, callback))
            {
                // The native source owns the next pacing signal. Do not deliver the fallback tick
                // as well, otherwise backend startup could produce two scheduler requests.
                return;
            }
        }

        callback?.Invoke();
    }

    private IPlatformAnimationFrameSource? ResolvePlatformSourceLocked()
    {
        IPlatformAnimationFrameSource? candidate;
        try
        {
            candidate = resolvePlatformSource();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Failed to resolve the platform animation frame source: {exception}");
            return null;
        }

        return ReferenceEquals(candidate, rejectedPlatformSource) ? null : candidate;
    }

    private bool TryUsePlatformSourceLocked(
        IPlatformAnimationFrameSource candidate,
        Action callback)
    {
        try
        {
            candidate.Start(callback);
            platformSource = candidate;
            fallback.Stop();
            return true;
        }
        catch (Exception exception)
        {
            // A native source that cannot accept demand must not strand an already registered
            // shared animation. Reject that immutable registry instance and keep the safe fallback.
            try
            {
                // Start is permitted to fail after recording demand (for example when Android's
                // main Looper is already shutting down). Stop must still release that delegate.
                candidate.Stop();
            }
            catch (Exception cleanupException)
            {
                Trace.TraceError(
                    $"Failed to stop a rejected platform animation frame source: {cleanupException}");
            }

            platformSource = null;
            rejectedPlatformSource = candidate;
            fallback.Start(HandleFallbackTick);
            Trace.TraceError($"Failed to start the platform animation frame source: {exception}");
            return false;
        }
    }

    private void StopPlatformSourceLocked()
    {
        if (platformSource is not { } source)
            return;

        try
        {
            source.Stop();
        }
        catch (Exception exception)
        {
            // Native Stop implementations clear their delegate before unscheduling the platform
            // callback. A shutdown-time unschedule failure must not break scheduler cleanup.
            Trace.TraceError($"Failed to stop the platform animation frame source: {exception}");
        }
    }
}
