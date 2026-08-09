using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Rejects direct and project-local cyclic design-root references before code is generated.
/// </summary>
internal static class DesignerControlReferenceGuard
{
    public static bool CanReference(
        DesignDocument document,
        string typeName,
        string? projectPath,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (MatchesRoot(document, typeName))
        {
            error = $"Cannot add {typeName}: a design root cannot contain itself.";
            return false;
        }

        var projectDirectory = DesignerProjectUserControlDiscovery.GetProjectDirectory(projectPath);

        if (projectDirectory is null)
        {
            error = null;
            return true;
        }

        var documents = LoadProjectDocuments(projectDirectory, document);
        var target = FindDocument(documents, typeName, document.Namespace);

        if (target is not null && ReferencesRoot(target, document, documents, new HashSet<string>(StringComparer.Ordinal)))
        {
            error = $"Cannot add {typeName}: the project design documents would contain a cyclic UserControl reference.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool CanReferenceTree(
        DesignDocument document,
        DesignControlNode node,
        string? projectPath,
        out string? error)
    {
        if (!CanReference(document, node.TypeName, projectPath, out error))
            return false;

        foreach (var child in node.Children)
        {
            if (!CanReferenceTree(document, child, projectPath, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static IReadOnlyList<DesignDocument> LoadProjectDocuments(string projectDirectory, DesignDocument activeDocument)
    {
        var documents = new Dictionary<string, DesignDocument>(StringComparer.Ordinal)
        {
            [GetRootIdentity(activeDocument)] = activeDocument
        };

        try
        {
            foreach (var path in DesignerProjectUserControlDiscovery.EnumerateProjectFiles(
                projectDirectory,
                "*.mfdesign"))
            {
                try
                {
                    var loaded = DesignDocumentSerializer.Default.Load(path);

                    if (string.IsNullOrWhiteSpace(loaded.ClassName) || loaded.Controls is null)
                        continue;

                    documents.TryAdd(GetRootIdentity(loaded), loaded);
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or System.Text.Json.JsonException
                    or ArgumentException
                    or InvalidOperationException
                    or NotSupportedException)
                {
                    // A broken unrelated document must not make the active designer unusable.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return documents.Values.ToArray();
        }

        return documents.Values.ToArray();
    }

    private static bool ReferencesRoot(
        DesignDocument source,
        DesignDocument target,
        IReadOnlyList<DesignDocument> documents,
        HashSet<string> visited)
    {
        var identity = GetRootIdentity(source);

        if (!visited.Add(identity))
            return false;

        foreach (var typeName in EnumerateTypeNames(source.Controls))
        {
            if (MatchesRoot(target, typeName))
                return true;

            var dependency = FindDocument(documents, typeName, source.Namespace);

            if (dependency is not null && ReferencesRoot(dependency, target, documents, visited))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateTypeNames(IEnumerable<DesignControlNode> controls)
    {
        foreach (var control in controls)
        {
            if (control is null || string.IsNullOrWhiteSpace(control.TypeName))
                continue;

            yield return control.TypeName;

            if (control.Children is not null)
            {
                foreach (var childTypeName in EnumerateTypeNames(control.Children))
                    yield return childTypeName;
            }
        }
    }

    private static DesignDocument? FindDocument(
        IEnumerable<DesignDocument> documents,
        string typeName,
        string? sourceNamespace)
    {
        var candidates = documents.ToArray();
        var normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        var exact = candidates.FirstOrDefault(document =>
            string.Equals(GetRootIdentity(document), normalized, StringComparison.Ordinal));

        if (exact is not null)
            return exact;

        if (!string.IsNullOrWhiteSpace(sourceNamespace))
        {
            var relativeName = $"{sourceNamespace}.{normalized}";
            var relative = candidates.FirstOrDefault(document =>
                string.Equals(GetRootIdentity(document), relativeName, StringComparison.Ordinal));

            if (relative is not null)
                return relative;
        }

        var shortMatches = candidates
            .Where(document => string.Equals(document.ClassName, normalized, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return shortMatches.Length == 1 ? shortMatches[0] : null;
    }

    private static bool MatchesRoot(DesignDocument document, string typeName)
    {
        var normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        return string.Equals(normalized, document.ClassName, StringComparison.Ordinal)
            || string.Equals(normalized, GetRootIdentity(document), StringComparison.Ordinal);
    }

    private static string GetRootIdentity(DesignDocument document)
        => string.IsNullOrWhiteSpace(document.Namespace)
            ? document.ClassName ?? string.Empty
            : $"{document.Namespace}.{document.ClassName}";
}
