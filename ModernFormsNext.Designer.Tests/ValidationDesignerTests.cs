using System.ComponentModel;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class ValidationDesignerTests
{
    [Fact]
    public void InheritedValidationEventsAppearThroughRuntimeMetadata()
    {
        using var session = new DesignerSession();
        var document = DesignerSession.CreateDefaultDocument();
        var node = new DesignControlNode { TypeName = "TextBox", Name = "editor", Bounds = new DesignBounds(10, 10, 150, 30) };
        document.Controls.Add(node);
        session.OpenDocument(document, "Validation.mfdesign");
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        grid.SetMode(DesignerPropertyGridMode.Events);
        Assert.Contains(grid.Events, e => e.Name == "Validating");
        Assert.Contains(grid.Events, e => e.Name == "Validated");
        Assert.Equal(typeof(CancelEventHandler), TypeDescriptor.GetEvents(typeof(TextBox))["Validating"]!.EventType);
        Assert.Equal(typeof(EventHandler), TypeDescriptor.GetEvents(typeof(TextBox))["Validated"]!.EventType);
        Assert.Equal(true, ((DefaultValueAttribute)TypeDescriptor.GetProperties(typeof(TextBox))["CausesValidation"]!.Attributes[typeof(DefaultValueAttribute)]!).Value);
    }
}
