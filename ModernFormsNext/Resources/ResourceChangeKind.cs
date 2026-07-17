namespace ModernFormsNext;

/// <summary>
/// Identifies the kind of change made to a <see cref="ResourceDictionary"/> entry.
/// </summary>
public enum ResourceChangeKind
{
    /// <summary>
    /// A new resource was added.
    /// </summary>
    Added,

    /// <summary>
    /// An existing resource value was replaced.
    /// </summary>
    Replaced,

    /// <summary>
    /// A resource was removed explicitly.
    /// </summary>
    Removed,

    /// <summary>
    /// A resource was removed as part of clearing its dictionary.
    /// </summary>
    Cleared
}
