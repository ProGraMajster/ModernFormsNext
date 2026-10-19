namespace ModernFormsNext;

/// <summary>Identifies how a <see cref="FlyoutPage"/> arranges its retained flyout and detail.</summary>
public enum FlyoutDisplayMode
{
    /// <summary>The flyout opens above the retained detail, with an owned dismissal backdrop.</summary>
    Overlay,
    /// <summary>The flyout and detail are presented side by side regardless of overlay open intent.</summary>
    Split,
    /// <summary>Choose Split at or above the logical breakpoint, otherwise Overlay.</summary>
    Auto
}
