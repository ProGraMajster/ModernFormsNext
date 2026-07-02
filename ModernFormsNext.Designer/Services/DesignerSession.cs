using ModernFormsNext;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Holds the active designer document, selection, output log, pointer state, and dirty state.
/// </summary>
/// <remarks>
/// A session is UI-framework neutral within the designer library: panels, surfaces, and host
/// adapters all observe the same session instance instead of maintaining their own copies of
/// document state.
/// </remarks>
public sealed class DesignerSession
{
    private readonly List<string> outputLines = [];
    private readonly List<DesignerOpenDocument> openDocuments = [];
    private readonly IDesignerHostEnvironment? environment;
    private DesignerOpenDocument? activeDocument;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerSession"/> class.
    /// </summary>
    /// <param name="environment">Optional host environment used for status and output routing.</param>
    /// <param name="initialRenderMode">The initial designer surface render mode.</param>
    public DesignerSession(
        IDesignerHostEnvironment? environment = null,
        DesignerControlRenderMode initialRenderMode = DesignerControlRenderMode.Runtime)
    {
        this.environment = environment;
        ControlRenderMode = initialRenderMode;
        Host = new DesignerHost(CreateDefaultDocument());
        Host.Selection.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Log("Designer session ready.");
        Log($"Designer diagnostics log: {DesignerDiagnosticLog.Path}");
        Log($"Initial designer surface render mode: {ControlRenderMode}.");
    }

    /// <summary>
    /// Occurs when the active document changes or a document property is modified.
    /// </summary>
    public event EventHandler? DocumentChanged;

    /// <summary>
    /// Occurs when the active designer selection changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Occurs when the output log changes.
    /// </summary>
    public event EventHandler? OutputChanged;

    /// <summary>
    /// Occurs when the pointer position over the designer surface changes.
    /// </summary>
    public event EventHandler? PointerPositionChanged;

    /// <summary>
    /// Occurs when designer settings that affect UI presentation change.
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Occurs when the set of open designer documents or the active document changes.
    /// </summary>
    public event EventHandler? DocumentTabsChanged;

    /// <summary>
    /// Gets the neutral designer host that owns the document and selection service.
    /// </summary>
    public DesignerHost Host { get; }

    /// <summary>
    /// Gets the active design document.
    /// </summary>
    public DesignDocument Document => Host.Document;

    /// <summary>
    /// Gets the currently selected control node, or <see langword="null"/> when the form itself is selected.
    /// </summary>
    public DesignControlNode? SelectedNode => Host.Selection.SelectedNode;

    /// <summary>
    /// Gets the last known pointer position in document coordinates.
    /// </summary>
    public DesignPoint? PointerPosition { get; private set; }

    /// <summary>
    /// Gets the rolling output lines emitted by the designer session.
    /// </summary>
    public IReadOnlyList<string> OutputLines => outputLines;

    /// <summary>
    /// Gets a value indicating whether the document has unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets the current control rendering mode used by the designer surface.
    /// </summary>
    public DesignerControlRenderMode ControlRenderMode { get; private set; }

    /// <summary>
    /// Gets the active document path supplied by the host environment, if one is known.
    /// </summary>
    public string? CurrentDocumentPath => activeDocument?.Path ?? environment?.CurrentDocumentPath;

    /// <summary>
    /// Gets the active project path supplied by the host environment, if one is known.
    /// </summary>
    public string? CurrentProjectPath => environment?.CurrentProjectPath;

    internal IReadOnlyList<DesignerOpenDocument> OpenDocuments => openDocuments;

    internal int ActiveDocumentIndex => activeDocument is null ? -1 : openDocuments.IndexOf(activeDocument);

    /// <summary>
    /// Replaces the active document and resets the dirty state.
    /// </summary>
    /// <param name="document">The document to load into the session.</param>
    public void LoadDocument(DesignDocument document)
        => OpenDocument(document, environment?.CurrentDocumentPath, markDirty: false);

    /// <summary>
    /// Opens a design document in the shell document tab area.
    /// </summary>
    /// <param name="document">The document to open.</param>
    /// <param name="path">The file path for the document, or <see langword="null"/> for an unsaved document.</param>
    public void OpenDocument(DesignDocument document, string? path)
        => OpenDocument(document, path, markDirty: false);

    internal void OpenDocument(DesignDocument document, string? path, bool markDirty)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalizedPath = DesignerDocumentPath.NormalizeDesignPath(path);
        var existing = !string.IsNullOrWhiteSpace(normalizedPath)
            ? openDocuments.FirstOrDefault(tab => string.Equals(tab.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
            : null;

        if (existing is null)
        {
            existing = new DesignerOpenDocument(document, normalizedPath)
            {
                IsDirty = markDirty
            };
            openDocuments.Add(existing);
        }
        else
        {
            activeDocument = existing;
            Host.LoadDocument(existing.Document);
            IsDirty = existing.IsDirty;
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
            Log($"Activated {existing.DisplayName}.");
            return;
        }

        activeDocument = existing;
        Host.LoadDocument(document);
        IsDirty = existing.IsDirty;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        Log($"Loaded {document.ClassName}.mfdesign.");
    }

    internal void SwitchDocument(int index)
    {
        if (index < 0 || index >= openDocuments.Count)
            return;

        if (ReferenceEquals(activeDocument, openDocuments[index]))
            return;

        activeDocument = openDocuments[index];
        Host.LoadDocument(activeDocument.Document);
        IsDirty = activeDocument.IsDirty;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        Log($"Activated {activeDocument.DisplayName}.");
    }

    /// <summary>
    /// Marks the current document as saved.
    /// </summary>
    /// <param name="statusMessage">The status message reported to the host environment.</param>
    public void MarkSaved(string statusMessage = "Document saved.")
    {
        IsDirty = false;
        if (activeDocument is not null)
            activeDocument.IsDirty = false;

        DocumentChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus(statusMessage);
    }

    /// <summary>
    /// Creates a new default document for interactive designer testing.
    /// </summary>
    public void NewDocument()
    {
        var document = CreateDefaultDocument();
        document.ClassName = openDocuments.Count == 0 ? "MainForm" : $"MainForm{openDocuments.Count + 1}";
        document.FormName = openDocuments.Count == 0 ? "Form1" : $"Form{openDocuments.Count + 1}";
        OpenDocument(document, path: null, markDirty: true);
        NotifyDocumentChanged();
        Log($"Created new document {document.ClassName}.mfdesign.");
    }

    /// <summary>
    /// Selects the form root by clearing the selected control node.
    /// </summary>
    public void SelectForm()
    {
        Host.Selection.Clear();
        Log($"Selected {Document.FormName}.");
    }

    /// <summary>
    /// Selects the specified control node.
    /// </summary>
    /// <param name="node">The node to select, or <see langword="null"/> to select the form.</param>
    public void SelectNode(DesignControlNode? node)
    {
        if (node is null)
        {
            SelectForm();
            return;
        }

        Host.Selection.Select(node);
        Log($"Selected {node.Name}.");
    }

    /// <summary>
    /// Selects the deepest control at the specified document position.
    /// </summary>
    /// <param name="x">The document X coordinate in logical pixels.</param>
    /// <param name="y">The document Y coordinate in logical pixels.</param>
    public void SelectAt(int x, int y)
    {
        var result = Host.SelectAt(x, y);
        Log(result.Node is null ? $"Selected {Document.FormName}." : $"Selected {result.Node.Name}.");
    }

    /// <summary>
    /// Changes how controls are drawn on the designer surface.
    /// </summary>
    /// <param name="renderMode">The render mode to use.</param>
    public void SetControlRenderMode(DesignerControlRenderMode renderMode)
    {
        if (ControlRenderMode == renderMode)
        {
            Log($"Designer surface render mode already set to {renderMode}.");
            return;
        }

        ControlRenderMode = renderMode;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        Log(renderMode == DesignerControlRenderMode.Runtime
            ? "Designer surface uses runtime control rendering."
            : "Designer surface uses placeholder control rendering.");
    }

    /// <summary>
    /// Notifies designer UI components that shell-level presentation settings changed.
    /// </summary>
    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the pointer position reported by the designer surface.
    /// </summary>
    /// <param name="point">The pointer position, or <see langword="null"/> when the pointer is outside the document.</param>
    public void SetPointerPosition(DesignPoint? point)
    {
        if (PointerPosition == point)
            return;

        PointerPosition = point;
        PointerPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a control of the specified ModernFormsNext type to the selected container or form.
    /// </summary>
    /// <param name="typeName">The short or fully qualified control type name.</param>
    /// <returns>The created design control node.</returns>
    public DesignControlNode AddControl(string typeName)
    {
        var parent = IsContainerNode(SelectedNode) ? SelectedNode : null;
        var index = CountControls(typeName) + 1;
        var namePrefix = char.ToLowerInvariant(typeName[0]) + typeName[1..];
        var node = new DesignControlNode
        {
            TypeName = typeName,
            Name = $"{namePrefix}{index}",
            Bounds = GetDefaultBounds(typeName, parent, index),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                ["Text"] = DesignPropertyValue.FromString(GetDefaultText(typeName, index))
            }
        };

        if (typeName == "Panel")
            node.Properties["Text"] = DesignPropertyValue.FromString(string.Empty);

        if (parent is null)
            Document.Controls.Add(node);
        else
            parent.Children.Add(node);

        Host.Selection.Select(node);
        NotifyDocumentChanged();
        Log($"Added {typeName} {node.Name}.");
        return node;
    }

    /// <summary>
    /// Moves a node to a target chosen in the document outline.
    /// </summary>
    /// <param name="node">The node being moved.</param>
    /// <param name="target">The target row, or <see langword="null"/> to move the node to the form root.</param>
    /// <returns><see langword="true"/> when the node was moved; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Container targets receive the node as a child. Non-container targets receive the node
    /// as the next sibling. Bounds remain local to the new parent so the model hierarchy stays
    /// explicit and serialization/code generation use the same structure as the outline.
    /// </remarks>
    public bool MoveNodeToOutlineTarget(DesignControlNode node, DesignControlNode? target)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (ReferenceEquals(node, target))
            return false;

        if (target is not null && ContainsDescendant(node, target))
        {
            Log($"Cannot move {node.Name} into one of its own children.");
            return false;
        }

        if (!TryFindNode(node, out var sourceCollection, out var sourceIndex))
        {
            Log($"Cannot move {node.Name}: the node is not in the active document.");
            return false;
        }

        DesignControlCollection destinationCollection;
        int destinationIndex;
        string targetName;

        if (target is null)
        {
            destinationCollection = Document.Controls;
            destinationIndex = destinationCollection.Count;
            targetName = Document.FormName;
        }
        else if (IsContainerNode(target))
        {
            destinationCollection = target.Children;
            destinationIndex = destinationCollection.Count;
            targetName = target.Name;
        }
        else if (TryFindNode(target, out var targetCollection, out var targetIndex))
        {
            destinationCollection = targetCollection;
            destinationIndex = targetIndex + 1;
            targetName = target.Name;
        }
        else
        {
            Log($"Cannot move {node.Name}: the drop target is not in the active document.");
            return false;
        }

        if (ReferenceEquals(sourceCollection, destinationCollection) && sourceIndex < destinationIndex)
            destinationIndex--;

        var layout = new DesignerLayoutEngine().Layout(Document);
        var absoluteBounds = layout.GetEffectiveBounds(node);
        var parentBounds = target is not null && IsContainerNode(target)
            ? layout.GetEffectiveBounds(target)
            : TryFindParentForCollection(destinationCollection, out var destinationParent) && destinationParent is not null
                ? layout.GetEffectiveBounds(destinationParent)
                : new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height);

        sourceCollection.RemoveAt(sourceIndex);
        node.Bounds = new DesignBounds(
            absoluteBounds.X - parentBounds.X,
            absoluteBounds.Y - parentBounds.Y,
            absoluteBounds.Width,
            absoluteBounds.Height);
        destinationCollection.Insert(Math.Clamp(destinationIndex, 0, destinationCollection.Count), node);
        Host.Selection.Select(node);
        NotifyDocumentChanged();
        Log($"Moved {node.Name} to {targetName}.");
        return true;
    }

    /// <summary>
    /// Reparents a node after an interactive surface drag while preserving its visual bounds.
    /// </summary>
    /// <param name="node">The node that was dragged.</param>
    /// <param name="documentPoint">The drop point in document coordinates.</param>
    /// <returns><see langword="true"/> when the node changed parent; otherwise, <see langword="false"/>.</returns>
    public bool ReparentNodeAtDocumentPoint(DesignControlNode node, DesignPoint documentPoint)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!TryFindNodeWithParent(node, Document.Controls, parent: null, out var currentParent, out var sourceCollection, out var sourceIndex))
            return false;

        var layout = new DesignerLayoutEngine().Layout(Document);
        var targetParent = FindDeepestContainerAtPoint(Document.Controls, layout, node, documentPoint, new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height));

        if (ReferenceEquals(currentParent, targetParent))
            return false;

        if (targetParent is not null && ContainsDescendant(node, targetParent))
            return false;

        var absoluteBounds = layout.GetEffectiveBounds(node);
        var targetBounds = targetParent is null
            ? new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height)
            : layout.GetEffectiveBounds(targetParent);
        var destination = targetParent is null ? Document.Controls : targetParent.Children;

        sourceCollection.RemoveAt(sourceIndex);
        node.Bounds = new DesignBounds(
            absoluteBounds.X - targetBounds.X,
            absoluteBounds.Y - targetBounds.Y,
            absoluteBounds.Width,
            absoluteBounds.Height);
        destination.Add(node);
        Host.Selection.Select(node);
        NotifyDocumentChanged();
        Log($"Reparented {node.Name} to {(targetParent is null ? Document.FormName : targetParent.Name)}.");
        return true;
    }

    /// <summary>
    /// Removes the specified node and its children from the active document.
    /// </summary>
    /// <param name="node">The node to remove.</param>
    /// <returns><see langword="true"/> when the node was removed; otherwise, <see langword="false"/>.</returns>
    public bool DeleteNode(DesignControlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!TryFindNodeWithParent(node, Document.Controls, parent: null, out var parentNode, out var collection, out var index))
        {
            Log($"Cannot delete {node.Name}: the node is not in the active document.");
            return false;
        }

        collection.RemoveAt(index);

        if (parentNode is null)
            Host.Selection.Clear();
        else
            Host.Selection.Select(parentNode);

        NotifyDocumentChanged();
        Log($"Deleted {node.Name}.");
        return true;
    }

    /// <summary>
    /// Determines whether the specified node can contain child controls in the designer model.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when child controls can be added to the node.</returns>
    public bool IsContainerNode(DesignControlNode? node)
    {
        if (node is null)
            return false;

        var type = ResolveControlType(node);

        return type is not null
            ? typeof(ScrollableControl).IsAssignableFrom(type)
                || type.Name.Contains("Panel", StringComparison.Ordinal)
                || type.Name.Contains("Tab", StringComparison.Ordinal)
                || type.Name.Contains("Group", StringComparison.Ordinal)
                || type.Name.Contains("Split", StringComparison.Ordinal)
            : node.TypeName.Contains("Panel", StringComparison.Ordinal)
                || node.TypeName.Contains("Tab", StringComparison.Ordinal)
                || node.TypeName.Contains("Group", StringComparison.Ordinal)
                || node.TypeName.Contains("Split", StringComparison.Ordinal);
    }

    /// <summary>
    /// Updates the selected node or form from primitive property-grid values.
    /// </summary>
    /// <param name="name">The new control or form name.</param>
    /// <param name="text">The new display text.</param>
    /// <param name="x">The new local X coordinate.</param>
    /// <param name="y">The new local Y coordinate.</param>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    /// <param name="memberVisibility">The generated member visibility for selected controls.</param>
    public void UpdateSelectedProperties(
        string name,
        string text,
        int x,
        int y,
        int width,
        int height,
        DesignerMemberVisibility memberVisibility)
    {
        if (SelectedNode is null)
        {
            Document.FormName = string.IsNullOrWhiteSpace(name) ? Document.FormName : name.Trim();
            Document.Size = new DesignSize(Math.Max(1, width), Math.Max(1, height));
            NotifyDocumentChanged();
            Log($"Updated form {Document.FormName}.");
            return;
        }

        var node = SelectedNode;
        node.Name = string.IsNullOrWhiteSpace(name) ? node.Name : name.Trim();
        node.Bounds = new DesignBounds(x, y, Math.Max(1, width), Math.Max(1, height));
        node.MemberVisibility = memberVisibility;
        node.Properties["Text"] = DesignPropertyValue.FromString(text);

        NotifyDocumentChanged();
        Log($"Updated {node.Name}.");
    }

    /// <summary>
    /// Enumerates all document control nodes in visual hierarchy order.
    /// </summary>
    /// <returns>The node/depth pairs for the current document.</returns>
    public IEnumerable<(DesignControlNode Node, int Depth)> EnumerateNodes()
        => EnumerateNodes(Document.Controls, 1);

    /// <summary>
    /// Resolves the runtime control type represented by a design node.
    /// </summary>
    /// <param name="node">The node to resolve.</param>
    /// <returns>The runtime control type, or <see langword="null"/> when it cannot be found.</returns>
    public Type? ResolveControlType(DesignControlNode node)
        => ResolveControlType(node.TypeName);

    /// <summary>
    /// Resolves a short or fully qualified ModernFormsNext control type name.
    /// </summary>
    /// <param name="typeName">The control type name.</param>
    /// <returns>The runtime control type, or <see langword="null"/> when it cannot be found.</returns>
    public Type? ResolveControlType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var frameworkAssembly = typeof(Control).Assembly;

        return Type.GetType(typeName, throwOnError: false)
            ?? frameworkAssembly.GetType(typeName, throwOnError: false)
            ?? frameworkAssembly.GetType($"ModernFormsNext.{typeName}", throwOnError: false);
    }

    /// <summary>
    /// Notifies designer UI components that the document model changed.
    /// </summary>
    public void NotifyDocumentChanged()
    {
        IsDirty = true;
        if (activeDocument is not null)
        {
            activeDocument.Document = Host.Document;
            activeDocument.IsDirty = true;
        }

        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus("Document changed.");
    }

    /// <summary>
    /// Appends a message to the designer output log.
    /// </summary>
    /// <param name="message">The message to append.</param>
    public void Log(string message)
    {
        DesignerDiagnosticLog.Write(message);
        outputLines.Add(message);

        if (outputLines.Count > 200)
            outputLines.RemoveAt(0);

        environment?.ReportOutput(message);
        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void LogDiagnostic(string message)
        => DesignerDiagnosticLog.Write(message);

    /// <summary>
    /// Creates the sample document used by standalone hosts and tests.
    /// </summary>
    /// <returns>A new default design document.</returns>
    public static DesignDocument CreateDefaultDocument()
    {
        var document = new DesignDocument
        {
            Namespace = "ModernFormsNext.Designer.Generated",
            ClassName = "MainForm",
            FormName = "Form1",
            Size = new DesignSize(820, 480)
        };

        var panel = new DesignControlNode
        {
            TypeName = "Panel",
            Name = "panel1",
            Bounds = new DesignBounds(230, 145, 240, 110),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                ["Text"] = DesignPropertyValue.FromString(string.Empty)
            }
        };

        panel.Children.Add(new DesignControlNode
        {
            TypeName = "Label",
            Name = "label1",
            Bounds = new DesignBounds(74, 24, 90, 24),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                ["Text"] = DesignPropertyValue.FromString("label1")
            }
        });

        panel.Children.Add(new DesignControlNode
        {
            TypeName = "Button",
            Name = "button1",
            Bounds = new DesignBounds(58, 54, 90, 28),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                ["Text"] = DesignPropertyValue.FromString("button1")
            }
        });

        document.Controls.Add(panel);
        return document;
    }

    private static IEnumerable<(DesignControlNode Node, int Depth)> EnumerateNodes(
        IEnumerable<DesignControlNode> nodes,
        int depth)
    {
        foreach (var node in nodes)
        {
            yield return (node, depth);

            foreach (var child in EnumerateNodes(node.Children, depth + 1))
                yield return child;
        }
    }

    private bool TryFindNode(
        DesignControlNode node,
        out DesignControlCollection collection,
        out int index)
        => TryFindNode(Document.Controls, node, out collection, out index);

    private static bool TryFindNode(
        DesignControlCollection currentCollection,
        DesignControlNode node,
        out DesignControlCollection collection,
        out int index)
    {
        for (var i = 0; i < currentCollection.Count; i++)
        {
            var current = currentCollection[i];

            if (ReferenceEquals(current, node))
            {
                collection = currentCollection;
                index = i;
                return true;
            }

            if (TryFindNode(current.Children, node, out collection, out index))
                return true;
        }

        collection = currentCollection;
        index = -1;
        return false;
    }

    private static bool TryFindNodeWithParent(
        DesignControlNode node,
        DesignControlCollection currentCollection,
        DesignControlNode? parent,
        out DesignControlNode? parentNode,
        out DesignControlCollection collection,
        out int index)
    {
        for (var i = 0; i < currentCollection.Count; i++)
        {
            var current = currentCollection[i];

            if (ReferenceEquals(current, node))
            {
                parentNode = parent;
                collection = currentCollection;
                index = i;
                return true;
            }

            if (TryFindNodeWithParent(node, current.Children, current, out parentNode, out collection, out index))
                return true;
        }

        parentNode = null;
        collection = currentCollection;
        index = -1;
        return false;
    }

    private bool TryFindParentForCollection(DesignControlCollection collection, out DesignControlNode? parent)
    {
        if (ReferenceEquals(collection, Document.Controls))
        {
            parent = null;
            return true;
        }

        return TryFindParentForCollection(Document.Controls, collection, out parent);
    }

    private static bool TryFindParentForCollection(
        DesignControlCollection nodes,
        DesignControlCollection collection,
        out DesignControlNode? parent)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Children, collection))
            {
                parent = node;
                return true;
            }

            if (TryFindParentForCollection(node.Children, collection, out parent))
                return true;
        }

        parent = null;
        return false;
    }

    private DesignControlNode? FindDeepestContainerAtPoint(
        DesignControlCollection nodes,
        DesignerLayoutResult layout,
        DesignControlNode draggedNode,
        DesignPoint point,
        DesignBounds clip)
    {
        DesignControlNode? result = null;

        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            var node = nodes[index];

            if (ReferenceEquals(node, draggedNode) || ContainsDescendant(draggedNode, node))
                continue;

            var bounds = layout.GetEffectiveBounds(node);
            var visible = Intersect(bounds, clip);

            if (!visible.Contains(point.X, point.Y))
                continue;

            var childResult = FindDeepestContainerAtPoint(node.Children, layout, draggedNode, point, visible);

            if (childResult is not null)
                return childResult;

            if (IsContainerNode(node))
                result = node;
        }

        return result;
    }

    private static DesignBounds Intersect(DesignBounds first, DesignBounds second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);

        if (right <= left || bottom <= top)
            return new DesignBounds(left, top, 0, 0);

        return new DesignBounds(left, top, right - left, bottom - top);
    }

    private static bool ContainsDescendant(DesignControlNode root, DesignControlNode candidate)
    {
        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, candidate) || ContainsDescendant(child, candidate))
                return true;
        }

        return false;
    }

    private int CountControls(string typeName)
    {
        var prefix = char.ToLowerInvariant(typeName[0]) + typeName[1..];
        return EnumerateNodes().Count(item => item.Node.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static DesignBounds GetDefaultBounds(string typeName, DesignControlNode? parent, int index)
    {
        var offset = Math.Min(index, 8) * 12;
        var originX = parent is null ? 80 : 24;
        var originY = parent is null ? 80 : 24;

        return typeName switch
        {
            "Panel" => new DesignBounds(120 + offset, 95 + offset, 240, 120),
            "Label" => new DesignBounds(originX + offset, originY + offset, 100, 24),
            "TextBox" => new DesignBounds(originX + offset, originY + offset, 150, 25),
            _ => new DesignBounds(originX + offset, originY + offset, 90, 28)
        };
    }

    private static string GetDefaultText(string typeName, int index)
        => typeName switch
        {
            "Button" => $"button{index}",
            "Label" => $"label{index}",
            "TextBox" => $"textBox{index}",
            _ => string.Empty
        };
}

internal sealed class DesignerOpenDocument
{
    public DesignerOpenDocument(DesignDocument document, string? path)
    {
        Document = document;
        Path = path;
    }

    public DesignDocument Document { get; set; }

    public string? Path { get; set; }

    public bool IsDirty { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Path)
        ? $"{Document.ClassName}.mfdesign"
        : System.IO.Path.GetFileName(Path);
}
