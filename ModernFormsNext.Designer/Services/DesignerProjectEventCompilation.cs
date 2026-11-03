using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.Designing;
using System.Text;

namespace ModernFormsNext.Designer.Services;

internal static partial class DesignerProjectUserControlDiscovery
{
    // Event analysis reuses the metadata discovery's project identity, exclusions and literal
    // reference resolver. It never invokes MSBuild or loads a project assembly into the runtime.
    internal static CSharpCompilation CreateEventCompilation(SyntaxTree activeTree, string? projectPath, CancellationToken cancellationToken = default)
    {
        var trees = new List<SyntaxTree> { activeTree };
        var remaining = DesignerEventSource.MaximumProjectCharacters - activeTree.Length;
        var directory = GetProjectDirectory(projectPath);
        if (directory is not null)
        {
            var count = 1;
            foreach (var path in EnumerateProjectFiles(directory, "*.cs", maximumEntries: 8192))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(IOPath.GetFullPath(path), IOPath.GetFullPath(activeTree.FilePath), StringComparison.OrdinalIgnoreCase)) continue;
                if (++count > DesignerEventSource.MaximumProjectFiles)
                    throw new InvalidDataException("Event analysis exceeds the 512 source file safety budget. Edit handlers in code.");
                var text = ReadEventSourceText(path);
                remaining -= text.Length;
                if (remaining < 0) throw new InvalidDataException("Event analysis exceeds the 8 Mi character project safety budget.");
                if (!DesignerEventSource.TryParse(text, path, out var tree, out var error, cancellationToken))
                    throw new InvalidDataException(error);
                trees.Add(tree);
            }
        }
        if (directory is not null)
        {
            var projects = File.Exists(projectPath) && IOPath.GetExtension(projectPath) == ".csproj"
                ? new[] { projectPath! } : Directory.EnumerateFiles(directory, "*.csproj").Take(2).ToArray();
            var implicitUsings = projects.Length == 1 ? Property(ReadProjectXml(projects[0]), "ImplicitUsings") : null;
            if (implicitUsings is "enable" or "true")
                trees.Add(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.IO; "
                    + "global using System.Linq; global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks;"));
        }
        var references = new List<MetadataReference>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in (((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(IOPath.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            .Concat([typeof(Control).Assembly.Location, typeof(DesignDocument).Assembly.Location]))
            if (paths.Add(path)) references.Add(MetadataReference.CreateFromFile(path));
        if (paths.Count > 1024) throw new InvalidDataException("Event runtime metadata exceeds the reference identity safety budget.");
        if (directory is not null)
        {
            var diagnostics = new List<string>();
            var referenceBytesRemaining = 64L * 1024 * 1024;
            foreach (var path in ReadReferencePaths(projectPath, directory, diagnostics, sourceEnumerationBudget: 8192))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!paths.Add(path)) continue;
                if (paths.Count > 1024) throw new InvalidDataException("Event metadata reference count exceeds its safety budget.");
                var length = new FileInfo(path).Length;
                if (length > 16 * 1024 * 1024 || (referenceBytesRemaining -= length) < 0)
                    throw new InvalidDataException("Event metadata references exceed their safety budget.");
                // Read bounded data into an immutable image and release the file immediately.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length != length) throw new IOException("Reference changed while being inspected.");
                var bytes = new byte[(int)length];
                stream.ReadExactly(bytes);
                references.Add(MetadataReference.CreateFromImage(bytes, filePath: path));
            }
        }
        return CSharpCompilation.Create("DesignerEventMetadata", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    internal static string ReadEventSourceText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // A byte limit precedes decoding, so a file's declared length cannot allocate arbitrary memory.
        if (stream.Length > DesignerEventSource.MaximumSourceCharacters * 4L)
            throw new InvalidDataException("Event source file exceeds its safety budget.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var buffer = new char[DesignerEventSource.MaximumSourceCharacters + 1];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        if (count > DesignerEventSource.MaximumSourceCharacters)
            throw new InvalidDataException("Event source file exceeds its character safety budget.");
        return new string(buffer, 0, count);
    }

    internal static byte[] ReadEventSourceBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > DesignerEventSource.MaximumSourceCharacters * 4L)
            throw new InvalidDataException("Event source file exceeds its byte safety budget.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
