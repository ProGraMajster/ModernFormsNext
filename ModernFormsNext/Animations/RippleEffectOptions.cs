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

/// <summary>Specifies which active wave is evicted when a ripple limit is reached.</summary>
public enum RippleEvictionPolicy
{
    /// <summary>Cancel and remove the oldest active wave.</summary>
    Oldest
}
