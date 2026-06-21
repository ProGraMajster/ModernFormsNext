using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace ModernFormsNext.WindowKit.Platform.Storage.FileIO;

/// <summary>
/// Provides helper methods used by BCL-backed storage providers.
/// </summary>
public static class StorageProviderHelpers
{
    /// <summary>
    /// Creates a BCL-backed storage item for an existing local file-system path.
    /// </summary>
    /// <param name="path">The local file or directory path to inspect.</param>
    /// <returns>
    /// A <see cref="BclStorageFolder"/> for an existing directory, a <see cref="BclStorageFile"/>
    /// for an existing file, or <see langword="null"/> when the path is empty or does not exist.
    /// </returns>
    public static IStorageItem? TryCreateBclStorageItem(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directory = new DirectoryInfo(path);
            if (directory.Exists)
            {
                return new BclStorageFolder(directory);
            }

            var file = new FileInfo(path);
            if (file.Exists)
            {
                return new BclStorageFile(file);
            }
        }

        return null;
    }
    
    /// <summary>
    /// Converts a local file-system path to a file URI.
    /// </summary>
    /// <param name="path">The local file-system path to convert.</param>
    /// <returns>A <c>file:</c> URI that represents <paramref name="path"/>.</returns>
    /// <remarks>
    /// The method escapes characters that are meaningful in URI syntax before constructing
    /// the resulting <see cref="Uri"/>.
    /// </remarks>
    public static Uri FilePathToUri(string path)
    {
        var uriPath = new StringBuilder(path)
            .Replace("%", $"%{(int)'%':X2}")
            .Replace("[", $"%{(int)'[':X2}")
            .Replace("]", $"%{(int)']':X2}")
            .ToString();

        return new UriBuilder("file", string.Empty) { Path = uriPath }.Uri;
    }
    
    /// <summary>
    /// Attempts to convert a local file-system path to a file URI.
    /// </summary>
    /// <param name="path">The local file-system path to convert.</param>
    /// <param name="uri">Receives the converted URI when the method succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryFilePathToUri(string path, [NotNullWhen(true)] out Uri? uri)
    {
        try
        {
            uri = FilePathToUri(path);
            return true;
        }
        catch
        {
            uri = null;
            return false;
        }
    }
    
    /// <summary>
    /// Applies a default extension or picker filter extension to a file name when it has no extension.
    /// </summary>
    /// <param name="path">The suggested path or file name.</param>
    /// <param name="defaultExtension">The default extension to append when no filter extension applies.</param>
    /// <param name="filter">The selected file type filter, if any.</param>
    /// <returns>
    /// The original path when it already has an extension, a path with an inferred extension,
    /// or <see langword="null"/> when <paramref name="path"/> is <see langword="null"/>.
    /// </returns>
    [return: NotNullIfNotNull(nameof(path))]
    public static string? NameWithExtension(string? path, string? defaultExtension, FilePickerFileType? filter)
    {
        var name = Path.GetFileName(path);
        if (name != null && !Path.HasExtension(name))
        {
            if (filter?.Patterns?.Count > 0)
            {
                if (defaultExtension != null
                    && filter.Patterns.Contains(defaultExtension))
                {
                    return Path.ChangeExtension(path, defaultExtension.TrimStart('.'));
        }

                var ext = filter.Patterns.FirstOrDefault(x => x != "*.*");
                ext = ext?.Split(new[] { "*." }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (ext != null)
                {
                    return Path.ChangeExtension(path, ext);
                }
            }

            if (defaultExtension != null)
            {
                return Path.ChangeExtension(path, defaultExtension);
            }
        }

        return path;
    }
}
