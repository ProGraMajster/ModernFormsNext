using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Configures reverse parsing of ModernFormsNext-generated C# designer code.
/// </summary>
public sealed class CSharpDesignerParseOptions
{
    /// <summary>
    /// Gets or sets an optional namespace override for the parsed design document.
    /// </summary>
    public string? NamespaceOverride { get; set; }

    /// <summary>
    /// Gets or sets an optional class name override for the parsed design document.
    /// </summary>
    public string? ClassNameOverride { get; set; }

    /// <summary>
    /// Gets or sets an optional fallback form name used when code does not assign <c>this.Name</c> or <c>this.Text</c>.
    /// </summary>
    public string? FormNameOverride { get; set; }

    /// <summary>
    /// Gets or sets the fallback form size used when code does not assign <c>this.Size</c> or <c>this.ClientSize</c>.
    /// </summary>
    public DesignSize DefaultFormSize { get; set; } = new(800, 600);

    /// <summary>
    /// Gets or sets a value indicating whether warnings should make the parse result unsuccessful.
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; }
}
