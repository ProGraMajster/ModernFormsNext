using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides runtime services for the MicroCOM interop layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class acts as the central registry and utility hub for MicroCOM type metadata,
    /// proxy creation, native pointer conversion, vtable lookup, and interface resolution.
    /// </para>
    /// <para>
    /// It is responsible for mapping managed interface types to native interface identifiers,
    /// associating interface types with proxy factories, and creating native callable wrappers
    /// for managed objects exposed to unmanaged code.
    /// </para>
    /// <para>
    /// The runtime is initialized with a default registration for <see cref="IUnknown"/>,
    /// including its standard COM GUID and the base vtable implementation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// MicroComRuntime.Register(
    ///     typeof(IMyInterface),
    ///     new Guid("00000000-0000-0000-0000-000000000001"),
    ///     (ptr, owns) => new MyInterfaceProxy(ptr, owns));
    ///
    /// MicroComRuntime.RegisterVTable(typeof(IMyInterface), MyInterfaceVtbl.Vtable);
    /// </code>
    /// </example>
    public static unsafe class MicroComRuntime
    {
        private static ConcurrentDictionary<Type, IntPtr> _vtables = new ConcurrentDictionary<Type, IntPtr>();

        private static ConcurrentDictionary<Type, Func<IntPtr, bool, object>> _factories =
            new ConcurrentDictionary<Type, Func<IntPtr, bool, object>>();
        private static ConcurrentDictionary<Type, Guid> _guids = new ConcurrentDictionary<Type, Guid>();
        private static ConcurrentDictionary<Guid, Type> _guidsToTypes = new ConcurrentDictionary<Guid, Type>();

        /// <summary>
        /// Initializes static runtime registrations for built-in MicroCOM types.
        /// </summary>
        /// <remarks>
        /// The runtime registers the base <see cref="IUnknown"/> mapping and associates it with
        /// <see cref="MicroComProxyBase"/> and <see cref="MicroComVtblBase"/>.
        /// </remarks>
        static MicroComRuntime()
        {
            Register(typeof(IUnknown), new Guid("00000000-0000-0000-C000-000000000046"),
                (ppv, owns) => new MicroComProxyBase(ppv, owns));
            RegisterVTable(typeof(IUnknown), MicroComVtblBase.Vtable);
        }

        /// <summary>
        /// Registers a native virtual method table for the specified interface type.
        /// </summary>
        /// <param name="t">The managed interface type associated with the vtable.</param>
        /// <param name="vtable">A pointer to the native virtual method table.</param>
        /// <remarks>
        /// This registration is used when creating native callable wrappers for managed objects.
        /// </remarks>
        public static void RegisterVTable(Type t, IntPtr vtable)
        {
            _vtables[t] = vtable;
        }

        /// <summary>
        /// Registers a managed interface type with its native interface identifier and proxy factory.
        /// </summary>
        /// <param name="t">The managed interface type to register.</param>
        /// <param name="guid">The native interface GUID associated with the type.</param>
        /// <param name="proxyFactory">
        /// A factory delegate used to create managed proxy instances for native pointers of the specified type.
        /// </param>
        /// <remarks>
        /// This method updates both type-to-GUID and GUID-to-type lookup tables.
        /// </remarks>
        public static void Register(Type t, Guid guid, Func<IntPtr, bool, object> proxyFactory)
        {
            _factories[t] = proxyFactory;
            _guids[t] = guid;
            _guidsToTypes[guid] = t;
        }

        /// <summary>
        /// Gets the registered interface GUID for the specified managed type.
        /// </summary>
        /// <param name="type">The managed interface type.</param>
        /// <returns>The interface GUID associated with the specified type.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when the specified type has not been registered.
        /// </exception>
        public static Guid GetGuidFor(Type type) => _guids[type];

        /// <summary>
        /// Creates a managed proxy of the specified type for a native interface pointer.
        /// </summary>
        /// <typeparam name="T">The managed proxy interface type.</typeparam>
        /// <param name="pObject">A native interface pointer.</param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> if the created proxy owns the native reference; otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>A managed proxy instance for the specified native pointer.</returns>
        public static T CreateProxyFor<T>(void* pObject, bool ownsHandle)
            => (T)CreateProxyFor(typeof(T), new IntPtr(pObject), ownsHandle);

        /// <summary>
        /// Creates a managed proxy of the specified type for a native interface pointer.
        /// </summary>
        /// <typeparam name="T">The managed proxy interface type.</typeparam>
        /// <param name="pObject">A native interface pointer.</param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> if the created proxy owns the native reference; otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>A managed proxy instance for the specified native pointer.</returns>
        public static T CreateProxyFor<T>(IntPtr pObject, bool ownsHandle)
            => (T)CreateProxyFor(typeof(T), pObject, ownsHandle);

        /// <summary>
        /// Creates a managed proxy for the specified interface type and native pointer.
        /// </summary>
        /// <param name="type">The managed interface type to create a proxy for.</param>
        /// <param name="pObject">The native interface pointer.</param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> if the created proxy owns the native reference; otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>A managed proxy instance created by the registered factory.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when no proxy factory has been registered for the specified type.
        /// </exception>
        public static object CreateProxyFor(Type type, IntPtr pObject, bool ownsHandle)
            => _factories[type](pObject, ownsHandle);

        /// <summary>
        /// Gets the native pointer for the specified MicroCOM object as an <see cref="IntPtr"/>.
        /// </summary>
        /// <typeparam name="T">The interface type of the object.</typeparam>
        /// <param name="obj">The managed object or proxy.</param>
        /// <param name="owned">
        /// <see langword="true"/> to increment the native reference count before returning the pointer;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>
        /// A native pointer for the specified object, or <see cref="IntPtr.Zero"/> if <paramref name="obj"/> is <see langword="null"/>.
        /// </returns>
        public static IntPtr GetNativeIntPtr<T>(this T obj, bool owned = false) where T : IUnknown
            => new IntPtr(GetNativePointer(obj, owned));

        /// <summary>
        /// Gets the native pointer for the specified MicroCOM object.
        /// </summary>
        /// <typeparam name="T">The interface type of the object.</typeparam>
        /// <param name="obj">The managed object or proxy.</param>
        /// <param name="owned">
        /// <see langword="true"/> to increment the native reference count before returning the pointer;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>
        /// A native interface pointer for the specified object, or <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// If the object is already a <see cref="MicroComProxyBase"/>, its native pointer is returned directly.
        /// </para>
        /// <para>
        /// If the object is a managed callback object implementing <see cref="IMicroComShadowContainer"/>,
        /// a <see cref="MicroComShadow"/> is created if necessary and used to expose the object to native code.
        /// </para>
        /// </remarks>
        /// <exception cref="COMException">
        /// Thrown when a native callable wrapper cannot be created for the specified object and interface type.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the object type cannot be converted to a native pointer.
        /// </exception>
        public static void* GetNativePointer<T>(T obj, bool owned = false) where T : IUnknown
        {
            if (obj == null)
                return null;

            if (obj is MicroComProxyBase proxy)
            {
                if (owned)
                    proxy.AddRef();

                return (void*)proxy.NativePointer;
            }

            if (obj is IMicroComShadowContainer container)
            {
                var shadow = container.Shadow ??= new MicroComShadow(container);

                void* ptr = null;
                var res = shadow.GetOrCreateNativePointer(typeof(T), &ptr);
                if (res != 0)
                    throw new COMException(
                        "Unable to create native callable wrapper for type " + typeof(T) + " for instance of type " +
                        obj.GetType(),
                        res);

                if (owned)
                    shadow.AddRef((Ccw*)ptr);

                return ptr;
            }

            throw new ArgumentException("Unable to get a native pointer for " + obj);
        }

        /// <summary>
        /// Gets the managed object associated with a native COM callable wrapper pointer.
        /// </summary>
        /// <param name="ccw">A pointer to a native COM callable wrapper.</param>
        /// <returns>The managed target object associated with the specified wrapper.</returns>
        /// <remarks>
        /// This method resolves the <see cref="MicroComShadow"/> from the wrapper's GC handle and then
        /// returns its managed target.
        /// </remarks>
        public static object GetObjectFromCcw(IntPtr ccw)
        {
            var ptr = (Ccw*)ccw;
            var shadow = GCHandle.FromIntPtr(ptr->GcShadowHandle).Target as MicroComShadow
                ?? throw new InvalidOperationException("The callable wrapper no longer references a MicroCom shadow.");
            return shadow.Target;
        }

        /// <summary>
        /// Attempts to get the managed interface type registered for the specified GUID.
        /// </summary>
        /// <param name="guid">The interface GUID to resolve.</param>
        /// <param name="t">
        /// When this method returns, contains the registered managed type if the lookup succeeded;
        /// otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a matching type was found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryGetTypeForGuid(Guid guid, [NotNullWhen(true)] out Type? t) =>
            _guidsToTypes.TryGetValue(guid, out t);

        /// <summary>
        /// Attempts to get the registered vtable pointer for the specified managed interface type.
        /// </summary>
        /// <param name="type">The managed interface type to resolve.</param>
        /// <param name="ptr">
        /// When this method returns, contains the vtable pointer if the lookup succeeded;
        /// otherwise, <see cref="IntPtr.Zero"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a vtable pointer was found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool GetVtableFor(Type type, out IntPtr ptr) => _vtables.TryGetValue(type, out ptr);

        /// <summary>
        /// Dispatches an exception raised during interop execution to the specified target object.
        /// </summary>
        /// <param name="target">The target object that may handle the exception.</param>
        /// <param name="e">The exception to dispatch.</param>
        /// <remarks>
        /// If the target implements <see cref="IMicroComExceptionCallback"/>, its
        /// <see cref="IMicroComExceptionCallback.RaiseException(Exception)"/> method is invoked.
        /// Any exception thrown while handling the callback is suppressed.
        /// </remarks>
        public static void UnhandledException(object target, Exception e)
        {
            if (target is IMicroComExceptionCallback cb)
            {
                try
                {
                    cb.RaiseException(e);
                }
                catch
                {
                    // We've tried
                }
            }
        }

        /// <summary>
        /// Creates a new managed proxy that owns a cloned native reference to the specified interface.
        /// </summary>
        /// <typeparam name="T">The interface type to clone.</typeparam>
        /// <param name="iface">The interface instance whose native reference should be cloned.</param>
        /// <returns>
        /// A new managed proxy that owns an additional native reference to the same underlying object.
        /// </returns>
        /// <remarks>
        /// This method increments the native reference count before creating the new proxy.
        /// </remarks>
        public static T CloneReference<T>(this T iface) where T : IUnknown
        {
            var ownedPointer = GetNativePointer(iface, true);
            return CreateProxyFor<T>(ownedPointer, true);
        }

        /// <summary>
        /// Queries the specified object for another interface type.
        /// </summary>
        /// <typeparam name="T">The interface type to query.</typeparam>
        /// <param name="unknown">The source interface instance.</param>
        /// <returns>A managed proxy for the requested interface.</returns>
        /// <remarks>
        /// This is a convenience extension method over <see cref="MicroComProxyBase.QueryInterface{T}()"/>.
        /// </remarks>
        public static T QueryInterface<T>(this IUnknown unknown) where T : IUnknown
        {
            var proxy = (MicroComProxyBase)unknown;
            return proxy.QueryInterface<T>();
        }

        /// <summary>
        /// Increments the native reference count for the specified interface without performing safety checks.
        /// </summary>
        /// <param name="unknown">The interface whose native reference count should be incremented.</param>
        /// <remarks>
        /// This method assumes that <paramref name="unknown"/> is backed by a <see cref="MicroComProxyBase"/>.
        /// </remarks>
        public static void UnsafeAddRef(this IUnknown unknown)
        {
            ((MicroComProxyBase)unknown).AddRef();
        }

        /// <summary>
        /// Decrements the native reference count for the specified interface without performing safety checks.
        /// </summary>
        /// <param name="unknown">The interface whose native reference count should be decremented.</param>
        /// <remarks>
        /// This method assumes that <paramref name="unknown"/> is backed by a <see cref="MicroComProxyBase"/>.
        /// </remarks>
        public static void UnsafeRelease(this IUnknown unknown)
        {
            ((MicroComProxyBase)unknown).Release();
        }
    }
}
