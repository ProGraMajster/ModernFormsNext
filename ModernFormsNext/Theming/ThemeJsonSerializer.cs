using System.Text;
using System.Text.Json;

namespace ModernFormsNext;

/// <summary>
/// Reports a safe theme JSON error together with its logical JSON path.
/// </summary>
public sealed class ThemeSerializationException : Exception
{
    /// <summary>Creates a theme serialization exception.</summary>
    /// <param name="message">A safe user-facing message.</param>
    /// <param name="jsonPath">The logical JSON path, when known.</param>
    /// <param name="innerException">The parser exception, when available.</param>
    public ThemeSerializationException(string message, string? jsonPath = null, Exception? innerException = null)
        : base(jsonPath is null ? message : $"{message} Path: {jsonPath}.", innerException)
    {
        JsonPath = jsonPath;
    }

    /// <summary>Gets the logical JSON path associated with the failure.</summary>
    public string? JsonPath { get; }
}

/// <summary>
/// Loads and saves versioned ModernFormsNext theme JSON using a closed value allow-list.
/// </summary>
/// <remarks>
/// Unknown properties, duplicate properties, arbitrary CLR type names, non-finite numbers, and
/// unsupported future schema versions are rejected. A base-theme identifier is data only; this
/// serializer never probes adjacent files or automatically follows a path.
/// </remarks>
public sealed partial class ThemeJsonSerializer
{
    /// <summary>The only currently supported schema major version.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "id", "name", "description", "author", "baseTheme", "variant",
        "metadata", "tags", "colors", "brushes", "typography", "spacing", "padding", "sizing",
        "corners", "borderThickness", "animations", "resources"
    };

    private readonly ThemeSecurityLimits limits;
    private readonly IReadOnlyDictionary<int, IThemeSchemaMigration> migrations;

    /// <summary>Creates a serializer with default security limits.</summary>
    public ThemeJsonSerializer()
        : this(new ThemeSecurityLimits(), Array.Empty<IThemeSchemaMigration>())
    {
    }

    /// <summary>Creates a serializer with copied, validated security limits.</summary>
    /// <param name="limits">The limits to copy.</param>
    public ThemeJsonSerializer(ThemeSecurityLimits limits)
        : this(limits, Array.Empty<IThemeSchemaMigration>())
    {
    }

    internal ThemeJsonSerializer(
        ThemeSecurityLimits limits,
        IEnumerable<IThemeSchemaMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(migrations);
        this.limits = limits.CloneValidated();
        this.migrations = migrations.ToDictionary(static migration => migration.SourceVersion);
    }

    /// <summary>Gets a defensive copy of the active security limits.</summary>
    public ThemeSecurityLimits Limits => limits.CloneValidated();

    /// <summary>Deserializes a theme from a JSON string.</summary>
    /// <param name="json">The untrusted JSON document.</param>
    /// <returns>A mutable theme definition.</returns>
    public ThemeDefinition Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int bytes = Encoding.UTF8.GetByteCount(json);
        EnsureDocumentSize(bytes);
        return Deserialize(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Deserializes a theme from a stream without closing it.</summary>
    /// <param name="stream">The readable stream.</param>
    /// <returns>A mutable theme definition.</returns>
    public ThemeDefinition Deserialize(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Deserialize(ReadLimited(stream));
    }

    /// <summary>Asynchronously deserializes a theme from a stream without closing it.</summary>
    /// <param name="stream">The readable stream.</param>
    /// <param name="cancellationToken">Cancels reading before parsing starts.</param>
    /// <returns>A mutable theme definition.</returns>
    public async Task<ThemeDefinition> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] data = await ReadLimitedAsync(stream, cancellationToken).ConfigureAwait(false);
        return Deserialize(data);
    }

    /// <summary>Loads a theme from an explicitly selected file.</summary>
    /// <param name="path">The file path selected by the application.</param>
    /// <returns>A mutable theme definition.</returns>
    /// <remarks>The serializer never loads a base theme or neighboring file automatically.</remarks>
    public ThemeDefinition LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Deserialize(stream);
    }

    /// <summary>Asynchronously loads a theme from an explicitly selected file.</summary>
    /// <param name="path">The file path selected by the application.</param>
    /// <param name="cancellationToken">Cancels file reading.</param>
    /// <returns>A mutable theme definition.</returns>
    public async Task<ThemeDefinition> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using FileStream stream = File.OpenRead(path);
        return await DeserializeAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serializes a definition to deterministic JSON.</summary>
    /// <param name="theme">The definition to serialize.</param>
    /// <param name="indented">Whether to pretty-print the document.</param>
    /// <returns>The JSON document.</returns>
    public string Serialize(ThemeDefinition theme, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(theme);
        using var stream = new MemoryStream();
        Serialize(theme, stream, indented);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Serializes a definition to a stream without closing it.</summary>
    /// <param name="theme">The definition to serialize.</param>
    /// <param name="stream">The writable destination stream.</param>
    /// <param name="indented">Whether to pretty-print the document.</param>
    public void Serialize(ThemeDefinition theme, Stream stream, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(stream);
        ThemeValidationResult validation = new ThemeResolver(
            static _ => null,
            static () => ThemeVariant.Light,
            limits).ValidateWithoutBases(theme);
        if (!validation.IsValid)
        {
            ThemeDiagnostic diagnostic = validation.Diagnostics.First(
                static item => item.Severity == ThemeDiagnosticSeverity.Error);
            throw new ThemeSerializationException(
                diagnostic.Message,
                diagnostic.Path is null ? "$" : "$." + diagnostic.Path);
        }

        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented });
        WriteTheme(writer, theme);
        writer.Flush();
    }

    /// <summary>Asynchronously serializes a definition to a stream without closing it.</summary>
    /// <param name="theme">The definition to serialize.</param>
    /// <param name="stream">The writable destination stream.</param>
    /// <param name="indented">Whether to pretty-print the document.</param>
    /// <param name="cancellationToken">Cancels the final stream flush.</param>
    public async Task SerializeAsync(
        ThemeDefinition theme,
        Stream stream,
        bool indented = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(stream);
        await using var buffer = new MemoryStream();
        Serialize(theme, buffer, indented);
        buffer.Position = 0;
        await buffer.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves a theme to an explicitly selected file.</summary>
    /// <param name="theme">The definition to save.</param>
    /// <param name="path">The destination path.</param>
    /// <param name="indented">Whether to pretty-print the document.</param>
    public void SaveFile(ThemeDefinition theme, string path, bool indented = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.Create(path);
        Serialize(theme, stream, indented);
    }

    /// <summary>Asynchronously saves a theme to an explicitly selected file.</summary>
    /// <param name="theme">The definition to save.</param>
    /// <param name="path">The destination path.</param>
    /// <param name="indented">Whether to pretty-print the document.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    public async Task SaveFileAsync(
        ThemeDefinition theme,
        string path,
        bool indented = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using FileStream stream = File.Create(path);
        await SerializeAsync(theme, stream, indented, cancellationToken).ConfigureAwait(false);
    }

    private ThemeDefinition Deserialize(byte[] data)
        => DeserializeCore(data, new HashSet<int>());

    private ThemeDefinition DeserializeCore(byte[] data, HashSet<int> migratedVersions)
    {
        EnsureDocumentSize(data.Length);
        try
        {
            using JsonDocument document = JsonDocument.Parse(data, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.MaximumJsonDepth
            });
            JsonElement root = document.RootElement;
            RequireKind(root, JsonValueKind.Object, "$");
            EnsureAllowedProperties(root, RootProperties, "$");

            int schemaVersion = ReadRequiredInt32(root, "schemaVersion", "$.schemaVersion");
            if (schemaVersion != CurrentSchemaVersion)
            {
                if (migrations.TryGetValue(schemaVersion, out IThemeSchemaMigration? migration) &&
                    migratedVersions.Add(schemaVersion))
                {
                    byte[] migrated;
                    try
                    {
                        migrated = migration.Migrate(data, limits) ??
                            throw new InvalidOperationException("A schema migration returned no document.");
                    }
                    catch (Exception exception)
                    {
                        throw new ThemeSerializationException(
                            "Theme schema migration failed",
                            "$.schemaVersion",
                            exception);
                    }

                    EnsureDocumentSize(migrated.Length);
                    return DeserializeCore(migrated, migratedVersions);
                }

                throw Error(
                    $"Theme schema version {schemaVersion} is not supported; version {CurrentSchemaVersion} is required",
                    "$.schemaVersion");
            }

            string id = ReadRequiredString(root, "id", "$.id");
            string name = ReadRequiredString(root, "name", "$.name");
            var theme = new ThemeDefinition(id, name)
            {
                SchemaVersion = schemaVersion,
                Description = ReadOptionalString(root, "description", "$.description"),
                Author = ReadOptionalString(root, "author", "$.author"),
                BaseTheme = ReadOptionalString(root, "baseTheme", "$.baseTheme"),
                Variant = ReadEnum(root, "variant", ThemeVariant.Custom, "$.variant")
            };

            ReadStringDictionary(root, "metadata", theme.Metadata);
            ReadStringArray(root, "tags", theme.Tags);
            ReadColorDictionary(root, "colors", theme.Colors);
            ReadBrushDictionary(root, "brushes", theme.Brushes);
            ReadTypographyDictionary(root, "typography", theme.Typography);
            ReadNumberDictionary(root, "spacing", theme.Spacing);
            ReadPaddingDictionary(root, "padding", theme.Padding);
            ReadNumberDictionary(root, "sizing", theme.Sizing);
            ReadNumberDictionary(root, "corners", theme.Corners);
            ReadNumberDictionary(root, "borderThickness", theme.BorderThickness);
            ReadAnimationDictionary(root, "animations", theme.Animations);
            ReadResourceDictionary(root, "resources", theme.Resources);

            ThemeValidationResult validation = new ThemeResolver(
                static _ => null,
                static () => ThemeVariant.Light,
                limits).ValidateWithoutBases(theme);
            if (!validation.IsValid)
            {
                ThemeDiagnostic diagnostic = validation.Diagnostics.First(static item => item.Severity == ThemeDiagnosticSeverity.Error);
                throw Error(diagnostic.Message, diagnostic.Path is null ? "$" : "$." + diagnostic.Path);
            }
            return theme;
        }
        catch (ThemeSerializationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ThemeSerializationException("Theme JSON is malformed", exception.Path ?? "$", exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new ThemeSerializationException("Theme JSON contains an invalid value", "$", exception);
        }
    }

    private byte[] ReadLimited(Stream stream)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            EnsureDocumentSize(checked((int)output.Length + read));
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            EnsureDocumentSize(checked((int)output.Length + read));
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return output.ToArray();
    }

    private void EnsureDocumentSize(int bytes)
    {
        if (bytes > limits.MaximumDocumentBytes)
        {
            throw new ThemeSerializationException(
                $"Theme JSON exceeds the configured limit of {limits.MaximumDocumentBytes} UTF-8 bytes",
                "$");
        }
    }

    private static ThemeSerializationException Error(string message, string path)
        => new(message, path);
}

internal interface IThemeSchemaMigration
{
    int SourceVersion { get; }

    byte[] Migrate(ReadOnlyMemory<byte> source, ThemeSecurityLimits limits);
}
