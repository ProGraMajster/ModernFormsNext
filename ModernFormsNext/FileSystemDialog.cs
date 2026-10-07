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
        /// Gets or sets the initial directory for the dialog.
        /// </summary>
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
            if (InitialLocation is not null) return InitialLocation;
            if (InitialDirectory is not null && !(Uri.TryCreate(InitialDirectory, UriKind.Absolute, out var uri) && !uri.IsFile)) {
                var dir_info = new DirectoryInfo (InitialDirectory);

                if (dir_info.Exists)
                    return new BclStorageFolder (dir_info);
            }

            return null;
        }
    }
}
