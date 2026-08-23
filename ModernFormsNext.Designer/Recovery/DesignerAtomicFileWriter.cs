using System.Text;

namespace ModernFormsNext.Designer.Recovery;

/// <summary>
/// Writes a complete UTF-8 file through a same-directory temporary file and atomic commit.
/// </summary>
internal static class DesignerAtomicFileWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteUtf8(string destinationPath, string content)
        => WriteUtf8(destinationPath, content, DesignerAtomicFileCommitter.Instance);

    internal static void WriteUtf8(
        string destinationPath,
        string content,
        IDesignerAtomicFileCommitter committer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(committer);

        var fullDestinationPath = IOPath.GetFullPath(destinationPath);
        var directory = IOPath.GetDirectoryName(fullDestinationPath)
            ?? throw new ArgumentException("The destination must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        var temporaryPath = IOPath.Combine(
            directory,
            $".mfn-recovery-{IOPath.GetFileName(fullDestinationPath)}-{Guid.NewGuid():N}.tmp");

        try
        {
            var bytes = Utf8WithoutBom.GetBytes(content);
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                // Flush the file contents before making the completed temporary file visible as
                // the destination. A commit failure leaves the previous destination untouched.
                stream.Flush(flushToDisk: true);
            }

            committer.Commit(temporaryPath, fullDestinationPath);
            temporaryPath = string.Empty;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The original write/commit failure is more actionable than temp cleanup.
                }
            }
        }
    }
}

internal interface IDesignerAtomicFileCommitter
{
    void Commit(string temporaryPath, string destinationPath);
}

internal sealed class DesignerAtomicFileCommitter : IDesignerAtomicFileCommitter
{
    public static DesignerAtomicFileCommitter Instance { get; } = new();

    private DesignerAtomicFileCommitter()
    {
    }

    public void Commit(string temporaryPath, string destinationPath)
    {
        // File.Replace supplies the strongest replacement contract on the primary Windows target.
        // File.Move handles first publication without an observable partially written destination.
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(temporaryPath, destinationPath, overwrite: false);
    }
}
