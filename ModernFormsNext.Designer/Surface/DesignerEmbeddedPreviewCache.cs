using System.Text.Json;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Surface;

/// <summary>
/// Resolves and caches read-only design-time projections for project-owned UserControls.
/// </summary>
/// <remarks>
/// The cache reads only <c>.mfdesign</c> files. It never loads the project assembly or creates a
/// project control instance. A fresh document clone is materialized when the requested instance
/// size changes, leaving the source document used for identity matching untouched.
/// </remarks>
internal sealed class DesignerEmbeddedPreviewCache
{
    private readonly IReadOnlyList<DesignerProjectUserControlInfo> projectUserControls;
    private readonly string? projectDirectory;
    private readonly Dictionary<string, string> documentPathsByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedPreviewSource> sourcesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly DesignerLayoutEngine layoutEngine = new();
    private bool documentIndexBuilt;

    /// <summary>
    /// Initializes a preview cache for one project discovery snapshot.
    /// </summary>
    /// <param name="projectPath">The active project file or directory path.</param>
    /// <param name="projectUserControls">
    /// Source-discovered controls that may be associated with design documents.
    /// </param>
    public DesignerEmbeddedPreviewCache(
        string? projectPath,
        IReadOnlyList<DesignerProjectUserControlInfo> projectUserControls)
    {
        ArgumentNullException.ThrowIfNull(projectUserControls);

        projectDirectory = DesignerProjectUserControlDiscovery.GetProjectDirectory(projectPath);
        this.projectUserControls = projectUserControls;
    }

    /// <summary>
    /// Gets the number of JSON deserializations performed by this cache.
    /// </summary>
    /// <remarks>
    /// This diagnostic verifies that repeated paint frames reuse the same parsed and laid-out
    /// preview instead of reparsing its file.
    /// </remarks>
    internal int ParseCount { get; private set; }

    /// <summary>
    /// Tries to obtain a private visual projection for one custom UserControl instance.
    /// </summary>
    /// <param name="typeName">The short, qualified, or assembly-qualified persisted type name.</param>
    /// <param name="instanceSize">The current size of the instance in designer logical pixels.</param>
    /// <param name="preview">The cached projection when resolution and validation succeed.</param>
    /// <param name="error">A diagnostic suitable for the designer output when fallback is required.</param>
    /// <returns><see langword="true"/> when a safe projection is available.</returns>
    public bool TryGetPreview(
        string typeName,
        DesignSize instanceSize,
        out DesignerEmbeddedPreview? preview,
        out string? error)
    {
        preview = null;

        if (!TryResolveControl(typeName, out var control, out error))
            return false;

        EnsureDocumentIndex();

        if (!documentPathsByType.TryGetValue(control.FullName, out var documentPath))
        {
            error = $"No .mfdesign document was found for custom UserControl '{control.FullName}'.";
            return false;
        }

        var source = GetOrRefreshSource(documentPath);

        if (source.Document is null || source.Json is null)
        {
            error = source.Error
                ?? $"The .mfdesign document for custom UserControl '{control.FullName}' could not be loaded.";
            return false;
        }

        var documentIdentity = GetDocumentIdentity(source.Document);

        if (!string.Equals(documentIdentity, control.FullName, StringComparison.Ordinal))
        {
            error = $"The custom UserControl preview document '{documentPath}' declares " +
                $"'{documentIdentity}' instead of '{control.FullName}'.";
            return false;
        }

        if (source.Document.RootKind != DesignRootKind.UserControl)
        {
            error = $"The custom UserControl preview document '{documentPath}' does not declare a UserControl root.";
            return false;
        }

        var normalizedSize = new DesignSize(
            Math.Max(1, instanceSize.Width),
            Math.Max(1, instanceSize.Height));

        if (source.Previews.TryGetValue(normalizedSize, out var cachedPreview))
        {
            preview = cachedPreview;
            error = null;
            return true;
        }

        try
        {
            // Deserialize the cached source text into a private materialization. Layout helpers may
            // normalize structural nodes, but the identity document and file contents stay read-only.
            var document = Deserialize(source.Json);
            NormalizePreviewDocument(document);
            DesignerSpecialContainers.NormalizeDocument(document);
            var layout = layoutEngine.Layout(document, normalizedSize);
            preview = new DesignerEmbeddedPreview(
                control.FullName,
                documentPath,
                normalizedSize,
                document,
                layout);
            source.Previews[normalizedSize] = preview;
            error = null;
            return true;
        }
        catch (Exception exception) when (IsRecoverablePreviewException(exception))
        {
            source.Previews.Remove(normalizedSize);
            error = $"Could not materialize preview for '{control.FullName}' from '{documentPath}': {exception.Message}";
            return false;
        }
    }

    private bool TryResolveControl(
        string typeName,
        out DesignerProjectUserControlInfo control,
        out string? error)
    {
        var matches = projectUserControls
            .Where(candidate => DesignerProjectUserControlDiscovery.Matches(candidate, typeName))
            .Take(2)
            .ToArray();

        if (matches.Length == 1)
        {
            control = matches[0];
            error = null;
            return true;
        }

        control = null!;
        error = matches.Length == 0
            ? $"Custom UserControl type '{typeName}' is not present in project discovery."
            : $"Custom UserControl type name '{typeName}' is ambiguous in project discovery.";
        return false;
    }

    private void EnsureDocumentIndex()
    {
        if (documentIndexBuilt)
            return;

        documentIndexBuilt = true;

        foreach (var sourceGroup in projectUserControls.GroupBy(
            control => Path.GetFullPath(control.SourceFilePath),
            StringComparer.OrdinalIgnoreCase))
        {
            var controls = sourceGroup.ToArray();
            var siblingPath = Path.ChangeExtension(sourceGroup.Key, ".mfdesign");

            // A sibling path is the strongest source-level association and also lets the caller
            // report malformed or stale identity data precisely. TryGetPreview still validates the
            // document identity and root kind before exposing a projection.
            if (controls.Length == 1 && File.Exists(siblingPath))
                documentPathsByType[controls[0].FullName] = Path.GetFullPath(siblingPath);
        }

        var unresolvedControls = projectUserControls
            .Where(control => !documentPathsByType.ContainsKey(control.FullName))
            .ToArray();

        if (unresolvedControls.Length == 0 || projectDirectory is null)
            return;

        var pathsByIdentity = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var path in DesignerProjectUserControlDiscovery.EnumerateProjectFiles(projectDirectory, "*.mfdesign"))
        {
            var source = GetOrRefreshSource(path);

            if (source.Document is null || string.IsNullOrWhiteSpace(source.Document.ClassName))
                continue;

            var identity = GetDocumentIdentity(source.Document);
            if (!pathsByIdentity.TryGetValue(identity, out var identityPaths))
            {
                identityPaths = [];
                pathsByIdentity.Add(identity, identityPaths);
            }

            if (!identityPaths.Contains(source.Path, StringComparer.OrdinalIgnoreCase))
                identityPaths.Add(source.Path);
        }

        foreach (var control in unresolvedControls)
        {
            // Never guess by short class name: identical names in different namespaces are common,
            // and a stale namespace/class rename must degrade to a placeholder instead of rendering
            // the wrong component. Ambiguous duplicate identities are rejected for the same reason.
            if (pathsByIdentity.TryGetValue(control.FullName, out var exactPaths) && exactPaths.Count == 1)
                documentPathsByType[control.FullName] = exactPaths[0];
        }
    }

    private CachedPreviewSource GetOrRefreshSource(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        sourcesByPath.TryGetValue(normalizedPath, out var cached);
        var observedLastWriteTimeUtc = default(DateTime);
        var observedLength = -1L;

        try
        {
            var file = new FileInfo(normalizedPath);
            file.Refresh();

            if (!file.Exists)
            {
                var missing = new CachedPreviewSource(normalizedPath)
                {
                    Error = $"The custom UserControl preview document is missing: '{normalizedPath}'."
                };
                sourcesByPath[normalizedPath] = missing;
                return missing;
            }

            observedLastWriteTimeUtc = file.LastWriteTimeUtc;
            observedLength = file.Length;

            if (cached is not null
                && cached.LastWriteTimeUtc == observedLastWriteTimeUtc
                && cached.Length == observedLength)
            {
                return cached;
            }

            var json = File.ReadAllText(normalizedPath);
            var document = Deserialize(json);
            var refreshed = new CachedPreviewSource(normalizedPath)
            {
                LastWriteTimeUtc = observedLastWriteTimeUtc,
                Length = observedLength,
                Json = json,
                Document = document
            };
            sourcesByPath[normalizedPath] = refreshed;
            return refreshed;
        }
        catch (Exception exception) when (IsRecoverablePreviewException(exception))
        {
            var invalid = new CachedPreviewSource(normalizedPath)
            {
                LastWriteTimeUtc = observedLastWriteTimeUtc,
                Length = observedLength,
                Error = $"Could not load custom UserControl preview document '{normalizedPath}': {exception.Message}"
            };
            sourcesByPath[normalizedPath] = invalid;
            return invalid;
        }
    }

    private DesignDocument Deserialize(string json)
    {
        ParseCount++;
        return DesignDocumentSerializer.Default.Deserialize(json);
    }

    private static void NormalizePreviewDocument(DesignDocument document)
    {
        document.Properties ??= new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        document.Events ??= new SortedDictionary<string, string?>(StringComparer.Ordinal);
        document.Controls ??= [];

        foreach (var node in document.Controls)
            NormalizePreviewNode(node);
    }

    private static void NormalizePreviewNode(DesignControlNode node)
    {
        if (node is null)
            throw new JsonException("A custom UserControl preview contains a null control node.");

        node.TypeName ??= string.Empty;
        node.Name ??= string.Empty;
        node.Properties ??= new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal);
        node.Events ??= new SortedDictionary<string, string?>(StringComparer.Ordinal);
        node.Children ??= [];

        foreach (var child in node.Children)
            NormalizePreviewNode(child);
    }

    private static bool IsRecoverablePreviewException(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException;

    private static string GetDocumentIdentity(DesignDocument document)
        => string.IsNullOrWhiteSpace(document.Namespace)
            ? document.ClassName
            : $"{document.Namespace}.{document.ClassName}";

    private sealed class CachedPreviewSource(string path)
    {
        public string Path { get; } = path;

        public DateTime LastWriteTimeUtc { get; init; }

        public long Length { get; init; }

        public string? Json { get; init; }

        public DesignDocument? Document { get; init; }

        public string? Error { get; init; }

        public Dictionary<DesignSize, DesignerEmbeddedPreview> Previews { get; } = [];
    }
}

/// <summary>
/// Contains one cached, read-only visual projection of a custom UserControl design document.
/// </summary>
internal sealed record DesignerEmbeddedPreview(
    string TypeName,
    string DocumentPath,
    DesignSize InstanceSize,
    DesignDocument Document,
    DesignerLayoutResult Layout);
