using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ModernFormsNext.Designer.ExecutablePreview;
using FilePath = System.IO.Path;

namespace ModernFormsNext.VisualStudioDesignerHost.ExecutablePreview;

// All copy/PE operations run in the broker's background queue and parse detached bytes only.
internal sealed class PreviewSnapshot : IDisposable
{
    private readonly List<Artifact> artifacts;
    internal string Root { get; }
    internal string WorkerDirectory { get; }
    internal string Scratch { get; }
    internal string Owner => FilePath.GetFileName(Root);
    internal string Digest { get; }
    internal IReadOnlyList<string> Paths => artifacts.Select(artifact => artifact.Source).ToArray();
    internal IEnumerable<string> Summary => artifacts.Select(artifact => $"{artifact.Name}.dll   {artifact.Hash}");

    private PreviewSnapshot(string root, string workerDirectory, string scratch, List<Artifact> artifacts)
    {
        Root = root;
        WorkerDirectory = workerDirectory;
        Scratch = scratch;
        this.artifacts = artifacts;
        Digest = PreviewManifest.GetDigest(artifacts.Select(artifact => (artifact.Name, artifact.Hash)));
    }

    internal static PreviewSnapshot Capture(string projectPath, string worker, IReadOnlyList<string> selected)
    {
        if (selected.Count is < 1 or > PreviewManifest.MaxFiles || !File.Exists(worker))
            throw new InvalidDataException();
        string project = FilePath.GetFullPath(FilePath.GetDirectoryName(projectPath)!);
        if (!File.Exists(projectPath) || new DriveInfo(FilePath.GetPathRoot(project)!).DriveType != DriveType.Fixed)
            throw new InvalidDataException();

        string root = FilePath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MfnPreview." + Guid.NewGuid().ToString("N"));
        using var user = WindowsIdentity.GetCurrent();
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { user.User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(root).Create(acl);
        try
        {
            var runtime = Directory.CreateDirectory(FilePath.Combine(root, "worker")).FullName;
            var scratch = Directory.CreateDirectory(FilePath.Combine(root, "scratch")).FullName;
            var projectFiles = Directory.CreateDirectory(FilePath.Combine(runtime, "artifacts")).FullName;
            var trustedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(FilePath.GetDirectoryName(worker)!))
            {
                if (FilePath.GetExtension(path).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException();
                File.Copy(path, FilePath.Combine(runtime, FilePath.GetFileName(path)));
                if (FilePath.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    trustedAssemblies.Add(FilePath.GetFileNameWithoutExtension(path));
            }
            File.WriteAllText(FilePath.Combine(root, "outside.txt"), "preview outside");
            File.WriteAllText(FilePath.Combine(runtime, "canary.txt"), "preview readonly");
            long remaining = PreviewManifest.MaxTotalBytes;
            var artifacts = new List<Artifact>(selected.Count);
            foreach (string selection in selected)
            {
                string path = FilePath.GetFullPath(selection);
                if (!FilePath.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    !IsWithin(project, path))
                    throw new InvalidDataException();
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!string.Equals(GetLocalFinalPath(source.SafeFileHandle), path, StringComparison.OrdinalIgnoreCase) ||
                    source.Length is <= 0 or > PreviewManifest.MaxFileBytes || (remaining -= source.Length) < 0)
                    throw new InvalidDataException();
                using var reader = new PEReader(source, PEStreamOptions.LeaveOpen);
                var metadata = reader.GetMetadataReader();
                string name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
                if (!PreviewManifest.IsAssemblyName(name) || !trustedAssemblies.Add(name))
                    throw new InvalidDataException();
                source.Position = 0;
                string staged = FilePath.Combine(projectFiles, name + ".dll");
                using (var copy = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    source.CopyTo(copy);
                source.Position = 0;
                artifacts.Add(new Artifact(path, name, staged, Convert.ToHexString(SHA256.HashData(source))));
            }

            artifacts.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            return new PreviewSnapshot(root, runtime, scratch, artifacts);
        }
        catch
        {
            DeleteOwnedDirectory(root);
            throw;
        }
    }

    internal bool HasCurrentSources()
    {
        foreach (Artifact item in artifacts)
        {
            try
            {
                using var file = new FileStream(item.Source, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (file.Length is <= 0 or > PreviewManifest.MaxFileBytes ||
                    !string.Equals(GetLocalFinalPath(file.SafeFileHandle), item.Source, StringComparison.OrdinalIgnoreCase) ||
                    Convert.ToHexString(SHA256.HashData(file)) != item.Hash)
                    return false;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        return true;
    }

    internal string? ResolveMethod(byte index, int token)
    {
        if (index == 0 || index > artifacts.Count || (token >> 24) != 6)
            return null;
        var artifact = artifacts[index - 1];
        using var source = File.OpenRead(artifact.Staged);
        using var reader = new PEReader(source);
        var metadata = reader.GetMetadataReader();
        int row = token & 0x00ffffff;
        if (row == 0 || row > metadata.MethodDefinitions.Count)
            return null;
        var method = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        var definition = metadata.GetTypeDefinition(method.GetDeclaringType());
        string type = metadata.GetString(definition.Name);
        string name = metadata.GetString(method.Name);
        return PreviewWire.IsIdentifier(type) && (PreviewWire.IsIdentifier(name) || name is ".ctor" or ".cctor")
            ? $"{artifact.Name}/{type}.{name}" : null;
    }

    private static bool IsWithin(string root, string path)
        => path.StartsWith(FilePath.TrimEndingDirectorySeparator(root) + FilePath.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string GetLocalFinalPath(SafeFileHandle handle)
    {
        char[] name = new char[32768];
        uint length = GetFinalPathNameByHandle(handle, name, (uint)name.Length, 0);
        const string prefix = @"\\?\";
        if (length is <= 6 or >= 32768 || !name.AsSpan(0, prefix.Length).SequenceEqual(prefix) ||
            name[5] != ':')
            throw new IOException();
        return new string(name, prefix.Length, (int)length - prefix.Length);
    }

    public void Dispose() => DeleteOwnedDirectory(Root);

    private static void DeleteOwnedDirectory(string root)
    {
        var path = new DirectoryInfo(root);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.Equals(path.Parent?.FullName, local, StringComparison.OrdinalIgnoreCase) ||
            !path.Name.StartsWith("MfnPreview.", StringComparison.Ordinal) ||
            !Guid.TryParseExact(path.Name.AsSpan("MfnPreview.".Length), "N", out _) ||
            !path.Exists || (path.Attributes & FileAttributes.ReparsePoint) != 0)
            return;

        // A terminated worker may have left links in scratch. Never traverse them during cleanup.
        var pending = new Stack<(DirectoryInfo Directory, bool Visited)>();
        pending.Push((path, false));
        while (pending.TryPop(out var item))
        {
            if (item.Visited)
            {
                item.Directory.Delete();
                continue;
            }

            pending.Push((item.Directory, true));
            foreach (var entry in item.Directory.EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo child && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                    pending.Push((child, false));
                else
                    entry.Delete();
            }
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, [Out] char[] name, uint size, uint flags);
    private sealed record Artifact(string Source, string Name, string Staged, string Hash);
}
