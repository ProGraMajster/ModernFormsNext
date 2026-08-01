using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Provides a base implementation for managed proxy objects that wrap native COM-like interfaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type stores a native interface pointer and exposes helper methods for reference counting,
    /// interface querying, and ownership management.
    /// </para>
    /// <para>
    /// Instances of this class may either own the wrapped native reference or act as a non-owning view
    /// over an existing native pointer. When the proxy owns the handle, it is responsible for releasing
    /// the native reference during disposal or finalization.
    /// </para>
    /// <para>
    /// The finalizer attempts to release the native reference on the captured
    /// <see cref="SynchronizationContext"/> when one was available at construction time. This is useful
    /// for interop scenarios where native resources must be released on a specific thread.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// IntPtr nativePointer = GetNativeInterfacePointer();
    /// using var proxy = new MicroComProxyBase(nativePointer, ownsHandle: true);
    ///
    /// IntPtr otherInterface;
    /// int hr = proxy.QueryInterface(someGuid, out otherInterface);
    /// if (hr == 0)
    /// {
    ///     // Successfully obtained another interface pointer.
    /// }
    /// </code>
    /// </example>
    public unsafe class MicroComProxyBase : CriticalFinalizerObject, IUnknown
    {
        private IntPtr _nativePointer;
        private bool _ownsHandle;
        private readonly SynchronizationContext? _synchronizationContext;

        /// <summary>
        /// Gets the wrapped native interface pointer.
        /// </summary>
        /// <returns>
        /// The native pointer represented by this proxy.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the proxy has already been disposed and no longer contains a valid native pointer.
        /// </exception>
        public IntPtr NativePointer
        {
            get
            {
                if (_nativePointer == IntPtr.Zero)
                    throw new ObjectDisposedException(this.GetType().FullName);
                return _nativePointer;
            }
        }

        /// <summary>
        /// Gets the native pointer cast to a pointer-to-vtable-pointer representation.
        /// </summary>
        /// <remarks>
        /// This property is intended for low-level interop operations that need direct access to the
        /// native virtual method table.
        /// </remarks>
        public void*** PPV => (void***)NativePointer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicroComProxyBase"/> class.
        /// </summary>
        /// <param name="nativePointer">The native interface pointer to wrap.</param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> to indicate that the proxy owns the native reference and must release it;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <remarks>
        /// The current <see cref="SynchronizationContext"/> is captured during construction and may later
        /// be used by the finalizer when releasing owned native references.
        /// </remarks>
        public MicroComProxyBase(IntPtr nativePointer, bool ownsHandle)
        {
            _nativePointer = nativePointer;
            _ownsHandle = ownsHandle;
            _synchronizationContext = SynchronizationContext.Current;
            if (!_ownsHandle)
                GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the number of entries expected in the base virtual method table.
        /// </summary>
        /// <remarks>
        /// The default COM-like base layout contains three methods: QueryInterface, AddRef, and Release.
        /// Derived proxy types can override this property if they need to describe a larger vtable contract.
        /// </remarks>
        protected virtual int VTableSize => 3;

        /// <summary>
        /// Increments the native reference count.
        /// </summary>
        /// <remarks>
        /// This method forwards the call to the native <c>AddRef</c> entry in the wrapped interface's
        /// virtual method table.
        /// </remarks>
        public void AddRef()
        {
            LocalInterop.CalliStdCallvoid(PPV, (*PPV)[1]);
        }

        /// <summary>
        /// Decrements the native reference count.
        /// </summary>
        /// <remarks>
        /// This method forwards the call to the native <c>Release</c> entry in the wrapped interface's
        /// virtual method table.
        /// </remarks>
        public void Release()
        {
            LocalInterop.CalliStdCallvoid(PPV, (*PPV)[2]);
        }

        /// <summary>
        /// Queries the wrapped native object for a specific interface.
        /// </summary>
        /// <param name="guid">The interface identifier to query.</param>
        /// <param name="ppv">
        /// When this method returns, contains the native pointer to the requested interface if the call
        /// succeeded; otherwise, a null pointer.
        /// </param>
        /// <returns>
        /// The HRESULT returned by the native <c>QueryInterface</c> call.
        /// </returns>
        /// <remarks>
        /// A return value of <c>0</c> indicates success.
        /// </remarks>
        public int QueryInterface(Guid guid, out IntPtr ppv)
        {
            IntPtr r = default;
            var rv = LocalInterop.CalliStdCallint(PPV, &guid, &r, (*PPV)[0]);
            ppv = r;
            return rv;
        }

        /// <summary>
        /// Queries the wrapped native object for the specified interface type and returns a managed proxy.
        /// </summary>
        /// <typeparam name="T">The interface type to query.</typeparam>
        /// <returns>
        /// A managed proxy representing the requested interface.
        /// </returns>
        /// <exception cref="COMException">
        /// Thrown when the native <c>QueryInterface</c> call fails.
        /// </exception>
        /// <remarks>
        /// The interface type must be registered with <see cref="MicroComRuntime"/> so that its GUID and
        /// proxy factory can be resolved.
        /// </remarks>
        /// <example>
        /// <code>
        /// var stream = proxy.QueryInterface&lt;IMyNativeStream&gt;();
        /// </code>
        /// </example>
        public T QueryInterface<T>() where T : IUnknown
        {
            var guid = MicroComRuntime.GetGuidFor(typeof(T));
            var rv = QueryInterface(guid, out var ppv);
            if (rv == 0)
                return (T)MicroComRuntime.CreateProxyFor(typeof(T), ppv, true);

            throw new COMException("QueryInterface failed", rv);
        }

        /// <summary>
        /// Gets a value indicating whether the proxy has been disposed.
        /// </summary>
        public bool IsDisposed => _nativePointer == IntPtr.Zero;

        /// <summary>
        /// Releases resources used by the current proxy.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> when called from <see cref="Dispose()"/>; <see langword="false"/> when
        /// called from the finalizer path.
        /// </param>
        /// <remarks>
        /// If the proxy owns the native handle, this method calls <see cref="Release"/> exactly once before
        /// clearing the stored pointer.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (_nativePointer == IntPtr.Zero)
                return;

            if (_ownsHandle)
            {
                Release();
                _ownsHandle = false;
            }

            _nativePointer = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the current proxy and, if owned, the wrapped native reference.
        /// </summary>
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Gets a value indicating whether this proxy currently owns the wrapped native reference.
        /// </summary>
        public bool OwnsHandle => _ownsHandle;

        /// <summary>
        /// Ensures that this proxy owns the wrapped native reference.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the proxy is currently non-owning, this method increments the native reference count and
        /// marks the proxy as the owner of that reference.
        /// </para>
        /// <para>
        /// This also re-enables finalization so that the owned handle can be released if the proxy is not
        /// disposed explicitly.
        /// </para>
        /// </remarks>
        public void EnsureOwned()
        {
            if (!_ownsHandle)
            {
                GC.ReRegisterForFinalize(this);
                AddRef();
                _ownsHandle = true;
            }
        }

        private static readonly SendOrPostCallback _disposeDelegate = DisposeOnContext;

        /// <summary>
        /// Releases a proxy instance on a synchronization context callback.
        /// </summary>
        /// <param name="state">The proxy instance to dispose.</param>
        private static void DisposeOnContext(object? state)
        {
            (state as MicroComProxyBase)?.Dispose(false);
        }

        /// <summary>
        /// Finalizes the current proxy and releases the owned native reference if required.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The finalizer does nothing when the proxy does not own the wrapped native pointer.
        /// </para>
        /// <para>
        /// When a <see cref="SynchronizationContext"/> was captured at construction time, the actual release
        /// is posted back to that context. Otherwise, disposal is performed directly on the finalizer thread.
        /// </para>
        /// </remarks>
        ~MicroComProxyBase()
        {
            if (!_ownsHandle)
                return;

            if (_synchronizationContext == null)
                Dispose();
            else
                _synchronizationContext.Post(_disposeDelegate, this);
        }
    }
}
