using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerTabPageCollectionDialog : Form
{
    private readonly DesignerSession session;
    private readonly DesignControlNode tabControl;
    private readonly List<PageEntry> pages = [];
    private readonly ListBox pageList;
    private readonly TextBox nameTextBox;
    private readonly TextBox textTextBox;
    private readonly Label errorLabel;

    public DesignerTabPageCollectionDialog(DesignerSession session, DesignControlNode tabControl)
    {
        this.session = session;
        this.tabControl = tabControl;

        Text = "TabPage Collection Editor";
        Name = "DesignerTabPageCollectionDialog";
        Size = new System.Drawing.Size(680, 460);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label
        {
            Left = 18,
            Top = 16,
            Width = 220,
            Height = 22,
            Text = "Members"
        });

        pageList = Controls.Add(new ListBox
        {
            Left = 18,
            Top = 42,
            Width = 245,
            Height = 285
        });

        var addButton = Controls.Add(new Button
        {
            Left = 18,
            Top = 338,
            Width = 76,
            Height = 30,
            Text = "Add"
        });

        var removeButton = Controls.Add(new Button
        {
            Left = 102,
            Top = 338,
            Width = 76,
            Height = 30,
            Text = "Remove"
        });

        var upButton = Controls.Add(new Button
        {
            Left = 270,
            Top = 74,
            Width = 38,
            Height = 30,
            Text = "Up"
        });

        var downButton = Controls.Add(new Button
        {
            Left = 270,
            Top = 110,
            Width = 38,
            Height = 30,
            Text = "Down"
        });

        Controls.Add(new Label
        {
            Left = 328,
            Top = 16,
            Width = 250,
            Height = 22,
            Text = "Selected TabPage"
        });

        Controls.Add(new Label
        {
            Left = 328,
            Top = 54,
            Width = 90,
            Height = 24,
            Text = "(Name)"
        });

        nameTextBox = Controls.Add(new TextBox
        {
            Left = 426,
            Top = 50,
            Width = 210,
            Height = 26
        });

        Controls.Add(new Label
        {
            Left = 328,
            Top = 90,
            Width = 90,
            Height = 24,
            Text = "Text"
        });

        textTextBox = Controls.Add(new TextBox
        {
            Left = 426,
            Top = 86,
            Width = 210,
            Height = 26
        });

        errorLabel = Controls.Add(new Label
        {
            Left = 328,
            Top = 124,
            Width = 308,
            Height = 62,
            Text = string.Empty
        });

        var okButton = Controls.Add(new Button
        {
            Left = 456,
            Top = 374,
            Width = 84,
            Height = 30,
            Text = "OK"
        });

        var cancelButton = Controls.Add(new Button
        {
            Left = 552,
            Top = 374,
            Width = 84,
            Height = 30,
            Text = "Cancel"
        });

        foreach (var page in tabControl.Children.Where(DesignerSpecialContainers.IsTabPage))
        {
            pages.Add(new PageEntry(page, page.Name, GetText(page)));
        }

        RefreshList();

        pageList.SelectedIndexChanged += (_, _) => LoadSelectedPage();
        nameTextBox.TextChanged += (_, _) => UpdateSelectedEntry();
        textTextBox.TextChanged += (_, _) => UpdateSelectedEntry();
        addButton.Click += (_, _) => AddPage();
        removeButton.Click += (_, _) => RemoveSelectedPage();
        upButton.Click += (_, _) => MoveSelectedPage(-1);
        downButton.Click += (_, _) => MoveSelectedPage(1);
        okButton.Click += (_, _) => CommitPages();
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        if (pages.Count > 0)
            pageList.SelectedIndex = Math.Clamp(DesignerSpecialContainers.GetSelectedTabIndex(tabControl), 0, pages.Count - 1);
    }

    private void RefreshList()
    {
        var selectedIndex = pageList.SelectedIndex;

        pageList.Items.Clear();

        foreach (var page in pages)
            pageList.Items.Add(page);

        if (pages.Count == 0)
        {
            nameTextBox.Text = string.Empty;
            textTextBox.Text = string.Empty;
            return;
        }

        pageList.SelectedIndex = Math.Clamp(selectedIndex, 0, pages.Count - 1);
    }

    private void LoadSelectedPage()
    {
        if (!TryGetSelectedEntry(out var entry))
            return;

        nameTextBox.Text = entry.Name;
        textTextBox.Text = entry.Text;
    }

    private void UpdateSelectedEntry()
    {
        if (!TryGetSelectedEntry(out var entry))
            return;

        entry.Name = nameTextBox.Text.Trim();
        entry.Text = textTextBox.Text;

        var selectedIndex = pageList.SelectedIndex;
        pageList.Items[selectedIndex] = entry;
        pageList.SelectedIndex = selectedIndex;
    }

    private void AddPage()
    {
        var name = CreateUniquePageName();
        pages.Add(new PageEntry(null, name, name));
        RefreshList();
        pageList.SelectedIndex = pages.Count - 1;
    }

    private void RemoveSelectedPage()
    {
        if (pages.Count <= 1)
        {
            ShowError("A TabControl must keep at least one TabPage in the designer.");
            return;
        }

        if (!TryGetSelectedEntry(out _))
            return;

        pages.RemoveAt(pageList.SelectedIndex);
        RefreshList();
    }

    private void MoveSelectedPage(int delta)
    {
        if (!TryGetSelectedEntry(out var entry))
            return;

        var oldIndex = pageList.SelectedIndex;
        var newIndex = oldIndex + delta;

        if (newIndex < 0 || newIndex >= pages.Count)
            return;

        pages.RemoveAt(oldIndex);
        pages.Insert(newIndex, entry);
        RefreshList();
        pageList.SelectedIndex = newIndex;
    }

    private void CommitPages()
    {
        var validation = ValidatePages();

        if (validation is not null)
        {
            ShowError(validation);
            return;
        }

        var committedPages = new List<DesignControlNode>(pages.Count);

        foreach (var page in pages)
        {
            var node = page.Node ?? DesignerSpecialContainers.CreateTabPage(page.Name, page.Text);
            if (page.Node is null)
            {
                node.Name = page.Name;
                node.Properties["Text"] = DesignPropertyValue.FromString(page.Text);
            }
            else
            {
                session.SetNodeName(node, page.Name);
                session.SetPropertyValue(node, "Text", DesignPropertyValue.FromString(page.Text));
            }

            committedPages.Add(node);
        }

        session.ReplaceChildren(tabControl, committedPages, "Edit Tab Pages");

        session.SetPropertyValue(
            tabControl,
            DesignerSpecialContainers.SelectedIndexPropertyName,
            DesignPropertyValue.FromInt32(Math.Clamp(pageList.SelectedIndex, 0, Math.Max(0, pages.Count - 1))));

        DialogResult = DialogResult.OK;
    }

    private string? ValidatePages()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Name))
                return "TabPage name cannot be empty.";

            if (!DesignDocumentValidator.IsValidCSharpIdentifier(page.Name))
                return $"'{page.Name}' is not a valid C# identifier.";

            if (!names.Add(page.Name))
                return $"Duplicate TabPage name '{page.Name}'.";

            var duplicateDocumentNode = session.EnumerateNodes()
                .Select(item => item.Node)
                .FirstOrDefault(node => !ReferenceEquals(node, page.Node) && string.Equals(node.Name, page.Name, StringComparison.Ordinal));

            if (duplicateDocumentNode is not null)
                return $"A control named '{page.Name}' already exists.";
        }

        return null;
    }

    private void ShowError(string message)
    {
        errorLabel.Text = message;
        session.Log($"TabPage editor: {message}");
    }

    private bool TryGetSelectedEntry(out PageEntry entry)
    {
        if (pageList.SelectedIndex >= 0 && pageList.SelectedIndex < pages.Count)
        {
            entry = pages[pageList.SelectedIndex];
            return true;
        }

        entry = null!;
        return false;
    }

    private string CreateUniquePageName()
    {
        var usedNames = session.EnumerateNodes()
            .Select(item => item.Node.Name)
            .Concat(pages.Select(page => page.Name))
            .ToHashSet(StringComparer.Ordinal);

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"tabPage{index}";

            if (!usedNames.Contains(candidate))
                return candidate;
        }

        return $"tabPage{pages.Count + 1}";
    }

    private static string GetText(DesignControlNode node)
        => node.Properties.TryGetValue("Text", out var value)
            ? value.Value?.ToString() ?? string.Empty
            : node.Name;

    private sealed class PageEntry
    {
        public PageEntry(DesignControlNode? node, string name, string text)
        {
            Node = node;
            Name = name;
            Text = text;
        }

        public DesignControlNode? Node { get; }

        public string Name { get; set; }

        public string Text { get; set; }

        public override string ToString()
            => Name;
    }
}
