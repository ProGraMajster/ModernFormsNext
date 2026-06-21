using System.Collections.Generic;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Provides screen information for a platform backend.
    /// </summary>
    [Unstable]
    public interface IScreenImpl
    {
        /// <summary>
        /// Gets the total number of screens available on the device.
        /// </summary>
        int ScreenCount { get; }

        /// <summary>
        /// Gets the list of all screens available on the device.
        /// </summary>
        IReadOnlyList<Screen> AllScreens { get; }

        /// <summary>
        /// Gets the screen that contains or is nearest to a window.
        /// </summary>
        /// <param name="window">The window implementation to locate.</param>
        /// <returns>The matching screen, or <see langword="null"/> when no screen can be resolved.</returns>
        Screen? ScreenFromWindow(IWindowBaseImpl window);

        /// <summary>
        /// Gets the screen that contains or is nearest to a point.
        /// </summary>
        /// <param name="point">The point in device pixels.</param>
        /// <returns>The matching screen, or <see langword="null"/> when no screen can be resolved.</returns>
        Screen? ScreenFromPoint(PixelPoint point);

        /// <summary>
        /// Gets the screen that contains or is nearest to a rectangle.
        /// </summary>
        /// <param name="rect">The rectangle in device pixels.</param>
        /// <returns>The matching screen, or <see langword="null"/> when no screen can be resolved.</returns>
        Screen? ScreenFromRect(PixelRect rect);
    }
}
