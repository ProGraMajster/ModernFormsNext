using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernFormsNext.Designing;

/// <summary>
/// Serializes and deserializes <see cref="DesignDocument"/> instances to stable JSON.
/// </summary>
/// <remarks>
/// The default JSON format is intended for the future <c>.mfdesign</c> extension. It
/// uses camel-case property names, indentation, deterministic property ordering from
/// the model, and primitive JSON values for simple designer properties.
/// </remarks>
public sealed class DesignDocumentSerializer
{
    private const string FormatVersionJsonPath = "$.metadata.formatVersion";
    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IReadOnlyDictionary<int, IDesignDocumentMigration> migrations;

    /// <summary>
    /// Identifies the designer document format written by this serializer.
    /// </summary>
    /// <remarks>
    /// Format version 1 remains the current format. Adding migration infrastructure does not
    /// itself change the persisted <c>.mfdesign</c> schema.
    /// </remarks>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Gets the default serializer instance.
    /// </summary>
    public static DesignDocumentSerializer Default { get; } = new();

    /// <summary>
    /// Initializes a serializer that accepts the current format and versionless legacy documents.
    /// </summary>
    public DesignDocumentSerializer()
        : this(Array.Empty<IDesignDocumentMigration>())
    {
    }

    /// <summary>
    /// Initializes a serializer with trusted migration steps for explicitly supported older JSON.
    /// </summary>
    /// <param name="migrations">
    /// Migration steps keyed by their source format version. Each step must advance toward
    /// <see cref="CurrentFormatVersion"/> and no source version may be registered twice.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a migration is duplicated or does not advance from an older version toward the
    /// current format.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="migrations"/> is <see langword="null"/>.
    /// </exception>
    public DesignDocumentSerializer(IEnumerable<IDesignDocumentMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        this.migrations = CreateMigrationMap(migrations);
    }

    /// <summary>
    /// Gets the JSON serializer options used by the designer document serializer.
    /// </summary>
    public JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>
    /// Converts a designer document to JSON.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>A readable JSON representation of the document.</returns>
    public string Serialize(DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureCurrentDocumentFormat(document);

        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>
    /// Converts JSON into a designer document.
    /// </summary>
    /// <param name="json">The JSON document text.</param>
    /// <returns>The deserialized designer document.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON cannot be parsed, declares an unsupported format version, or cannot be
    /// migrated to the current designer document format.
    /// </exception>
    public DesignDocument Deserialize(string json)
    {
        return DeserializeWithDiagnostics(json).Document;
    }

    /// <summary>
    /// Converts JSON into a current-format designer document and reports compatibility actions.
    /// </summary>
    /// <param name="json">The JSON document text.</param>
    /// <returns>The deserialized document together with migration and compatibility diagnostics.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON is malformed, declares an unsupported format version, or a registered
    /// migration fails to produce its declared target version.
    /// </exception>
    public DesignDocumentDeserializationResult DeserializeWithDiagnostics(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var diagnostics = new List<string>();
        FormatVersionInspection inspection = InspectFormatVersion(json);
        int? sourceFormatVersion = inspection.DeclaredVersion;
        if (sourceFormatVersion is null)
        {
            diagnostics.Add(
                $"The document does not declare {FormatVersionJsonPath}; " +
                $"format version {CurrentFormatVersion} was assumed for backward compatibility.");
        }

        bool wasMigrated = false;
        string currentJson = json;
        int currentVersion = inspection.EffectiveVersion;
        var migratedVersions = new HashSet<int>();

        while (currentVersion != CurrentFormatVersion)
        {
            if (currentVersion > CurrentFormatVersion)
            {
                throw FormatVersionError(
                    $"Designer document format version {currentVersion} is newer than the " +
                    $"supported version {CurrentFormatVersion}.");
            }

            if (!migrations.TryGetValue(currentVersion, out IDesignDocumentMigration? migration))
            {
                throw FormatVersionError(
                    $"Designer document format version {currentVersion} is not supported because " +
                    $"no migration to version {CurrentFormatVersion} is registered.");
            }

            if (!migratedVersions.Add(currentVersion))
            {
                throw FormatVersionError(
                    $"Designer document migration encountered a cycle at format version {currentVersion}.");
            }

            DesignDocumentMigrationResult migrationResult;
            try
            {
                migrationResult = migration.Migrate(currentJson)
                    ?? throw new InvalidOperationException("The migration returned no result.");
            }
            catch (Exception exception)
            {
                throw new JsonException(
                    $"Designer document migration from format version {migration.SourceFormatVersion} " +
                    $"to {migration.TargetFormatVersion} failed: {exception.Message}",
                    FormatVersionJsonPath,
                    lineNumber: null,
                    bytePositionInLine: null,
                    exception);
            }

            FormatVersionInspection migratedInspection = InspectFormatVersion(migrationResult.Json);
            if (migratedInspection.DeclaredVersion != migration.TargetFormatVersion)
            {
                string actualVersion = migratedInspection.DeclaredVersion?.ToString() ?? "missing";
                throw FormatVersionError(
                    $"Designer document migration from format version {migration.SourceFormatVersion} " +
                    $"declared target version {migration.TargetFormatVersion}, but its output version was " +
                    $"{actualVersion}.");
            }

            diagnostics.Add(
                $"Migrated designer document format from version {migration.SourceFormatVersion} " +
                $"to version {migration.TargetFormatVersion}.");
            diagnostics.AddRange(migrationResult.Diagnostics);
            currentJson = migrationResult.Json;
            currentVersion = migration.TargetFormatVersion;
            wasMigrated = true;
        }

        DesignDocument document = JsonSerializer.Deserialize<DesignDocument>(currentJson, Options)
            ?? throw new JsonException("The JSON did not contain a designer document.");
        EnsureCurrentDocumentFormat(document);

        return new DesignDocumentDeserializationResult(
            document,
            sourceFormatVersion,
            wasMigrated,
            diagnostics);
    }

    /// <summary>
    /// Saves a designer document to disk.
    /// </summary>
    /// <param name="path">The destination path, commonly ending in <c>.mfdesign</c>.</param>
    /// <param name="document">The document to save.</param>
    /// <remarks>
    /// The document is written as UTF-8 to a uniquely named file in the destination directory,
    /// flushed to durable storage, and then atomically moved or replaced. A failure before that
    /// final operation leaves an existing destination unchanged.
    /// </remarks>
    public void Save(string path, DesignDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        // Serialize before creating the temporary file. A model or version failure therefore
        // cannot disturb either the destination or an earlier valid temporary replacement.
        string json = Serialize(document);
        string destinationPath = Path.GetFullPath(path);
        string destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new IOException($"The destination path '{path}' has no containing directory.");
        string temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        bool temporaryFileExists = false;
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                temporaryFileExists = true;
                using (var writer = new StreamWriter(
                    stream,
                    Utf8WithoutBom,
                    bufferSize: 4096,
                    leaveOpen: true))
                {
                    writer.Write(json);
                    writer.Flush();
                }

                // Flush the file contents through the operating-system cache before the atomic
                // rename. Until the rename succeeds, an existing destination remains untouched.
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(destinationPath))
            {
                File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }

            temporaryFileExists = false;
        }
        finally
        {
            if (temporaryFileExists)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Loads a designer document from disk.
    /// </summary>
    /// <param name="path">The source path, commonly ending in <c>.mfdesign</c>.</param>
    /// <returns>The loaded designer document.</returns>
    public DesignDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Deserialize(File.ReadAllText(path));
    }

    /// <summary>
    /// Loads a designer document from disk and reports format compatibility actions.
    /// </summary>
    /// <param name="path">The source path, commonly ending in <c>.mfdesign</c>.</param>
    /// <returns>The loaded document together with migration and compatibility diagnostics.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the file contains malformed JSON or an unsupported document format version.
    /// </exception>
    public DesignDocumentDeserializationResult LoadWithDiagnostics(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return DeserializeWithDiagnostics(File.ReadAllText(path));
    }

    private static IReadOnlyDictionary<int, IDesignDocumentMigration> CreateMigrationMap(
        IEnumerable<IDesignDocumentMigration> migrations)
    {
        var result = new Dictionary<int, IDesignDocumentMigration>();
        foreach (IDesignDocumentMigration migration in migrations)
        {
            if (migration is null)
            {
                throw new ArgumentException("Migration collections cannot contain null entries.", nameof(migrations));
            }

            if (migration.SourceFormatVersion < 0 ||
                migration.SourceFormatVersion >= CurrentFormatVersion ||
                migration.TargetFormatVersion <= migration.SourceFormatVersion ||
                migration.TargetFormatVersion > CurrentFormatVersion)
            {
                throw new ArgumentException(
                    $"The migration from version {migration.SourceFormatVersion} to " +
                    $"{migration.TargetFormatVersion} must advance from an older non-negative " +
                    $"version toward current version {CurrentFormatVersion}.",
                    nameof(migrations));
            }

            if (!result.TryAdd(migration.SourceFormatVersion, migration))
            {
                throw new ArgumentException(
                    $"A migration from format version {migration.SourceFormatVersion} is already registered.",
                    nameof(migrations));
            }
        }

        return result;
    }

    private static void EnsureCurrentDocumentFormat(DesignDocument document)
    {
        if (document.Metadata is null)
        {
            throw new InvalidOperationException("Designer document metadata cannot be null.");
        }

        if (document.Metadata.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidOperationException(
                $"Designer document format version {document.Metadata.FormatVersion} cannot be " +
                $"serialized or materialized by a version {CurrentFormatVersion} serializer.");
        }
    }

    private static FormatVersionInspection InspectFormatVersion(string json)
    {
        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                "A designer document must be a JSON object.",
                "$",
                lineNumber: null,
                bytePositionInLine: null);
        }

        if (!root.TryGetProperty("metadata", out JsonElement metadata))
        {
            return new FormatVersionInspection(null, CurrentFormatVersion);
        }

        if (metadata.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                "Designer document metadata must be a JSON object.",
                "$.metadata",
                lineNumber: null,
                bytePositionInLine: null);
        }

        if (!metadata.TryGetProperty("formatVersion", out JsonElement formatVersion))
        {
            return new FormatVersionInspection(null, CurrentFormatVersion);
        }

        if (formatVersion.ValueKind != JsonValueKind.Number || !formatVersion.TryGetInt32(out int version))
        {
            throw FormatVersionError("Designer document formatVersion must be a 32-bit integer.");
        }

        return new FormatVersionInspection(version, version);
    }

    private static JsonException FormatVersionError(string message)
        => new(
            message,
            FormatVersionJsonPath,
            lineNumber: null,
            bytePositionInLine: null);

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup must not hide the write/replace failure that caused this path to run. The
            // uniquely named file remains isolated beside the intended destination for diagnosis.
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        options.Converters.Add(new DesignPropertyValueJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }

    private readonly record struct FormatVersionInspection(int? DeclaredVersion, int EffectiveVersion);
}
