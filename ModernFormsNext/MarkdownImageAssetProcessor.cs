using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ModernFormsNext;

/// <summary>
/// Validates and copies local image files into a host-configured asset directory.
/// </summary>
/// <remarks>
/// The processor performs bounded asynchronous I/O and finalizes copies by moving a temporary file
/// within the destination directory. It is platform-neutral and does not open a file picker or
/// assume a project layout.
/// </remarks>
public static class MarkdownImageAssetProcessor
{
    private const int CopyBufferSize = 64 * 1024;
    private static readonly string[] reservedWindowsFileNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// Copies a local image into an asset directory and returns its normalized Markdown reference.
    /// </summary>
    /// <param name="sourcePath">The local source file selected by the host.</param>
    /// <param name="options">Validation, destination, and collision options.</param>
    /// <param name="cancellationToken">Cancels validation or copying.</param>
    /// <returns>A result that never exposes a partial temporary file as a successful asset.</returns>
    public static async Task<MarkdownImageAssetResult> CopyAsync(
        string sourcePath,
        MarkdownImageAssetOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(options);

        string? temporaryPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourcePath))
                return Failed("The local image source cannot be empty.");
            if (string.IsNullOrWhiteSpace(options.DestinationDirectory))
                return Failed("The destination directory must be configured by the host.");
            if (string.IsNullOrWhiteSpace(options.MarkdownBaseDirectory))
                return Failed("The Markdown base directory must be configured by the host.");
            if (!System.IO.Path.IsPathFullyQualified(options.DestinationDirectory)
                || !System.IO.Path.IsPathFullyQualified(options.MarkdownBaseDirectory))
            {
                return Failed("Asset destination and Markdown base directories must be fully qualified.");
            }

            var fullSourcePath = System.IO.Path.GetFullPath(sourcePath);
            var sourceInfo = new FileInfo(fullSourcePath);
            if (!sourceInfo.Exists)
                return Failed("The selected image file does not exist.");
            if (sourceInfo.Length > options.MaxFileBytes)
                return Failed($"The selected image exceeds the {options.MaxFileBytes} byte limit.");

            var extension = MarkdownImageAssetOptions.NormalizeExtension(sourceInfo.Extension);
            if (!options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return Failed($"The image extension '{extension}' is not allowed.");
            if (options.ValidateImageSignature
                && !await HasMatchingImageSignatureAsync(fullSourcePath, extension, cancellationToken).ConfigureAwait(false))
            {
                return Failed("The image content does not match its file extension.");
            }

            var destinationDirectory = System.IO.Path.GetFullPath(options.DestinationDirectory);
            var markdownBaseDirectory = System.IO.Path.GetFullPath(options.MarkdownBaseDirectory);
            Directory.CreateDirectory(destinationDirectory);

            var preferredName = string.IsNullOrWhiteSpace(options.PreferredFileName)
                ? sourceInfo.Name
                : options.PreferredFileName;
            var sanitizedName = SanitizeFileName(preferredName!, extension);
            var destinationPath = System.IO.Path.Combine(destinationDirectory, sanitizedName);

            if (File.Exists(destinationPath))
            {
                if (options.CollisionBehavior == MarkdownImageAssetCollisionBehavior.Cancel)
                    return new MarkdownImageAssetResult(MarkdownImageAssetStatus.Cancelled, destinationPath);

                if (options.CollisionBehavior == MarkdownImageAssetCollisionBehavior.UseExisting)
                {
                    var existingValidation = await ValidateExistingAsync(destinationPath, extension, options, cancellationToken)
                        .ConfigureAwait(false);
                    if (existingValidation is not null)
                        return existingValidation;

                    return CreateSuccess(MarkdownImageAssetStatus.UsedExisting, destinationPath, markdownBaseDirectory, options);
                }

                if (options.CollisionBehavior == MarkdownImageAssetCollisionBehavior.GenerateUniqueName)
                    destinationPath = GetUniquePath(destinationPath);
            }

            var pathValidation = TryCreateMarkdownSource(destinationPath, markdownBaseDirectory, options, out _);
            if (pathValidation is not null)
                return pathValidation;

            temporaryPath = System.IO.Path.Combine(destinationDirectory, $".mfn-{Guid.NewGuid():N}.tmp");
            await CopyLimitedAsync(fullSourcePath, temporaryPath, options.MaxFileBytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.CollisionBehavior == MarkdownImageAssetCollisionBehavior.GenerateUniqueName)
            {
                while (true)
                {
                    try
                    {
                        File.Move(temporaryPath, destinationPath, overwrite: false);
                        break;
                    }
                    catch (IOException) when (File.Exists(destinationPath))
                    {
                        destinationPath = GetUniquePath(destinationPath);
                    }
                }
            }
            else
            {
                File.Move(
                    temporaryPath,
                    destinationPath,
                    overwrite: options.CollisionBehavior == MarkdownImageAssetCollisionBehavior.Overwrite);
            }

            temporaryPath = null;
            return CreateSuccess(MarkdownImageAssetStatus.Copied, destinationPath, markdownBaseDirectory, options);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or SecurityException)
        {
            return Failed("The image asset could not be copied.", exception);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The primary operation result is more useful than a cleanup failure. The
                    // hidden, uniquely named temporary file is never returned as a valid asset.
                }
            }
        }
    }

    private static async Task CopyLimitedAsync(
        string sourcePath,
        string destinationPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[CopyBufferSize];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new IOException("The source image grew beyond the configured size limit while copying.");

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static MarkdownImageAssetResult CreateSuccess(
        MarkdownImageAssetStatus status,
        string destinationPath,
        string markdownBaseDirectory,
        MarkdownImageAssetOptions options)
    {
        var failure = TryCreateMarkdownSource(destinationPath, markdownBaseDirectory, options, out var markdownSource);
        return failure ?? new MarkdownImageAssetResult(status, destinationPath, markdownSource);
    }

    private static MarkdownImageAssetResult Failed(string message, Exception? exception = null)
        => new(MarkdownImageAssetStatus.Failed, errorMessage: message, exception: exception);

    private static string GetUniquePath(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var extension = System.IO.Path.GetExtension(path);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = System.IO.Path.Combine(directory, $"{name}-{suffix}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("A unique image asset filename could not be generated.");
    }

    private static async Task<bool> HasMatchingImageSignatureAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        var signature = new byte[12];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            signature.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = 0;
        while (length < signature.Length)
        {
            var read = await stream.ReadAsync(signature.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            length += read;
        }

        return extension switch
        {
            ".png" => length >= 8 && signature.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => length >= 3 && signature[0] == 0xff && signature[1] == 0xd8 && signature[2] == 0xff,
            ".gif" => length >= 6 && (signature.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || signature.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
            ".webp" => length >= 12 && signature.AsSpan(0, 4).SequenceEqual("RIFF"u8) && signature.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".bmp" => length >= 2 && signature[0] == (byte)'B' && signature[1] == (byte)'M',
            _ => false
        };
    }

    private static string SanitizeFileName(string preferredName, string sourceExtension)
    {
        var baseName = System.IO.Path.GetFileNameWithoutExtension(preferredName).Trim().TrimEnd('.');
        if (baseName.Length == 0)
            baseName = "image";

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var portableInvalid = "<>:\"/\\|?*";
        var sanitized = new string(baseName
            .Select(character => invalid.Contains(character) || portableInvalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');
        if (sanitized.Length == 0)
            sanitized = "image";

        // Keep generated asset names portable even when copying on a platform that permits
        // Windows device names. A document project may later be moved between backends.
        if (reservedWindowsFileNames.Contains(sanitized, StringComparer.OrdinalIgnoreCase))
            sanitized += "_";

        return sanitized + sourceExtension;
    }

    private static MarkdownImageAssetResult? TryCreateMarkdownSource(
        string destinationPath,
        string markdownBaseDirectory,
        MarkdownImageAssetOptions options,
        out string markdownSource)
    {
        markdownSource = System.IO.Path.GetRelativePath(markdownBaseDirectory, destinationPath);
        if (System.IO.Path.IsPathFullyQualified(markdownSource) && !options.AllowAbsoluteMarkdownSource)
            return Failed("A relative Markdown path cannot be formed for the selected destination.");

        markdownSource = markdownSource.Replace('\\', '/');
        return null;
    }

    private static async Task<MarkdownImageAssetResult?> ValidateExistingAsync(
        string destinationPath,
        string extension,
        MarkdownImageAssetOptions options,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(destinationPath);
        if (info.Length > options.MaxFileBytes)
            return Failed("The existing image exceeds the configured size limit.");
        if (options.ValidateImageSignature
            && !await HasMatchingImageSignatureAsync(destinationPath, extension, cancellationToken).ConfigureAwait(false))
        {
            return Failed("The existing image content does not match its file extension.");
        }

        return null;
    }
}
