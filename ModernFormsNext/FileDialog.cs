using System;
using System.Collections.Generic;
using System.IO;
using ModernFormsNext.WindowKit.Platform.Storage;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a base class for file dialogs.
    /// </summary>
    public abstract class FileDialog : FileSystemDialog
    {
        /// <summary>Gets selected storage files, including content URIs that have no local path.</summary>
        /// <remarks>Use OpenReadAsync/OpenWriteAsync. The caller owns returned items and streams.</remarks>
        public IReadOnlyList<IStorageFile> SelectedFiles { get; internal set; } = Array.Empty<IStorageFile>();

        internal List<FilePickerFileType> filters = new List<FilePickerFileType> ();

        /// <summary>
        /// Adds a file filter choice to the dialog.
        /// </summary>
        /// <param name="name">Name of the filter, for example: "Text Files".</param>
        /// <param name="extensions">File extensions to filter for, for example: "*.txt", "*.log".</param>
        public void AddFilter (string name, params string[] extensions)
        {
            var filter = new FilePickerFileType (name) {
                Patterns = new List<string> (extensions)
            };

            filters.Add (filter);
        }

        /// <summary>
        /// Gets or sets the first selection as a local path or absolute storage URI.
        /// </summary>
        public string? FileName {
            get => FileNames.Count > 0 ? NormalizeSelection (FileNames[0]) : null;
            set {
                FileNames.Clear ();

                if (value != null)
                    FileNames.Add (NormalizeSelection (value));
            }
        }

        /// <summary>
        /// Gets the selected local paths or absolute storage URIs. Use SelectedFiles for streams.
        /// </summary>
        public List<string> FileNames { get; } = new List<string> ();

        private static string NormalizeSelection(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile
                ? uri.AbsoluteUri : System.IO.Path.GetFullPath(value);
    }
}
