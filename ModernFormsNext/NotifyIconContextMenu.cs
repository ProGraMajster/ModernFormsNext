using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a native context menu associated with a <see cref="NotifyIcon"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This menu is separate from <see cref="ContextMenu"/> because tray icons are not
    /// controls and do not have a parent <see cref="Form"/>. The active platform backend
    /// displays the menu using operating system facilities.
    /// </para>
    /// <para>
    /// The Windows backend displays this menu using a native popup menu. Backends that do
    /// not support tray menus should report lack of support through <see cref="NotifyIcon"/>
    /// creation rather than silently ignoring menu requests.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var menu = new NotifyIconContextMenu();
    /// menu.Items.Add("Open", (_, _) => mainForm.Show());
    /// menu.Items.AddSeparator();
    /// menu.Items.Add("Exit", (_, _) => Application.Exit());
    ///
    /// notifyIcon.ContextMenu = menu;
    /// </code>
    /// </example>
    public class NotifyIconContextMenu : Component
    {
        private readonly NotifyIconMenuItemCollection items;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyIconContextMenu"/> class.
        /// </summary>
        public NotifyIconContextMenu ()
        {
            items = new NotifyIconMenuItemCollection (null);
        }

        /// <summary>
        /// Gets the root items displayed in the tray icon context menu.
        /// </summary>
        public NotifyIconMenuItemCollection Items {
            get {
                ThrowIfDisposed ();
                return items;
            }
        }

        internal void Show (IPlatformTrayIcon platformIcon, PixelPoint screenLocation)
        {
            ThrowIfDisposed ();

            var commands = new Dictionary<int, NotifyIconMenuItem> ();
            var next_command_id = 1;
            var platform_items = BuildPlatformItems (items, commands, ref next_command_id);

            if (platform_items.Count == 0)
                return;

            var command_id = platformIcon.ShowContextMenu (platform_items, screenLocation);

            if (command_id != 0 && commands.TryGetValue (command_id, out var item))
                item.PerformClick ();
        }

        /// <summary>
        /// Releases resources used by the <see cref="NotifyIconContextMenu"/>.
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

        private static IReadOnlyList<PlatformTrayMenuItem> BuildPlatformItems (
            IReadOnlyList<NotifyIconMenuItem> source,
            IDictionary<int, NotifyIconMenuItem> commands,
            ref int nextCommandId)
        {
            var result = new List<PlatformTrayMenuItem> (source.Count);

            foreach (var item in source) {
                if (item.Separator) {
                    result.Add (new PlatformTrayMenuItem (0, string.Empty, false, false, true));
                    continue;
                }

                var child_items = BuildPlatformItems (item.Items, commands, ref nextCommandId);
                var command_id = child_items.Count == 0 ? nextCommandId++ : 0;

                if (command_id != 0)
                    commands.Add (command_id, item);

                result.Add (new PlatformTrayMenuItem (
                    command_id,
                    item.Text,
                    item.Enabled,
                    item.Checked,
                    false,
                    child_items));
            }

            return result;
        }

        private void ThrowIfDisposed ()
        {
            ObjectDisposedException.ThrowIf (disposed, this);
        }
    }
}
