using System.Drawing;
using System.Globalization;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.CodeGeneration.Utilities;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class ShapeDesignerTests
{
    [Fact]
    public void ConcreteShapesAppearInDedicatedToolboxCategory()
    {
        IReadOnlyList<DesignerToolboxItem> items = new DesignerToolboxService().GetItems();

        foreach (string type in ShapeTypes)
            Assert.Contains(items, item => item.TypeName == type && item.Category == "Shapes");
        Assert.DoesNotContain(items, item => item.TypeName == nameof(Shape));
    }

    [Theory]
    [MemberData(nameof(GetShapeTypes))]
    public void ShapeCanBeAddedToFormWithVisibleDefaults(string typeName)
    {
        DesignerSession session = CreateBlankSession();

        DesignControlNode node = session.AddControl(typeName);

        Assert.Equal(typeName, node.TypeName);
        Assert.True(node.Bounds.Width >= 120);
        Assert.True(node.Properties.ContainsKey("Stroke"));
        Assert.False(node.Properties.ContainsKey("Text"));
    }

    [Fact]
    public void ShapePropertyGridShowsAppearanceAndGeometryProperties()
    {
        DesignerSession session = CreateBlankSession();
        session.AddControl(nameof(Polygon));
        var state = new DesignerPropertyGridState(session);

        foreach (string name in new[] { "Fill", "Stroke", "StrokeThickness", "StrokeLineCap", "StrokeLineJoin", "MiterLimit", "Points" })
            Assert.Contains(state.Properties, property => property.Name == name);
    }

    [Fact]
    public void LinePropertyGridHidesMeaninglessFill()
    {
        DesignerSession session = CreateBlankSession();
        session.AddControl(nameof(Line));
        var state = new DesignerPropertyGridState(session);

        Assert.DoesNotContain(state.Properties, property => property.Name == "Fill");
        Assert.Contains(state.Properties, property => property.Name == "StartPoint");
        Assert.Contains(state.Properties, property => property.Name == "EndPoint");
    }

    [Fact]
    public void PolylinePropertyGridHidesFillAndComplexValuesHaveDialogEditors()
    {
        DesignerSession session = CreateBlankSession();
        session.AddControl(nameof(Polyline));
        var polylineState = new DesignerPropertyGridState(session);
        DesignerPropertyDescriptor points = Assert.Single(polylineState.Properties, property => property.Name == "Points");

        Assert.DoesNotContain(polylineState.Properties, property => property.Name == "Fill");
        Assert.True(points.HasDialogEditor);

        session.SelectForm();
        session.AddControl(nameof(ModernFormsNext.Path));
        var pathState = new DesignerPropertyGridState(session);
        DesignerPropertyDescriptor data = Assert.Single(pathState.Properties, property => property.Name == "Data");
        Assert.True(data.HasDialogEditor);
    }

    [Fact]
    public void PointCollectionTextAndStructuredValueRoundTrip()
    {
        var source = new PointCollection([new(1.25f, -2), new(8, 9.5f), new(40, 3)]);
        string text = DesignerPropertyValueEditor.ToDisplayString(source);

        Assert.True(DesignerPropertyValueEditor.TryConvert(text, typeof(PointCollection), out object? parsed, out string? error), error);
        Assert.Equal(source, Assert.IsType<PointCollection>(parsed));
        DesignPropertyValue stored = DesignerPropertyValueEditor.ToDesignPropertyValue(source, typeof(PointCollection));
        Assert.Equal(source, Assert.IsType<PointCollection>(DesignerPropertyValueEditor.FromDesignPropertyValue(stored, typeof(PointCollection))));
    }

    [Fact]
    public void PathGeometryTextAndStructuredValueRoundTrip()
    {
        PathGeometry source = CreatePathGeometry();
        string text = DesignerPropertyValueEditor.ToDisplayString(source);

        Assert.True(DesignerPropertyValueEditor.TryConvert(text, typeof(Geometry), out object? textResult, out string? error), error);
        PathGeometry parsedText = Assert.IsType<PathGeometry>(textResult);
        Assert.Equal(2, parsedText.Figures[0].Segments.Count);
        DesignPropertyValue stored = DesignerPropertyValueEditor.ToDesignPropertyValue(source, typeof(Geometry));
        PathGeometry restored = Assert.IsType<PathGeometry>(DesignerPropertyValueEditor.FromDesignPropertyValue(stored, typeof(Geometry)));
        Assert.Equal(source.FillRule, restored.FillRule);
        Assert.Equal(source.Transform, restored.Transform);
        Assert.Equal(source.Figures[0].Segments.Count, restored.Figures[0].Segments.Count);
    }

    [Fact]
    public void ShapeMfdesignSaveLoadPreservesStructuredValues()
    {
        DesignDocument document = CreateShapeDocument();

        string json = DesignDocumentSerializer.Default.Serialize(document);
        DesignDocument restored = DesignDocumentSerializer.Default.Deserialize(json);

        DesignControlNode polygon = Assert.Single(restored.Controls, node => node.TypeName == nameof(Polygon));
        Assert.True(polygon.Properties.ContainsKey("Points"));
        DesignControlNode path = Assert.Single(restored.Controls, node => node.TypeName == nameof(ModernFormsNext.Path));
        Assert.True(path.Properties.ContainsKey("Data"));
        Assert.Contains("ModernFormsNext.Drawing.PathGeometry", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorEmitsReadablePublicShapeApi()
    {
        CSharpDesignerGenerationResult result = new CSharpDesignerGenerator().Generate(CreateShapeDocument());

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        Assert.Contains("new Polygon()", result.Code, StringComparison.Ordinal);
        Assert.Contains("new ModernFormsNext.Drawing.PointCollection", result.Code, StringComparison.Ordinal);
        Assert.Contains("new ModernFormsNext.Drawing.PathGeometry", result.Code, StringComparison.Ordinal);
        Assert.Contains("StrokeThickness = 2", result.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedShapeCodeCompiles()
    {
        string code = new CSharpDesignerGenerator().Generate(CreateShapeDocument()).Code;

        AssertGeneratedCodeCompiles(code);
    }

    [Fact]
    public void ReverseParserRestoresGeneratedShapesAndGeometry()
    {
        string code = new CSharpDesignerGenerator().Generate(CreateShapeDocument()).Code;

        CSharpDesignerParseResult result = new CSharpDesignerParser().Parse(code);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        DesignDocument document = Assert.IsType<DesignDocument>(result.Document);
        DesignControlNode polygon = Assert.Single(document.Controls, node => node.TypeName.EndsWith(nameof(Polygon), StringComparison.Ordinal));
        Assert.True(polygon.Properties.ContainsKey("Points"));
        DesignControlNode path = Assert.Single(document.Controls, node => node.TypeName.EndsWith(nameof(ModernFormsNext.Path), StringComparison.Ordinal));
        Assert.True(path.Properties.ContainsKey("Data"));
    }

    [Fact]
    public void MfdesignCodeParserRoundTripIsStableForShapes()
    {
        var serializer = DesignDocumentSerializer.Default;
        var generator = new CSharpDesignerGenerator();
        DesignDocument document = serializer.Deserialize(serializer.Serialize(CreateShapeDocument()));
        string firstCode = generator.Generate(document).Code;
        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(firstCode);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));

        string secondCode = generator.Generate(Assert.IsType<DesignDocument>(parsed.Document)).Code;

        Assert.Equal(firstCode, secondCode);
    }

    [Fact]
    public void ComplexPathRoundTripPreservesBrushTransformFiguresAndEverySegmentKind()
    {
        DesignerSession session = CreateBlankSession();
        DesignControlNode path = session.AddControl(nameof(ModernFormsNext.Path));
        var fill = new LinearGradientBrush
        {
            Start = new PointF(0.125f, 0.25f),
            End = new PointF(0.875f, 0.75f),
            Transform = Matrix3x2.CreateTranslation(1.5f, -2.25f)
        };
        fill.GradientStops.AddRange([
            new GradientStop(Color.CornflowerBlue, 0.125f),
            new GradientStop(Color.Gold, 0.625f),
            new GradientStop(Color.DarkRed, 1f)
        ]);
        path.Properties["Fill"] = DesignerPropertyValueEditor.ToDesignPropertyValue(fill, typeof(ModernFormsNext.Drawing.Brush));
        path.Properties["Stroke"] = DesignerPropertyValueEditor.ToDesignPropertyValue(new SolidColorBrush(Color.Navy), typeof(ModernFormsNext.Drawing.Brush));
        path.Properties["StrokeThickness"] = DesignPropertyValue.FromDouble(2.75);
        path.Properties["Data"] = DesignerPropertyValueEditor.ToDesignPropertyValue(CreateComplexPathGeometry(), typeof(Geometry));

        var serializer = DesignDocumentSerializer.Default;
        var generator = new CSharpDesignerGenerator();
        DesignDocument stored = serializer.Deserialize(serializer.Serialize(session.Document));
        string firstCode = generator.Generate(stored).Code;
        CSharpDesignerParseResult parsed = new CSharpDesignerParser().Parse(firstCode);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        DesignDocument reversed = Assert.IsType<DesignDocument>(parsed.Document);
        string secondCode = generator.Generate(reversed).Code;

        Assert.Equal(firstCode, secondCode);
        DesignControlNode restoredNode = Assert.Single(reversed.Controls);
        PathGeometry restored = Assert.IsType<PathGeometry>(DesignerPropertyValueEditor.FromDesignPropertyValue(restoredNode.Properties["Data"], typeof(Geometry)));
        Assert.Equal(2, restored.Figures.Count);
        Assert.IsType<LineSegment>(restored.Figures[0].Segments[0]);
        Assert.IsType<QuadraticBezierSegment>(restored.Figures[0].Segments[1]);
        Assert.IsType<BezierSegment>(restored.Figures[0].Segments[2]);
        Assert.True(restored.Figures[0].IsClosed);
        Assert.False(restored.Figures[1].IsClosed);
        Assert.Equal(GeometryFillRule.EvenOdd, restored.FillRule);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("pl-PL")]
    public void GeometryEditingAndCodeGenerationAreCultureInvariant(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            var points = new PointCollection([new(1.5f, -2.25f), new(3.75f, 4.125f)]);
            PathGeometry geometry = CreateComplexPathGeometry();

            string pointText = DesignerPropertyValueEditor.ToDisplayString(points);
            string geometryText = DesignerPropertyValueEditor.ToDisplayString(geometry);
            Assert.True(DesignerPropertyValueEditor.TryConvert(pointText, typeof(PointCollection), out object? restoredPoints, out string? pointError), pointError);
            Assert.True(DesignerPropertyValueEditor.TryConvert(geometryText, typeof(Geometry), out object? restoredGeometry, out string? geometryError), geometryError);
            Assert.True(DesignerPropertyValueEditor.TryConvert("2.75", typeof(float), out object? thickness, out string? thicknessError), thicknessError);

            Assert.Equal(points, Assert.IsType<PointCollection>(restoredPoints));
            Assert.Equal(geometry.Transform, Assert.IsType<PathGeometry>(restoredGeometry).Transform);
            Assert.Equal(2.75f, Assert.IsType<float>(thickness));
            string generated = CSharpLiteralWriter.WriteValue(DesignerPropertyValueEditor.ToDesignPropertyValue(geometry, typeof(Geometry)));
            Assert.Contains("1.5f", generated, StringComparison.Ordinal);
            Assert.DoesNotContain("1,5f", generated, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void RuntimeDesignerPainterRendersActualEllipse()
    {
        using var target = new SKBitmap(100, 80);
        target.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(target);
        using var ellipse = new Ellipse
        {
            Size = new Size(80, 60),
            Fill = new SolidColorBrush(Color.CornflowerBlue)
        };

        bool painted = RuntimeControlPainter.TryPaint(
            new PaintEventArgs(target.Info, canvas, 1),
            ellipse,
            ellipse.Size,
            new Rectangle(10, 10, 80, 60),
            out _,
            out string? error);
        canvas.Flush();

        Assert.True(painted, error);
        Assert.Equal(new SKColor(100, 149, 237), target.GetPixel(50, 40));
    }

    [Fact]
    public void LegacyDocumentWithoutShapesStillRoundTrips()
    {
        const string json = """
            {"schemaVersion":1,"namespace":"Example","className":"MainForm","formName":"Form1","size":{"width":640,"height":480},"controls":[]}
            """;

        DesignDocument document = DesignDocumentSerializer.Default.Deserialize(json);
        CSharpDesignerGenerationResult generated = new CSharpDesignerGenerator().Generate(document);

        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        Assert.Empty(document.Controls);
    }

    [Theory]
    [InlineData(DesignRootKind.Form)]
    [InlineData(DesignRootKind.UserControl)]
    public void ExistingRootKindsGenerateWithoutShapeRegression(DesignRootKind rootKind)
    {
        DesignDocument document = BlankDocument();
        document.RootKind = rootKind;
        document.Controls.Add(new DesignControlNode
        {
            TypeName = nameof(Button),
            Name = "button1",
            Bounds = new DesignBounds(10, 10, 100, 30)
        });

        CSharpDesignerGenerationResult result = new CSharpDesignerGenerator().Generate(document);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        Assert.DoesNotContain("PathGeometry", result.Code, StringComparison.Ordinal);
    }

    public static TheoryData<string> GetShapeTypes()
    {
        var result = new TheoryData<string>();
        foreach (string type in ShapeTypes)
            result.Add(type);
        return result;
    }

    private static readonly string[] ShapeTypes =
    [
        nameof(Ellipse), nameof(Circle), nameof(Line), nameof(Polygon), nameof(Polyline), nameof(ModernFormsNext.Path)
    ];

    private static DesignerSession CreateBlankSession()
    {
        var session = new DesignerSession();
        session.LoadDocument(BlankDocument());
        session.SelectForm();
        return session;
    }

    private static DesignDocument CreateShapeDocument()
    {
        DesignerSession session = CreateBlankSession();
        DesignControlNode ellipse = session.AddControl(nameof(Ellipse));
        var gradient = new LinearGradientBrush { Start = new PointF(0, 0), End = new PointF(1, 1) };
        gradient.GradientStops.AddRange([
            new GradientStop(Color.CornflowerBlue, 0),
            new GradientStop(Color.MidnightBlue, 1)
        ]);
        ellipse.Properties["Fill"] = DesignerPropertyValueEditor.ToDesignPropertyValue(gradient, typeof(ModernFormsNext.Drawing.Brush));
        session.SelectForm();
        session.AddControl(nameof(Line));
        session.SelectForm();
        session.AddControl(nameof(Polygon));
        session.SelectForm();
        session.AddControl(nameof(Polyline));
        session.SelectForm();
        session.AddControl(nameof(ModernFormsNext.Path));
        return session.Document;
    }

    private static DesignDocument BlankDocument()
        => new()
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "Form1",
            Size = new DesignSize(640, 480)
        };

    private static PathGeometry CreatePathGeometry()
    {
        var geometry = new PathGeometry
        {
            FillRule = GeometryFillRule.EvenOdd,
            Transform = System.Numerics.Matrix3x2.CreateTranslation(3, 4)
        };
        var figure = new PathFigure(new PointF(1, 2), true);
        figure.Segments.Add(new QuadraticBezierSegment(new PointF(4, 5), new PointF(8, 9)));
        figure.Segments.Add(new BezierSegment(new PointF(10, 11), new PointF(12, 13), new PointF(14, 15)));
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry CreateComplexPathGeometry()
    {
        var geometry = new PathGeometry
        {
            FillRule = GeometryFillRule.EvenOdd,
            Transform = new Matrix3x2(1.25f, 0.125f, -0.25f, 0.875f, 1.5f, -2.25f)
        };
        var first = new PathFigure(new PointF(1.5f, 2.25f), isClosed: true);
        first.Segments.Add(new LineSegment(new PointF(10.5f, 3.25f)));
        first.Segments.Add(new QuadraticBezierSegment(new PointF(12.5f, 5.75f), new PointF(16.25f, 9.5f)));
        first.Segments.Add(new BezierSegment(new PointF(18.5f, 11.25f), new PointF(21.75f, 13.5f), new PointF(24.25f, 17.75f)));
        var second = new PathFigure(new PointF(-3.5f, 4.25f));
        second.Segments.Add(new LineSegment(new PointF(5.5f, 8.75f)));
        geometry.Figures.Add(first);
        geometry.Figures.Add(second);
        return geometry;
    }

    private static void AssertGeneratedCodeCompiles(string generatedCode)
    {
        const string baseClass = """
            namespace Example;
            public partial class MainForm : ModernFormsNext.Form { }
            """;
        string[] trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
        IEnumerable<MetadataReference> references = trustedAssemblies
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(assembly => assembly.Location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratedShapeDesignerValidation",
            [CSharpSyntaxTree.ParseText(generatedCode), CSharpSyntaxTree.ParseText(baseClass)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
