namespace ModernFormsNext.Virtualization;

/// <summary>Creates and binds owned visual or drawn-item containers for an indexed source.</summary>
/// <typeparam name="TContainer">A reference-type container. Native/backend types need not appear in shared APIs.</typeparam>
/// <remarks>
/// The factory is borrowed; the generator owns each successfully returned distinct container until DiscardContainer.
/// Calls occur synchronously on the generator host's owning thread. A control adapter must use the existing UI-thread
/// checks and focus/input retirement before detach/recycle. Callbacks may request another range, notify mutations,
/// or dispose the generator; no callback should block the UI thread. The source is owned separately by the host.
/// </remarks>
public interface IItemContainerFactory<TContainer> where TContainer : class
{
    /// <summary>Creates one distinct, unbound container that has not been discarded. Ownership transfers on successful return.</summary>
    /// <returns>A non-null container not already owned by the generator.</returns>
    TContainer CreateContainer();
    /// <summary>Binds a container to one current logical occurrence and attaches it to its existing host where appropriate.</summary>
    /// <param name="container">The owned container, previously cleared or newly created.</param>
    /// <param name="index">The current source index. A lazy/paged source may present a bounded placeholder.</param>
    /// <remarks>The binding is retired with ClearContainer even if this callback fails partway through initialization.</remarks>
    void PrepareContainer(TContainer container, int index);
    /// <summary>Retires a previous binding, including subscriptions, commands, input/capture/focus and host attachment.</summary>
    /// <param name="container">The owned container that must become unbound before reuse/discard.</param>
    /// <param name="previousIndex">Its recorded binding index. The source may already have changed; use saved binding data.</param>
    /// <remarks>Do not resolve the old item from the mutated source. Do not dispose a recyclable container here.</remarks>
    void ClearContainer(TContainer container, int previousIndex);
    /// <summary>Releases a cleared container permanently, including all resources owned by it.</summary>
    /// <param name="container">The container whose ownership is being relinquished. This method is called at most once per container.</param>
    /// <remarks>The factory determines real disposal; an invocation counter is not proof of successful native/resource cleanup.</remarks>
    void DiscardContainer(TContainer container);
}
