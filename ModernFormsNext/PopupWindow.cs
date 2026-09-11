using System.Drawing;
using ModernFormsNext.WindowKit.Controls.Primitives.PopupPositioning;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a popup window used for things like ComboBoxes and context menus.
    /// </summary>
    public class PopupWindow : WindowBase
    {
        private readonly Form parent_form;
        private bool ownsTextInput;
        private long textInputOwnershipVersion;

        /// <summary>
        /// Initializes a new instance of the PopupWindow class.
        /// </summary>
        public PopupWindow (Form parentForm) : base (parentForm.window.CreatePopup ()!) // NRT - This would only be null if we were using WindowKit overlaw popups
        {
            StartPosition = FormStartPosition.Manual;

            parent_form = parentForm;
            parent_form.Deactivated += ParentFormDeactivated;
            Closed += PopupClosed;
        }

        private void ParentFormDeactivated(object? sender, System.EventArgs e) => Hide();

        private void PopupClosed(object? sender, System.EventArgs e)
        {
            parent_form.Deactivated -= ParentFormDeactivated;
            RestoreParentTextInput();
        }

        internal Form ParentForm => parent_form;
        internal bool OwnsParentTextInput => ownsTextInput && !InputBindingsClosed;
        internal long TextInputOwnershipVersion => textInputOwnershipVersion;

        internal void RestoreParentTextInput() => RestoreParentTextInput(textInputOwnershipVersion);

        internal void RestoreParentTextInput(long expectedVersion)
        {
            if (!ownsTextInput || expectedVersion != textInputOwnershipVersion) return;
            ownsTextInput = false;
            // Relinquish only this popup. A finish observer may already have opened another
            // popup (or a fresh session of this one), which owns the shared native method now.
            if (ReferenceEquals(Application.ActivePopupWindow, this))
                Application.ActivePopupWindow = null;
            if (parent_form.IsActive && !parent_form.InputBindingsClosed)
                parent_form.SetTextInputActive(true);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // Hiding retains a popup for reuse. Destruction must release the parent's event
            // subscription so it neither retains this tree nor calls Hide on a closed backend.
            if (disposing)
                parent_form.Deactivated -= ParentFormDeactivated;
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 100);

        /// <summary>
        /// Gets the default style for all controls of this type.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlMidColor;
            });

        private IPopupImpl PopupImpl => (IPopupImpl)window;

        /// <summary>
        /// Show the PopupWindow at the specified screen coordinates
        /// </summary>
        public void Show (int x, int y)
        {
            System.ObjectDisposedException.ThrowIf(InputBindingsClosed, this);
            System.ObjectDisposedException.ThrowIf(parent_form.InputBindingsClosed, parent_form);
            var point = parent_form.PointToClient (new Point (x, y));

            var ppp = new PopupPositionerParameters {
                AnchorRectangle = new WindowKit.Rect (point.X, point.Y, 1, 1),
                Anchor = PopupAnchor.TopLeft,
                Gravity = PopupGravity.BottomRight,
                ConstraintAdjustment = PopupPositionerConstraintAdjustment.All,
                Size = Size.ToAvaloniaSize ()
            };

            PopupImpl.PopupPositioner?.Update (ppp);
            if (InputBindingsClosed || parent_form.InputBindingsClosed) return;

            var previousPopup = Application.ActivePopupWindow;
            Application.ActivePopupWindow = this;
            ownsTextInput = true;
            long version = ++textInputOwnershipVersion;
            bool IsCurrentShow() => !InputBindingsClosed && !parent_form.InputBindingsClosed &&
                version == textInputOwnershipVersion && ownsTextInput &&
                ReferenceEquals(Application.ActivePopupWindow, this);
            try {
                // Only one popup leases native text input. Retire the old core proxy too,
                // even when that popup stays visible for its existing menu lifecycle.
                if (previousPopup is not null && !ReferenceEquals(previousPopup, this))
                    previousPopup.SetTextInputActive(false);
                if (!IsCurrentShow()) {
                    RestoreParentTextInput(version);
                    return;
                }
                parent_form.SetTextInputActive(false);
                if (!IsCurrentShow()) {
                    RestoreParentTextInput(version);
                    return;
                }
                Show ();
                if (!Visible) RestoreParentTextInput(version);
            }
            catch (System.Exception failure) {
                try {
                    if (version == textInputOwnershipVersion) {
                        if (Visible && !InputBindingsClosed) Hide();
                        else RestoreParentTextInput(version);
                    }
                }
                catch (System.Exception cleanup) {
                    throw new System.AggregateException("Popup show and input-owner cleanup failed.", failure, cleanup);
                }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                throw;
            }
        }

        /// <summary>
        /// Show the PopupWindow at the specified screen coordinates
        /// </summary>
        public void Show (Point screenLocation) => Show (screenLocation.X, screenLocation.Y);

        /// <summary>
        /// Show the PopupWindow at the specified coordinates relative to the provided Control
        /// </summary>
        public void Show (Control control, int x, int y)
        {
            var pos = control.GetPositionInForm ();

            Show (parent_form.PointToScreen (new Point (pos.X + x, pos.Y + y)));
        }

        /// <summary>
        /// Gets or sets the unscaled size of the window.
        /// </summary>
        public new Size Size { get; set; }

        /// <summary>
        /// Gets the ControlStyle properties for this instance of the Control.
        /// </summary>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
