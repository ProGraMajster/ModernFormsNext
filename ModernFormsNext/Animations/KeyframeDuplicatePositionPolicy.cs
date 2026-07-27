namespace ModernFormsNext.Animations;

/// <summary>
/// Specifies how a keyframe animation handles two keyframes at the same position.
/// </summary>
public enum KeyframeDuplicatePositionPolicy
{
    /// <summary>Rejects a duplicate position. This is the default.</summary>
    Reject,

    /// <summary>Replaces the previously declared keyframe at the position.</summary>
    ReplacePrevious,

    /// <summary>
    /// Keeps both keyframes and selects the last declared value at the exact position.
    /// </summary>
    KeepBoth
}
