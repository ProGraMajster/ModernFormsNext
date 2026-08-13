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
    private readonly DesignerCommandService commands;
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
        Session = new DesignerSession(environment, this.options.InitialControlRenderMode);

        var files = new DesignerFileService(
            environment,
            () => Session.CurrentDocumentPath,
            () => Session.AnimationDefinitions);
        commands = new DesignerCommandService(Session, files, this.options);

        Style.BackgroundColor = DesignerColors.AppBackground;

        toolbar = Controls.Add(new DesignerToolbar(commands, this.options));
        toolbox = Controls.Add(new ToolboxPanel(Session, commands, this.options, T("Toolbox"), T("SearchToolbox")));
        outline = Controls.Add(new DocumentOutlinePanel(Session, this.options, T("DocumentOutline"), T("Delete"), T("SearchDocumentOutline")));
        documentTab = Controls.Add(new DesignerDocumentTab(Session));
        surface = Controls.Add(new DesignerSurface(Session));
        solutionExplorer = Controls.Add(new SolutionExplorerPanel(Session, T("SolutionExplorer"), T("NoProjectPath")));
        properties = Controls.Add(new DesignerPropertyGrid(Session, files, T("Properties")));
        output = Controls.Add(new OutputPanel(Session, T("Output")));
        statusBar = Controls.Add(new DesignerStatusBar(Session, this.options));
        dockManager = new DesignerDockManager(this, this.options, LayoutChildren, Session.Log);
        dockManager.AddWindow(DesignerToolWindowId.Toolbox, T("Toolbox"), toolbox);
        dockManager.AddWindow(DesignerToolWindowId.DocumentOutline, T("DocumentOutline"), outline);
        dockManager.AddWindow(DesignerToolWindowId.SolutionExplorer, T("SolutionExplorer"), solutionExplorer);
        dockManager.AddWindow(DesignerToolWindowId.Properties, T("Properties"), properties);
        dockManager.AddWindow(DesignerToolWindowId.Output, T("Output"), output);

        toolbar.Visible = this.options.ShowToolbar;

        SizeChanged += (_, _) => LayoutChildren();
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
    /// editing keeps normal <c>Delete</c>, <c>Ctrl+C</c>, and <c>Ctrl+V</c> behavior.
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
        else if (e.Control && !e.Alt && !e.Shift)
        {
            handled = e.KeyCode switch
            {
                Keys.C => Session.CopySelectedNode(),
                Keys.V => Session.PasteCopiedNode(),
                Keys.D => Session.DuplicateSelectedNode(),
                _ => false
            };
        }

        if (!handled)
            return false;

        e.SuppressKeyPress = true;
        InvalidateDesignerViews();
        return true;
    }

    private void LayoutChildren()
    {
        var toolbarHeight = options.ShowToolbar ? DefaultToolbarHeight : 0;

        toolbar.Visible = options.ShowToolbar;
        toolbar.SetBounds(0, 0, Width, toolbarHeight);
        statusBar.SetBounds(0, Math.Max(0, Height - StatusHeight), Width, StatusHeight);

        var bodyTop = toolbarHeight;
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
