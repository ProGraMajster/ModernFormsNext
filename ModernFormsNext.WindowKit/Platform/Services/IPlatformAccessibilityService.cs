using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WindowKit.Platform.Services
{
    /// <summary>
    /// Provides platform-specific accessibility notification support for shared ModernFormsNext controls.
    /// </summary>
    /// <remarks>
    /// Shared framework code uses this service to report semantic changes, focus changes,
    /// visibility changes, and location changes without depending on a native accessibility API.
    /// Windows implementations translate the integer event identifiers to Win32 <c>EVENT_*</c>
    /// values. Other backends may ignore unsupported notifications until their native
    /// accessibility layer is implemented.
    /// </remarks>
    public interface IPlatformAccessibilityService
    {
        /// <summary>
        /// Notifies the platform accessibility layer that a control or one of its children changed.
        /// </summary>
        /// <param name="owner">The platform window that owns the notifying control.</param>
        /// <param name="eventId">The platform-neutral event identifier.</param>
        /// <param name="objectId">
        /// The platform object identifier. A value of <c>0</c> asks the backend to use its
        /// default client object for the owner window.
        /// </param>
        /// <param name="childId">The child identifier, or <c>0</c> for the owner object itself.</param>
        /// <remarks>
        /// This method is expected to be called from the UI thread because most native
        /// accessibility notification APIs are associated with the owning window handle.
        /// </remarks>
        void NotifyClients(IWindowBaseImpl owner, int eventId, int objectId, int childId);

    }

    /// <summary>
    /// Adds element-level notification routing for platform backends that support native UI
    /// Automation providers. This contract remains internal so the public WindowKit API stays
    /// unchanged.
    /// </summary>
    internal interface IPlatformUiaAccessibilityService : IPlatformAccessibilityService
    {
        void NotifyClients(
            IWindowBaseImpl owner,
            Platform.Accessibility.IPlatformAccessibleObject source,
            int eventId,
            int objectId,
            int childId);
    }
}
