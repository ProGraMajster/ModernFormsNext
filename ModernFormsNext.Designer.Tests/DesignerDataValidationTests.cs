using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Utilities;
using ModernFormsNext.Designing;
using System.Text.Json;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerDataValidationTests
{
    [Theory]
    [InlineData("PathGeometry", "FigureCount")]
    [InlineData("PathFigure", "SegmentCount")]
    [InlineData("PointCollection", "Count")]
    [InlineData("LinearGradientBrush", "GradientStopCount")]
    public void DeclaredCollectionsMustContainTheirIndexedValues(string type, string countName)
    {
        var value = Structured(type, (countName, DesignPropertyValue.FromInt32(2)));
        Assert.False(Validate(value).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void CollectionCountsAreBoundedBeforeMaterialization(int count)
        => Assert.False(Validate(Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromInt32(count)))).IsValid);

    [Fact]
    public void CollectionCountsRequireAnIntegerValue()
        => Assert.False(Validate(Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromString("two")))).IsValid);

    [Fact]
    public void GeometryCollectionsRejectIncorrectElementTypes()
    {
        var value = Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromInt32(1)),
            ("Figure0", DesignPropertyValue.FromString("not a figure")));
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void GeometryCollectionsRejectUnclaimedIndexedElements()
    {
        var value = Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromInt32(0)),
            ("Figure0", Structured("PathFigure")));
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void GeometryCoordinatesRequireTheirKnownDataShape()
    {
        var value = Structured("LineSegment", ("Point", DesignPropertyValue.FromString("not a point")));
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void GeometryCoordinatesMustRemainFiniteAfterFloatConversion()
    {
        var value = DesignPropertyValue.FromStructuredObject("System.Drawing.PointF",
            new Dictionary<string, DesignPropertyValue> { ["X"] = DesignPropertyValue.FromDouble(double.MaxValue) });
        Assert.False(Validate(value).IsValid);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteNumbersAreDocumentErrors(double number)
        => Assert.False(Validate(DesignPropertyValue.FromDouble(number)).IsValid);

    [Fact]
    public void ValueKindsMustMatchTheirStoredClrValues()
        => Assert.False(Validate(new DesignPropertyValue(DesignPropertyValueKind.Int32, "two")).IsValid);

    [Fact]
    public void NullPropertyEntriesAreDocumentErrors()
    {
        var document = Document();
        document.Properties["Value"] = null!;
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
    }

    [Fact]
    public void PropertyValueCyclesAreRejectedByPureDataPreflight()
    {
        var value = Structured("RecoveryState");
        ((IDictionary<string, DesignPropertyValue>)value.ObjectProperties!)["Child"] = value;
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void PropertyValueDepthIsBoundedBeforeRecursiveConsumers()
    {
        var value = Structured("RecoveryState");
        for (int index = 0; index <= DesignDocumentValidator.MaximumTreeDepth; index++)
            value = Structured("RecoveryState", ("Child", value));
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void SharedImmutableValuesRemainValid()
    {
        var value = DesignPropertyValue.FromString("retained metadata");
        var document = Document();
        document.Properties["First"] = value;
        document.Properties["Second"] = value;
        Assert.True(new DesignDocumentValidator().ValidateStructure(document).IsValid);
    }

    [Fact]
    public void PreviewRejectsUnknownGeometryInsteadOfSubstitutingAnEmptyPath()
    {
        var document = Document();
        var path = Node("shape", "Path");
        path.Properties["Data"] = Structured("UnknownGeometry");
        var page = Node("content", "ContentPage");
        page.Children.Add(path);
        var host = Node("host", "PageHost");
        host.Children.Add(page);
        document.Controls.Add(host);
        Assert.NotEmpty(new DesignerLayoutEngine().Layout(document).PreviewDiagnostics);
    }

    [Fact]
    public void JsonNullRemainsAnExplicitNullLiteral()
    {
        var document = Document();
        document.Properties["Optional"] = DesignPropertyValue.FromNull();
        var restored = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        Assert.Equal(DesignPropertyValueKind.Null, restored.Properties["Optional"].Kind);
    }

    [Theory]
    [InlineData("{\"className\":\"First\",\"className\":\"Second\"}")]
    [InlineData("{\"properties\":{\"Caption\":\"first\",\"Caption\":\"second\"}}")]
    public void DuplicateJsonMembersAreRejected(string json)
        => Assert.Throws<JsonException>(() => DesignDocumentSerializer.Default.Deserialize(json));

    [Fact]
    public void OversizedJsonIsRejectedBeforeParsing()
        => Assert.Throws<JsonException>(() => DesignDocumentSerializer.Default.Deserialize(
            new string(' ', DesignDocumentSerializer.MaximumContentLength + 1) + "{}"));

    [Fact]
    public void FileReadUsesTheSameCharacterBudget()
    {
        string file = IOPath.GetTempFileName();
        try
        {
            File.WriteAllText(file, new string(' ', DesignDocumentSerializer.MaximumContentLength + 1));
            Assert.Throws<JsonException>(() => DesignDocumentSerializer.Default.Load(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void InvalidSavePreservesTheDestinationAndCreatesNoTemporaryFile()
    {
        string directory = IOPath.Combine(IOPath.GetTempPath(), "DesignerSafeData-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = IOPath.Combine(directory, "ContentView.mfdesign");
        try
        {
            File.WriteAllText(file, "retained document");
            var document = Document();
            document.Properties["Value"] = DesignPropertyValue.FromDouble(double.NaN);
            Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Save(file, document));
            Assert.Equal("retained document", File.ReadAllText(file));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { File.Delete(file); Directory.Delete(directory); }
    }

    [Fact]
    public void MigrationOutputHasTheSameJsonBudget()
    {
        var serializer = new DesignDocumentSerializer([new OversizedMigration()]);
        Assert.Throws<JsonException>(() => serializer.Deserialize("{\"metadata\":{\"formatVersion\":0}}"));
    }

    [Fact]
    public void PropertyBudgetSpansAllNodes()
    {
        var document = Document();
        for (int nodeIndex = 0; nodeIndex < 2; nodeIndex++)
        {
            var node = Node("panel" + nodeIndex, "Panel");
            for (int index = 0; index <= DesignDocumentValidator.MaximumPropertyValueCount / 2; index++)
                node.Properties["Value" + index] = DesignPropertyValue.FromInt32(index);
            document.Controls.Add(node);
        }
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewRejectsSmallerValueAndTextBudgets(bool textBudget)
    {
        var document = Document();
        var page = Node("content", "ContentPage");
        if (textBudget) page.Properties["Title"] = DesignPropertyValue.FromString(
            new string('a', DesignerPagePreview.MaximumPropertyTextLength + 1));
        else
            for (int index = 0; index <= DesignerPagePreview.MaximumPropertyValues; index++)
                page.Properties["Value" + index] = DesignPropertyValue.FromInt32(index);
        document.Controls.Add(page);
        Assert.True(new DesignDocumentValidator().ValidateStructure(document).IsValid);
        Assert.NotEmpty(new DesignerLayoutEngine().Layout(document).PreviewDiagnostics);
    }

    [Fact]
    public void MalformedDataFailsGenerationWithNoPartialCode()
    {
        var document = Document();
        var value = Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromInt32(2)));
        document.Properties["Value"] = value;
        Assert.Throws<FormatException>(() => CSharpLiteralWriter.WriteValue(value));
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }

    [Theory]
    [InlineData("Sample<")]
    [InlineData("Sample..View")]
    [InlineData("Sample<View]")]
    [InlineData("Sample View")]
    public void IncompleteOrMalformedTypeNamesFailClosed(string typeName)
    {
        var document = Document();
        document.Controls.Add(Node("view", typeName));
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.False(generated.Succeeded);
        Assert.Empty(generated.Code);
    }

    [Theory]
    [InlineData("Panel")]
    [InlineData("global::ModernFormsNext.Panel")]
    [InlineData("Sample.View<System.String>")]
    public void CompleteNamedTypesRemainSupported(string typeName)
    {
        var document = Document();
        document.Controls.Add(Node("view", typeName));
        Assert.True(new CSharpDesignerGenerator().Generate(document).Succeeded);
    }

    [Theory]
    [InlineData("SkiaSharp.SKRect", "Left")]
    [InlineData("ModernFormsNext.Drawing.RadialGradientBrush", "Radius")]
    [InlineData("ModernFormsNext.Drawing.GradientStop", "Offset")]
    public void FloatingDataCannotOverflowItsRuntimeRepresentation(string type, string field)
    {
        var value = DesignPropertyValue.FromStructuredObject(type,
            new Dictionary<string, DesignPropertyValue> { [field] = DesignPropertyValue.FromDouble(double.MaxValue) });
        Assert.False(Validate(value).IsValid);
    }

    [Fact]
    public void KnownDenseGeometrySurvivesSerializationGenerationAndConversion()
    {
        var value = Structured("PathGeometry", ("FigureCount", DesignPropertyValue.FromInt32(1)),
            ("Figure0", Structured("PathFigure", ("SegmentCount", DesignPropertyValue.FromInt32(1)),
                ("Segment0", Structured("LineSegment", ("Point", DesignPropertyValue.FromStructuredObject(
                    "System.Drawing.PointF", new Dictionary<string, DesignPropertyValue>
                    { ["X"] = DesignPropertyValue.FromDouble(12.5), ["Y"] = DesignPropertyValue.FromDouble(8) })))))));
        var document = Document();
        document.Properties["Data"] = value;
        var restored = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        var geometry = Assert.IsType<ModernFormsNext.Drawing.PathGeometry>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(restored.Properties["Data"], typeof(ModernFormsNext.Drawing.Geometry)));
        Assert.Single(Assert.Single(geometry.Figures).Segments);
        Assert.Contains("LineSegment", CSharpLiteralWriter.WriteValue(restored.Properties["Data"]));
    }

    private sealed class OversizedMigration : IDesignDocumentMigration
    {
        public int SourceFormatVersion => 0;
        public int TargetFormatVersion => 1;
        public DesignDocumentMigrationResult Migrate(string sourceJson)
            => new(new string(' ', DesignDocumentSerializer.MaximumContentLength + 1) + "{}");
    }

    [Theory]
    [InlineData(0, 0, -1, 20)]
    [InlineData(int.MaxValue, 0, 1, 20)]
    [InlineData(0, int.MaxValue, 20, 1)]
    public void AuthoredBoundsRejectNegativeSizesAndOverflowedEdges(int x, int y, int width, int height)
    {
        var document = Document();
        var node = Node("panel", "Panel");
        node.Bounds = new(x, y, width, height);
        document.Controls.Add(node);
        Assert.False(new DesignDocumentValidator().ValidateStructure(document).IsValid);
        Assert.Empty(new CSharpDesignerGenerator().Generate(document).Code);
    }

    [Fact]
    public void NegativeRootDimensionsCannotReachSerialization()
    {
        var document = Document();
        document.Size = new(-1, 240);
        Assert.Throws<InvalidOperationException>(() => DesignDocumentSerializer.Default.Serialize(document));
    }

    [Fact]
    public void PreviewFloatConversionRejectsOverflowWithoutRejectingValidDoubleData()
    {
        var value = DesignPropertyValue.FromDouble(double.MaxValue);
        Assert.Equal(double.MaxValue, DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(double)));
        Assert.Throws<FormatException>(() => DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(float)));
    }

    [Theory]
    [InlineData("ModernFormsNext.Font", "Size")]
    [InlineData("ModernFormsNext.Drawing.SolidColorBrush", "Opacity")]
    public void KnownSingleMembersRejectOverflowBeforeConversionOrEmission(string type, string field)
    {
        var value = DesignPropertyValue.FromStructuredObject(type,
            new Dictionary<string, DesignPropertyValue> { [field] = DesignPropertyValue.FromDouble(double.MaxValue) });
        Assert.False(Validate(value).IsValid);
        Assert.Throws<FormatException>(() => CSharpLiteralWriter.WriteValue(value));
        Assert.Throws<FormatException>(() => DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(object)));
    }

    private static DesignDocumentValidationResult Validate(DesignPropertyValue value)
    {
        var document = Document();
        document.Properties["Value"] = value;
        return new DesignDocumentValidator().ValidateStructure(document);
    }

    private static DesignDocument Document() => new()
    {
        Namespace = "SafeDesignerData", ClassName = "ContentView", FormName = "ContentView",
        RootKind = DesignRootKind.UserControl, Size = new(320, 240)
    };

    private static DesignControlNode Node(string name, string type)
        => new() { Name = name, TypeName = type, Bounds = new(0, 0, 320, 240) };

    private static DesignPropertyValue Structured(string type, params (string Name, DesignPropertyValue Value)[] properties)
        => DesignPropertyValue.FromStructuredObject("ModernFormsNext.Drawing." + type,
            properties.ToDictionary(property => property.Name, property => property.Value));
}
