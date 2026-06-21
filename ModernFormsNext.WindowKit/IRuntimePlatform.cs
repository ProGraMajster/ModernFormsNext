using System;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Provides runtime services that are supplied by the active platform backend.
    /// </summary>
    [Unstable]
    public interface IRuntimePlatform
    {
        /// <summary>
        /// Starts a repeating system timer.
        /// </summary>
        /// <param name="interval">The interval between timer ticks.</param>
        /// <param name="tick">The callback invoked on each tick.</param>
        /// <returns>An object that stops the timer when disposed.</returns>
        IDisposable StartSystemTimer(TimeSpan interval, Action tick);

        /// <summary>
        /// Gets information about the current runtime platform.
        /// </summary>
        /// <returns>The runtime platform information reported by the backend.</returns>
        RuntimePlatformInfo GetRuntimeInfo();

        /// <summary>
        /// Allocates an unmanaged memory blob owned by the runtime platform.
        /// </summary>
        /// <param name="size">The number of bytes to allocate.</param>
        /// <returns>The allocated unmanaged blob.</returns>
        IUnmanagedBlob AllocBlob(int size);
    }

    /// <summary>
    /// Represents a platform-allocated unmanaged memory block.
    /// </summary>
    /// <remarks>
    /// Call <see cref="IDisposable.Dispose"/> to release the allocation. The <see cref="Address"/>
    /// property is invalid after disposal.
    /// </remarks>
    [Unstable]
    public interface IUnmanagedBlob : IDisposable
    {
        /// <summary>
        /// Gets the starting address of the unmanaged memory block.
        /// </summary>
        IntPtr Address { get; }

        /// <summary>
        /// Gets the allocation size in bytes.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Gets a value indicating whether the unmanaged memory block has been released.
        /// </summary>
        bool IsDisposed { get; }

    }

    /// <summary>
    /// Describes the form factor reported by the runtime platform.
    /// </summary>
    [Unstable]
    public record struct RuntimePlatformInfo
    {
        /// <summary>
        /// Gets the normalized form factor derived from <see cref="IsDesktop"/> and <see cref="IsMobile"/>.
        /// </summary>
        public FormFactorType FormFactor => IsDesktop ? FormFactorType.Desktop :
            IsMobile ? FormFactorType.Mobile : FormFactorType.Unknown;

        /// <summary>
        /// Gets or sets a value indicating whether the runtime platform is desktop-oriented.
        /// </summary>
        public bool IsDesktop { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the runtime platform is mobile-oriented.
        /// </summary>
        public bool IsMobile { get; set; }
    }

    /// <summary>
    /// Identifies the broad device form factor of the runtime platform.
    /// </summary>
    [Unstable]
    public enum FormFactorType
    {
        /// <summary>
        /// The form factor is unknown or has not been reported by the backend.
        /// </summary>
        Unknown,

        /// <summary>
        /// The platform is desktop-oriented.
        /// </summary>
        Desktop,

        /// <summary>
        /// The platform is mobile-oriented.
        /// </summary>
        Mobile
    }
}
