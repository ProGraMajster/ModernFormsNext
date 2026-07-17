namespace ModernFormsNext;

/// <summary>
/// Provides information about a dynamic resource value that could not be applied to a control.
/// </summary>
public sealed class ResourceReferenceErrorEventArgs : EventArgs
{
    internal ResourceReferenceErrorEventArgs(
        object resourceKey,
        string propertyName,
        Type expectedType,
        Type? actualType,
        Exception exception)
    {
        ResourceKey = resourceKey;
        PropertyName = propertyName;
        ExpectedType = expectedType;
        ActualType = actualType;
        Exception = exception;
    }

    /// <summary>
    /// Gets the key of the resource that could not be applied.
    /// </summary>
    public object ResourceKey { get; }

    /// <summary>
    /// Gets the name of the target control property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the type accepted by the target property.
    /// </summary>
    public Type ExpectedType { get; }

    /// <summary>
    /// Gets the runtime type of the resource value, or <see langword="null"/> when the value was
    /// <see langword="null"/>.
    /// </summary>
    public Type? ActualType { get; }

    /// <summary>
    /// Gets the exception describing why the value could not be applied.
    /// </summary>
    public Exception Exception { get; }
}
