using ModernFormsNext.Designer.Services;

namespace ModernFormsNext.Designer.Properties;

internal sealed class DesignerPropertyDialogContext
{
    public DesignerPropertyDialogContext(
        Form owner,
        DesignerSession session,
        DesignerPropertyDescriptor property)
    {
        Owner = owner;
        Session = session;
        Property = property;
    }

    public Form Owner { get; }

    public DesignerSession Session { get; }

    public DesignerPropertyDescriptor Property { get; }
}
