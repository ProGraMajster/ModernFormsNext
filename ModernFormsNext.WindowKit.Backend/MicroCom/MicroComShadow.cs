using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Represents a managed shadow object that exposes a managed instance to native code through
    /// COM-like callable wrappers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="MicroComShadow"/> is responsible for creating and managing native callable wrappers
    /// for a managed object implementing <see cref="IMicroComShadowContainer"/>.
    /// </para>
    /// <para>
    /// Each supported interface type can have its own native wrapper pointer associated with the same
    /// managed target. The shadow keeps track of these wrappers, their reference counts, and the GC handle
    /// used to resolve the managed object back from native code.
    /// </para>
    /// <para>
    /// This class is a core part of the managed-to-native bridge in the MicroCOM runtime.
    /// </para>
    /// </remarks>
    public unsafe class MicroComShadow : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, IntPtr> _shadows = new Dictionary<Type, IntPtr>();
        private readonly Dictionary<IntPtr, Type> _backShadows = new Dictionary<IntPtr, Type>();
        private GCHandle? _handle;
        private volatile int _refCount;

        /// <summary>
        /// Gets the managed target object associated with this shadow.
        /// </summary>
        /// <remarks>
        /// The target object is the managed instance exposed to native code through one or more
        /// callable wrappers.
        /// </remarks>
        internal IMicroComShadowContainer Target { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicroComShadow"/> class for the specified target.
        /// </summary>
        /// <param name="target">The managed object to expose to native code.</param>
        /// <remarks>
        /// The constructor also assigns this instance to the target's <see cref="IMicroComShadowContainer.Shadow"/>
        /// property.
        /// </remarks>
        internal MicroComShadow(IMicroComShadowContainer target)
        {
            Target = target;
            Target.Shadow = this;
        }

        /// <summary>
        /// Resolves a native interface request using the specified interface identifier.
        /// </summary>
        /// <param name="ccw">The native callable wrapper that received the request.</param>
        /// <param name="guid">A pointer to the requested interface GUID.</param>
        /// <param name="ppv">
        /// When this method returns, contains the native pointer for the requested interface if successful.
        /// </param>
        /// <returns>
        /// An HRESULT indicating success or failure.
        /// </returns>
        /// <remarks>
        /// Returns <c>E_NOINTERFACE</c> when the specified GUID is not registered in the runtime.
        /// </remarks>
        internal int QueryInterface(Ccw* ccw, Guid* guid, void** ppv)
        {
            if (MicroComRuntime.TryGetTypeForGuid(*guid, out var type))
                return QueryInterface(type, ppv);
            else
                return unchecked((int)0x80004002u);
        }

        /// <summary>
        /// Resolves a native interface request using the specified managed interface type.
        /// </summary>
        /// <param name="type">The requested managed interface type.</param>
        /// <param name="ppv">
        /// When this method returns, contains the native pointer for the requested interface if successful.
        /// </param>
        /// <returns>
        /// An HRESULT indicating success or failure.
        /// </returns>
        /// <remarks>
        /// Returns <c>E_NOINTERFACE</c> when the target object does not implement the requested interface.
        /// </remarks>
        internal int QueryInterface(Type type, void** ppv)
        {
            if (!type.IsInstanceOfType(Target))
                return unchecked((int)0x80004002u);

            var rv = GetOrCreateNativePointer(type, ppv);
            if (rv == 0)
                AddRef((Ccw*)*ppv);

            return rv;
        }

        /// <summary>
        /// Gets an existing native callable wrapper for the specified interface type or creates a new one.
        /// </summary>
        /// <param name="type">The interface type to expose.</param>
        /// <param name="ppv">
        /// When this method returns, contains a pointer to the native callable wrapper.
        /// </param>
        /// <returns>
        /// An HRESULT indicating success or failure.
        /// </returns>
        /// <remarks>
        /// <para>
        /// If a wrapper for the specified interface type already exists, it is reused.
        /// </para>
        /// <para>
        /// If no wrapper exists, a new one is allocated and initialized with the registered vtable and
        /// a GC handle pointing back to this shadow.
        /// </para>
        /// <para>
        /// Returns <c>E_NOINTERFACE</c> when no vtable has been registered for the specified type.
        /// </para>
        /// </remarks>
        internal int GetOrCreateNativePointer(Type type, void** ppv)
        {
            if (!MicroComRuntime.GetVtableFor(type, out var vtable))
                return unchecked((int)0x80004002u);

            lock (_lock)
            {
                if (_shadows.TryGetValue(type, out var shadow))
                {
                    var targetCcw = (Ccw*)shadow;
                    AddRef(targetCcw);
                    *ppv = targetCcw;
                    return 0;
                }
                else
                {
                    var intPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Ccw>());
                    var targetCcw = (Ccw*)intPtr;
                    *targetCcw = default;
                    targetCcw->RefCount = 0;
                    targetCcw->VTable = vtable;

                    if (_handle == null)
                        _handle = GCHandle.Alloc(this);

                    targetCcw->GcShadowHandle = GCHandle.ToIntPtr(_handle.Value);
                    _shadows[type] = intPtr;
                    _backShadows[intPtr] = type;
                    *ppv = targetCcw;

                    return 0;
                }
            }
        }

        /// <summary>
        /// Increments the native reference count for the specified callable wrapper.
        /// </summary>
        /// <param name="ccw">The callable wrapper whose reference count should be incremented.</param>
        /// <returns>The updated native reference count for the specified wrapper.</returns>
        /// <remarks>
        /// <para>
        /// When the first native reference is acquired across all wrappers managed by this shadow,
        /// <see cref="IMicroComShadowContainer.OnReferencedFromNative"/> is called on the target object.
        /// </para>
        /// <para>
        /// Exceptions thrown by the target are forwarded to
        /// <see cref="MicroComRuntime.UnhandledException(object, Exception)"/>.
        /// </para>
        /// </remarks>
        internal int AddRef(Ccw* ccw)
        {
            if (Interlocked.Increment(ref _refCount) == 1)
            {
                try
                {
                    Target.OnReferencedFromNative();
                }
                catch (Exception e)
                {
                    MicroComRuntime.UnhandledException(Target, e);
                }
            }

            return Interlocked.Increment(ref ccw->RefCount);
        }

        /// <summary>
        /// Decrements the native reference count for the specified callable wrapper.
        /// </summary>
        /// <param name="ccw">The callable wrapper whose reference count should be decremented.</param>
        /// <returns>
        /// The updated native reference count, or the result of wrapper cleanup when the count reaches zero.
        /// </returns>
        internal int Release(Ccw* ccw)
        {
            Interlocked.Decrement(ref _refCount);
            var cnt = Interlocked.Decrement(ref ccw->RefCount);
            if (cnt == 0)
                return FreeCcw(ccw);

            return cnt;
        }

        /// <summary>
        /// Releases and removes a native callable wrapper whose reference count reached zero.
        /// </summary>
        /// <param name="ccw">The callable wrapper to free.</param>
        /// <returns>The resulting reference count after cleanup.</returns>
        /// <remarks>
        /// <para>
        /// If the wrapper has been resurrected by another thread before cleanup completes, the current
        /// reference count is returned and the wrapper is not freed.
        /// </para>
        /// <para>
        /// When the last wrapper is removed, the GC handle is released and
        /// <see cref="IMicroComShadowContainer.OnUnreferencedFromNative"/> is called on the target object.
        /// </para>
        /// </remarks>
        private int FreeCcw(Ccw* ccw)
        {
            lock (_lock)
            {
                // Shadow got resurrected by a call to QueryInterface from another thread
                if (ccw->RefCount != 0)
                    return ccw->RefCount;

                var intPtr = new IntPtr(ccw);
                var type = _backShadows[intPtr];
                _backShadows.Remove(intPtr);
                _shadows.Remove(type);
                Marshal.FreeHGlobal(intPtr);

                if (_shadows.Count == 0)
                {
                    _handle?.Free();
                    _handle = null;

                    try
                    {
                        Target.OnUnreferencedFromNative();
                    }
                    catch (Exception e)
                    {
                        MicroComRuntime.UnhandledException(Target, e);
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Releases any unused callable wrappers that were created but never acquired by native code.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is needed for cases where a managed object is exposed to native code and a callable
        /// wrapper is created, but the native side never increments its reference count.
        /// </para>
        /// <para>
        /// In such a scenario, the shadow may still hold a GC handle and unmanaged memory allocation even
        /// though no active native references exist.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            lock (_lock)
            {
                List<IntPtr>? toRemove = null;

                foreach (var kv in _backShadows)
                {
                    var ccw = (Ccw*)kv.Key;
                    if (ccw->RefCount == 0)
                    {
                        toRemove ??= new List<IntPtr>();
                        toRemove.Add(kv.Key);
                    }
                }

                if (toRemove != null)
                    foreach (var intPtr in toRemove)
                        FreeCcw((Ccw*)intPtr);
            }
        }
    }

    /// <summary>
    /// Represents a native COM-like callable wrapper structure used by <see cref="MicroComShadow"/>.
    /// </summary>
    /// <remarks>
    /// This structure stores the vtable pointer, a GC handle back to the owning
    /// <see cref="MicroComShadow"/>, and the native reference count.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    struct Ccw
    {
        /// <summary>
        /// The native virtual method table pointer.
        /// </summary>
        public IntPtr VTable;

        /// <summary>
        /// A GC handle pointing to the owning <see cref="MicroComShadow"/>.
        /// </summary>
        public IntPtr GcShadowHandle;

        /// <summary>
        /// The native reference count for this callable wrapper.
        /// </summary>
        public volatile int RefCount;

        /// <summary>
        /// Gets the <see cref="MicroComShadow"/> associated with this callable wrapper.
        /// </summary>
        /// <returns>The owning <see cref="MicroComShadow"/> instance.</returns>
        public MicroComShadow GetShadow() =>
            GCHandle.FromIntPtr(GcShadowHandle).Target as MicroComShadow
            ?? throw new InvalidOperationException("The callable wrapper no longer references a MicroCom shadow.");
    }
}
