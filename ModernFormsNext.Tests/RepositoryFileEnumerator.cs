using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ModernFormsNext.Tests;

internal enum RepositoryFileSource
{
    GitTrackedFiles,
    FileSystemFallback
}

internal sealed record RepositoryPathExclusion(string Path, string Reason);

internal sealed record RepositoryFileEnumeration(
    RepositoryFileSource Source,
    IReadOnlyList<string> Files,
    IReadOnlyList<RepositoryPathExclusion> Exclusions)
{
    public string FormatDiagnostics(IEnumerable<string>? expectedFiles = null)
    {
        var builder = new StringBuilder()
            .AppendLine($"Repository input source: {Source}")
            .AppendLine("Included repository-relative files:");

        foreach (string file in Files)
            builder.Append("  + ").AppendLine(file);

        if (expectedFiles is not null)
        {
            builder.AppendLine("Expected repository-relative files:");
            foreach (string file in expectedFiles.Order(StringComparer.Ordinal))
                builder.Append("  = ").AppendLine(file);
        }

        builder.AppendLine("Excluded repository-relative paths:");
        foreach (RepositoryPathExclusion exclusion in Exclusions)
            builder.Append("  - ").Append(exclusion.Path).Append(" (").Append(exclusion.Reason).AppendLine(")");

        return builder.ToString();
    }
}

/// <summary>
/// Enumerates repository-owned files without treating generated output or nested checkouts as source.
/// </summary>
/// <remarks>
/// Git-tracked files are authoritative when repository metadata and Git are available. Source archives
/// use a filesystem fallback that prunes generated directories, nested Git roots, and reparse points.
/// All returned paths and diagnostics are repository-relative and use forward slashes.
/// </remarks>
internal static class RepositoryFileEnumerator
{
    private static readonly HashSet<string> GeneratedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".codex",
        ".nuget",
        ".cache",
        "artifacts",
        "bin",
        "obj",
        "TestResult",
        "TestResults",
        "packages",
        "node_modules",
        "BenchmarkDotNet.Artifacts",
        "Generated Files",
        "GeneratedArtifacts",
        "AppPackages",
        "BundleArtifacts",
        ".codex-build",
        ".codex-pack",
        ".codex-pack-api",
        "_site"
    };

    internal static string FindRepositoryRoot(string? startPath = null)
    {
        string candidate = IOPath.GetFullPath(startPath ?? AppContext.BaseDirectory);
        if (File.Exists(candidate))
            candidate = IOPath.GetDirectoryName(candidate)
                ?? throw new DirectoryNotFoundException($"Could not resolve a directory from '{startPath}'.");

        for (DirectoryInfo? directory = new(candidate); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(IOPath.Combine(directory.FullName, "Directory.Build.props"))
                && File.Exists(IOPath.Combine(directory.FullName, "ModernFormsNext.slnx")))
            {
                return IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(directory.FullName));
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the ModernFormsNext repository root from the supplied start path.");
    }

    internal static RepositoryFileEnumeration EnumerateFiles(
        string repositoryRoot,
        Func<string, bool> includeFile)
        => EnumerateFiles(repositoryRoot, includeFile, TryEnumerateGitTrackedFiles);

    internal static RepositoryFileEnumeration EnumerateFiles(
        string repositoryRoot,
        Func<string, bool> includeFile,
        Func<string, IReadOnlyList<string>?> trackedFileProvider)
    {
        ArgumentNullException.ThrowIfNull(includeFile);
        ArgumentNullException.ThrowIfNull(trackedFileProvider);

        string root = NormalizeRoot(repositoryRoot);
        IReadOnlyList<string>? trackedFiles = trackedFileProvider(root);
        return trackedFiles is null
            ? EnumerateFileSystem(root, includeFile)
            : EnumerateTrackedFiles(root, includeFile, trackedFiles);
    }

    internal static bool ContainsGeneratedDirectorySegment(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return NormalizeSeparators(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(GeneratedDirectoryNames.Contains);
    }

    internal static bool IsReparsePoint(FileAttributes attributes)
        => (attributes & FileAttributes.ReparsePoint) != 0;

    private static RepositoryFileEnumeration EnumerateTrackedFiles(
        string root,
        Func<string, bool> includeFile,
        IReadOnlyList<string> trackedFiles)
    {
        var files = new List<string>();
        var exclusions = new List<RepositoryPathExclusion>();

        foreach (string candidate in trackedFiles)
        {
            string relative = NormalizeRepositoryRelativePath(root, candidate);
            if (!includeFile(relative))
                continue;

            if (ContainsGeneratedDirectorySegment(relative))
            {
                exclusions.Add(new(relative, "generated directory segment"));
                continue;
            }

            string fullPath = IOPath.GetFullPath(IOPath.Combine(root, relative.Replace('/', IOPath.DirectorySeparatorChar)));
            if (ContainsExistingReparsePoint(root, fullPath))
            {
                exclusions.Add(new(relative, "reparse point"));
                continue;
            }

            files.Add(relative);
        }

        return CreateResult(RepositoryFileSource.GitTrackedFiles, files, exclusions);
    }

    private static RepositoryFileEnumeration EnumerateFileSystem(
        string root,
        Func<string, bool> includeFile)
    {
        var files = new List<string>();
        var exclusions = new List<RepositoryPathExclusion>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            string[] childFiles;
            string[] childDirectories;

            try
            {
                childFiles = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                childDirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                exclusions.Add(new(ToRelativePath(root, directory), exception.GetType().Name));
                continue;
            }

            foreach (string file in childFiles.Order(StringComparer.Ordinal))
            {
                string relative = ToRelativePath(root, file);
                if (!includeFile(relative))
                    continue;

                if (IsExistingReparsePoint(file))
                {
                    exclusions.Add(new(relative, "reparse point"));
                    continue;
                }

                files.Add(relative);
            }

            foreach (string childDirectory in childDirectories.OrderDescending(StringComparer.Ordinal))
            {
                string relative = ToRelativePath(root, childDirectory);
                string name = IOPath.GetFileName(childDirectory);

                if (GeneratedDirectoryNames.Contains(name))
                {
                    exclusions.Add(new(relative, "generated directory"));
                    continue;
                }

                if (IsExistingReparsePoint(childDirectory))
                {
                    exclusions.Add(new(relative, "reparse point"));
                    continue;
                }

                if (File.Exists(IOPath.Combine(childDirectory, ".git"))
                    || Directory.Exists(IOPath.Combine(childDirectory, ".git")))
                {
                    exclusions.Add(new(relative, "nested Git worktree/repository"));
                    continue;
                }

                pending.Push(childDirectory);
            }
        }

        return CreateResult(RepositoryFileSource.FileSystemFallback, files, exclusions);
    }

    private static RepositoryFileEnumeration CreateResult(
        RepositoryFileSource source,
        List<string> files,
        List<RepositoryPathExclusion> exclusions)
    {
        files.Sort(StringComparer.Ordinal);
        exclusions.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return new(source, files, exclusions);
    }

    private static IReadOnlyList<string>? TryEnumerateGitTrackedFiles(string root)
    {
        string gitEntry = IOPath.Combine(root, ".git");
        if (!File.Exists(gitEntry) && !Directory.Exists(gitEntry))
            return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            process.StartInfo.ArgumentList.Add("-C");
            process.StartInfo.ArgumentList.Add(root);
            process.StartInfo.ArgumentList.Add("ls-files");
            process.StartInfo.ArgumentList.Add("-z");
            process.StartInfo.ArgumentList.Add("--cached");
            process.StartInfo.ArgumentList.Add("--");

            if (!process.Start())
                return null;

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(milliseconds: 10_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Task.WaitAll(standardOutput, standardError);
                return null;
            }

            Task.WaitAll(standardOutput, standardError);
            if (process.ExitCode != 0)
                return null;

            return standardOutput.Result.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string NormalizeRoot(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string root = IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(repositoryRoot));
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Repository root does not exist.");

        return root;
    }

    private static string NormalizeRepositoryRelativePath(string root, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        string normalized = NormalizeSeparators(candidate);
        bool hasDriveRoot = normalized.Length >= 3
            && char.IsAsciiLetter(normalized[0])
            && normalized[1] == ':'
            && normalized[2] == '/';
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || hasDriveRoot
            || IOPath.IsPathRooted(candidate)
            || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"Repository traversal returned an unsafe relative path: '{candidate}'.");
        }

        string fullPath = IOPath.GetFullPath(IOPath.Combine(root, normalized.Replace('/', IOPath.DirectorySeparatorChar)));
        if (!IsWithinRoot(root, fullPath))
            throw new InvalidDataException($"Repository traversal escaped the repository root: '{candidate}'.");

        return NormalizeSeparators(IOPath.GetRelativePath(root, fullPath));
    }

    private static string ToRelativePath(string root, string path)
    {
        string fullPath = IOPath.GetFullPath(path);
        if (!IsWithinRoot(root, fullPath))
            throw new InvalidDataException("Filesystem traversal escaped the repository root.");

        return NormalizeSeparators(IOPath.GetRelativePath(root, fullPath));
    }

    private static bool IsWithinRoot(string root, string path)
    {
        string rootPrefix = IOPath.EndsInDirectorySeparator(root)
            ? root
            : root + IOPath.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.StartsWith(rootPrefix, comparison);
    }

    private static bool ContainsExistingReparsePoint(string root, string filePath)
    {
        string? directory = IOPath.GetDirectoryName(filePath);
        while (!string.IsNullOrWhiteSpace(directory) && !string.Equals(directory, root, PathComparison))
        {
            if (IsExistingReparsePoint(directory))
                return true;

            directory = IOPath.GetDirectoryName(directory);
        }

        return IsExistingReparsePoint(filePath);
    }

    private static bool IsExistingReparsePoint(string path)
    {
        try
        {
            return (File.Exists(path) || Directory.Exists(path)) && IsReparsePoint(File.GetAttributes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');
}
