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
    private readonly bool knownPageOwner;
    private readonly ComboBox? addType;
    private readonly ComboBox? slot;
    private bool loading;

    public DesignerTabPageCollectionDialog(DesignerSession session, DesignControlNode tabControl)
    {
        this.session = session;
        this.tabControl = tabControl;
        knownPageOwner = DesignPageContainers.IsPage(tabControl.TypeName)
            || DesignPageContainers.IsType(tabControl.TypeName, "PageHost");

        Text = knownPageOwner ? "Authored Page Content Editor" : "TabPage Collection Editor";
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
        if (knownPageOwner)
        {
            addType = Controls.Add(new ComboBox { Left = 328, Top = 156, Width = 308, Height = 28 });
            addType.Items.AddRange((DesignPageContainers.IsType(tabControl.TypeName, "ContentPage")
                ? new[] { "Panel", "Button", "Label", "TextBox", "PageHost", "ContentPage", "NavigationPage", "TabbedPage", "FlyoutPage", "AppShell" }
                : new[] { "ContentPage", "NavigationPage", "TabbedPage", "FlyoutPage", "AppShell" }).Cast<object>().ToArray());
            addType.SelectedIndex = 0;
            Controls.Add(new Label { Left = 328, Top = 128, Width = 308, Height = 24, Text = "Type to add (existing nodes keep their type)" });
            if (DesignPageContainers.IsType(tabControl.TypeName, "FlyoutPage"))
            {
                Controls.Add(new Label { Left = 328, Top = 192, Width = 308, Height = 24, Text = "Selected/new slot" });
                slot = Controls.Add(new ComboBox { Left = 328, Top = 220, Width = 308, Height = 28 });
                slot.Items.AddRange(new object[] { "Detail", "Flyout" });
                slot.SelectedIndex = 0;
                slot.SelectedIndexChanged += (_, _) => UpdateSelectedEntry();
            }
            errorLabel.Top = 260;
            errorLabel.Height = 94;
        }

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

        foreach (var page in tabControl.Children.Where(page => knownPageOwner || DesignerSpecialContainers.IsTabPage(page)))
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

        loading = true;
        try
        {
            nameTextBox.Text = entry.Name;
            textTextBox.Text = entry.Text;
            if (slot is not null) slot.SelectedIndex = entry.Role == DesignNodeRoleNames.FlyoutPageFlyout ? 1 : 0;
        }
        finally { loading = false; }
    }

    private void UpdateSelectedEntry()
    {
        if (loading || !TryGetSelectedEntry(out var entry))
            return;

        entry.Name = nameTextBox.Text.Trim();
        entry.Text = textTextBox.Text;
        if (slot is not null) entry.Role = slot.SelectedIndex == 1
            ? DesignNodeRoleNames.FlyoutPageFlyout : DesignNodeRoleNames.FlyoutPageDetail;

        var selectedIndex = pageList.SelectedIndex;
        pageList.Items[selectedIndex] = entry;
        pageList.SelectedIndex = selectedIndex;
    }

    private void AddPage()
    {
        if (pages.Count >= 256) { ShowError("This editor supports at most 256 authored entries."); return; }
        var name = CreateUniquePageName();
        var entry = new PageEntry(null, name, name);
        if (addType is not null && addType.SelectedIndex >= 0)
            entry.TypeName = addType.Items[addType.SelectedIndex].ToString()!;
        if (slot is not null) entry.Role = slot.SelectedIndex == 1
            ? DesignNodeRoleNames.FlyoutPageFlyout : DesignNodeRoleNames.FlyoutPageDetail;
        pages.Add(entry);
        RefreshList();
        pageList.SelectedIndex = pages.Count - 1;
    }

    private void RemoveSelectedPage()
    {
        if (!knownPageOwner && pages.Count <= 1)
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

        using var transaction = session.Transactions.Begin(knownPageOwner ? "Edit authored page content" : "Edit Tab Pages");
        var committedPages = new List<DesignControlNode>(pages.Count);

        foreach (var page in pages)
        {
            var node = page.Node ?? (knownPageOwner
                ? new DesignControlNode { Name = page.Name, TypeName = page.TypeName, Bounds = new DesignBounds(0, 0, 300, 200) }
                : DesignerSpecialContainers.CreateTabPage(page.Name, page.Text));
            var textProperty = DesignPageContainers.IsPage(node.TypeName) ? "Title" : "Text";
            if (page.Node is null)
            {
                node.Name = page.Name;
                node.Properties[textProperty] = DesignPropertyValue.FromString(page.Text);
                if (slot is not null) node.Properties[DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(page.Role);
            }
            else
            {
                session.SetNodeName(node, page.Name);
                session.SetPropertyValue(node, textProperty, DesignPropertyValue.FromString(page.Text));
                if (textProperty == "Title") session.RemovePropertyValue(node, "Text");
                if (slot is not null) session.SetPropertyValue(node, DesignNodeRoleNames.RolePropertyName, DesignPropertyValue.FromString(page.Role));
            }

            committedPages.Add(node);
        }

        session.ReplaceChildren(tabControl, committedPages, "Edit Tab Pages");

        if (!knownPageOwner || DesignPageContainers.IsType(tabControl.TypeName, "TabbedPage")) session.SetPropertyValue(
            tabControl,
            DesignerSpecialContainers.SelectedIndexPropertyName,
            DesignPropertyValue.FromInt32(pages.Count == 0 ? -1 : Math.Clamp(pageList.SelectedIndex, 0, pages.Count - 1)));

        transaction.Commit();
        DialogResult = DialogResult.OK;
    }

    private string? ValidatePages()
    {
        if (knownPageOwner)
        {
            var proposed = new DesignControlNode { TypeName = tabControl.TypeName };
            foreach (var page in pages)
            {
                var candidate = new DesignControlNode { Name = page.Name, TypeName = page.TypeName };
                if (slot is not null) candidate.Properties[DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(page.Role);
                proposed.Children.Add(candidate);
            }
            if (DesignPageContainers.GetChildError(proposed) is { } error) return error;
        }
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
            var candidate = $"{(knownPageOwner ? "pageContent" : "tabPage")}{index}";

            if (!usedNames.Contains(candidate))
                return candidate;
        }

        return $"tabPage{pages.Count + 1}";
    }

    private static string GetText(DesignControlNode node)
        => node.Properties.TryGetValue(DesignPageContainers.IsPage(node.TypeName) && node.Properties.ContainsKey("Title") ? "Title" : "Text", out var value)
            ? value.Value?.ToString() ?? string.Empty
            : node.Name;

    private sealed class PageEntry
    {
        public PageEntry(DesignControlNode? node, string name, string text)
        {
            Node = node;
            Name = name;
            Text = text;
            TypeName = node?.TypeName ?? "TabPage";
            Role = node?.Properties.TryGetValue(DesignNodeRoleNames.RolePropertyName, out var role) == true ? role.GetString() : string.Empty;
        }

        public DesignControlNode? Node { get; }

        public string Name { get; set; }

        public string Text { get; set; }
        public string TypeName { get; set; }
        public string Role { get; set; }

        public override string ToString()
            => Name;
    }
}
