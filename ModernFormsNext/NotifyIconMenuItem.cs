using System;
using System.ComponentModel;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents an item displayed in a <see cref="NotifyIconContextMenu"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="NotifyIconMenuItem"/> is a non-visual menu item intended for tray icon
    /// context menus. It does not participate in the normal control tree and is rendered by
    /// the active platform backend.
    /// </remarks>
    /// <example>
    /// <code>
    /// var item = new NotifyIconMenuItem("Open", (_, _) => mainForm.Show());
    /// </code>
    /// </example>
    public class NotifyIconMenuItem : Component
    {
        private bool checked_value;
        private bool enabled = true;
        private bool disposed;
        private NotifyIconMenuItemCollection? items;
        private string text = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyIconMenuItem"/> class.
        /// </summary>
        public NotifyIconMenuItem ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyIconMenuItem"/> class with text
        /// and an optional click handler.
        /// </summary>
        /// <param name="text">The text displayed by the tray context menu.</param>
        /// <param name="onClick">The optional handler invoked when the item is selected.</param>
        public NotifyIconMenuItem (string text, EventHandler? onClick = null)
        {
            Text = text;

            if (onClick is not null)
                Click += onClick;
        }

        /// <summary>
        /// Occurs when the user selects the menu item.
        /// </summary>
        public event EventHandler? Click;

        /// <summary>
        /// Gets or sets a value indicating whether the platform menu shows a check mark for
        /// this item.
        /// </summary>
        /// <remarks>
        /// Selecting a checked item does not toggle it automatically. Update this property in
        /// the <see cref="Click"/> handler when toggle behavior is required.
        /// </remarks>
        public bool Checked {
            get => checked_value;
            set {
                ThrowIfDisposed ();
                checked_value = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the item can be selected.
        /// </summary>
        public bool Enabled {
            get => enabled;
            set {
                ThrowIfDisposed ();
                enabled = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this item has child items.
        /// </summary>
        public bool HasItems => items?.Count > 0;

        /// <summary>
        /// Gets the collection of child items displayed as a submenu.
        /// </summary>
        public NotifyIconMenuItemCollection Items {
            get {
                ThrowIfDisposed ();
                return items ??= new NotifyIconMenuItemCollection (this);
            }
        }

        /// <summary>
        /// Gets the parent item that owns this item, or <see langword="null"/> when the item is
        /// in the root context menu.
        /// </summary>
        public NotifyIconMenuItem? Parent { get; internal set; }

        /// <summary>
        /// Gets or sets the text displayed by the tray context menu.
        /// </summary>
        public string Text {
            get => text;
            set {
                ThrowIfDisposed ();
                text = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this item represents a menu separator.
        /// </summary>
        internal virtual bool Separator => false;

        /// <summary>
        /// Programmatically raises the <see cref="Click"/> event when the item is enabled.
        /// </summary>
        public void PerformClick ()
        {
            ThrowIfDisposed ();

            if (!Enabled || Separator)
                return;

            OnClick (EventArgs.Empty);
        }

        /// <summary>
        /// Releases resources used by the <see cref="NotifyIconMenuItem"/>.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.
        /// </param>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                disposed = true;

            base.Dispose (disposing);
        }

        /// <summary>
        /// Raises the <see cref="Click"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnClick (EventArgs e) => Click?.Invoke (this, e);

        private void ThrowIfDisposed ()
        {
            ObjectDisposedException.ThrowIf (disposed, this);
        }
    }
}
