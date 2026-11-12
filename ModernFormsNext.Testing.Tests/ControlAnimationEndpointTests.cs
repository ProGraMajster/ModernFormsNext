using System.Drawing;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ControlAnimationEndpointTests
{
    [Theory]
    [InlineData(nameof(Control.Opacity), 0f, 0.99995f, 1f)]
    [InlineData(nameof(Control.Opacity), 1f, 0.00005f, 0f)]
    [InlineData(nameof(Control.TranslationX), 1f, 0.00005f, 0f)]
    [InlineData(nameof(Control.TranslationY), 1f, 0.00005f, 0f)]
    [InlineData(nameof(Control.ScaleX), 0f, 0.99995f, 1f)]
    [InlineData(nameof(Control.ScaleY), 0f, 0.99995f, 1f)]
    [InlineData(nameof(Control.Rotation), 1f, 0.00005f, 0f)]
    [InlineData(nameof(Control.TranslationX), 1f, 12.49995f, 12.5f)]
    [InlineData(nameof(Control.ScaleY), 1f, 1.49995f, 1.5f)]
    public void NearEndpointChangesAreRetainedAndRepaintWithoutChangingLayout(
        string propertyName, float start, float penultimate, float endpoint)
    {
        using var host = ModernFormsTestHost.Create();
        var control = new Control { Bounds = new Rectangle(10, 20, 100, 40) };
        var form = new Form();
        form.Controls.Add(control);
        using var window = host.Show(form, 300, 200);
        var bounds = control.Bounds;
        var property = typeof(Control).GetProperty(propertyName)!;
        property.SetValue(control, start);
        property.SetValue(control, penultimate);
        Assert.Equal(penultimate, property.GetValue(control));
        int invalidations = 0, layouts = 0;
        control.Invalidated += (_, _) => invalidations++;
        control.Layout += (_, _) => layouts++;

        property.SetValue(control, endpoint);

        Assert.Equal(endpoint, property.GetValue(control));
        Assert.True(invalidations > 0);
        Assert.Equal(bounds, control.Bounds);
        Assert.Equal(0, layouts);
        int finalInvalidations = invalidations;
        property.SetValue(control, endpoint);
        Assert.Equal(finalInvalidations, invalidations);
        Assert.Equal(0, layouts);
    }
}
