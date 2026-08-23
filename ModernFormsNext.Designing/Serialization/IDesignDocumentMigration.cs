namespace ModernFormsNext.Designing;

/// <summary>
/// Defines one trusted migration step between two designer document format versions.
/// </summary>
/// <remarks>
/// Migrations are registered explicitly by the application that constructs a
/// <see cref="DesignDocumentSerializer"/>. A design document cannot select a migration type,
/// so deserializing untrusted JSON does not instantiate or execute types named by that JSON.
/// Implementations should transform only the serialized data and must not load project code.
/// </remarks>
public interface IDesignDocumentMigration
{
    /// <summary>
    /// Gets the older format version accepted by this migration.
    /// </summary>
    int SourceFormatVersion { get; }

    /// <summary>
    /// Gets the newer format version produced by this migration.
    /// </summary>
    int TargetFormatVersion { get; }

    /// <summary>
    /// Migrates serialized designer document JSON to <see cref="TargetFormatVersion"/>.
    /// </summary>
    /// <param name="sourceJson">The complete JSON document in <see cref="SourceFormatVersion"/>.</param>
    /// <returns>
    /// The migrated JSON and any user-facing diagnostics that explain compatibility changes.
    /// </returns>
    /// <remarks>
    /// The returned JSON must explicitly declare <see cref="TargetFormatVersion"/> in
    /// <c>metadata.formatVersion</c>. The serializer validates that declaration before running
    /// another migration step or materializing a <see cref="DesignDocument"/>.
    /// </remarks>
    DesignDocumentMigrationResult Migrate(string sourceJson);
}
