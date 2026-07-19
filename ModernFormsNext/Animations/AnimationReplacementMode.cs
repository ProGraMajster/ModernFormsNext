namespace ModernFormsNext.Animations;

/// <summary>
/// Specifies how a new animation handles an existing animation with the same owner and key.
/// </summary>
public enum AnimationReplacementMode
{
    /// <summary>Cancel the existing animation and install the new animation.</summary>
    Replace,

    /// <summary>Keep the existing animation and return its handle without scheduling the new one.</summary>
    IgnoreNew
}
