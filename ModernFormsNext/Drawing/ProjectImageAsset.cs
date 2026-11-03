using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using IOPath = System.IO.Path;

namespace ModernFormsNext.Drawing;

// The asset reference is also the project-relative source path and the build
// output path (or exact manifest resource name). It never contains a host path.
internal static class ProjectImageAsset
{
    internal const int MaximumBytes = 8 * 1024 * 1024;
    private const int MaximumDimension = 4096;
    private const long MaximumPixels = 4 * 1024 * 1024;

    internal static FileStream OpenLocal(string root, string id)
    {
        if (!ProjectAssetPath.IsValidId(id))
            throw new ArgumentException("The asset identifier is not a portable project path.", nameof(id));

        string path = RequireLocalRoot(root);
        foreach (var segment in id.Split('/'))
        {
            path = IOPath.Combine(path, segment);
            RequireOrdinaryEntry(path);
        }

        return File.OpenRead(path);
    }

    internal static string RequireLocalRoot(string root)
    {
        root = IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(root));
        if (OperatingSystem.IsWindows() && (root.StartsWith(@"\\", StringComparison.Ordinal)
            || new DriveInfo(IOPath.GetPathRoot(root)!).DriveType == DriveType.Network))
            throw new IOException("Project assets require a local directory.");

        // Reject links in the trusted root and every asset segment before opening.
        var ancestry = new Stack<string>();
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            ancestry.Push(directory.FullName);
        while (ancestry.TryPop(out var directory))
            RequireOrdinaryEntry(directory);

        return root;
    }

    internal static void RequireOrdinaryEntry(string path)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            throw new IOException("Project assets cannot use links or device paths.");
    }

    internal static SKBitmap Decode(Stream stream)
    {
        long length = stream.CanSeek ? stream.Length : 0;
        if (length <= 0 || length > MaximumBytes)
            throw new IOException("Project images must be nonempty and at most 8 MiB.");

        // Read only the admitted byte count. A file that grows concurrently
        // cannot increase the memory budget passed to the native codec.
        var bytes = new byte[(int)length];
        stream.ReadExactly(bytes);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not
            (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Bmp
                or SKEncodedImageFormat.Gif or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Ico))
            throw new IOException("The project asset is not a supported raster image.");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension
            || (long)info.Width * info.Height > MaximumPixels)
            throw new IOException("The project image exceeds the decoded size budget.");

        var outputInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap();
        try
        {
            if (!bitmap.TryAllocPixels(outputInfo)
                || codec.GetPixels(outputInfo, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new IOException("The project image could not be decoded.");

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
