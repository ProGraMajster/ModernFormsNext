using ModernFormsNext;
using ModernFormsNext.Designer.Clipboard;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using System.Runtime.ExceptionServices;

namespace ModernFormsNext.Designer.Services;

/// <summary>
/// Holds the active designer document, selection, output log, pointer state, and dirty state.
/// </summary>
/// <remarks>
/// A session is UI-framework neutral within the designer library: panels, surfaces, and host
/// adapters all observe the same session instance instead of maintaining their own copies of
/// document state.
/// </remarks>
public sealed class DesignerSession : IDisposable
{
    private readonly List<string> outputLines = [];
    private readonly List<DesignerOpenDocument> openDocuments = [];
    private readonly DesignerHitTestService hitTestService = new(new DesignerCoordinateMapper());
    private readonly IDesignerHostEnvironment? environment;
    private readonly IReadOnlyList<DesignerProjectUserControlInfo> projectUserControls;
    private readonly DesignerHistory detachedHistory;
    private readonly IDesignerClipboard clipboard;
    private DesignerOpenDocument? activeDocument;
    private int historyLimit;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerSession"/> class.
    /// </summary>
    /// <param name="environment">Optional host environment used for status and output routing.</param>
    /// <param name="initialRenderMode">The initial designer surface render mode.</param>
    public DesignerSession(
        IDesignerHostEnvironment? environment = null,
        DesignerControlRenderMode initialRenderMode = DesignerControlRenderMode.Runtime)
        : this(environment, initialRenderMode, historyLimit: 500)
    {
    }

    /// <summary>
    /// Initializes a new Designer session with an explicit per-document history limit.
    /// </summary>
    /// <param name="environment">Optional host environment used for status and output routing.</param>
    /// <param name="initialRenderMode">The initial designer surface render mode.</param>
    /// <param name="historyLimit">The maximum number of undo units retained per document.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="historyLimit"/> is less than one.</exception>
    public DesignerSession(
        IDesignerHostEnvironment? environment,
        DesignerControlRenderMode initialRenderMode,
        int historyLimit)
        : this(environment, initialRenderMode, historyLimit, new DesignerClipboard())
    {
    }

    internal DesignerSession(
        IDesignerHostEnvironment? environment,
        DesignerControlRenderMode initialRenderMode,
        int historyLimit,
        IDesignerClipboard clipboard)
    {
        if (historyLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(historyLimit), "Designer history limit must be at least one entry.");

        this.environment = environment;
        this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        this.historyLimit = historyLimit;
        detachedHistory = new DesignerHistory(historyLimit, initiallyDirty: false);
        projectUserControls = DesignerProjectUserControlDiscovery.Discover(environment?.CurrentProjectPath);
        ControlRenderMode = initialRenderMode;
        Host = new DesignerHost(CreateDefaultDocument());
        Transactions = new DesignerTransactionManager(this);
        Host.Selection.SelectionChanged += HostSelection_SelectionChanged;
        clipboard.Changed += Clipboard_Changed;
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

    internal event EventHandler? ClipboardChanged;

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

    internal event EventHandler<DesignerOpenDocumentEventArgs>? DocumentOpened;

    internal event EventHandler<DesignerOpenDocumentEventArgs>? DocumentClosed;

    internal event EventHandler<DesignerDocumentPathChangedEventArgs>? DocumentPathChanged;

    internal event EventHandler<DesignerOpenDocumentEventArgs>? DocumentBaselineChanged;

    /// <summary>
    /// Gets the neutral designer host that owns the document and selection service.
    /// </summary>
    public DesignerHost Host { get; }

    /// <summary>
    /// Gets the transaction and undo/redo manager for the active Designer document.
    /// </summary>
    public DesignerTransactionManager Transactions { get; }

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
    /// Gets the active document's canonical path, or <see langword="null"/> when the active
    /// document has not been saved yet.
    /// </summary>
    /// <remarks>
    /// An unsaved document deliberately does not fall back to a previous host path. This prevents
    /// Save or recovery logic from overwriting the file that was active before a new tab opened.
    /// </remarks>
    public string? CurrentDocumentPath => activeDocument is null
        ? environment?.CurrentDocumentPath
        : activeDocument.Path;

    /// <summary>
    /// Gets the active project path supplied by the host environment, if one is known.
    /// </summary>
    public string? CurrentProjectPath => activeDocument?.ProjectPath ?? environment?.CurrentProjectPath;

    internal IReadOnlyList<DesignerProjectUserControlInfo> ProjectUserControls => projectUserControls;

    internal IReadOnlyList<DesignAnimationDefinitionDescriptor> AnimationDefinitions
        => DesignerProjectAnimationDefinitionDiscovery.Discover(CurrentProjectPath);

    internal IReadOnlyList<DesignerOpenDocument> OpenDocuments => openDocuments;

    internal DesignerOpenDocument? ActiveOpenDocument => activeDocument;

    internal int ActiveDocumentIndex => activeDocument is null ? -1 : openDocuments.IndexOf(activeDocument);

    internal DesignerHistory CurrentHistory => activeDocument?.History ?? detachedHistory;

    internal int HistoryLimit => historyLimit;

    internal IDesignerClipboard Clipboard => clipboard;

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
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("A Designer document cannot be opened during an active transaction.");

        DesignerSpecialContainers.NormalizeDocument(document);

        var normalizedPath = DesignerDocumentPath.NormalizeDesignPath(path);
        markDirty |= SynchronizeProjectUserControlIdentity(document, normalizedPath);
        var existing = !string.IsNullOrWhiteSpace(normalizedPath)
            ? openDocuments.FirstOrDefault(tab => string.Equals(tab.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
            : null;

        if (existing is null)
        {
            existing = new DesignerOpenDocument(
                document,
                normalizedPath,
                environment?.CurrentProjectPath,
                historyLimit,
                markDirty);
            openDocuments.Add(existing);
        }
        else
        {
            activeDocument = existing;
            Host.LoadDocument(existing.Document);
            RefreshDirtyState();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
            Transactions.NotifyActiveHistoryChanged();
            Log($"Activated {existing.DisplayName}.");
            return;
        }

        activeDocument = existing;
        Host.LoadDocument(document);
        RefreshDirtyState();
        DocumentOpened?.Invoke(this, new DesignerOpenDocumentEventArgs(existing));
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        Transactions.NotifyActiveHistoryChanged();
        Log($"Loaded {document.ClassName}.mfdesign.");
    }

    private bool SynchronizeProjectUserControlIdentity(DesignDocument document, string? designPath)
    {
        if (string.IsNullOrWhiteSpace(designPath))
            return false;

        var sourcePath = IOPath.ChangeExtension(designPath, ".cs");
        var matchingControls = projectUserControls
            .Where(control => string.Equals(
                IOPath.GetFullPath(control.SourceFilePath),
                IOPath.GetFullPath(sourcePath),
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (matchingControls.Length != 1)
            return false;

        var control = matchingControls[0];
        // A legacy document may not yet have rootKind. Infer UserControl only when the original
        // class name still identifies the discovered declaration; otherwise a Form file containing
        // an unrelated helper UserControl must remain a Form.
        if (document.RootKind != DesignRootKind.UserControl
            && !string.Equals(document.ClassName, control.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var namespaceName = control.FullName.Length == control.Name.Length
            ? string.Empty
            : control.FullName[..^(control.Name.Length + 1)];
        var changed = document.RootKind != DesignRootKind.UserControl
            || !string.Equals(document.ClassName, control.Name, StringComparison.Ordinal)
            || !string.Equals(document.Namespace, namespaceName, StringComparison.Ordinal);

        if (!changed)
            return false;

        document.RootKind = DesignRootKind.UserControl;
        document.ClassName = control.Name;
        document.Namespace = namespaceName;
        return true;
    }

    internal void SwitchDocument(int index)
    {
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("The active Designer document cannot change during a transaction.");

        if (index < 0 || index >= openDocuments.Count)
            return;

        if (ReferenceEquals(activeDocument, openDocuments[index]))
            return;

        activeDocument = openDocuments[index];
        Host.LoadDocument(activeDocument.Document);
        RefreshDirtyState();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        Transactions.NotifyActiveHistoryChanged();
        Log($"Activated {activeDocument.DisplayName}.");
    }

    internal void CloseDocument(int index)
    {
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("A Designer document cannot close during an active transaction.");

        if (index < 0 || index >= openDocuments.Count)
            return;

        if (openDocuments.Count == 1)
        {
            Log("The last designer document tab cannot be closed.");
            return;
        }

        var closedDocument = openDocuments[index];
        var wasActive = ReferenceEquals(activeDocument, closedDocument);
        openDocuments.RemoveAt(index);
        closedDocument.History.Clear(preserveDirtyState: false);
        closedDocument.RevisionGeneration++;

        if (wasActive)
        {
            activeDocument = openDocuments[Math.Clamp(index, 0, openDocuments.Count - 1)];
            Host.LoadDocument(activeDocument.Document);
            RefreshDirtyState();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        Transactions.NotifyActiveHistoryChanged();
        DocumentClosed?.Invoke(this, new DesignerOpenDocumentEventArgs(closedDocument));
        Log($"Closed {closedDocument.DisplayName}.");
    }

    /// <summary>
    /// Marks the current document as saved.
    /// </summary>
    /// <param name="statusMessage">The status message reported to the host environment.</param>
    public void MarkSaved(string statusMessage = "Document saved.")
    {
        ThrowIfDisposed();
        Transactions.MarkSavedState();

        DocumentChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus(statusMessage);
    }

    internal bool MarkSaved(
        DesignerOpenDocument document,
        long revisionGeneration,
        long revision,
        string statusMessage = "Document saved.")
    {
        ArgumentNullException.ThrowIfNull(document);
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("A Designer document cannot be marked saved during an active transaction.");
        if (!openDocuments.Contains(document) || document.RevisionGeneration != revisionGeneration)
            return false;

        document.History.MarkSaved(revision);
        if (ReferenceEquals(activeDocument, document))
            RefreshDirtyState();

        DocumentChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus(statusMessage);
        return true;
    }

    internal void UpdateDocumentPath(DesignerOpenDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("A Designer document path cannot change during an active transaction.");
        if (!openDocuments.Contains(document))
            throw new InvalidOperationException("The Designer document is no longer open.");

        var normalizedPath = DesignerDocumentPath.NormalizeDesignPath(path)
            ?? throw new ArgumentException("The Designer document path could not be normalized.", nameof(path));
        var duplicate = openDocuments.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, document)
            && string.Equals(candidate.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            throw new InvalidOperationException($"The Designer document '{normalizedPath}' is already open.");

        var oldPath = document.Path;
        if (string.Equals(oldPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            return;

        document.Path = normalizedPath;
        document.ProjectPath ??= environment?.CurrentProjectPath;
        DocumentPathChanged?.Invoke(this, new DesignerDocumentPathChangedEventArgs(document, oldPath, normalizedPath));
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ReloadDocumentBaseline(
        DesignerOpenDocument document,
        DesignDocument replacement,
        bool markDirty,
        string statusMessage)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusMessage);
        ThrowIfDisposed();
        if (Transactions.HasActiveTransaction)
            throw new InvalidOperationException("A Designer document cannot reload during an active transaction.");
        if (!openDocuments.Contains(document))
            throw new InvalidOperationException("The Designer document is no longer open.");

        DesignerSpecialContainers.NormalizeDocument(replacement);
        var isActiveDocument = ReferenceEquals(activeDocument, document);
        var previousHostDocument = Host.Document;
        var previousSelection = Host.Selection.SelectedNode;
        var selectedName = isActiveDocument ? previousSelection?.Name : null;

        if (isActiveDocument)
        {
            try
            {
                // Stage the replacement in the host before releasing history or changing the open
                // document. Selection observers can throw from LoadDocument/Select; until both
                // complete, the prior model and undo history remain the authoritative baseline.
                Host.LoadDocument(replacement);
                if (!string.IsNullOrWhiteSpace(selectedName))
                    Host.Selection.Select(FindNodeByName(replacement.Controls, selectedName));
            }
            catch
            {
                Host.Document = previousHostDocument;
                try
                {
                    Host.Selection.Select(previousSelection);
                }
                catch
                {
                    // Selection state changes before its event is raised. Preserve the original
                    // reload exception after restoring the model even if an observer also rejects
                    // the compensating selection notification.
                }

                throw;
            }
        }

        document.Document = replacement;
        document.History.Clear(preserveDirtyState: false);
        if (markDirty)
            document.History.MarkUnsaved();
        document.RevisionGeneration++;

        if (isActiveDocument)
        {
            RefreshDirtyState();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Transactions.NotifyActiveHistoryChanged();
        }

        DocumentBaselineChanged?.Invoke(this, new DesignerOpenDocumentEventArgs(document));
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
        Log($"Created new document {document.ClassName}.mfdesign.");
    }

    /// <summary>
    /// Selects the design root by clearing the selected control node.
    /// </summary>
    public void SelectForm()
    {
        Host.Selection.Clear();
        Log($"Selected {Document.FormName}.");
    }

    /// <summary>
    /// Selects the specified control node.
    /// </summary>
    /// <param name="node">The node to select, or <see langword="null"/> to select the design root.</param>
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
        var result = hitTestService.HitTestControl(this, new DesignPoint(x, y));
        var selectedNode = GetComponentBoundary(result.Node);
        Host.Selection.Select(selectedNode);
        Log(selectedNode is null ? $"Selected {Document.FormName}." : $"Selected {selectedNode.Name}.");
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
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ThrowIfDisposed();

        if (!DesignerControlReferenceGuard.CanReference(Document, typeName, CurrentProjectPath, out var error))
            throw new InvalidOperationException(error);

        var parent = IsContainerNode(SelectedNode) ? SelectedNode : null;
        var index = CountControls(typeName) + 1;
        var shortTypeName = GetShortTypeName(typeName);
        var namePrefix = char.ToLowerInvariant(shortTypeName[0]) + shortTypeName[1..];
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

        if (shortTypeName == "Panel")
            node.Properties["Text"] = DesignPropertyValue.FromString(string.Empty);

        InitializeShapeDefaults(node, shortTypeName);

        DesignerSpecialContainers.InitializeNewNode(node);

        var destination = GetChildCollectionForNewControl(parent, out var targetName);
        AssignDefaultTableLayoutPosition(node, parent);
        var targetCollection = destination ?? Document.Controls;

        using var transaction = Transactions.Begin($"Add {shortTypeName}");
        Transactions.ExecuteChange(new DesignerTreeInsertChange(targetCollection, node, targetCollection.Count));
        Host.Selection.Select(node);
        transaction.Commit();
        Log($"Added {typeName} {node.Name} to {targetName}.");
        return node;
    }

    /// <summary>
    /// Moves a node to a target chosen in the document outline.
    /// </summary>
    /// <param name="node">The node being moved.</param>
    /// <param name="target">The target row, or <see langword="null"/> to move the node to the design root.</param>
    /// <returns><see langword="true"/> when the node was moved; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Container targets receive the node as a child. Non-container targets receive the node
    /// as the next sibling. Bounds remain local to the new parent so the model hierarchy stays
    /// explicit and serialization/code generation use the same structure as the outline.
    /// </remarks>
    public bool MoveNodeToOutlineTarget(DesignControlNode node, DesignControlNode? target)
    {
        ArgumentNullException.ThrowIfNull(node);
        ThrowIfDisposed();

        if (ReferenceEquals(node, target))
            return false;

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            Log($"{DesignerSpecialContainers.GetOutlineName(node)} is a structural designer node and cannot be moved.");
            return false;
        }

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
            destinationCollection = GetChildCollectionForNewControl(target, out targetName) ?? target.Children;
            destinationIndex = destinationCollection.Count;
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

        using var transaction = Transactions.Begin($"Move {node.Name}");
        var snapshot = DesignerModelMutationSnapshot.CaptureNode(node);
        try
        {
            Transactions.ExecuteChange(new DesignerTreeMoveChange(
                node,
                sourceCollection,
                sourceIndex,
                destinationCollection,
                destinationIndex));
            node.Bounds = new DesignBounds(
                absoluteBounds.X - parentBounds.X,
                absoluteBounds.Y - parentBounds.Y,
                absoluteBounds.Width,
                absoluteBounds.Height);
            AssignDefaultTableLayoutPosition(node, target);
        }
        finally
        {
            // Capture direct layout metadata even when a later mutation in this compound move
            // throws. Disposal can then restore the exact pre-transaction node state.
            snapshot.RecordChanges(Transactions);
        }
        Host.Selection.Select(node);
        transaction.Commit();
        Log($"Moved {node.Name} to {targetName}.");
        return true;
    }

    /// <summary>
    /// Moves the selected control one position earlier inside its current parent collection.
    /// </summary>
    /// <remarks>
    /// For ordinary containers, an earlier collection index moves the control toward the front of
    /// the Z-order. For flow, table, and tab containers, it moves the control earlier in the
    /// container's authored sequence.
    /// </remarks>
    /// <returns><see langword="true"/> when the selected control was moved; otherwise, <see langword="false"/>.</returns>
    public bool MoveSelectedNodeUp()
        => MoveSelectedNodeWithinCurrentContainer(delta: -1);

    /// <summary>
    /// Moves the selected control one position later inside its current parent collection.
    /// </summary>
    /// <remarks>
    /// For ordinary containers, a later collection index moves the control toward the back of the
    /// Z-order. For flow, table, and tab containers, it moves the control later in the container's
    /// authored sequence.
    /// </remarks>
    /// <returns><see langword="true"/> when the selected control was moved; otherwise, <see langword="false"/>.</returns>
    public bool MoveSelectedNodeDown()
        => MoveSelectedNodeWithinCurrentContainer(delta: 1);

    /// <summary>
    /// Moves the selected control out of its current container while preserving its visual position.
    /// </summary>
    /// <returns><see langword="true"/> when the selected control was reparented; otherwise, <see langword="false"/>.</returns>
    public bool MoveSelectedNodeOutOfContainer()
    {
        ThrowIfDisposed();
        if (SelectedNode is null)
        {
            Log("No control is selected to move.");
            return false;
        }

        var node = SelectedNode;

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            Log($"{DesignerSpecialContainers.GetOutlineName(node)} is a structural designer node and cannot be moved.");
            return false;
        }

        if (!TryFindNodeWithParent(node, Document.Controls, parent: null, out var parentNode, out var sourceCollection, out var sourceIndex)
            || parentNode is null)
        {
            Log($"{node.Name} is already at the form level.");
            return false;
        }

        var exitNode = parentNode;
        var destinationParent = FindParent(exitNode);

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(exitNode)
            && destinationParent is not null)
        {
            exitNode = destinationParent;
            destinationParent = FindParent(exitNode);
        }

        var layout = new DesignerLayoutEngine().Layout(Document);
        var absoluteBounds = layout.GetEffectiveBounds(node);
        var destinationParentBounds = destinationParent is null
            ? new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height)
            : layout.GetEffectiveBounds(destinationParent);
        var destinationCollection = destinationParent is null ? Document.Controls : destinationParent.Children;

        if (!TryFindNode(exitNode, out var exitCollection, out var exitIndex))
            exitIndex = destinationCollection.Count - 1;

        var destinationIndex = ReferenceEquals(destinationCollection, exitCollection)
            ? Math.Clamp(exitIndex + 1, 0, destinationCollection.Count)
            : destinationCollection.Count;

        using var transaction = Transactions.Begin($"Move {node.Name}");
        var snapshot = DesignerModelMutationSnapshot.CaptureNode(node);
        try
        {
            Transactions.ExecuteChange(new DesignerTreeMoveChange(
                node,
                sourceCollection,
                sourceIndex,
                destinationCollection,
                destinationIndex));
            node.Bounds = new DesignBounds(
                absoluteBounds.X - destinationParentBounds.X,
                absoluteBounds.Y - destinationParentBounds.Y,
                absoluteBounds.Width,
                absoluteBounds.Height);
        }
        finally
        {
            snapshot.RecordChanges(Transactions);
        }
        Host.Selection.Select(node);
        transaction.Commit();
        Log($"Moved {node.Name} out of {DesignerSpecialContainers.GetOutlineName(parentNode)}.");
        return true;
    }

    /// <summary>
    /// Moves the selected control into the next container in document-outline order.
    /// </summary>
    /// <returns><see langword="true"/> when the selected control was moved; otherwise, <see langword="false"/>.</returns>
    public bool MoveSelectedNodeToNextContainer()
    {
        if (SelectedNode is null)
        {
            Log("No control is selected to move.");
            return false;
        }

        var node = SelectedNode;

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            Log($"{DesignerSpecialContainers.GetOutlineName(node)} is a structural designer node and cannot be moved.");
            return false;
        }

        var containers = EnumerateNodes()
            .Select(item => item.Node)
            .Where(candidate => !ReferenceEquals(candidate, node))
            .Where(candidate => !ContainsDescendant(node, candidate))
            .Where(IsContainerNode)
            .ToArray();

        if (containers.Length == 0)
        {
            Log("No destination container is available.");
            return false;
        }

        var parent = FindParent(node);
        var currentContainerIndex = parent is null
            ? -1
            : Array.FindIndex(containers, candidate => ReferenceEquals(candidate, parent));
        var startIndex = Math.Max(currentContainerIndex + 1, 0);

        for (var offset = 0; offset < containers.Length; offset++)
        {
            var target = containers[(startIndex + offset) % containers.Length];

            if (!ReferenceEquals(target, parent))
                return MoveNodeToOutlineTarget(node, target);
        }

        Log("No different destination container is available.");
        return false;
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
        ThrowIfDisposed();

        if (!TryFindNodeWithParent(node, Document.Controls, parent: null, out var currentParent, out var sourceCollection, out var sourceIndex))
            return false;

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
            return false;

        var layout = new DesignerLayoutEngine().Layout(Document);
        var targetParent = FindDeepestContainerAtPoint(Document.Controls, parentNode: null, layout, node, documentPoint, new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height));

        if (ReferenceEquals(currentParent, targetParent))
            return false;

        if (targetParent is not null && ContainsDescendant(node, targetParent))
            return false;

        var absoluteBounds = layout.GetEffectiveBounds(node);
        var targetBounds = targetParent is null
            ? new DesignBounds(0, 0, Document.Size.Width, Document.Size.Height)
            : layout.GetEffectiveBounds(targetParent);
        var destination = targetParent is null
            ? Document.Controls
            : GetChildCollectionForNewControl(targetParent, out _) ?? targetParent.Children;

        using var transaction = Transactions.Begin($"Move {node.Name}");
        var snapshot = DesignerModelMutationSnapshot.CaptureNode(node);
        try
        {
            Transactions.ExecuteChange(new DesignerTreeMoveChange(
                node,
                sourceCollection,
                sourceIndex,
                destination,
                destination.Count));
            node.Bounds = new DesignBounds(
                absoluteBounds.X - targetBounds.X,
                absoluteBounds.Y - targetBounds.Y,
                absoluteBounds.Width,
                absoluteBounds.Height);
            AssignTableLayoutPositionFromPoint(node, targetParent, targetBounds, documentPoint);
        }
        finally
        {
            snapshot.RecordChanges(Transactions);
        }
        Host.Selection.Select(node);
        transaction.Commit();
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
        ThrowIfDisposed();

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            Log($"{DesignerSpecialContainers.GetOutlineName(node)} is owned by its SplitContainer and cannot be deleted.");
            return false;
        }

        if (!TryFindNodeWithParent(node, Document.Controls, parent: null, out var parentNode, out var collection, out var index))
        {
            Log($"Cannot delete {node.Name}: the node is not in the active document.");
            return false;
        }

        using var transaction = Transactions.Begin($"Delete {node.Name}");
        Transactions.ExecuteChange(new DesignerTreeRemoveChange(collection, node, index));

        if (parentNode is null)
            Host.Selection.Clear();
        else
            Host.Selection.Select(parentNode);

        transaction.Commit();
        Log($"Deleted {node.Name}.");
        return true;
    }

    /// <summary>
    /// Deletes the currently selected control node when a control is selected.
    /// </summary>
    /// <returns><see langword="true"/> when a control was deleted; otherwise, <see langword="false"/>.</returns>
    public bool DeleteSelectedNode()
        => SelectedNode is not null && DeleteNode(SelectedNode);

    /// <summary>
    /// Copies the currently selected control node into the designer clipboard.
    /// </summary>
    /// <remarks>
    /// Copy stores a detached, versioned data payload. It does not mutate the document, change the
    /// dirty state, create history, or retain the selected live control tree.
    /// </remarks>
    /// <returns><see langword="true"/> when a control was copied; otherwise, <see langword="false"/>.</returns>
    public bool CopySelectedNode()
    {
        ThrowIfDisposed();
        if (!TryGetClipboardSource(out var source, out var error))
        {
            Log(error!);
            return false;
        }

        if (!DesignerClipboardSerializer.TrySerialize(source, out var content, out error))
        {
            Log(error!);
            return false;
        }

        clipboard.SetContent(content!);
        Log($"Copied {source.Name}.");
        return true;
    }

    /// <summary>
    /// Cuts the currently selected control subtree into the Designer clipboard.
    /// </summary>
    /// <remarks>
    /// The detached payload is prepared before the model changes. Removing the subtree and updating
    /// selection are then committed as exactly one undo unit through the shared transaction manager.
    /// </remarks>
    /// <returns><see langword="true"/> when a control was cut; otherwise, <see langword="false"/>.</returns>
    public bool CutSelectedNode()
    {
        ThrowIfDisposed();
        if (!TryGetClipboardSource(out var source, out var error))
        {
            Log(error!);
            return false;
        }

        if (!TryFindNodeWithParent(source, Document.Controls, parent: null, out var parent, out var collection, out var index))
        {
            Log($"Cannot cut {source.Name}: the node is not in the active document.");
            return false;
        }

        if (!DesignerClipboardSerializer.TrySerialize(source, out var content, out error))
        {
            Log(error!);
            return false;
        }

        // Store the data-only copy first. If a clipboard observer fails, no document mutation has
        // begun; the operation safely degrades to Copy instead of losing the selected subtree.
        clipboard.SetContent(content!);
        using var transaction = Transactions.Begin($"Cut {source.Name}");
        Transactions.ExecuteChange(new DesignerTreeRemoveChange(collection, source, index));
        Host.Selection.Select(parent);
        transaction.Commit();
        Log($"Cut {source.Name}.");
        return true;
    }

    /// <summary>
    /// Duplicates the currently selected control node as a sibling with a unique name.
    /// </summary>
    /// <returns><see langword="true"/> when a control was duplicated; otherwise, <see langword="false"/>.</returns>
    public bool DuplicateSelectedNode()
    {
        ThrowIfDisposed();
        if (!TryGetClipboardSource(out var source, out var error))
        {
            Log(error!);
            return false;
        }

        if (!TryFindNodeWithParent(source, Document.Controls, parent: null, out var parent, out var collection, out var index))
        {
            Log($"Cannot duplicate {source.Name}: the node is not in the active document.");
            return false;
        }

        if (!TryCreateDetachedClone(source, out var clone, out error)
            || !TryPrepareInsertion(clone!, new DesignerPasteTarget(collection, parent, parent?.Name ?? Document.FormName, index + 1), out error))
        {
            Log(error!);
            return false;
        }

        var duplicate = clone!;
        using var transaction = Transactions.Begin($"Duplicate {source.Name}");
        Transactions.ExecuteChange(new DesignerTreeInsertChange(collection, duplicate, index + 1));
        Host.Selection.Select(duplicate);
        transaction.Commit();
        Log($"Duplicated {source.Name} as {duplicate.Name}.");
        return true;
    }

    /// <summary>
    /// Pastes the last copied control node into the active document.
    /// </summary>
    /// <returns><see langword="true"/> when a control was pasted; otherwise, <see langword="false"/>.</returns>
    public bool PasteCopiedNode()
    {
        ThrowIfDisposed();
        if (!DesignerClipboardSerializer.TryDeserialize(clipboard.Content, out var clone, out var error))
        {
            Log(error!);
            return false;
        }

        if (!TryResolvePasteTarget(clone!, out var target, out error)
            || !TryPrepareInsertion(clone!, target, out error))
        {
            Log(error!);
            return false;
        }

        var pasted = clone!;
        using var transaction = Transactions.Begin($"Paste {pasted.Name}");
        Transactions.ExecuteChange(new DesignerTreeInsertChange(target.Collection, pasted, target.Index));
        Host.Selection.Select(pasted);
        transaction.Commit();
        Log($"Pasted {pasted.Name} into {target.DisplayName}.");
        return true;
    }

    internal bool CanCopySelectedNode => CanUseSelectedNodeAsClipboardSource();

    internal bool CanCutSelectedNode => CanUseSelectedNodeAsClipboardSource();

    internal bool CanDuplicateSelectedNode
    {
        get
        {
            if (!CanUseSelectedNodeAsClipboardSource()
                || SelectedNode is not { } source
                || !TryFindNodeWithParent(source, Document.Controls, parent: null, out var parent, out var collection, out var index)
                || !TryCreateDetachedClone(source, out var clone, out _))
            {
                return false;
            }

            return CanInsertNode(clone!, new DesignerPasteTarget(collection, parent, parent?.Name ?? Document.FormName, index + 1), out _);
        }
    }

    internal bool CanPasteCopiedNode
    {
        get
        {
            if (disposed
                || !DesignerClipboardSerializer.TryDeserialize(clipboard.Content, out var node, out _)
                || !TryResolvePasteTarget(node!, out var target, out _))
            {
                return false;
            }

            return CanInsertNode(node!, target, out _)
                && DesignerControlReferenceGuard.CanReferenceTree(Document, node!, CurrentProjectPath, out _);
        }
    }

    private bool MoveSelectedNodeWithinCurrentContainer(int delta)
    {
        ThrowIfDisposed();
        if (SelectedNode is null)
        {
            Log("No control is selected to move.");
            return false;
        }

        var node = SelectedNode;

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            Log($"{DesignerSpecialContainers.GetOutlineName(node)} is a structural designer node and cannot be moved.");
            return false;
        }

        if (!TryFindNode(node, out var collection, out var index))
        {
            Log($"Cannot move {node.Name}: the node is not in the active document.");
            return false;
        }

        var newIndex = index + delta;

        if (newIndex < 0 || newIndex >= collection.Count)
        {
            Log(delta < 0
                ? $"{node.Name} is already first in its container."
                : $"{node.Name} is already last in its container.");
            return false;
        }

        using var transaction = Transactions.Begin(delta < 0 ? $"Move {node.Name} up" : $"Move {node.Name} down");
        Transactions.ExecuteChange(new DesignerTreeMoveChange(node, collection, index, collection, newIndex));
        Host.Selection.Select(node);
        transaction.Commit();
        Log(delta < 0
            ? $"Moved {node.Name} up in its container."
            : $"Moved {node.Name} down in its container.");
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

        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
            return true;

        // A project-owned UserControl is a component boundary in its parent's designer. Its
        // internal children belong to its own .mfdesign document and are never edited in place.
        if (IsProjectUserControlType(node.TypeName))
            return false;

        var type = ResolveControlType(node);

        return type is not null
            ? typeof(ScrollableControl).IsAssignableFrom(type)
                || type.Name.Contains("Panel", StringComparison.Ordinal)
                || type.Name.Contains("Tab", StringComparison.Ordinal)
                || type.Name.Contains("Group", StringComparison.Ordinal)
                || type.Name.Contains("Split", StringComparison.Ordinal)
                || type.Name.Contains("Layout", StringComparison.Ordinal)
            : node.TypeName.Contains("Panel", StringComparison.Ordinal)
                || node.TypeName.Contains("Tab", StringComparison.Ordinal)
                || node.TypeName.Contains("Group", StringComparison.Ordinal)
                || node.TypeName.Contains("Split", StringComparison.Ordinal)
                || node.TypeName.Contains("Layout", StringComparison.Ordinal);
    }

    /// <summary>
    /// Changes one control's generated field name through the active transaction layer.
    /// </summary>
    /// <param name="node">The control node to rename.</param>
    /// <param name="name">The new non-empty field name.</param>
    public void SetNodeName(DesignControlNode node, string name)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureNodeBelongsToDocument(node);

        var change = new DesignerNodeValueChange(
            node,
            DesignerNodeValueKind.Name,
            node.Name,
            name.Trim());
        if (Transactions.IsReplaying)
        {
            Transactions.ExecuteChange(change);
            return;
        }

        using var transaction = Transactions.Begin($"Rename {node.Name} to {name.Trim()}");
        Transactions.ExecuteChange(change);
        transaction.Commit();
    }

    /// <summary>
    /// Changes one control's authored bounds through the active transaction layer.
    /// </summary>
    /// <param name="node">The control node to update.</param>
    /// <param name="bounds">The new parent-local logical bounds.</param>
    public void SetNodeBounds(DesignControlNode node, DesignBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureNodeBelongsToDocument(node);

        var change = new DesignerNodeValueChange(
            node,
            DesignerNodeValueKind.Bounds,
            node.Bounds,
            bounds);
        if (Transactions.IsReplaying)
        {
            Transactions.ExecuteChange(change);
            return;
        }

        using var transaction = Transactions.Begin($"Change bounds of {node.Name}");
        Transactions.ExecuteChange(change);
        transaction.Commit();
    }

    /// <summary>
    /// Sets a serialized property on the design root or a control through the active transaction.
    /// </summary>
    /// <param name="node">The target control, or <see langword="null"/> for the design root.</param>
    /// <param name="propertyName">The runtime property name.</param>
    /// <param name="value">The deterministic Designer value to store.</param>
    public void SetPropertyValue(DesignControlNode? node, string propertyName, DesignPropertyValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(value);
        if (node is not null)
            EnsureNodeBelongsToDocument(node);

        var properties = node?.Properties ?? Document.Properties;
        var existed = properties.TryGetValue(propertyName, out var previous);
        var change = new DesignerPropertyDictionaryChange(
            properties,
            propertyName,
            existed,
            previous,
            existsAfter: true,
            value);
        if (Transactions.IsReplaying)
        {
            Transactions.ExecuteChange(change);
            return;
        }

        using var transaction = Transactions.Begin($"Change {propertyName}");
        Transactions.ExecuteChange(change);
        transaction.Commit();
    }

    /// <summary>
    /// Removes a serialized property from the design root or a control through the active transaction.
    /// </summary>
    /// <param name="node">The target control, or <see langword="null"/> for the design root.</param>
    /// <param name="propertyName">The runtime property name.</param>
    public void RemovePropertyValue(DesignControlNode? node, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        if (node is not null)
            EnsureNodeBelongsToDocument(node);

        var properties = node?.Properties ?? Document.Properties;
        var existed = properties.TryGetValue(propertyName, out var previous);
        var change = new DesignerPropertyDictionaryChange(
            properties,
            propertyName,
            existed,
            previous,
            existsAfter: false,
            after: null);
        if (Transactions.IsReplaying)
        {
            Transactions.ExecuteChange(change);
            return;
        }

        using var transaction = Transactions.Begin($"Reset {propertyName}");
        Transactions.ExecuteChange(change);
        transaction.Commit();
    }

    /// <summary>
    /// Replaces the ordered child collection of the design root or one container atomically.
    /// </summary>
    /// <param name="parent">The parent node, or <see langword="null"/> for the design root.</param>
    /// <param name="children">The complete final ordered child sequence.</param>
    /// <param name="description">The user-visible history description.</param>
    public void ReplaceChildren(
        DesignControlNode? parent,
        IReadOnlyList<DesignControlNode> children,
        string description = "Edit control collection")
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (parent is not null)
            EnsureNodeBelongsToDocument(parent);
        if (children.Count != children.Distinct(ReferenceEqualityComparer.Instance).Count())
            throw new ArgumentException("A Designer child collection cannot contain the same node more than once.", nameof(children));

        var collection = parent?.Children ?? Document.Controls;
        using var transaction = Transactions.Begin(description);
        Transactions.ExecuteChange(new DesignerChildrenReplaceChange(
            collection,
            collection.ToArray(),
            children.ToArray()));
        transaction.Commit();
    }

    /// <summary>
    /// Resizes the design root and records Anchor-derived descendant bounds in the same undo unit.
    /// </summary>
    /// <param name="size">The new root size in logical pixels.</param>
    public void ResizeDesignRoot(DesignSize size)
    {
        using var transaction = Transactions.Begin($"Resize {Document.FormName}");
        var snapshot = DesignerModelMutationSnapshot.CaptureDocumentLayout(Document);
        try
        {
            new DesignerLayoutEngine().ResizeRoot(Document, size);
        }
        finally
        {
            snapshot.RecordChanges(Transactions);
        }
        transaction.Commit();
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
        ThrowIfDisposed();
        var description = SelectedNode is null ? "Change design root properties" : $"Change {SelectedNode.Name} properties";
        using var transaction = Transactions.Begin(description);
        var snapshot = DesignerModelMutationSnapshot.CaptureSelected(
            this,
            includeDescendantState: SelectedNode is null);

        try
        {
            if (SelectedNode is null)
            {
                Document.FormName = string.IsNullOrWhiteSpace(name) ? Document.FormName : name.Trim();
                new DesignerLayoutEngine().ResizeRoot(
                    Document,
                    new DesignSize(Math.Max(1, width), Math.Max(1, height)));
            }
            else
            {
                var node = SelectedNode;
                node.Name = string.IsNullOrWhiteSpace(name) ? node.Name : name.Trim();
                node.Bounds = new DesignBounds(x, y, Math.Max(1, width), Math.Max(1, height));
                node.MemberVisibility = memberVisibility;
                node.Properties["Text"] = DesignPropertyValue.FromString(text);
            }
        }
        finally
        {
            snapshot.RecordChanges(Transactions);
        }

        transaction.Commit();
        Log(SelectedNode is null ? $"Updated form {Document.FormName}." : $"Updated {SelectedNode.Name}.");
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

    internal bool IsProjectUserControlType(string typeName)
        => projectUserControls.Any(control => DesignerProjectUserControlDiscovery.Matches(control, typeName));

    private DesignControlNode? GetComponentBoundary(DesignControlNode? node)
    {
        if (node is null)
            return null;

        var boundary = IsProjectUserControlType(node.TypeName) ? node : null;

        for (var parent = FindParent(node); parent is not null; parent = FindParent(parent))
        {
            if (IsProjectUserControlType(parent.TypeName))
                boundary = parent;
        }

        return boundary ?? node;
    }

    internal Type GetRootControlType()
        => Document.RootKind == DesignRootKind.UserControl ? typeof(UserControl) : typeof(Form);

    internal string GetRootTypeName()
        => Document.RootKind == DesignRootKind.UserControl
            ? typeof(UserControl).FullName!
            : typeof(Form).FullName!;

    internal DesignControlNode? FindParent(DesignControlNode node)
        => TryFindNodeWithParent(node, Document.Controls, parent: null, out var parentNode, out _, out _)
            ? parentNode
            : null;

    private void EnsureNodeBelongsToDocument(DesignControlNode node)
    {
        ThrowIfDisposed();
        if (!TryFindNode(node, out _, out _))
            throw new InvalidOperationException($"Designer node '{node.Name}' is not part of the active document.");
    }

    /// <summary>
    /// Reports a model change made outside the transaction-aware Designer editing APIs.
    /// </summary>
    /// <remarks>
    /// Core Designer operations use <see cref="Transactions"/> and do not call this method. An
    /// external direct mutation cannot be reconstructed safely, so reporting one clears stale
    /// undo/redo units and leaves the document dirty. Extensions should prefer a scoped
    /// transaction plus the session's transaction-aware mutation helpers.
    /// </remarks>
    public void NotifyDocumentChanged()
        => Transactions.InvalidateForExternalMutation();

    internal void ReplaceDocument(DesignDocument document, string description)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ThrowIfDisposed();
        DesignerSpecialContainers.NormalizeDocument(document);

        using var transaction = Transactions.Begin(description);
        Transactions.ExecuteChange(new DesignerDocumentReplaceChange(Host, Document, document));
        transaction.Commit();
    }

    internal void NotifyCommittedModelState(string statusMessage)
    {
        SynchronizeCommittedModelState();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus(statusMessage);
    }

    internal void NotifyRolledBackModelState(string description)
    {
        SynchronizeCommittedModelState();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        DocumentTabsChanged?.Invoke(this, EventArgs.Empty);
        environment?.ReportStatus($"Rolled back {description}.");
    }

    internal void SynchronizeCommittedModelState()
    {
        if (activeDocument is not null)
            activeDocument.Document = Host.Document;

        RefreshDirtyState();
    }

    internal void RefreshDirtyState()
        => IsDirty = CurrentHistory.IsDirty;

    internal void IncrementActiveRevisionGeneration()
    {
        if (activeDocument is not null)
            activeDocument.RevisionGeneration++;
    }

    internal void SetHistoryLimit(int value)
    {
        historyLimit = value;
        detachedHistory.SetLimit(value);
        foreach (var document in openDocuments)
            document.History.SetLimit(value);

        RefreshDirtyState();
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
        DesignControlNode? parentNode,
        DesignerLayoutResult layout,
        DesignControlNode draggedNode,
        DesignPoint point,
        DesignBounds clip)
    {
        foreach (var index in GetFrontToBackIndices(nodes.Count, parentNode))
        {
            var node = nodes[index];

            if (ReferenceEquals(node, draggedNode) || ContainsDescendant(draggedNode, node))
                continue;

            var bounds = layout.GetEffectiveBounds(node);
            var visible = Intersect(bounds, clip);

            if (!visible.Contains(point.X, point.Y))
                continue;

            if (!IsProjectUserControlType(node.TypeName))
            {
                var childResult = FindDeepestContainerAtPoint(node.Children, node, layout, draggedNode, point, visible);

                if (childResult is not null)
                    return childResult;
            }

            if (IsContainerNode(node))
                return node;
        }

        return null;
    }

    private static IEnumerable<int> GetFrontToBackIndices(int count, DesignControlNode? parentNode)
    {
        if (parentNode is not null && PreservesSequentialChildOrder(parentNode))
        {
            for (var index = count - 1; index >= 0; index--)
                yield return index;

            yield break;
        }

        for (var index = 0; index < count; index++)
            yield return index;
    }

    private static bool PreservesSequentialChildOrder(DesignControlNode node)
        => DesignerSpecialContainers.IsFlowLayoutPanel(node)
        || DesignerSpecialContainers.IsTableLayoutPanel(node)
        || DesignerSpecialContainers.IsTabControl(node);

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

    private bool TryResolvePasteTarget(
        DesignControlNode clipboardRoot,
        out DesignerPasteTarget target,
        out string? error)
    {
        if (IsContainerNode(SelectedNode))
        {
            var selectedContainer = SelectedNode!;
            DesignControlCollection? collection;

            if (DesignerSpecialContainers.IsTabControl(selectedContainer))
            {
                collection = DesignerSpecialContainers.IsTabPage(clipboardRoot)
                    ? selectedContainer.Children
                    : DesignerSpecialContainers.GetSelectedTabPage(selectedContainer)?.Children;
            }
            else if (DesignerSpecialContainers.IsSplitContainer(selectedContainer))
                collection = DesignerSpecialContainers.GetPanel1(selectedContainer)?.Children;
            else
                collection = selectedContainer.Children;

            if (collection is null || !TryFindParentForCollection(collection, out var actualParent))
            {
                target = default;
                error = $"Cannot paste into {selectedContainer.Name}: its structural child container is unavailable.";
                return false;
            }

            target = new DesignerPasteTarget(
                collection,
                actualParent,
                actualParent is null ? Document.FormName : DesignerSpecialContainers.GetOutlineName(actualParent),
                collection.Count);
            return CanInsertNode(clipboardRoot, target, out error);
        }

        if (SelectedNode is not null)
        {
            if (!TryFindNodeWithParent(
                SelectedNode,
                Document.Controls,
                parent: null,
                out var parent,
                out var collection,
                out _))
            {
                target = default;
                error = "The selected paste target is not attached to the active document.";
                return false;
            }

            target = new DesignerPasteTarget(collection, parent, parent?.Name ?? Document.FormName, collection.Count);
            return CanInsertNode(clipboardRoot, target, out error);
        }

        target = new DesignerPasteTarget(Document.Controls, Parent: null, Document.FormName, Document.Controls.Count);
        return CanInsertNode(clipboardRoot, target, out error);
    }

    private DesignControlCollection? GetChildCollectionForNewControl(DesignControlNode? parent, out string targetName)
    {
        if (parent is null)
        {
            targetName = Document.FormName;
            return null;
        }

        DesignerSpecialContainers.EnsureSpecialChildren(parent);

        if (DesignerSpecialContainers.IsTabControl(parent)
            && DesignerSpecialContainers.GetSelectedTabPage(parent) is { } selectedPage)
        {
            targetName = DesignerSpecialContainers.GetOutlineName(selectedPage);
            return selectedPage.Children;
        }

        if (DesignerSpecialContainers.IsSplitContainer(parent)
            && DesignerSpecialContainers.GetPanel1(parent) is { } panel1)
        {
            targetName = DesignerSpecialContainers.GetOutlineName(panel1);
            return panel1.Children;
        }

        targetName = DesignerSpecialContainers.GetOutlineName(parent);
        return parent.Children;
    }

    private void AssignDefaultTableLayoutPosition(DesignControlNode node, DesignControlNode? parent)
    {
        if (parent is null || !DesignerSpecialContainers.IsTableLayoutPanel(parent))
            return;

        var columns = Math.Max(1, DesignerSpecialContainers.GetInt(parent, DesignerSpecialContainers.ColumnCountPropertyName, 2));
        var rows = Math.Max(1, DesignerSpecialContainers.GetInt(parent, DesignerSpecialContainers.RowCountPropertyName, 2));
        var existingChildren = parent.Children.Where(child => !ReferenceEquals(child, node)).ToArray();
        var used = existingChildren
            .Select(child => (
                Column: DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableColumnPropertyName, 0),
                Row: DesignerSpecialContainers.GetInt(child, DesignerSpecialContainers.TableRowPropertyName, 0)))
            .ToHashSet();

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (used.Contains((column, row)))
                    continue;

                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnPropertyName, column);
                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowPropertyName, row);
                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnSpanPropertyName, 1);
                DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowSpanPropertyName, 1);
                return;
            }
        }

        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnPropertyName, existingChildren.Length % columns);
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowPropertyName, Math.Clamp(existingChildren.Length / columns, 0, rows - 1));
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnSpanPropertyName, 1);
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowSpanPropertyName, 1);
    }

    private static void AssignTableLayoutPositionFromPoint(
        DesignControlNode node,
        DesignControlNode? parent,
        DesignBounds parentBounds,
        DesignPoint documentPoint)
    {
        if (parent is null || !DesignerSpecialContainers.IsTableLayoutPanel(parent))
            return;

        var columns = Math.Max(1, DesignerSpecialContainers.GetInt(parent, DesignerSpecialContainers.ColumnCountPropertyName, 2));
        var rows = Math.Max(1, DesignerSpecialContainers.GetInt(parent, DesignerSpecialContainers.RowCountPropertyName, 2));
        var columnWidth = Math.Max(1, parentBounds.Width / columns);
        var rowHeight = Math.Max(1, parentBounds.Height / rows);
        var column = Math.Clamp((documentPoint.X - parentBounds.X) / columnWidth, 0, columns - 1);
        var row = Math.Clamp((documentPoint.Y - parentBounds.Y) / rowHeight, 0, rows - 1);

        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnPropertyName, column);
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowPropertyName, row);
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableColumnSpanPropertyName, 1);
        DesignerSpecialContainers.SetInt(node, DesignerSpecialContainers.TableRowSpanPropertyName, 1);
    }

    private bool CanUseSelectedNodeAsClipboardSource()
        => !disposed
        && SelectedNode is { } selected
        && !DesignerSpecialContainers.IsSpecialGeneratedPart(selected)
        && TryFindNode(selected, out _, out _);

    private bool TryGetClipboardSource(out DesignControlNode source, out string? error)
    {
        if (SelectedNode is null)
        {
            source = null!;
            error = "The Form or UserControl design root cannot be copied as a child control.";
            return false;
        }

        source = SelectedNode;
        if (DesignerSpecialContainers.IsSpecialGeneratedPart(source))
        {
            error = $"{DesignerSpecialContainers.GetOutlineName(source)} is structural SplitContainer data and cannot be copied independently.";
            return false;
        }

        if (!TryFindNode(source, out _, out _))
        {
            error = $"Cannot copy {source.Name}: the node is not in the active document.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryCreateDetachedClone(
        DesignControlNode source,
        out DesignControlNode? clone,
        out string? error)
    {
        if (!DesignerClipboardSerializer.TrySerialize(source, out var content, out error))
        {
            clone = null;
            return false;
        }

        return DesignerClipboardSerializer.TryDeserialize(content, out clone, out error);
    }

    private bool TryPrepareInsertion(
        DesignControlNode node,
        DesignerPasteTarget target,
        out string? error)
    {
        if (!CanInsertNode(node, target, out error))
            return false;

        if (!DesignerControlReferenceGuard.CanReferenceTree(Document, node, CurrentProjectPath, out error))
            return false;

        var usedNames = GetUsedControlNames();
        RemapClipboardNames(node, parent: null, usedNames);
        ApplyClipboardPositioning(node, target.Parent);
        return true;
    }

    private bool CanInsertNode(
        DesignControlNode node,
        DesignerPasteTarget target,
        out string? error)
    {
        if (DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            error = "SplitContainer panel nodes are structural Designer data and cannot be pasted independently.";
            return false;
        }

        if (target.Parent is not null && !IsContainerNode(target.Parent))
        {
            error = $"Cannot paste into {target.DisplayName}: the target does not accept child controls.";
            return false;
        }

        if (DesignerSpecialContainers.IsTabPage(node)
            && (target.Parent is null || !DesignerSpecialContainers.IsTabControl(target.Parent)))
        {
            error = "A TabPage can only be duplicated inside its owning TabControl.";
            return false;
        }

        if (target.Parent is not null
            && DesignerSpecialContainers.IsTabControl(target.Parent)
            && !DesignerSpecialContainers.IsTabPage(node))
        {
            error = "Only TabPage nodes can be direct children of a TabControl.";
            return false;
        }

        if (target.Parent is not null && DesignerSpecialContainers.IsSplitContainer(target.Parent))
        {
            error = "Controls must be pasted into a SplitContainer panel rather than directly into the SplitContainer.";
            return false;
        }

        error = null;
        return true;
    }

    private void ApplyClipboardPositioning(DesignControlNode node, DesignControlNode? parent)
    {
        if (parent is not null && DesignerSpecialContainers.IsTableLayoutPanel(parent))
        {
            AssignDefaultTableLayoutPosition(node, parent);
            return;
        }

        // Flow, table, and tab containers own child placement. Docked controls likewise derive
        // their final position from layout, so applying a visual nudge would only persist noise.
        if (parent is not null && PreservesSequentialChildOrder(parent))
            return;
        if (DesignerSpecialContainers.GetEnum(node, "Dock", DockStyle.None) != DockStyle.None)
            return;

        node.Bounds = new DesignBounds(
            node.Bounds.X + 16,
            node.Bounds.Y + 16,
            node.Bounds.Width,
            node.Bounds.Height);
    }

    private void RemapClipboardNames(
        DesignControlNode node,
        DesignControlNode? parent,
        HashSet<string> usedNames)
    {
        if (parent is not null
            && DesignerSpecialContainers.IsSplitContainer(parent)
            && DesignerSpecialContainers.IsSpecialGeneratedPart(node))
        {
            var panelNumber = DesignerSpecialContainers.IsSplitPanel1(node) ? 1 : 2;
            node.Name = ReservePreferredName($"{parent.Name}Panel{panelNumber}", node.TypeName, usedNames);
            node.Properties[DesignNodeRoleNames.DisplayNamePropertyName] =
                DesignPropertyValue.FromString($"{parent.Name}.Panel{panelNumber}");
        }
        else
        {
            node.Name = CreateUniqueControlName(node.Name, node.TypeName, usedNames);
            usedNames.Add(node.Name);
        }

        foreach (var child in node.Children)
            RemapClipboardNames(child, node, usedNames);
    }

    private HashSet<string> GetUsedControlNames()
        => EnumerateNodes()
            .Select(item => item.Node.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string CreateUniqueControlName(string currentName, string typeName, HashSet<string> usedNames)
    {
        var fallbackTypeName = GetShortTypeName(typeName);
        var sanitized = SanitizeIdentifierBase(string.IsNullOrWhiteSpace(currentName)
            ? char.ToLowerInvariant(fallbackTypeName[0]) + fallbackTypeName[1..]
            : currentName);
        var digitStart = sanitized.Length;
        while (digitStart > 0 && char.IsDigit(sanitized[digitStart - 1]))
            digitStart--;

        var baseName = digitStart == 0 ? "control" : sanitized[..digitStart];
        var firstSuffix = digitStart < sanitized.Length
            && int.TryParse(sanitized[digitStart..], out var existingSuffix)
                ? Math.Max(1, existingSuffix + 1)
                : 1;

        for (var suffix = firstSuffix; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseName}{suffix}";

            if (!usedNames.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"Cannot create a unique designer name for '{currentName}'.");
    }

    private static string ReservePreferredName(string preferredName, string typeName, HashSet<string> usedNames)
    {
        var sanitized = SanitizeIdentifierBase(preferredName);
        if (!usedNames.Contains(sanitized))
        {
            usedNames.Add(sanitized);
            return sanitized;
        }

        var unique = CreateUniqueControlName(sanitized, typeName, usedNames);
        usedNames.Add(unique);
        return unique;
    }

    private static string SanitizeIdentifierBase(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "control" : value.Trim();
        var chars = text
            .Select((character, index) =>
                (index == 0 ? char.IsLetter(character) || character == '_' : char.IsLetterOrDigit(character) || character == '_')
                    ? character
                    : '_')
            .ToArray();
        var result = new string(chars);

        return DesignDocumentValidator.IsValidCSharpIdentifier(result)
            ? result
            : $"control_{result}";
    }

    private int CountControls(string typeName)
    {
        var shortTypeName = GetShortTypeName(typeName);
        var prefix = char.ToLowerInvariant(shortTypeName[0]) + shortTypeName[1..];
        return EnumerateNodes().Count(item => item.Node.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static DesignBounds GetDefaultBounds(string typeName, DesignControlNode? parent, int index)
    {
        typeName = GetShortTypeName(typeName);
        var offset = Math.Min(index, 8) * 12;
        var originX = parent is null ? 80 : 24;
        var originY = parent is null ? 80 : 24;

        return typeName switch
        {
            "Panel" => new DesignBounds(120 + offset, 95 + offset, 240, 120),
            "FlowLayoutPanel" => new DesignBounds(120 + offset, 95 + offset, 260, 130),
            "TableLayoutPanel" => new DesignBounds(120 + offset, 95 + offset, 260, 130),
            "SplitContainer" => new DesignBounds(120 + offset, 95 + offset, 300, 160),
            "TabControl" => new DesignBounds(120 + offset, 95 + offset, 320, 180),
            "Label" => new DesignBounds(originX + offset, originY + offset, 100, 24),
            "TextBox" => new DesignBounds(originX + offset, originY + offset, 150, 25),
            "RichTextBox" => new DesignBounds(originX + offset, originY + offset, 220, 96),
            "Line" => new DesignBounds(originX + offset, originY + offset, 120, 40),
            "Ellipse" or "Circle" or "Polygon" or "Polyline" or "Path" => new DesignBounds(originX + offset, originY + offset, 120, 90),
            _ => new DesignBounds(originX + offset, originY + offset, 90, 28)
        };
    }

    private static void InitializeShapeDefaults(DesignControlNode node, string shortTypeName)
    {
        if (shortTypeName is not ("Ellipse" or "Circle" or "Line" or "Polygon" or "Polyline" or "Path"))
            return;

        node.Properties.Remove("Text");
        var stroke = new SolidColorBrush(new SKColor(0x31, 0x5B, 0xA6));
        node.Properties["Stroke"] = DesignerPropertyValueEditor.ToDesignPropertyValue(stroke, typeof(Brush));
        node.Properties["StrokeThickness"] = DesignPropertyValue.FromDouble(2);

        if (shortTypeName is "Ellipse" or "Circle" or "Polygon" or "Path")
        {
            var fill = new SolidColorBrush(new SKColor(0x70, 0xA1, 0xF5, 0x88));
            node.Properties["Fill"] = DesignerPropertyValueEditor.ToDesignPropertyValue(fill, typeof(Brush));
        }

        switch (shortTypeName)
        {
            case "Line":
                node.Properties["StartPoint"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new System.Drawing.PointF(8, 20), typeof(System.Drawing.PointF));
                node.Properties["EndPoint"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new System.Drawing.PointF(112, 20), typeof(System.Drawing.PointF));
                break;
            case "Polygon":
                node.Properties["Points"] = DesignerPropertyValueEditor.ToDesignPropertyValue(
                    new PointCollection([new(60, 6), new(114, 82), new(6, 82)]),
                    typeof(PointCollection));
                break;
            case "Polyline":
                node.Properties["Points"] = DesignerPropertyValueEditor.ToDesignPropertyValue(
                    new PointCollection([new(6, 68), new(32, 24), new(62, 62), new(88, 18), new(114, 50)]),
                    typeof(PointCollection));
                break;
            case "Path":
                var geometry = new PathGeometry();
                var figure = new PathFigure(new System.Drawing.PointF(8, 72), isClosed: true);
                figure.Segments.Add(new QuadraticBezierSegment(new System.Drawing.PointF(30, 4), new System.Drawing.PointF(60, 34)));
                figure.Segments.Add(new BezierSegment(new System.Drawing.PointF(78, 4), new System.Drawing.PointF(110, 20), new System.Drawing.PointF(112, 72)));
                geometry.Figures.Add(figure);
                node.Properties["Data"] = DesignerPropertyValueEditor.ToDesignPropertyValue(geometry, typeof(Geometry));
                break;
        }
    }

    private static string GetDefaultText(string typeName, int index)
        => GetShortTypeName(typeName) switch
        {
            "Button" => $"button{index}",
            "Label" => $"label{index}",
            "TextBox" => $"textBox{index}",
            "RichTextBox" => $"richTextBox{index}",
            "TabPage" => $"tabPage{index}",
            _ => string.Empty
        };

    private static string GetShortTypeName(string typeName)
    {
        var normalized = DesignerProjectUserControlDiscovery.NormalizeTypeName(typeName);
        var separator = normalized.LastIndexOf('.');
        return separator >= 0 ? normalized[(separator + 1)..] : normalized;
    }

    /// <summary>
    /// Rolls back an active edit and releases all retained undo/redo model references.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        Exception? rollbackNotificationException = null;
        try
        {
            Transactions.RollbackActiveTransactionForDisposal();
        }
        catch (Exception ex) when (!Transactions.HasActiveTransaction)
        {
            // A completed rollback observer must not prevent deterministic history and event
            // cleanup. Preserve the observer exception and rethrow it after disposal finishes.
            rollbackNotificationException = ex;
        }

        Transactions.ReleaseObservers();
        Host.Selection.SelectionChanged -= HostSelection_SelectionChanged;
        clipboard.Changed -= Clipboard_Changed;
        detachedHistory.Clear(preserveDirtyState: false);
        foreach (var document in openDocuments)
            document.History.Clear(preserveDirtyState: false);

        openDocuments.Clear();
        clipboard.Clear();
        activeDocument = null;
        DocumentChanged = null;
        SelectionChanged = null;
        ClipboardChanged = null;
        OutputChanged = null;
        PointerPositionChanged = null;
        SettingsChanged = null;
        DocumentTabsChanged = null;
        DocumentOpened = null;
        DocumentClosed = null;
        DocumentPathChanged = null;
        DocumentBaselineChanged = null;
        disposed = true;
        GC.SuppressFinalize(this);

        if (rollbackNotificationException is not null)
            ExceptionDispatchInfo.Capture(rollbackNotificationException).Throw();
    }

    private void HostSelection_SelectionChanged(object? sender, EventArgs e)
    {
        // Transaction commit, rollback, undo, and redo publish one consolidated selection update
        // after their model/history state is atomic. Forwarding the host event in the middle of
        // those operations would let an observer exception interrupt that atomic boundary.
        if (Transactions.HasActiveTransaction || Transactions.IsReplaying)
            return;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Clipboard_Changed(object? sender, EventArgs e)
        => ClipboardChanged?.Invoke(this, EventArgs.Empty);

    private readonly record struct DesignerPasteTarget(
        DesignControlCollection Collection,
        DesignControlNode? Parent,
        string DisplayName,
        int Index);

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(disposed, this);

    private static DesignControlNode? FindNodeByName(IEnumerable<DesignControlNode> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Name, name, StringComparison.Ordinal))
                return node;

            var descendant = FindNodeByName(node.Children, name);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}

internal sealed class DesignerOpenDocument
{
    public DesignerOpenDocument(
        DesignDocument document,
        string? path,
        string? projectPath,
        int historyLimit,
        bool initiallyDirty)
    {
        Id = Guid.NewGuid();
        Document = document;
        Path = path;
        ProjectPath = projectPath;
        History = new DesignerHistory(historyLimit, initiallyDirty);
    }

    public Guid Id { get; }

    public DesignDocument Document { get; set; }

    public string? Path { get; set; }

    public string? ProjectPath { get; set; }

    public DesignerHistory History { get; }

    public long RevisionGeneration { get; set; }

    public bool IsDirty => History.IsDirty;

    public string DisplayName => string.IsNullOrWhiteSpace(Path)
        ? $"{Document.ClassName}.mfdesign"
        : System.IO.Path.GetFileName(Path);
}

internal sealed class DesignerOpenDocumentEventArgs(DesignerOpenDocument document) : EventArgs
{
    public DesignerOpenDocument Document { get; } = document;
}

internal sealed class DesignerDocumentPathChangedEventArgs(
    DesignerOpenDocument document,
    string? oldPath,
    string newPath) : EventArgs
{
    public DesignerOpenDocument Document { get; } = document;

    public string? OldPath { get; } = oldPath;

    public string NewPath { get; } = newPath;
}
