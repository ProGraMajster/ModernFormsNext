using ModernFormsNext;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Panels;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer;

/// <summary>
/// Hosts the reusable ModernFormsNext Form and UserControl designer UI.
/// </summary>
/// <remarks>
/// This control contains the toolbox, document outline, document tab, designer surface,
/// property grid, output panel, status bar, and command toolbar. Standalone applications and
/// future Visual Studio editor panes should host this shell instead of copying designer UI code.
/// </remarks>
public sealed class ModernFormsDesignerShell : Panel
{
    private const int DefaultToolbarHeight = 42;
    private const int StatusHeight = 24;
    private const int TabHeight = 32;
    private const int Gap = 6;

    private readonly ModernFormsDesignerOptions options;
    private readonly DesignerToolbar toolbar;
    private readonly ToolboxPanel toolbox;
    private readonly DocumentOutlinePanel outline;
    private readonly DesignerDocumentTab documentTab;
    private readonly DesignerSurface surface;
    private readonly SolutionExplorerPanel solutionExplorer;
    private readonly DesignerPropertyGrid properties;
    private readonly OutputPanel output;
    private readonly DesignerStatusBar statusBar;
    private readonly DesignerSafetyBanner safetyBanner;
    private readonly DesignerCommandService commands;
    private readonly DesignerPersistenceCoordinator persistence;
    private readonly DesignerDockManager dockManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModernFormsDesignerShell"/> class.
    /// </summary>
    public ModernFormsDesignerShell()
        : this(new ModernFormsDesignerOptions(), environment: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModernFormsDesignerShell"/> class.
    /// </summary>
    /// <param name="options">Optional shell configuration.</param>
    /// <param name="environment">Optional host environment used for status and output routing.</param>
    public ModernFormsDesignerShell(
        ModernFormsDesignerOptions? options,
        IDesignerHostEnvironment? environment = null)
    {
        this.options = options ?? new ModernFormsDesignerOptions();
        Session = new DesignerSession(environment, this.options.InitialControlRenderMode, this.options.HistoryLimit);

        var files = new DesignerFileService(
            environment,
            () => Session.CurrentDocumentPath,
            () => Session.AnimationDefinitions);
        persistence = new DesignerPersistenceCoordinator(Session, files, this.options);
        commands = new DesignerCommandService(Session, files, this.options, persistence);

        Style.BackgroundColor = DesignerColors.AppBackground;

        toolbar = Controls.Add(new DesignerToolbar(commands, Session, this.options));
        safetyBanner = Controls.Add(new DesignerSafetyBanner(persistence, Session));
        toolbox = Controls.Add(new ToolboxPanel(Session, commands, this.options, T("Toolbox"), T("SearchToolbox")));
        outline = Controls.Add(new DocumentOutlinePanel(Session, this.options, T("DocumentOutline"), T("Delete"), T("SearchDocumentOutline")));
        documentTab = Controls.Add(new DesignerDocumentTab(Session, commands));
        surface = Controls.Add(new DesignerSurface(Session));
        solutionExplorer = Controls.Add(new SolutionExplorerPanel(Session, T("SolutionExplorer"), T("NoProjectPath")));
        properties = Controls.Add(new DesignerPropertyGrid(Session, files, T("Properties")));
        output = Controls.Add(new OutputPanel(Session, T("Output")));
        statusBar = Controls.Add(new DesignerStatusBar(Session, this.options, persistence));
        dockManager = new DesignerDockManager(this, this.options, LayoutChildren, Session.Log);
        dockManager.AddWindow(DesignerToolWindowId.Toolbox, T("Toolbox"), toolbox);
        dockManager.AddWindow(DesignerToolWindowId.DocumentOutline, T("DocumentOutline"), outline);
        dockManager.AddWindow(DesignerToolWindowId.SolutionExplorer, T("SolutionExplorer"), solutionExplorer);
        dockManager.AddWindow(DesignerToolWindowId.Properties, T("Properties"), properties);
        dockManager.AddWindow(DesignerToolWindowId.Output, T("Output"), output);

        toolbar.Visible = this.options.ShowToolbar;

        SizeChanged += (_, _) => LayoutChildren();
        persistence.StateChanged += (_, _) => LayoutChildren();
        Session.SettingsChanged += (_, _) =>
        {
            toolbar.RefreshTexts();
            toolbox.SetTitle(T("Toolbox"));
            outline.SetTitle(T("DocumentOutline"));
            solutionExplorer.SetTitle(T("SolutionExplorer"));
            properties.SetTitle(T("Properties"));
            output.SetTitle(T("Output"));
            LayoutChildren();
        };
        LayoutChildren();
    }

    /// <summary>
    /// Gets the active designer session owned by this shell.
    /// </summary>
    public DesignerSession Session { get; }

    /// <summary>
    /// Loads a design document into the shell.
    /// </summary>
    /// <param name="document">The document to load.</param>
    public void LoadDocument(DesignDocument document)
        => Session.LoadDocument(document);

    /// <summary>
    /// Imports a supported generated <c>.Designer.cs</c> file into the active design session.
    /// </summary>
    /// <param name="path">The generated C# designer file to import.</param>
    /// <remarks>
    /// The import path uses the conservative ModernFormsNext reverse parser. Unsupported
    /// C# statements are reported in the output log and are not guessed.
    /// </remarks>
    public void ImportDesignerCode(string path)
        => commands.ImportDesignerCode(path);

    /// <summary>
    /// Atomically saves the active design document to an explicit path and, when enabled,
    /// regenerates its <c>.Designer.cs</c> sibling.
    /// </summary>
    /// <param name="path">The user- or host-selected <c>.mfdesign</c> destination.</param>
    /// <returns><see langword="true"/> only when the canonical save completed successfully.</returns>
    /// <remarks>
    /// This method updates the saved revision only after the write succeeds. It does not silently
    /// overwrite an unresolved external-change conflict at the same canonical path. A successful
    /// canonical save also resolves older recovery copies for the same document identity because
    /// the active in-memory model has become the new user-selected canonical version.
    /// </remarks>
    public bool SaveDocument(string path)
        => commands.SaveDesignDocument(path).Succeeded;

    /// <summary>
    /// Prompts for every dirty document before a hosting window closes.
    /// </summary>
    /// <param name="owner">The window that owns Save, Don't Save, and Cancel dialogs.</param>
    /// <returns><see langword="true"/> when the host may close; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Choosing Don't Save also removes the document's recovery copy. Choosing Cancel leaves
    /// autosave scheduling and file monitoring active.
    /// </remarks>
    public Task<bool> ConfirmCloseAsync(Form owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return commands.ConfirmCloseAllDocuments(owner);
    }

    /// <summary>
    /// Rechecks the active <c>.mfdesign</c> and generated-code files using content fingerprints.
    /// </summary>
    public void CheckForExternalChanges()
        => commands.CheckForExternalChanges();

    /// <summary>
    /// Updates the active document path after a trusted host or project-system rename event.
    /// </summary>
    /// <param name="path">The new <c>.mfdesign</c> path reported by the host.</param>
    /// <remarks>
    /// A raw filesystem rename is deliberately treated as delete plus create. Hosts that know the
    /// project item identity can call this method to preserve that identity and recreate the two
    /// exact file watchers at the new path.
    /// </remarks>
    public void NotifyDocumentRenamed(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var document = Session.ActiveOpenDocument
            ?? throw new InvalidOperationException("There is no active Designer document to rename.");
        Session.UpdateDocumentPath(document, path);
    }

    /// <summary>
    /// Removes recovery state after a host has already received an explicit Don't Save decision.
    /// </summary>
    /// <remarks>
    /// Hosts must not call this method for an abnormal shutdown or an unresolved Cancel decision.
    /// </remarks>
    public void DiscardActiveDocumentRecovery()
    {
        if (Session.ActiveOpenDocument is { } document
            && !persistence.PrepareDocumentForDiscard(document, out var error))
        {
            throw new InvalidOperationException(error ?? "Designer recovery could not be discarded.");
        }
    }

    /// <summary>
    /// Gets the dedicated per-user directory that stores Designer recovery artifacts.
    /// </summary>
    /// <remarks>
    /// The path is intended for diagnostics and support tooling. Recovery metadata never grants
    /// authority to write or delete files outside this owned directory.
    /// </remarks>
    public string RecoveryDirectoryPath => persistence.RecoveryRootPath;

    /// <summary>
    /// Gets a value indicating whether the active document has a recovery copy that has not yet
    /// been resolved by Restore, Keep Recovery, Open Disk, Save As, Discard Recovery, or a
    /// successful canonical save.
    /// </summary>
    /// <remarks>
    /// Hosts must preserve the recovery artifact when this value is <see langword="true"/>. A
    /// clean in-memory document alone does not imply that the unresolved pre-crash version is
    /// obsolete; an explicit recovery action or verified canonical save resolves it.
    /// </remarks>
    public bool HasUnresolvedRecovery => persistence.ActiveDocumentHasUnresolvedRecovery;

    internal DesignerPersistenceCoordinator Persistence => persistence;

    /// <summary>
    /// Processes keyboard shortcuts that operate on the currently selected designer control.
    /// </summary>
    /// <param name="e">The key event raised by the hosting window.</param>
    /// <returns>
    /// <see langword="true"/> when the shortcut was handled by the designer; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Hosts should call this method before dispatching the key event to focused child controls.
    /// The method intentionally ignores shortcuts while a property value editor is active so text
    /// editing keeps normal <c>Delete</c>, clipboard, and undo/redo behavior.
    /// </remarks>
    public bool ProcessDesignerShortcut(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (properties.IsEditingValue)
            return false;

        var handled = false;

        if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None)
        {
            handled = Session.DeleteSelectedNode();
        }
        else if (e.Control && !e.Alt)
        {
            handled = (e.KeyCode, e.Shift) switch
            {
                (Keys.Z, false) => Session.Transactions.CanUndo && commands.Undo("Keyboard"),
                (Keys.Y, false) => Session.Transactions.CanRedo && commands.Redo("Keyboard"),
                (Keys.Z, true) => Session.Transactions.CanRedo && commands.Redo("Keyboard"),
                (Keys.C, false) => Session.CopySelectedNode(),
                (Keys.X, false) => Session.CutSelectedNode(),
                (Keys.V, false) => Session.PasteCopiedNode(),
                (Keys.D, false) => Session.DuplicateSelectedNode(),
                _ => false
            };
        }

        if (!handled)
            return false;

        e.SuppressKeyPress = true;
        InvalidateDesignerViews();
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            commands.Dispose();
            persistence.Dispose();
            Session.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LayoutChildren()
    {
        var toolbarHeight = options.ShowToolbar ? DefaultToolbarHeight : 0;
        var bannerHeight = safetyBanner.Visible ? safetyBanner.Height : 0;

        toolbar.Visible = options.ShowToolbar;
        toolbar.SetBounds(0, 0, Width, toolbarHeight);
        safetyBanner.SetBounds(0, toolbarHeight, Width, bannerHeight);
        statusBar.SetBounds(0, Math.Max(0, Height - StatusHeight), Width, StatusHeight);

        var bodyTop = toolbarHeight + bannerHeight;
        var bodyBottom = Math.Max(bodyTop, Height - StatusHeight);
        var centerBounds = dockManager.Layout(new System.Drawing.Rectangle(0, bodyTop, Width, Math.Max(1, bodyBottom - bodyTop)));

        documentTab.SetBounds(centerBounds.Left, centerBounds.Top, centerBounds.Width, TabHeight);
        surface.SetBounds(
            centerBounds.Left,
            centerBounds.Top + TabHeight,
            centerBounds.Width,
            Math.Max(1, centerBounds.Height - TabHeight - Gap));
    }

    private void InvalidateDesignerViews()
    {
        surface.Invalidate();
        outline.Invalidate();
        documentTab.Invalidate();
        properties.Invalidate();
        statusBar.Invalidate();
    }

    private string T(string key) => DesignerText.Get(key, options.Language);
}
