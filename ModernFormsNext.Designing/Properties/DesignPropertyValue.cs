using System.Globalization;

namespace ModernFormsNext.Designing;

/// <summary>
/// Stores a primitive designer property value that can be serialized to stable JSON
/// and later emitted as C# initialization code.
/// </summary>
/// <remarks>
/// The MVP intentionally supports only primitive values: strings, booleans, integers,
/// doubles, enum member names, small structured values, and <see langword="null"/>.
/// Structured values are intentionally limited to named primitive child values so
/// designer documents remain readable and deterministic.
/// </remarks>
public sealed class DesignPropertyValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignPropertyValue"/> class.
    /// </summary>
    /// <param name="kind">The primitive value kind.</param>
    /// <param name="value">The stored value, using the CLR type that matches <paramref name="kind"/>.</param>
    /// <param name="enumTypeName">The optional fully qualified enum type name when <paramref name="kind"/> is <see cref="DesignPropertyValueKind.Enum"/>.</param>
    /// <param name="objectTypeName">The optional fully qualified type name when <paramref name="kind"/> is <see cref="DesignPropertyValueKind.Object"/>.</param>
    /// <param name="objectProperties">The optional named child values when <paramref name="kind"/> is <see cref="DesignPropertyValueKind.Object"/>.</param>
    public DesignPropertyValue(
        DesignPropertyValueKind kind,
        object? value,
        string? enumTypeName = null,
        string? objectTypeName = null,
        IReadOnlyDictionary<string, DesignPropertyValue>? objectProperties = null)
    {
        Kind = kind;
        Value = value;
        EnumTypeName = enumTypeName;
        ObjectTypeName = objectTypeName;
        ObjectProperties = objectProperties is null
            ? null
            : new SortedDictionary<string, DesignPropertyValue>(
                objectProperties.ToDictionary(property => property.Key, property => property.Value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the primitive value kind.
    /// </summary>
    public DesignPropertyValueKind Kind { get; }

    /// <summary>
    /// Gets the stored CLR value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the optional fully qualified enum type name for enum values.
    /// </summary>
    public string? EnumTypeName { get; }

    /// <summary>
    /// Gets the optional fully qualified type name for structured object values.
    /// </summary>
    public string? ObjectTypeName { get; }

    /// <summary>
    /// Gets the named child values for structured object values.
    /// </summary>
    public IReadOnlyDictionary<string, DesignPropertyValue>? ObjectProperties { get; }

    /// <summary>
    /// Creates a designer property value that represents <see langword="null"/>.
    /// </summary>
    /// <returns>A designer property value containing <see langword="null"/>.</returns>
    public static DesignPropertyValue FromNull() => new(DesignPropertyValueKind.Null, null);

    /// <summary>
    /// Creates a designer property value from a string.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A designer property value containing the string.</returns>
    public static DesignPropertyValue FromString(string value) => new(DesignPropertyValueKind.String, value);

    /// <summary>
    /// Creates a designer property value from a Boolean.
    /// </summary>
    /// <param name="value">The Boolean value.</param>
    /// <returns>A designer property value containing the Boolean.</returns>
    public static DesignPropertyValue FromBoolean(bool value) => new(DesignPropertyValueKind.Boolean, value);

    /// <summary>
    /// Creates a designer property value from a 32-bit integer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A designer property value containing the integer.</returns>
    public static DesignPropertyValue FromInt32(int value) => new(DesignPropertyValueKind.Int32, value);

    /// <summary>
    /// Creates a designer property value from a double-precision number.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>A designer property value containing the double.</returns>
    public static DesignPropertyValue FromDouble(double value) => new(DesignPropertyValueKind.Double, value);

    /// <summary>
    /// Creates a designer property value from an enum member name.
    /// </summary>
    /// <param name="enumTypeName">The fully qualified enum type name used by generated C# code.</param>
    /// <param name="memberName">The enum member name.</param>
    /// <returns>A designer property value containing the enum member name.</returns>
    public static DesignPropertyValue FromEnum(string enumTypeName, string memberName)
        => new(DesignPropertyValueKind.Enum, memberName, enumTypeName);

    /// <summary>
    /// Creates a designer property value from named child values.
    /// </summary>
    /// <param name="typeName">The runtime type name represented by the structured value.</param>
    /// <param name="properties">The named primitive child values.</param>
    /// <returns>A designer property value containing structured child values.</returns>
    public static DesignPropertyValue FromStructuredObject(
        string typeName,
        IReadOnlyDictionary<string, DesignPropertyValue> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(properties);

        return new DesignPropertyValue(
            DesignPropertyValueKind.Object,
            null,
            objectTypeName: typeName,
            objectProperties: properties);
    }

    /// <summary>
    /// Returns the stored string value.
    /// </summary>
    /// <returns>The string value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a string or enum member name.</exception>
    public string GetString()
    {
        if (Kind is DesignPropertyValueKind.String or DesignPropertyValueKind.Enum && Value is string value)
            return value;

        throw new InvalidOperationException("The designer property value does not contain a string.");
    }

    /// <inheritdoc/>
    public override string ToString()
        => Value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Value.ToString() ?? string.Empty
        };

    /// <summary>
    /// Converts a CLR primitive value to a designer property value.
    /// </summary>
    /// <param name="value">The primitive value to convert.</param>
    /// <returns>A designer property value.</returns>
    /// <exception cref="NotSupportedException">Thrown when the value type is not supported by the MVP model.</exception>
    public static DesignPropertyValue FromObject(object? value)
        => value switch
        {
            null => FromNull(),
            string stringValue => FromString(stringValue),
            bool boolValue => FromBoolean(boolValue),
            int intValue => FromInt32(intValue),
            float floatValue => FromDouble(floatValue),
            double doubleValue => FromDouble(doubleValue),
            Enum enumValue => FromEnum(enumValue.GetType().FullName ?? enumValue.GetType().Name, enumValue.ToString()),
            _ => throw new NotSupportedException($"Designer property values do not support '{value.GetType().FullName}'.")
        };
}
