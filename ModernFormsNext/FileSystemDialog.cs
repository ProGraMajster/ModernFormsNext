using System;
using System.IO;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a base class for file system dialogs.
    /// </summary>
    public abstract class FileSystemDialog
    {
        /// <summary>
        /// Gets or sets the local directory suggested as the dialog's starting location.
        /// </summary>
        /// <remarks>
        /// When <see cref="InitialLocation"/> is set, it takes precedence. A null, empty,
        /// or whitespace-only value leaves the starting location up to the picker provider.
        /// Relative paths resolve against the current working directory. A path that cannot
        /// be found as a directory supplies no suggestion. Malformed paths can still raise
        /// an exception when the dialog opens.
        /// </remarks>
        public string? InitialDirectory { get; set; }

        /// <summary>Gets or sets caller cancellation; native UI may remain visible after the Task cancels.</summary>
        public System.Threading.CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets the title for the dialog.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Gets or sets a previously selected storage folder as the initial picker location.</summary>
        /// <remarks>Use for content URI folders. The dialog does not own or dispose this item.</remarks>
        public WindowKit.Platform.Storage.IStorageFolder? InitialLocation { get; set; }

        internal WindowKit.Platform.Storage.IStorageFolder? GetInitialDirectory ()
        {
            if (InitialLocation is { } location) return location;

            var path = InitialDirectory;
            if (string.IsNullOrWhiteSpace (path) || (Uri.TryCreate (path, UriKind.Absolute, out var uri) && !uri.IsFile))
                return null;

            var dir_info = new DirectoryInfo (path);
            return dir_info.Exists ? new BclStorageFolder (dir_info) : null;
        }
    }
}
