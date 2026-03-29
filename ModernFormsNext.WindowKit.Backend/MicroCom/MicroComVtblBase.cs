using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides a base implementation for building COM-like virtual method tables used by the MicroCOM runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A virtual method table, or vtable, is a native array of function pointers that allows unmanaged code
    /// to call managed methods through a COM-like calling convention.
    /// </para>
    /// <para>
    /// This base implementation creates a standard vtable containing the three fundamental COM-style methods:
    /// <c>QueryInterface</c>, <c>AddRef</c>, and <c>Release</c>.
    /// </para>
    /// <para>
    /// Derived types can extend this class and register additional methods in order to expose more complex
    /// interfaces to native code.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class MyInterfaceVtbl : MicroComVtblBase
    /// {
    ///     public static IntPtr Vtable { get; } = new MyInterfaceVtbl().CreateVTable();
    ///
    ///     public MyInterfaceVtbl()
    ///     {
    ///         AddMethod((MyCustomDelegate)MyCustomMethod);
    ///     }
    /// }
    /// </code>
    /// </example>
    public unsafe class MicroComVtblBase
    {
        private List<IntPtr> _methods = new List<IntPtr>();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int AddRefDelegate(Ccw* ccw);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(Ccw* ccw, Guid* guid, void** ppv);

        /// <summary>
        /// Gets the default base vtable pointer for the standard MicroCOM <see cref="IUnknown"/> contract.
        /// </summary>
        /// <remarks>
        /// This vtable contains function pointers for <c>QueryInterface</c>, <c>AddRef</c>, and <c>Release</c>.
        /// </remarks>
        public static IntPtr Vtable { get; } = new MicroComVtblBase().CreateVTable();

        /// <summary>
        /// Initializes a new instance of the <see cref="MicroComVtblBase"/> class.
        /// </summary>
        /// <remarks>
        /// The constructor registers the three base COM-like methods in the following order:
        /// <list type="number">
        /// <item><description><c>QueryInterface</c></description></item>
        /// <item><description><c>AddRef</c></description></item>
        /// <item><description><c>Release</c></description></item>
        /// </list>
        /// The order of methods in a vtable is significant and must match the expected unmanaged layout.
        /// </remarks>
        public MicroComVtblBase()
        {
            AddMethod((QueryInterfaceDelegate)QueryInterface);
            AddMethod((AddRefDelegate)AddRef);
            AddMethod((AddRefDelegate)Release);
        }

        /// <summary>
        /// Adds a managed delegate to the current vtable definition.
        /// </summary>
        /// <param name="d">The delegate representing the unmanaged-callable method.</param>
        /// <remarks>
        /// <para>
        /// The delegate is pinned by allocating a <see cref="GCHandle"/> so that the function pointer remains valid
        /// for unmanaged callers.
        /// </para>
        /// <para>
        /// The resulting function pointer is appended to the internal method list and later written into the
        /// allocated native vtable by <see cref="CreateVTable()"/>.
        /// </para>
        /// </remarks>
        protected void AddMethod(Delegate d)
        {
            GCHandle.Alloc(d);
            _methods.Add(Marshal.GetFunctionPointerForDelegate(d));
        }

        /// <summary>
        /// Allocates and initializes a native virtual method table from the currently registered methods.
        /// </summary>
        /// <returns>
        /// A pointer to the unmanaged vtable memory block.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The returned pointer refers to unmanaged memory allocated with <see cref="Marshal.AllocHGlobal(int)"/>.
        /// </para>
        /// <para>
        /// Each entry in the allocated table is a function pointer corresponding to one delegate previously added
        /// through <see cref="AddMethod(Delegate)"/>.
        /// </para>
        /// <para>
        /// Derived classes typically call this method after registering all required methods for the interface.
        /// </para>
        /// </remarks>
        protected unsafe IntPtr CreateVTable()
        {
            var ptr = (IntPtr*)Marshal.AllocHGlobal(IntPtr.Size * _methods.Count);//Marshal.AllocHGlobal((IntPtr.Size + 1) * _methods.Count);
            for (var c = 0; c < _methods.Count; c++)
                ptr[c] = _methods[c];

            return new IntPtr(ptr);
        }

        /// <summary>
        /// Handles a native <c>QueryInterface</c> call for the specified callable wrapper.
        /// </summary>
        /// <param name="ccw">The callable wrapper that received the interface query.</param>
        /// <param name="guid">A pointer to the requested interface GUID.</param>
        /// <param name="ppv">
        /// When this method returns, contains the native pointer for the requested interface if the query succeeds.
        /// </param>
        /// <returns>An HRESULT indicating success or failure.</returns>
        private static int QueryInterface(Ccw* ccw, Guid* guid, void** ppv)
            => ccw->GetShadow().QueryInterface(ccw, guid, ppv);

        /// <summary>
        /// Handles a native <c>AddRef</c> call for the specified callable wrapper.
        /// </summary>
        /// <param name="ccw">The callable wrapper whose reference count should be incremented.</param>
        /// <returns>The updated native reference count.</returns>
        private static int AddRef(Ccw* ccw)
            => ccw->GetShadow().AddRef(ccw);

        /// <summary>
        /// Handles a native <c>Release</c> call for the specified callable wrapper.
        /// </summary>
        /// <param name="ccw">The callable wrapper whose reference count should be decremented.</param>
        /// <returns>The updated native reference count.</returns>
        private static int Release(Ccw* ccw)
            => ccw->GetShadow().Release(ccw);
    }
}