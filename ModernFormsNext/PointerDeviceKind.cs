namespace ModernFormsNext;

/// <summary>Identifies the pointer source associated with a framework mouse-compatible event.</summary>
public enum PointerDeviceKind
{
    /// <summary>The event came from a mouse or mouse-compatible desktop device.</summary>
    Mouse,

    /// <summary>The event came from a direct touch contact.</summary>
    Touch,

    /// <summary>The event came from a pen or stylus.</summary>
    Pen,

    /// <summary>The platform did not report a specific pointer kind.</summary>
    Unknown
}
