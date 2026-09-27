using System;
using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Renderers;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a ComboBox control.
    /// </summary>
    public class ComboBox : Control
    {
        private PopupWindow? popup;
        private readonly ListBox popup_listbox;
        private bool suppress_popup_close;
        private bool retiring;

        /// <summary>
        /// Initializes a new instance of the ComboBox class.
        /// </summary>
        public ComboBox ()
        {
            popup_listbox = new ListBox { Dock = DockStyle.Fill, SelectItemOnMouseUp = true, ShowHover = true };
            popup_listbox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            popup_listbox.Items.AccessibilityCollectionChanged += Items_AccessibilityCollectionChanged;
        }

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (4, 0, 3, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (120, 28);

        /// <summary>
        /// The default ControlStyle for all instances of ComboBox.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlMidColor;
            });

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (!disposing) { base.Dispose(false); return; }
            if (retiring) return;
            retiring = true;

            // Retire ownership before callbacks: closing a popup can reenter this control's
            // disposal. A caller retaining Items must not retain/call the retired ComboBox.
            var ownedPopup = popup;
            popup = null;
            popup_listbox.SelectedIndexChanged -= ListBox_SelectedIndexChanged;
            popup_listbox.Items.AccessibilityCollectionChanged -= Items_AccessibilityCollectionChanged;

            System.Collections.Generic.List<Exception>? failures = null;
            void Attempt(Action cleanup)
            {
                try { cleanup(); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }

            // PopupWindow.Close destroys its backend and retires text input, but deliberately
            // does not dispose borrowed controls. This ComboBox owns its list in every state,
            // including never opened, hidden for reuse, or already closed with its owner.
            if (ownedPopup is not null) {
                Attempt(ownedPopup.Close);
                // Unlike general borrowed PopupWindow content, this popup's adapter/tree is
                // owned exclusively by the ComboBox, including its implicit scroll controls.
                if (!ownedPopup.adapter.IsDisposed) Attempt(ownedPopup.adapter.Dispose);
            }
            if (!popup_listbox.IsDisposed) Attempt(popup_listbox.Dispose);
            Attempt(() => base.Dispose(true));

            if (failures is { Count: 1 })
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures is not null)
                throw new AggregateException("ComboBox popup and control disposal failed.", failures);
        }

        private void Items_AccessibilityCollectionChanged(bool selectionChanged)
        {
            if (IsAccessibilityObjectCreated)
                _ = AccessibilityObject.GetChildCount();

            NotifyAccessibilityClients(AccessibleEvents.Reorder);

            if (selectionChanged) {
                NotifyAccessibilityClients(AccessibleEvents.Selection);
                NotifyAccessibilityClients(AccessibleEvents.ValueChange);
            }
        }

        /// <summary>
        /// Raised when the drop down portion of the ComboBox is closed.
        /// </summary>
        public event EventHandler? DropDownClosed;

        /// <summary>
        /// Raised when the drop down portion of the ComboBox is opened.
        /// </summary>
        public event EventHandler? DropDownOpened;

        /// <summary>
        /// Gets or sets whether the drop down portion of the ComboBox is currently shown.
        /// </summary>
        /// <remarks>Access on the owning UI thread. A hidden popup is retained for reuse;
        /// explicit disposal closes the popup and disposes its owned list. An opening request
        /// during or after disposal throws <see cref="ObjectDisposedException"/>.</remarks>
        public bool DroppedDown {
            get => popup?.Visible == true;
            set {
                if (value) ObjectDisposedException.ThrowIf(retiring || IsDisposed || Disposing, this);
                if (DroppedDown && !value) {
                    popup?.Hide ();
                    OnDropDownClosed (EventArgs.Empty);
                } else if (!DroppedDown && value) {
                    if (FindForm () is not Form form)
                        throw new InvalidOperationException ("Cannot drop down a ComboBox that is not parented to a Form");

                    popup ??= new PopupWindow (form) {
                        Size = new Size (Width, 102)
                    };

                    popup.Controls.Add (popup_listbox);

                    popup.Show (this, 1, Height);

                    OnDropDownOpened (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the collection of items contained by this ComboBox.
        /// </summary>
        public ListBoxItemCollection Items => popup_listbox.Items;

        // When the selected item of the popup ListBox changes, update the ComboBox
        private void ListBox_SelectedIndexChanged (object? sender, EventArgs e)
        {
            if (popup_listbox.SelectedIndex > -1) {
                if (!suppress_popup_close)
                    DroppedDown = false;

                Invalidate ();

                OnSelectedIndexChanged (e);
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            base.OnClick (e);

            DroppedDown = !DroppedDown;
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            DroppedDown = false;
        }

        /// <summary>
        /// Raises the DropDownClosed event.
        /// </summary>
        protected virtual void OnDropDownClosed (EventArgs e)
        {
            DropDownClosed?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.StateChange);
        }

        /// <summary>
        /// Raises the DropDownOpened event.
        /// </summary>
        protected virtual void OnDropDownOpened (EventArgs e)
        {
            DropDownOpened?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.StateChange);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            // Alt+Up/Down toggles the dropdown
            if (e.Alt && e.KeyCode.In (Keys.Up, Keys.Down)) {
                DroppedDown = !DroppedDown;
                e.Handled = true;
                return;
            }

            // If dropdown is shown, Esc/Enter will close it
            if (e.KeyCode.In (Keys.Escape, Keys.Enter) && DroppedDown) {
                DroppedDown = false;
                e.Handled = true;
                return;
            }

            // If you mouse click an item we automatically close the dropdown,
            // we don't want that behavior when using the keyboard.
            suppress_popup_close = true;
            popup_listbox.RaiseKeyUp (e);
            suppress_popup_close = false;

            if (e.Handled)
                return;

            base.OnKeyUp (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the SelectedIndexChanged event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e)
        {
            SelectedIndexChanged?.Invoke (this, e);
            NotifyAccessibilityClients (AccessibleEvents.Selection);
            NotifyAccessibilityClients (AccessibleEvents.ValueChange);
        }

        /// <summary>
        /// Gets or sets the index of the currently selected item.  Returns -1 if no item is selected.
        /// </summary>
        public int SelectedIndex {
            get => popup_listbox.SelectedIndex;
            set => popup_listbox.SelectedIndex = value;
        }

        /// <summary>
        /// Gets or sets the currently selected item, if any.
        /// </summary>
        public object? SelectedItem {
            get => popup_listbox.SelectedItem;
            set => popup_listbox.SelectedItem = value;
        }

        /// <summary>
        /// Raised when the value of the SelectedIndex property changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
