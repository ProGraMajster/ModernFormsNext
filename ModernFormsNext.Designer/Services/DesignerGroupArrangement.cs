namespace ModernFormsNext.Designer.Services;

/// <summary>Identifies an atomic arrangement of selected sibling controls in the Designer.</summary>
/// <remarks>Positions and sizes use parent-local logical pixels; runtime layout semantics are unchanged.</remarks>
public enum DesignerGroupArrangement
{
    /// <summary>Align left edges with the primary selected control.</summary>
    AlignLeft,
    /// <summary>Align right edges with the primary selected control.</summary>
    AlignRight,
    /// <summary>Align top edges with the primary selected control.</summary>
    AlignTop,
    /// <summary>Align bottom edges with the primary selected control.</summary>
    AlignBottom,
    /// <summary>Use the primary selected control's width.</summary>
    SameWidth,
    /// <summary>Use the primary selected control's height.</summary>
    SameHeight,
    /// <summary>Use the primary selected control's width and height.</summary>
    SameSize,
    /// <summary>Distribute horizontal gaps while retaining the outside left and right edges.</summary>
    DistributeHorizontally,
    /// <summary>Distribute vertical gaps while retaining the outside top and bottom edges.</summary>
    DistributeVertically
}
