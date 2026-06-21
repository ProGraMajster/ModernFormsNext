using System;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Represents a native platform surface handle with size and scaling metadata.
    /// </summary>
    /// <remarks>
    /// This backend-facing contract is unstable and should be implemented only by platform
    /// backends that own the native surface lifetime.
    /// </remarks>
    [Unstable]
    public interface INativePlatformHandleSurface : IPlatformHandle
    {
        /// <summary>
        /// Gets the surface size in device pixels.
        /// </summary>
        PixelSize Size { get; }

        /// <summary>
        /// Gets the scale factor that converts logical pixels to device pixels.
        /// </summary>
        double Scaling { get; }
    }
}
