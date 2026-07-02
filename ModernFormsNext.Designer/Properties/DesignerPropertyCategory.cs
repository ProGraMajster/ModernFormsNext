namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyCategory
{
    public DesignerPropertyCategory(string name, IReadOnlyList<DesignerPropertyDescriptor> properties)
    {
        Name = name;
        Properties = properties;
    }

    public string Name { get; }

    public IReadOnlyList<DesignerPropertyDescriptor> Properties { get; }
}
