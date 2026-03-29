namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Base class for managed objects exposed to native code through MicroCOM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides lifetime management for objects that are shared between managed and native environments.
    /// It tracks references from both sides and ensures proper destruction when no longer needed.
    /// </para>
    /// <para>
    /// A <see cref="MicroComShadow"/> is used internally to represent the native callable wrapper (CCW).
    /// </para>
    /// </remarks>
    public abstract class CallbackBase : IUnknown, IMicroComShadowContainer
    {
        private readonly object _lock = new object();
        private bool _referencedFromManaged = true;
        private bool _referencedFromNative = false;
        private bool _destroyed;

        /// <summary>
        /// Gets a value indicating whether the object has been destroyed.
        /// </summary>
        public bool IsDestroyed => _destroyed;

        /// <summary>
        /// Called when the object is destroyed.
        /// </summary>
        /// <remarks>
        /// Override this method to release additional resources.
        /// </remarks>
        protected virtual void Destroyed()
        {
        }

        /// <summary>
        /// Releases the managed reference to this object.
        /// </summary>
        /// <remarks>
        /// The object will be destroyed only when both managed and native references are released.
        /// </remarks>
        public void Dispose()
        {
            lock (_lock)
            {
                _referencedFromManaged = false;
                DestroyIfNeeded();
            }
        }

        void DestroyIfNeeded()
        {
            if (_destroyed)
                return;

            if (_referencedFromManaged == false && _referencedFromNative == false)
            {
                _destroyed = true;
                Shadow?.Dispose();
                Shadow = null;
                Destroyed();
            }
        }

        /// <summary>
        /// Gets or sets the associated shadow object.
        /// </summary>
        public MicroComShadow? Shadow { get; set; }

        /// <summary>
        /// Called when the object is referenced from native code.
        /// </summary>
        public void OnReferencedFromNative()
        {
            lock (_lock)
                _referencedFromNative = true;
        }

        /// <summary>
        /// Called when the native reference is released.
        /// </summary>
        public void OnUnreferencedFromNative()
        {
            lock (_lock)
            {
                _referencedFromNative = false;
                DestroyIfNeeded();
            }
        }
    }
}