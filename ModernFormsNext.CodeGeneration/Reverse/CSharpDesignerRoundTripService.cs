using System.Security.Cryptography;
using System.Text;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designing;

namespace ModernFormsNext.CodeGeneration.Reverse;

/// <summary>
/// Coordinates forward C# designer generation and conservative reverse parsing.
/// </summary>
/// <remarks>
/// Hosts such as the standalone designer playground and a future Visual Studio
/// extension should use this service instead of duplicating generation or reverse
/// parsing logic.
/// </remarks>
public sealed class CSharpDesignerRoundTripService
{
    private readonly CSharpDesignerGenerator generator = new();
    private readonly CSharpDesignerParser parser = new();

    /// <summary>
    /// Generates C# designer code from a design document.
    /// </summary>
    /// <param name="document">The source design document.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>The generated code and validation result.</returns>
    public CSharpDesignerGenerationResult Generate(
        DesignDocument document,
        CSharpDesignerGenerationOptions? options = null)
        => generator.Generate(document, options);

    /// <summary>
    /// Parses supported C# designer code back into a design document.
    /// </summary>
    /// <param name="sourceText">The C# designer source text.</param>
    /// <param name="options">Parser options.</param>
    /// <returns>The parsed document and diagnostics.</returns>
    public CSharpDesignerParseResult ParseDesignerCode(
        string sourceText,
        CSharpDesignerParseOptions? options = null)
        => parser.Parse(sourceText, options);

    /// <summary>
    /// Computes a stable SHA-256 hash for a design document.
    /// </summary>
    /// <param name="document">The design document to hash.</param>
    /// <returns>A lowercase hexadecimal SHA-256 hash of the serialized document.</returns>
    public string ComputeDesignHash(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var json = DesignDocumentSerializer.Default.Serialize(document);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
