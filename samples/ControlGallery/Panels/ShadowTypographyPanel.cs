using System.Diagnostics;
using ModernFormsNext.Validation;

namespace ControlGallery.Panels;

/// <summary>Demonstrates shared shadows and typography using the same example as native validation.</summary>
public sealed class ShadowTypographyPanel : BasePanel
{
    private readonly ShadowTypographyCertification? example;

    /// <summary>Creates a bounded example with an interactive appearance toggle and editable text.</summary>
    public ShadowTypographyPanel()
        => example = new ShadowTypographyCertification(Controls, message => Debug.WriteLine(message));

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing) example?.Dispose();
        base.Dispose(disposing);
    }
}
