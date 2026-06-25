using System;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides low-level interop call helpers for invoking native function pointers.
    /// </summary>
    /// <remarks>
    /// These helpers are used by the base <see cref="IUnknown"/> proxy implementation.
    /// Generated MicroCom proxies emit matching helper methods for their own interface-specific
    /// signatures. The calls use unmanaged function pointers directly so the runtime does not
    /// depend on a post-build IL patching step before COM proxies can be used.
    /// </remarks>
    unsafe class LocalInterop
    {
        /// <summary>
        /// Calls a native method with no return value using stdcall convention.
        /// </summary>
        public static unsafe void CalliStdCallvoid(void* thisObject, void* methodPtr)
        {
            ((delegate* unmanaged[Stdcall]<void*, void>)methodPtr)(thisObject);
        }

        /// <summary>
        /// Calls a native method returning an integer using stdcall convention.
        /// </summary>
        public static unsafe int CalliStdCallint(void* thisObject, Guid* guid, IntPtr* ppv, void* methodPtr)
        {
            return ((delegate* unmanaged[Stdcall]<void*, Guid*, IntPtr*, int>)methodPtr)(thisObject, guid, ppv);
        }
    }
}
