using ModernFormsNext.WindowKit.Controls.Primitives.PopupPositioning;
using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// Defines a platform-specific popup window implementation.
    /// </summary>
    [Unstable]
    public interface IPopupImpl : IWindowBaseImpl
    {
        /// <summary>
        /// Gets the popup positioner used to place the popup relative to an anchor.
        /// </summary>
        /// <remarks>
        /// Backends that delegate popup placement to the platform may return <see langword="null"/>.
        /// </remarks>
        IPopupPositioner? PopupPositioner { get; }

        /// <summary>
        /// Hints whether the platform window manager should draw a shadow around the popup.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to request a shadow; otherwise, <see langword="false"/>.</param>
        void SetWindowManagerAddShadowHint(bool enabled);
    }
}
