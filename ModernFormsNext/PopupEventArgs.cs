using System.ComponentModel;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Provides data for the <see cref="ToolTip.Popup"/> event.
    /// </summary>
    /// <remarks>
    /// The event is raised before a tooltip popup is shown. Handlers may set
    /// <see cref="CancelEventArgs.Cancel"/> to prevent display, or adjust
    /// <see cref="ToolTipSize"/> to reserve a custom drawing area.
    /// </remarks>
    public class PopupEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopupEventArgs"/> class.
        /// </summary>
        /// <param name="associatedWindow">The ModernFormsNext window that will own the popup.</param>
        /// <param name="associatedControl">The control associated with the tooltip.</param>
        /// <param name="isBalloon">Whether the tooltip is using balloon-style rounded rendering.</param>
        /// <param name="size">The proposed tooltip size, in logical pixels.</param>
        public PopupEventArgs(WindowBase? associatedWindow, Control? associatedControl, bool isBalloon, Size size)
        {
            AssociatedWindow = associatedWindow;
            AssociatedControl = associatedControl;
            IsBalloon = isBalloon;
            ToolTipSize = size;
        }

        /// <summary>
        /// Gets the ModernFormsNext window that will own the popup.
        /// </summary>
        public WindowBase? AssociatedWindow { get; }

        /// <summary>
        /// Gets the control associated with the tooltip.
        /// </summary>
        public Control? AssociatedControl { get; }

        /// <summary>
        /// Gets a value indicating whether balloon-style rounded rendering is enabled.
        /// </summary>
        public bool IsBalloon { get; }

        /// <summary>
        /// Gets or sets the tooltip size, in logical pixels.
        /// </summary>
        /// <remarks>
        /// Setting this value affects the popup window size before it is displayed. The value is
        /// clamped internally to at least one pixel in each dimension.
        /// </remarks>
        public Size ToolTipSize { get; set; }
    }
}
