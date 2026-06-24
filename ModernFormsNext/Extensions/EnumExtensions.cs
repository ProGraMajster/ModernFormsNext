using System;
using System.Runtime.CompilerServices;

namespace ModernFormsNext.Extensions
{
    internal static class EnumExtensions
    {
        /// <summary>
        ///  Sets the <paramref name="flags"/> if <paramref name="set"/> is <see langword="true"/>, otherwise clears them.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This helper intentionally avoids pointer-based casts. It is used for small state enums
        ///   in the data-binding layer, where predictable behavior is more important than shaving a
        ///   boxing allocation from a rendering hot path.
        ///  </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ChangeFlags<T>(ref this T value, T flags, bool set) where T : struct, Enum
        {
            ulong currentValue = ToUInt64(value);
            ulong flagValue = ToUInt64(flags);
            ulong newValue = set ? currentValue | flagValue : currentValue & ~flagValue;

            value = FromUInt64<T>(newValue);
        }

        private static ulong ToUInt64<T>(T value) where T : struct, Enum
        {
            return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T))) switch
            {
                TypeCode.SByte => unchecked((ulong)(sbyte)(object)value),
                TypeCode.Byte => (byte)(object)value,
                TypeCode.Int16 => unchecked((ulong)(short)(object)value),
                TypeCode.UInt16 => (ushort)(object)value,
                TypeCode.Int32 => unchecked((ulong)(int)(object)value),
                TypeCode.UInt32 => (uint)(object)value,
                TypeCode.Int64 => unchecked((ulong)(long)(object)value),
                TypeCode.UInt64 => (ulong)(object)value,
                _ => throw new InvalidOperationException($"Unsupported enum underlying type for {typeof(T).FullName}.")
            };
        }

        private static T FromUInt64<T>(ulong value) where T : struct, Enum
        {
            Type enumType = typeof(T);
            object typedValue = Type.GetTypeCode(Enum.GetUnderlyingType(enumType)) switch
            {
                TypeCode.SByte => unchecked((sbyte)value),
                TypeCode.Byte => unchecked((byte)value),
                TypeCode.Int16 => unchecked((short)value),
                TypeCode.UInt16 => unchecked((ushort)value),
                TypeCode.Int32 => unchecked((int)value),
                TypeCode.UInt32 => unchecked((uint)value),
                TypeCode.Int64 => unchecked((long)value),
                TypeCode.UInt64 => value,
                _ => throw new InvalidOperationException($"Unsupported enum underlying type for {enumType.FullName}.")
            };

            return (T)Enum.ToObject(enumType, typedValue);
        }
    }
}
