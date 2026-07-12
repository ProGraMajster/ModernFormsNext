namespace ModernFormsNext.WindowKit.Threading;

/// <summary>
/// Provides a platform-neutral gateway to a platform UI thread.
/// </summary>
/// <remarks>
/// Platform backends implement this service using their native main-loop primitive. Posted and
/// invoked delegates run on the UI thread and should not perform blocking work.
/// </remarks>
public interface IPlatformDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the caller is currently running on the platform UI thread.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Schedules an action on the platform UI thread without waiting for it to complete.
    /// </summary>
    /// <param name="action">The action to schedule.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    void Post(Action action);

    /// <summary>
    /// Runs an action on the platform UI thread and completes when the action finishes.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">A token that can cancel work before it starts.</param>
    /// <returns>A task that propagates cancellation and exceptions from the action.</returns>
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a function on the platform UI thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The function result type.</typeparam>
    /// <param name="function">The function to invoke.</param>
    /// <param name="cancellationToken">A token that can cancel work before it starts.</param>
    /// <returns>A task that produces the function result and propagates cancellation or exceptions.</returns>
    Task<T> InvokeAsync<T>(Func<T> function, CancellationToken cancellationToken = default);
}
