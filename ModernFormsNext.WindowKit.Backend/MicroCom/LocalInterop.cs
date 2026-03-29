using System;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides low-level interop call helpers for invoking native function pointers.
    /// </summary>
    /// <remarks>
    /// These methods are expected to be replaced or patched at runtime (e.g., via IL rewriting)
    /// to perform actual unmanaged calls using the stdcall convention.
    /// </remarks>
    unsafe class LocalInterop
    {
        /// <summary>
        /// Calls a native method with no return value using stdcall convention.
        /// </summary>
        public static unsafe void CalliStdCallvoid(void* thisObject, void* methodPtr)
        {
            throw null;
        }

        /// <summary>
        /// Calls a native method returning an integer using stdcall convention.
        /// </summary>
        public static unsafe int CalliStdCallint(void* thisObject, Guid* guid, IntPtr* ppv, void* methodPtr)
        {
            throw null;
        }
    }
}