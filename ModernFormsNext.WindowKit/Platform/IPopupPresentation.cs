namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Optional first-party popup presentation reached through the existing feature lookup.</summary>
/// <remarks>
/// UI thread only. A passive popup never acquires native focus or pointer input. Uniform opacity
/// applies to the whole existing window/view, including its background. Missing/refused support
/// leaves ordinary opaque presentation available. This owns no window, renderer, timer or loop.
/// </remarks>
internal interface IPopupPresentation
{
    /// <summary>Configures a popup as a passive presenter before it is first shown.</summary>
    void SetPassive();

    /// <summary>Applies finite uniform opacity in 0..1, or returns false for unavailable support.</summary>
    /// <remarks>A refusal must not leave a live popup stranded at a partial opacity.</remarks>
    bool TrySetOpacity(float opacity);
}
