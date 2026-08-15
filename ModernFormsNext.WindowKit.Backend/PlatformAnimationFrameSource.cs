namespace ModernFormsNext.WindowKit.Backend;

/// <summary>
/// Provides an idle-aware platform frame callback for the shared animation scheduler.
/// </summary>
/// <remarks>
/// <para>
/// Platform backends implement this contract with their native display-synchronization source.
/// The callback is a pacing signal only: elapsed animation time remains owned by the shared
/// monotonic scheduler clock, so irregular or dropped frames never accumulate a callback backlog.
/// </para>
/// <para>
/// <see cref="Start"/> and <see cref="Stop"/> must be thread-safe and idempotent. An implementation
/// must keep at most one native callback pending for one source, stop requesting callbacks while
/// idle, and release the callback delegate after <see cref="Stop"/>. <see cref="Start"/> schedules
/// future delivery and must not invoke the supplied callback synchronously.
/// </para>
/// <para>
/// The callback may execute on a platform UI thread. It must remain short and must not be invoked
/// while an implementation-specific lock is held.
/// </para>
/// </remarks>
public interface IPlatformAnimationFrameSource
{
    /// <summary>Gets whether a native frame callback is currently pending.</summary>
    bool IsCallbackPending { get; }

    /// <summary>
    /// Starts or updates frame delivery for active scheduler work.
    /// </summary>
    /// <param name="frameRequested">
    /// The short callback that asks the shared scheduler to process the current monotonic time.
    /// </param>
    void Start(Action frameRequested);

    /// <summary>
    /// Stops frame delivery and releases the previously supplied callback.
    /// </summary>
    void Stop();
}
