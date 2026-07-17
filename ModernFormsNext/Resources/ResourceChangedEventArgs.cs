namespace ModernFormsNext;

/// <summary>
/// Provides data for <see cref="ResourceDictionary.ResourceChanged"/>.
/// </summary>
public sealed class ResourceChangedEventArgs : EventArgs
{
    internal ResourceChangedEventArgs(
        object key,
        object? oldValue,
        object? newValue,
        ResourceChangeKind changeKind)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
        ChangeKind = changeKind;
    }

    /// <summary>
    /// Gets the resource key that changed.
    /// </summary>
    public object Key { get; }

    /// <summary>
    /// Gets the value that was stored before the change, or <see langword="null"/> when the
    /// resource did not previously exist.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the value stored after the change, or <see langword="null"/> when the resource was
    /// removed. A <see langword="null"/> value can also be an explicitly stored resource; use
    /// <see cref="ChangeKind"/> to distinguish removal from replacement.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Gets the operation that changed the resource.
    /// </summary>
    public ResourceChangeKind ChangeKind { get; }
}
