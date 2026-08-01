namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Represents an object that can be wrapped by a <see cref="MicroComShadow"/>.
    /// </summary>
    /// <remarks>
    /// Implemented by managed objects that need to be exposed to native code.
    /// </remarks>
    public interface IMicroComShadowContainer
    {
        /// <summary>
        /// Gets or sets the shadow wrapper, or <see langword="null"/> before a wrapper is created
        /// or after it has been released.
        /// </summary>
        MicroComShadow? Shadow { get; set; }

        /// <summary>
        /// Called when the object is referenced from native code.
        /// </summary>
        void OnReferencedFromNative();

        /// <summary>
        /// Called when the native reference is released.
        /// </summary>
        void OnUnreferencedFromNative();
    }
}
