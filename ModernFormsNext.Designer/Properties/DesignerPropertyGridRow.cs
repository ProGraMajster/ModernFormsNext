namespace ModernFormsNext.Designer.Properties;

internal enum DesignerPropertyGridRowKind
{
    Category,
    Property,
    Event
}

internal sealed class DesignerPropertyGridRow
{
    public DesignerPropertyGridRow(string categoryName)
    {
        Kind = DesignerPropertyGridRowKind.Category;
        CategoryName = categoryName;
    }

    public DesignerPropertyGridRow(DesignerPropertyDescriptor property)
    {
        Kind = DesignerPropertyGridRowKind.Property;
        Property = property;
    }

    public DesignerPropertyGridRow(DesignerEventDescriptor eventDescriptor)
    {
        Kind = DesignerPropertyGridRowKind.Event;
        Event = eventDescriptor;
    }

    public DesignerPropertyGridRowKind Kind { get; }

    public string? CategoryName { get; }

    public DesignerPropertyDescriptor? Property { get; }

    public DesignerEventDescriptor? Event { get; }
}
