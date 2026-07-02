namespace ModernFormsNext.Designer.Services;

internal sealed record DesignerToolboxItem(
    string DisplayName,
    string TypeName,
    string Category,
    string Description,
    bool IsComponent);
