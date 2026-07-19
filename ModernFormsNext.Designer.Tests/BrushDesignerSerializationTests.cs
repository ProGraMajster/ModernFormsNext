using System.Drawing;
using System.Numerics;
using ModernFormsNext.CodeGeneration.Utilities;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;
using MfnBrush = ModernFormsNext.Drawing.Brush;

namespace ModernFormsNext.Designer.Tests;

public sealed class BrushDesignerSerializationTests
{
    [Fact]
    public void LinearGradientRoundTripPreservesExtendedBrushValues()
    {
        var source = new LinearGradientBrush
        {
            Start = new PointF(0.1f, 0.2f),
            End = new PointF(0.8f, 0.9f),
            Opacity = 0.65f,
            Transform = Matrix3x2.CreateTranslation(3f, 4f),
            SpreadMode = GradientSpreadMode.Reflect
        };
        source.GradientStops.AddRange([
            new GradientStop(Color.FromArgb(100, 10, 20, 30), 0.75f),
            new GradientStop(Color.Red, 0.25f)
        ]);

        DesignPropertyValue value = DesignerPropertyValueEditor.ToDesignPropertyValue(source, typeof(MfnBrush));
        var restored = Assert.IsType<LinearGradientBrush>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(MfnBrush)));
        string generated = CSharpLiteralWriter.WriteValue(value);

        Assert.Equal(source.Start, restored.Start);
        Assert.Equal(source.End, restored.End);
        Assert.Equal(source.Opacity, restored.Opacity);
        Assert.Equal(source.Transform, restored.Transform);
        Assert.Equal(source.SpreadMode, restored.SpreadMode);
        Assert.Equal(2, restored.GradientStops.Count);
        Assert.Equal(source.GradientStops[0].Color, restored.GradientStops[0].Color);
        Assert.Contains("Opacity = 0.65f", generated, StringComparison.Ordinal);
        Assert.Contains("GradientSpreadMode.Reflect", generated, StringComparison.Ordinal);
        Assert.Contains("System.Numerics.Matrix3x2", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void RadialGradientRoundTripPreservesFocalOrigin()
    {
        var source = new RadialGradientBrush
        {
            CenterPoint = new PointF(0.6f, 0.4f),
            GradientOrigin = new PointF(0.2f, 0.3f),
            Radius = 0.75f,
            SpreadMode = GradientSpreadMode.Repeat
        };
        source.GradientStops.AddRange([
            new GradientStop(Color.White, 0f),
            new GradientStop(Color.Black, 1f)
        ]);

        DesignPropertyValue value = DesignerPropertyValueEditor.ToDesignPropertyValue(source, typeof(MfnBrush));
        var restored = Assert.IsType<RadialGradientBrush>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(MfnBrush)));
        string generated = CSharpLiteralWriter.WriteValue(value);

        Assert.Equal(source.CenterPoint, restored.CenterPoint);
        Assert.Equal(source.GradientOrigin, restored.GradientOrigin);
        Assert.Equal(source.Radius, restored.Radius);
        Assert.Equal(source.SpreadMode, restored.SpreadMode);
        Assert.Contains("GradientOrigin = new System.Drawing.PointF(0.2f, 0.3f)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyGradientDocumentUsesBackwardCompatibleDefaults()
    {
        var properties = new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
        {
            ["StartPoint"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new SKPoint(0f, 0f), typeof(SKPoint)),
            ["EndPoint"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new SKPoint(1f, 1f), typeof(SKPoint)),
            ["GradientStopCount"] = DesignPropertyValue.FromInt32(0)
        };
        DesignPropertyValue legacy = DesignPropertyValue.FromStructuredObject(
            typeof(LinearGradientBrush).FullName!,
            properties);

        var restored = Assert.IsType<LinearGradientBrush>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(legacy, typeof(MfnBrush)));

        Assert.Equal(1f, restored.Opacity);
        Assert.Equal(Matrix3x2.Identity, restored.Transform);
        Assert.Equal(GradientSpreadMode.Pad, restored.SpreadMode);
    }
}
