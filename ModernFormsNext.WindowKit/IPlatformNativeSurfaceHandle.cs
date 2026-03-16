using System;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    [Unstable]
    public interface INativePlatformHandleSurface : IPlatformHandle
    {
        PixelSize Size { get; }
        double Scaling { get; }
    }
}
