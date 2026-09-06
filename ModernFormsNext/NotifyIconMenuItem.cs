using System;
using System.ComponentModel;
using System.Windows.Input;
using ModernFormsNext.DataBinding;

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
    public class NotifyIconMenuItem : Component, ICommandBindingTargetProvider
    {
        private bool checked_value;
        private bool enabled = true;
        private bool disposed;
        private NotifyIconMenuItemCollection? items;
        private string text = string.Empty;
        private CommandSource? commandSource;
        private bool commandEnabled = true;

        /// <summary>
        /// Gets or sets the command executed after <see cref="Click"/> on activation.
        /// </summary>
        /// <remarks>
        /// Assign on the UI thread. Null preserves event-only behavior. The item shares Button's
        /// command binding behavior: parameter-aware availability, background requery through the
        /// UI dispatcher, and detach on replacement/removal/disposal. Predicate exceptions disable
        /// the item and propagate unchanged; later requery or removal can recover it. The command
        /// is not owned or disposed by the item. Open native menus retain their existing snapshot
        /// behavior; activation still rechecks availability. Designer serialization is deferred.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ICommand? Command {
            get => commandSource?.Command;
            set {
                ThrowIfDisposed();
                (commandSource ??= new CommandSource(this)).Command = value;
            }
        }

        /// <summary>
        /// Gets or sets the nullable parameter used to evaluate and execute <see cref="Command"/>.
        /// </summary>
        /// <remarks>
        /// Assign on the UI thread. A different reference immediately reevaluates availability.
        /// Mutating the same object requires the command's CanExecuteChanged notification.
        /// Execution uses the current parameter after Click. Disposal releases, but does not
        /// dispose, the parameter. Predicate exceptions follow <see cref="Command"/> semantics.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? CommandParameter {
            get => commandSource?.Parameter;
            set {
                ThrowIfDisposed();
                (commandSource ??= new CommandSource(this)).Parameter = value;
            }
        }

        bool ICommandBindingTargetProvider.IsCommandSourceDisposed => disposed;
        void ICommandBindingTargetProvider.SetCommandEnabled(bool value) => commandEnabled = value;

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
        /// <remarks>
        /// Set on the UI thread. The setter records local intent; the getter combines it with
        /// command availability. Requery never overwrites a locally assigned false value.
        /// Native menus read this effective state when their snapshot is constructed.
        /// </remarks>
        public bool Enabled {
            get => enabled && commandEnabled;
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
        /// <remarks>
        /// Call on the UI thread. Separators do nothing. An available command runs after Click,
        /// using the current binding and parameter and a fresh CanExecute check. Click, predicate
        /// and execute exceptions propagate unchanged. A Click exception prevents execution.
        /// </remarks>
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
            if (disposing) {
                commandSource?.Dispose();
                disposed = true;
            }

            base.Dispose (disposing);
        }

        /// <summary>
        /// Raises the <see cref="Click"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnClick (EventArgs e)
        {
            if (disposed || !Enabled || Separator)
                return;
            if (commandSource is not null && !commandSource.CanExecute())
                return;

            Click?.Invoke(this, e);
            if (!disposed && Enabled)
                commandSource?.Execute();
        }

        private void ThrowIfDisposed ()
        {
            ObjectDisposedException.ThrowIf (disposed, this);
        }
    }
}
