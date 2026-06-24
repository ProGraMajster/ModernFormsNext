using ModernFormsNext.Layout;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace ModernFormsNext.DataBinding
{
    /// <summary>
    ///  Converts values between data-source properties and bindable component properties.
    /// </summary>
    /// <remarks>
    ///  This is the ModernFormsNext replacement for the WinForms internal formatting helper used by
    ///  <see cref="Binding"/>. It deliberately avoids binary serialization. Normal binding conversions
    ///  still prefer <see cref="TypeConverter"/>, <see cref="IConvertible"/>, format strings, and
    ///  culture-aware conversions; JSON is only a final fallback for string-to-object and
    ///  object-to-string cases where no framework converter is available.
    /// </remarks>
    internal static class BindingValueFormatter
    {
        internal static object? GetDefaultDataSourceNullValue(Type? type)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType is not null ? null : DBNull.Value;
        }

        internal static bool IsNullData(object? value, object? dataSourceNullValue)
        {
            return value is null
                || value == DBNull.Value
                || Equals(value, dataSourceNullValue);
        }

        internal static object? ParseObject(
            object? value,
            Type targetType,
            Type sourceType,
            TypeConverter? targetConverter,
            TypeConverter? sourceConverter,
            IFormatProvider? formatInfo,
            object? nullValue,
            object? dataSourceNullValue)
        {
            if (IsFormattedNullValue(value, nullValue))
            {
                return dataSourceNullValue;
            }

            return ConvertValue(
                value,
                targetType,
                sourceType,
                targetConverter,
                sourceConverter,
                formatString: null,
                formatInfo);
        }

        internal static object? FormatObject(
            object? value,
            Type targetType,
            TypeConverter? sourceConverter,
            TypeConverter? targetConverter,
            string? formatString,
            IFormatProvider? formatInfo,
            object? nullValue,
            object? dataSourceNullValue)
        {
            if (IsNullData(value, dataSourceNullValue))
            {
                return nullValue;
            }

            return ConvertValue(
                value,
                targetType,
                value?.GetType() ?? typeof(object),
                targetConverter,
                sourceConverter,
                formatString,
                formatInfo);
        }

        private static object? ConvertValue(
            object? value,
            Type targetType,
            Type sourceType,
            TypeConverter? targetConverter,
            TypeConverter? sourceConverter,
            string? formatString,
            IFormatProvider? formatInfo)
        {
            Type effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is null || value == DBNull.Value)
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            if (targetType.IsInstanceOfType(value) || effectiveTargetType.IsInstanceOfType(value))
            {
                return value;
            }

            CultureInfo culture = formatInfo as CultureInfo ?? CultureInfo.CurrentCulture;

            if (effectiveTargetType == typeof(string))
            {
                return ConvertToString(value, sourceConverter, formatString, formatInfo, culture);
            }

            if (TryConvertFrom(value, effectiveTargetType, targetConverter, culture, out object? converted))
            {
                return converted;
            }

            if (TryConvertTo(value, effectiveTargetType, sourceConverter, culture, out converted))
            {
                return converted;
            }

            if (TryConvertFrom(value, effectiveTargetType, TypeDescriptor.GetConverter(effectiveTargetType), culture, out converted))
            {
                return converted;
            }

            if (TryConvertTo(value, effectiveTargetType, TypeDescriptor.GetConverter(sourceType), culture, out converted))
            {
                return converted;
            }

            if (effectiveTargetType.IsEnum && value is string enumText)
            {
                return Enum.Parse(effectiveTargetType, enumText, ignoreCase: true);
            }

            if (value is IConvertible)
            {
                return Convert.ChangeType(value, effectiveTargetType, formatInfo ?? culture);
            }

            if (value is string json && TryDeserializeJson(json, effectiveTargetType, out converted))
            {
                return converted;
            }

            throw new FormatException(SR.ListBindingFormatFailed);
        }

        private static string? ConvertToString(
            object value,
            TypeConverter? sourceConverter,
            string? formatString,
            IFormatProvider? formatInfo,
            CultureInfo culture)
        {
            if (value is IFormattable formattable && (!string.IsNullOrEmpty(formatString) || formatInfo is not null))
            {
                return formattable.ToString(formatString, formatInfo ?? culture);
            }

            if (TryConvertTo(value, typeof(string), sourceConverter, culture, out object? converted))
            {
                return converted as string;
            }

            if (value is IConvertible)
            {
                return Convert.ToString(value, formatInfo ?? culture);
            }

            return SerializeJson(value);
        }

        private static bool IsFormattedNullValue(object? value, object? nullValue)
        {
            return value is null
                || value == DBNull.Value
                || Equals(value, nullValue)
                || (value is string text && text.Length == 0 && nullValue is null);
        }

        private static bool TryConvertFrom(
            object value,
            Type targetType,
            TypeConverter? converter,
            CultureInfo culture,
            out object? converted)
        {
            converted = null;

            if (converter is null || !converter.CanConvertFrom(value.GetType()))
            {
                return false;
            }

            try
            {
                converted = converter.ConvertFrom(null, culture, value);
                return converted is null || targetType.IsInstanceOfType(converted);
            }
            catch (Exception ex) when (IsConversionException(ex))
            {
                return false;
            }
        }

        private static bool TryConvertTo(
            object value,
            Type targetType,
            TypeConverter? converter,
            CultureInfo culture,
            out object? converted)
        {
            converted = null;

            if (converter is null || !converter.CanConvertTo(targetType))
            {
                return false;
            }

            try
            {
                converted = converter.ConvertTo(null, culture, value, targetType);
                return converted is null || targetType.IsInstanceOfType(converted);
            }
            catch (Exception ex) when (IsConversionException(ex))
            {
                return false;
            }
        }

        private static bool TryDeserializeJson(string json, Type targetType, out object? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                value = JsonSerializer.Deserialize(json, targetType);
                return true;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return false;
            }
        }

        private static string SerializeJson(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType());
        }

        private static bool IsConversionException(Exception ex)
        {
            return ex is NotSupportedException
                or FormatException
                or InvalidCastException
                or ArgumentException;
        }
    }
}
