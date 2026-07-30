namespace ModernFormsNext.Animations;

/// <summary>Specifies how the maximum ripple radius is resolved.</summary>
public enum RippleRadiusMode
{
    /// <summary>Recomputes the farthest target corner so the ripple covers resized controls.</summary>
    CoverControl,

    /// <summary>Uses <see cref="RippleEffect.FixedRadius"/> logical pixels.</summary>
    Fixed
}

/// <summary>Specifies the shared layer used to draw ripple waves.</summary>
public enum RippleLayer
{
    /// <summary>Draw after background and border, before content.</summary>
    AboveBackgroundBelowContent,

    /// <summary>Draw after content and before the framework focus overlay.</summary>
    AboveContent
}

/// <summary>Specifies how a new ripple is handled when the active-wave limit is reached.</summary>
public enum RippleOverflowPolicy
{
    /// <summary>Cancel the oldest active wave and start the new wave.</summary>
    RemoveOldest,

    /// <summary>Cancel the newest active wave and start the new wave.</summary>
    RemoveNewest,

    /// <summary>Keep every active wave and do not create a wave or scheduler handle.</summary>
    IgnoreNew,

    /// <summary>Cancel every active wave and start only the new wave.</summary>
    ReplaceAll
}

/// <summary>
/// Specifies which active wave is evicted when a ripple limit is reached.
/// </summary>
/// <remarks>
/// This compatibility surface maps to <see cref="RippleEffect.OverflowPolicy"/>. New code should
/// prefer <see cref="RippleOverflowPolicy"/>, whose member names describe complete overflow
/// behavior.
/// </remarks>
public enum RippleEvictionPolicy
{
    /// <summary>Cancel and remove the oldest active wave.</summary>
    Oldest,

    /// <summary>Cancel and remove the newest active wave.</summary>
    Newest,

    /// <summary>Keep active waves and ignore the new wave.</summary>
    IgnoreNew,

    /// <summary>Cancel all active waves before starting the new wave.</summary>
    ReplaceAll
}
