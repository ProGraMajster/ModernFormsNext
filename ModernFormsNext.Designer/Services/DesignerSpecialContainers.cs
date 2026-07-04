using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

internal static class DesignerSpecialContainers
{
    public const string SplitterDistancePropertyName = "SplitterDistance";
    public const string SplitterWidthPropertyName = "SplitterWidth";
    public const string OrientationPropertyName = "Orientation";
    public const string SelectedIndexPropertyName = "SelectedIndex";
    public const string FlowDirectionPropertyName = "FlowDirection";
    public const string WrapContentsPropertyName = "WrapContents";
    public const string ColumnCountPropertyName = "ColumnCount";
    public const string RowCountPropertyName = "RowCount";
    public const string TableColumnPropertyName = "TableColumn";
    public const string TableRowPropertyName = "TableRow";
    public const string TableColumnSpanPropertyName = "TableColumnSpan";
    public const string TableRowSpanPropertyName = "TableRowSpan";

    public static void NormalizeDocument(DesignDocument document)
    {
        foreach (var node in document.Controls)
            NormalizeNode(node);
    }

    public static void NormalizeNode(DesignControlNode node)
    {
        EnsureSpecialChildren(node);

        foreach (var child in node.Children)
            NormalizeNode(child);
    }

    public static void InitializeNewNode(DesignControlNode node)
    {
        if (IsFlowLayoutPanel(node))
        {
            SetEnum(node, FlowDirectionPropertyName, typeof(FlowDirection), nameof(FlowDirection.LeftToRight));
            SetBoolean(node, WrapContentsPropertyName, true);
        }
        else if (IsTableLayoutPanel(node))
        {
            SetInt(node, ColumnCountPropertyName, 2);
            SetInt(node, RowCountPropertyName, 2);
        }
        else if (IsSplitContainer(node))
        {
            SetEnum(node, OrientationPropertyName, typeof(Orientation), nameof(Orientation.Horizontal));
            SetInt(node, SplitterDistancePropertyName, Math.Max(25, node.Bounds.Width / 2));
            SetInt(node, SplitterWidthPropertyName, 5);
            SetInt(node, "Panel1MinimumSize", 25);
            SetInt(node, "Panel2MinimumSize", 25);
            node.Properties["Text"] = DesignPropertyValue.FromString(string.Empty);
        }
        else if (IsTabControl(node))
        {
            SetInt(node, SelectedIndexPropertyName, 0);
            node.Properties["Text"] = DesignPropertyValue.FromString(string.Empty);
        }

        EnsureSpecialChildren(node);
    }

    public static void EnsureSpecialChildren(DesignControlNode node)
    {
        if (IsSplitContainer(node))
            EnsureSplitPanels(node);
        else if (IsTabControl(node))
            EnsureTabPages(node);
    }

    public static bool IsSpecialGeneratedPart(DesignControlNode node)
        => IsSplitPanel(node);

    public static bool IsSplitContainer(DesignControlNode node)
        => IsType(node, "SplitContainer");

    public static bool IsSplitPanel(DesignControlNode node)
        => IsRole(node, DesignNodeRoleNames.SplitContainerPanel1)
        || IsRole(node, DesignNodeRoleNames.SplitContainerPanel2);

    public static bool IsSplitPanel1(DesignControlNode node)
        => IsRole(node, DesignNodeRoleNames.SplitContainerPanel1);

    public static bool IsSplitPanel2(DesignControlNode node)
        => IsRole(node, DesignNodeRoleNames.SplitContainerPanel2);

    public static bool IsTabControl(DesignControlNode node)
        => IsType(node, "TabControl");

    public static bool IsTabPage(DesignControlNode node)
        => IsType(node, "TabPage");

    public static bool IsFlowLayoutPanel(DesignControlNode node)
        => IsType(node, "FlowLayoutPanel");

    public static bool IsTableLayoutPanel(DesignControlNode node)
        => IsType(node, "TableLayoutPanel");

    public static string GetOutlineName(DesignControlNode node)
        => TryGetString(node, DesignNodeRoleNames.DisplayNamePropertyName, out var value) ? value : node.Name;

    public static string GetOutlineType(DesignControlNode node)
        => TryGetString(node, DesignNodeRoleNames.DisplayTypePropertyName, out var value) ? value : node.TypeName;

    public static int GetSelectedTabIndex(DesignControlNode tabControl)
        => Math.Clamp(GetInt(tabControl, SelectedIndexPropertyName, 0), 0, Math.Max(0, tabControl.Children.Count - 1));

    public static DesignControlNode? GetSelectedTabPage(DesignControlNode tabControl)
    {
        if (!IsTabControl(tabControl) || tabControl.Children.Count == 0)
            return null;

        return tabControl.Children[GetSelectedTabIndex(tabControl)];
    }

    public static DesignControlNode? GetPanel1(DesignControlNode splitContainer)
        => splitContainer.Children.FirstOrDefault(IsSplitPanel1);

    public static DesignControlNode? GetPanel2(DesignControlNode splitContainer)
        => splitContainer.Children.FirstOrDefault(IsSplitPanel2);

    public static void AddTabPage(DesignControlNode tabControl, Func<string, bool> nameExists)
    {
        var nextIndex = 1;
        string name;

        do
        {
            name = $"tabPage{nextIndex++}";
        }
        while (nameExists(name));

        var page = CreateTabPage(name, name);
        tabControl.Children.Add(page);
        SetInt(tabControl, SelectedIndexPropertyName, tabControl.Children.Count - 1);
    }

    public static void RemoveTabPage(DesignControlNode tabControl, DesignControlNode page)
    {
        var index = tabControl.Children.IndexOf(page);

        if (index < 0)
            return;

        tabControl.Children.RemoveAt(index);
        SetInt(tabControl, SelectedIndexPropertyName, Math.Clamp(GetSelectedTabIndex(tabControl), 0, Math.Max(0, tabControl.Children.Count - 1)));
    }

    public static int GetInt(DesignControlNode node, string propertyName, int defaultValue)
    {
        if (!node.Properties.TryGetValue(propertyName, out var value))
            return defaultValue;

        return value.Value switch
        {
            int intValue => intValue,
            double doubleValue => (int)doubleValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    public static bool GetBoolean(DesignControlNode node, string propertyName, bool defaultValue)
    {
        if (!node.Properties.TryGetValue(propertyName, out var value))
            return defaultValue;

        return value.Value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    public static TEnum GetEnum<TEnum>(DesignControlNode node, string propertyName, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (!node.Properties.TryGetValue(propertyName, out var value) || value.Value is not string stringValue)
            return defaultValue;

        return Enum.TryParse<TEnum>(stringValue, ignoreCase: false, out var parsed) ? parsed : defaultValue;
    }

    public static void SetInt(DesignControlNode node, string propertyName, int value)
        => node.Properties[propertyName] = DesignPropertyValue.FromInt32(value);

    public static void SetBoolean(DesignControlNode node, string propertyName, bool value)
        => node.Properties[propertyName] = DesignPropertyValue.FromBoolean(value);

    public static void SetEnum(DesignControlNode node, string propertyName, Type enumType, string memberName)
        => node.Properties[propertyName] = DesignPropertyValue.FromEnum(enumType.FullName ?? enumType.Name, memberName);

    public static DesignBounds GetSplitterBounds(DesignControlNode splitContainer, DesignBounds containerBounds)
    {
        var orientation = GetEnum(splitContainer, OrientationPropertyName, Orientation.Horizontal);
        var splitterWidth = Math.Max(1, GetInt(splitContainer, SplitterWidthPropertyName, 5));
        var distance = GetInt(
            splitContainer,
            SplitterDistancePropertyName,
            orientation == Orientation.Horizontal ? containerBounds.Width / 2 : containerBounds.Height / 2);

        return orientation == Orientation.Horizontal
            ? new DesignBounds(containerBounds.X + distance, containerBounds.Y, splitterWidth, containerBounds.Height)
            : new DesignBounds(containerBounds.X, containerBounds.Y + distance, containerBounds.Width, splitterWidth);
    }

    public static bool TryGetString(DesignControlNode node, string propertyName, out string value)
    {
        if (node.Properties.TryGetValue(propertyName, out var propertyValue) && propertyValue.Value is string stringValue)
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static void EnsureSplitPanels(DesignControlNode node)
    {
        var panel1 = node.Children.FirstOrDefault(IsSplitPanel1);
        var panel2 = node.Children.FirstOrDefault(IsSplitPanel2);

        if (panel1 is null)
            node.Children.Insert(0, CreateSplitPanel(node, panelIndex: 1));

        if (panel2 is null)
            node.Children.Insert(Math.Min(node.Children.Count, 1), CreateSplitPanel(node, panelIndex: 2));
    }

    private static void EnsureTabPages(DesignControlNode node)
    {
        if (node.Children.Any(IsTabPage))
            return;

        node.Children.Add(CreateTabPage("tabPage1", "tabPage1"));
        node.Children.Add(CreateTabPage("tabPage2", "tabPage2"));
    }

    private static DesignControlNode CreateSplitPanel(DesignControlNode owner, int panelIndex)
    {
        var role = panelIndex == 1
            ? DesignNodeRoleNames.SplitContainerPanel1
            : DesignNodeRoleNames.SplitContainerPanel2;
        var displayName = $"{owner.Name}.Panel{panelIndex}";

        return new DesignControlNode
        {
            TypeName = "Panel",
            Name = $"{owner.Name}Panel{panelIndex}",
            Bounds = new DesignBounds(0, 0, 1, 1),
            MemberVisibility = DesignerMemberVisibility.None,
            Properties =
            {
                [DesignNodeRoleNames.RolePropertyName] = DesignPropertyValue.FromString(role),
                [DesignNodeRoleNames.DisplayNamePropertyName] = DesignPropertyValue.FromString(displayName),
                [DesignNodeRoleNames.DisplayTypePropertyName] = DesignPropertyValue.FromString("SplitterPanel"),
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None)),
                ["Text"] = DesignPropertyValue.FromString(string.Empty)
            }
        };
    }

    public static DesignControlNode CreateTabPage(string name, string text)
        => new()
        {
            TypeName = "TabPage",
            Name = name,
            Bounds = new DesignBounds(0, 0, 100, 80),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.Fill)),
                ["Text"] = DesignPropertyValue.FromString(text)
            }
        };

    private static bool IsRole(DesignControlNode node, string role)
        => TryGetString(node, DesignNodeRoleNames.RolePropertyName, out var actual)
        && string.Equals(actual, role, StringComparison.Ordinal);

    private static bool IsType(DesignControlNode node, string shortTypeName)
        => string.Equals(node.TypeName, shortTypeName, StringComparison.Ordinal)
        || node.TypeName.EndsWith("." + shortTypeName, StringComparison.Ordinal);
}
