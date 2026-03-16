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
        IPopupPositioner? PopupPositioner { get; }

        void SetWindowManagerAddShadowHint(bool enabled);
    }
}
