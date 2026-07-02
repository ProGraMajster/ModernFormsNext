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
    /// <summary>
    /// Gets the default serializer instance.
    /// </summary>
    public static DesignDocumentSerializer Default { get; } = new();

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

        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>
    /// Converts JSON into a designer document.
    /// </summary>
    /// <param name="json">The JSON document text.</param>
    /// <returns>The deserialized designer document.</returns>
    /// <exception cref="JsonException">Thrown when the JSON cannot be parsed as a designer document.</exception>
    public DesignDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<DesignDocument>(json, Options)
            ?? throw new JsonException("The JSON did not contain a designer document.");
    }

    /// <summary>
    /// Saves a designer document to disk.
    /// </summary>
    /// <param name="path">The destination path, commonly ending in <c>.mfdesign</c>.</param>
    /// <param name="document">The document to save.</param>
    public void Save(string path, DesignDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        File.WriteAllText(path, Serialize(document));
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
}
