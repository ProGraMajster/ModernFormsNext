using System;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows
{
    /// <summary>
    /// Implements <see cref="IPlatformAccessibilityService"/> by forwarding notifications to Win32.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext controls are rendered by the framework rather than by native child
    /// windows. Win32 accessibility notifications therefore target the owning top-level window's
    /// client object unless the shared caller supplies a more specific native object identifier.
    /// </remarks>
    internal sealed class WindowsAccessibilityService : IPlatformAccessibilityService
    {
        private const int ObjIdClient = unchecked((int)0xFFFFFFFC);

        /// <inheritdoc/>
        public void NotifyClients(IWindowBaseImpl owner, int eventId, int objectId, int childId)
        {
            ArgumentNullException.ThrowIfNull(owner);

            var handle = owner.Handle.Handle;

            if (handle == IntPtr.Zero || eventId == 0)
                return;

            var host = owner.TryGetFeature<IPlatformAccessibilityHost>();

            if (host?.AccessibilityRoot is null)
                return;

            var nativeObjectId = objectId == 0 ? ObjIdClient : objectId;
            NotifyWinEvent((uint)eventId, handle, nativeObjectId, childId);
        }

        /// <inheritdoc/>
        public void NotifyClients(
            IWindowBaseImpl owner,
            IPlatformAccessibleObject source,
            int eventId,
            int objectId,
            int childId)
        {
            NotifyClients(owner, eventId, objectId, childId);

            // UIA providers are created lazily by WM_GETOBJECT. A normal accessibility
            // notification must not instantiate a provider when no UIA client has queried the HWND.
            if (owner is WindowImpl window)
                window.TryRaiseUiaNotification(source, eventId);
        }
    }
}
