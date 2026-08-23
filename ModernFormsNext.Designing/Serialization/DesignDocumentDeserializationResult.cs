namespace ModernFormsNext.Designing;

/// <summary>
/// Contains a deserialized designer document and format compatibility diagnostics.
/// </summary>
/// <remarks>
/// Use this result when a host needs to explain legacy-format assumptions or migrations to a
/// user. <see cref="DesignDocumentSerializer.Deserialize(string)"/> remains available when the
/// caller only needs the resulting document.
/// </remarks>
public sealed class DesignDocumentDeserializationResult
{
    internal DesignDocumentDeserializationResult(
        DesignDocument document,
        int? sourceFormatVersion,
        bool wasMigrated,
        IEnumerable<string> diagnostics)
    {
        Document = document;
        SourceFormatVersion = sourceFormatVersion;
        FormatVersion = document.Metadata.FormatVersion;
        WasMigrated = wasMigrated;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets the materialized designer document in the current format.
    /// </summary>
    public DesignDocument Document { get; }

    /// <summary>
    /// Gets the format version explicitly declared by the source JSON, or <see langword="null"/>
    /// when the legacy document omitted its version marker.
    /// </summary>
    public int? SourceFormatVersion { get; }

    /// <summary>
    /// Gets the format version of <see cref="Document"/> after compatibility processing.
    /// </summary>
    public int FormatVersion { get; }

    /// <summary>
    /// Gets a value indicating whether one or more registered migrations transformed the JSON.
    /// </summary>
    public bool WasMigrated { get; }

    /// <summary>
    /// Gets user-facing format compatibility and migration diagnostics.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
