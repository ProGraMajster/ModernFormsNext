using System;
using System.Runtime.CompilerServices;

namespace ModernFormsNext.WindowKit
{
    /// <summary>
    /// Provides extension methods for enums.
    /// </summary>
    public static class EnumExtensions
    {

        /// <summary>
        /// Determines whether all specified flags are present in an enum value.
        /// </summary>
        /// <typeparam name="T">The unmanaged enum type.</typeparam>
        /// <param name="value">The enum value to inspect.</param>
        /// <param name="flags">The flags that must be present.</param>
        /// <returns><see langword="true"/> when all flags are present; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool HasAllFlags<T>(this T value, T flags) where T : unmanaged, Enum
        {
            if (sizeof(T) == 1)
            {
                var byteValue = Unsafe.As<T, byte>(ref value);
                var byteFlags = Unsafe.As<T, byte>(ref flags);
                return (byteValue & byteFlags) == byteFlags;
            }
            else if (sizeof(T) == 2)
            {
                var shortValue = Unsafe.As<T, short>(ref value);
                var shortFlags = Unsafe.As<T, short>(ref flags);
                return (shortValue & shortFlags) == shortFlags;
            }
            else if (sizeof(T) == 4)
            {
                var intValue = Unsafe.As<T, int>(ref value);
                var intFlags = Unsafe.As<T, int>(ref flags);
                return (intValue & intFlags) == intFlags;
            }
            else if (sizeof(T) == 8)
            {
                var longValue = Unsafe.As<T, long>(ref value);
                var longFlags = Unsafe.As<T, long>(ref flags);
                return (longValue & longFlags) == longFlags;
            }
            else
                throw new NotSupportedException("Enum with size of " + Unsafe.SizeOf<T>() + " are not supported");
        }

        /// <summary>
        /// Determines whether any specified flag is present in an enum value.
        /// </summary>
        /// <typeparam name="T">The unmanaged enum type.</typeparam>
        /// <param name="value">The enum value to inspect.</param>
        /// <param name="flags">The flags to test.</param>
        /// <returns><see langword="true"/> when any flag is present; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool HasAnyFlag<T>(this T value, T flags) where T : unmanaged, Enum
        {
            if (sizeof(T) == 1)
            {
                var byteValue = Unsafe.As<T, byte>(ref value);
                var byteFlags = Unsafe.As<T, byte>(ref flags);
                return (byteValue & byteFlags) != 0;
            }
            else if (sizeof(T) == 2)
            {
                var shortValue = Unsafe.As<T, short>(ref value);
                var shortFlags = Unsafe.As<T, short>(ref flags);
                return (shortValue & shortFlags) != 0;
            }
            else if (sizeof(T) == 4)
            {
                var intValue = Unsafe.As<T, int>(ref value);
                var intFlags = Unsafe.As<T, int>(ref flags);
                return (intValue & intFlags) != 0;
            }
            else if (sizeof(T) == 8)
            {
                var longValue = Unsafe.As<T, long>(ref value);
                var longFlags = Unsafe.As<T, long>(ref flags);
                return (longValue & longFlags) != 0;
            }
            else
                throw new NotSupportedException("Enum with size of " + Unsafe.SizeOf<T>() + " are not supported");
        }
            }
}
