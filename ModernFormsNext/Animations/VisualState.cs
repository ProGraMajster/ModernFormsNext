namespace ModernFormsNext.Animations;

/// <summary>Identifies the framework interaction state used by style transitions.</summary>
public enum VisualState
{
    /// <summary>The enabled control is idle and does not have pointer hover or focus.</summary>
    Normal,

    /// <summary>The pointer is over the enabled control.</summary>
    Hover,

    /// <summary>A pointer or activation key is currently held on the enabled control.</summary>
    Pressed,

    /// <summary>The enabled control has keyboard focus without a higher-priority state.</summary>
    Focused,

    /// <summary>The control is not enabled, including disabled-by-parent state.</summary>
    Disabled
}
