using System.ComponentModel;
using System.Reflection;
using ModernFormsNext;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerToolboxService
{
    private readonly DesignMetadataReader metadataReader = new();

    public IReadOnlyList<DesignerToolboxItem> GetItems(
        IEnumerable<DesignerProjectUserControlInfo>? projectUserControls = null)
    {
        var frameworkAssembly = typeof(Control).Assembly;
        var items = new List<DesignerToolboxItem>();

        foreach (var type in frameworkAssembly.GetTypes().Where(IsToolboxCandidate))
        {
            if (typeof(Control).IsAssignableFrom(type))
            {
                AddControl(items, type);
                continue;
            }

            if (typeof(Component).IsAssignableFrom(type))
                AddComponent(items, type);
        }

        foreach (var control in projectUserControls ?? [])
        {
            items.Add(new DesignerToolboxItem(
                control.Name,
                control.FullName,
                "My Project",
                $"Adds the project UserControl {control.FullName} as one component.",
                IsComponent: false));
        }

        return items
            .OrderBy(item => GetCategoryRank(item.Category))
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.TypeName, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddControl(List<DesignerToolboxItem> items, Type type)
    {
        if (IsExcludedControl(type))
            return;

        var metadata = metadataReader.ReadControl(type);

        if (!metadata.VisibleInToolbox)
            return;

        items.Add(new DesignerToolboxItem(
            metadata.DisplayName,
            type.Name,
            NormalizeCategory(metadata.Category, type, isComponent: false),
            metadata.Description ?? $"Adds a {type.Name} control to the design surface.",
            IsComponent: false));
    }

    private static void AddComponent(List<DesignerToolboxItem> items, Type type)
    {
        if (IsExcludedComponent(type))
            return;

        if (type.GetCustomAttribute<BrowsableAttribute>(inherit: true)?.Browsable == false)
            return;

        var displayName = ReadDisplayName(type) ?? type.Name;
        var category = ReadCategory(type) ?? "Components";
        var description = ReadDescription(type) ?? $"Adds a {type.Name} component to the component tray.";

        items.Add(new DesignerToolboxItem(
            displayName,
            type.Name,
            category,
            description,
            IsComponent: true));
    }

    private static bool IsToolboxCandidate(Type type)
        => type.IsClass
        && type.IsPublic
        && !type.IsAbstract
        && type.GetConstructor(Type.EmptyTypes) is not null
        && type.Assembly == typeof(Control).Assembly
        && (typeof(Control).IsAssignableFrom(type) || typeof(Component).IsAssignableFrom(type));

    private static bool IsExcludedControl(Type type)
        => type == typeof(Form)
        || type == typeof(UserControl)
        || type == typeof(FormTitleBar)
        || type.Name is "ControlAdapter";

    private static bool IsExcludedComponent(Type type)
        => type.Name is "NotifyIconContextMenu" or "NotifyIconMenuItem";

    private static string NormalizeCategory(string? category, Type type, bool isComponent)
    {
        if (!string.IsNullOrWhiteSpace(category))
            return category;

        if (isComponent)
            return "Components";

        return type.Name.Contains("Panel", StringComparison.Ordinal)
            || type.Name.Contains("Tab", StringComparison.Ordinal)
            || type.Name.Contains("Group", StringComparison.Ordinal)
            || type.Name.Contains("Split", StringComparison.Ordinal)
            ? "Containers"
            : "Common";
    }

    private static int GetCategoryRank(string category)
        => category switch
        {
            "Common" => 0,
            "Containers" => 1,
            "Shapes" => 2,
            "Components" => 3,
            _ => 10
        };

    private static string? ReadDisplayName(MemberInfo member)
        => member.GetCustomAttribute<DisplayNameAttribute>(inherit: true)?.DisplayName;

    private static string? ReadCategory(MemberInfo member)
        => member.GetCustomAttribute<CategoryAttribute>(inherit: true)?.Category;

    private static string? ReadDescription(MemberInfo member)
        => member.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description;
}
