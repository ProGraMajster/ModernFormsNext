using ModernFormsNext.Drawing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Services;

internal static class DesignerProjectAssets
{
    internal const string ImageProperty = ProjectAssetPath.ImageProperty;
    private const int MaximumEntries = 2048;
    private const int MaximumDepth = 12;

    internal static string? GetRoot(string? projectPath, string? documentPath)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var path = IOPath.GetFullPath(projectPath);
            var root = string.Equals(IOPath.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase)
                ? IOPath.GetDirectoryName(path)! : path;
            return ProjectImageAsset.RequireLocalRoot(root);
        }

        if (string.IsNullOrWhiteSpace(documentPath))
            return null;

        // Standalone editing uses the nearest unambiguous project above the
        // host-selected document. No project evaluation or recursive search.
        var documentDirectory = IOPath.GetDirectoryName(IOPath.GetFullPath(documentPath))!;
        var directory = new DirectoryInfo(ProjectImageAsset.RequireLocalRoot(documentDirectory));
        int visited = 0;
        for (int depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
        {
            FileSystemInfo? candidate = null;
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (++visited > MaximumEntries)
                    return null;
                if (!string.Equals(entry.Extension, ".csproj", StringComparison.OrdinalIgnoreCase)
                    || (entry.Attributes & FileAttributes.Directory) != 0)
                    continue;
                if (candidate is not null)
                    return null;
                candidate = entry;
            }
            if (candidate is not null)
            {
                ProjectImageAsset.RequireOrdinaryEntry(candidate.FullName);
                return directory.FullName;
            }
        }
        return null;
    }

    internal static bool IsImage(string path)
        => IOPath.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg"
            or ".bmp" or ".gif" or ".webp" or ".ico";

    internal static IReadOnlyList<string> Browse(string root, out bool truncated)
    {
        var pending = new Stack<(string Directory, int Depth)>();
        var assets = new List<string>();
        truncated = false;
        root = ProjectImageAsset.RequireLocalRoot(root);
        var assetsDirectory = IOPath.Combine(root, "Assets");
        if (!Directory.Exists(assetsDirectory))
            return assets;
        ProjectImageAsset.RequireOrdinaryEntry(assetsDirectory);
        foreach (var prefix in new[] { ProjectAssetPath.EmbeddedPrefix, ProjectAssetPath.ContentPrefix })
        {
            var directory = IOPath.Combine(root, prefix);
            if (Directory.Exists(directory))
                pending.Push((directory, 0));
        }

        int visited = 0;
        while (pending.TryPop(out var current))
        {
            ProjectImageAsset.RequireOrdinaryEntry(current.Directory);
            foreach (var entry in new DirectoryInfo(current.Directory).EnumerateFileSystemInfos())
            {
                if (++visited > MaximumEntries)
                {
                    truncated = true;
                    return Sort(assets);
                }

                var attributes = entry.Attributes;
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    continue;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (current.Depth < MaximumDepth)
                        pending.Push((entry.FullName, current.Depth + 1));
                    else
                        truncated = true;
                    continue;
                }

                var id = IOPath.GetRelativePath(root, entry.FullName).Replace('\\', '/');
                if (ProjectAssetPath.IsValidId(id))
                    assets.Add(id);
            }
        }

        return Sort(assets);
    }

    private static IReadOnlyList<string> Sort(List<string> assets)
    {
        assets.Sort(StringComparer.Ordinal);
        return assets;
    }

    internal static SKBitmap ReadImage(string root, string id)
    {
        using var stream = OpenImage(root, id);
        return ProjectImageAsset.Decode(stream);
    }

    internal static FileStream OpenImage(string root, string id)
    {
        if (!IsImage(id))
            throw new IOException("This project asset is not an image or icon.");
        return ProjectImageAsset.OpenLocal(root, id);
    }

    internal static string Import(string root, string source, bool embedded)
    {
        var prefix = embedded ? ProjectAssetPath.EmbeddedPrefix : ProjectAssetPath.ContentPrefix;
        var id = prefix + IOPath.GetFileName(source);
        if (!ProjectAssetPath.IsValidId(id))
            throw new IOException("Choose an asset with a portable file name.");

        // The source comes from an explicit picker action. Validate and copy the
        // same bounded bytes, then publish only into an unused destination.
        using var input = File.OpenRead(source);
        long length = input.Length;
        if (length <= 0 || length > ProjectImageAsset.MaximumBytes)
            throw new IOException("Imported project assets must be nonempty and at most 8 MiB.");
        var bytes = new byte[(int)length];
        input.ReadExactly(bytes);
        using var buffer = new MemoryStream(bytes, writable: false);
        using var validated = IsImage(id) ? ProjectImageAsset.Decode(buffer) : null;

        var directory = ProjectImageAsset.RequireLocalRoot(root);
        foreach (var segment in prefix.TrimEnd('/').Split('/'))
        {
            directory = IOPath.Combine(directory, segment);
            Directory.CreateDirectory(directory);
            ProjectImageAsset.RequireOrdinaryEntry(directory);
        }

        var destination = IOPath.Combine(directory, IOPath.GetFileName(id));
        var temporary = IOPath.Combine(directory, $".import-{Guid.NewGuid():N}.tmp");
        bool ownsTemporary = false;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                output.Write(bytes);
            }
            File.Move(temporary, destination);
            ownsTemporary = false;
            return id;
        }
        finally
        {
            if (ownsTemporary)
                File.Delete(temporary);
        }
    }
}
