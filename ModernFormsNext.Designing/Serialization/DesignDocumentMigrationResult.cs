namespace ModernFormsNext.Designing;

/// <summary>
/// Contains the serialized output and diagnostics produced by one designer document migration.
/// </summary>
public sealed class DesignDocumentMigrationResult
{
    /// <summary>
    /// Initializes a migration result.
    /// </summary>
    /// <param name="json">The complete migrated designer document JSON.</param>
    /// <param name="diagnostics">
    /// Optional user-facing messages that describe compatibility changes made by the migration.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="json"/> or one of the supplied diagnostics is empty.
    /// </exception>
    public DesignDocumentMigrationResult(string json, IEnumerable<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        string[] copiedDiagnostics = diagnostics?.ToArray() ?? [];
        if (copiedDiagnostics.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Migration diagnostics cannot contain null, empty, or whitespace-only messages.",
                nameof(diagnostics));
        }

        Json = json;
        Diagnostics = Array.AsReadOnly(copiedDiagnostics);
    }

    /// <summary>
    /// Gets the complete migrated designer document JSON.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Gets user-facing messages that describe compatibility changes made by the migration.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
