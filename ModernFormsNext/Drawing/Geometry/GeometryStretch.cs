namespace ModernFormsNext.Drawing;

/// <summary>Specifies how a shape maps transformed geometry into its padded client area.</summary>
public enum GeometryStretch
{
    /// <summary>Preserves raw logical coordinates, including their origin. This is the default.</summary>
    None,
    /// <summary>Scales each non-degenerate axis independently to fill the available area.</summary>
    Fill,
    /// <summary>Preserves proportions and centers the complete geometry inside the available area.</summary>
    Uniform,
    /// <summary>Preserves proportions and centers geometry to cover the area; excess pixels are clipped.</summary>
    UniformToFill
}
