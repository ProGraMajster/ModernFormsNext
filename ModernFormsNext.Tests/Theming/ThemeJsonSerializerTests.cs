using System.Drawing;
using System.Numerics;
using System.Text;
using ModernFormsNext.Drawing;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ThemeJsonSerializerTests
{
    [Fact]
    public void RoundTripPreservesMetadataTokensAndEveryAllowListedBrush()
    {
        var serializer = new ThemeJsonSerializer();
        ThemeDefinition source = CompleteTheme();

        string json = serializer.Serialize(source, indented: true);
        ThemeDefinition result = serializer.Deserialize(json);

        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Name, result.Name);
        Assert.Equal(source.Description, result.Description);
        Assert.Equal(source.Author, result.Author);
        Assert.Equal(source.BaseTheme, result.BaseTheme);
        Assert.Equal(source.Variant, result.Variant);
        Assert.Equal("value", result.Metadata["sample"]);
        Assert.Equal(["demo", "dark"], result.Tags);
        Assert.Equal(Color.FromArgb(128, 1, 2, 3).ToArgb(), result.Colors["Custom"].ToArgb());
        Assert.Equal(6, result.Brushes.Count);
        Assert.IsType<SolidColorBrush>(result.Brushes["Solid"]);
        Assert.IsType<LinearGradientBrush>(result.Brushes["Linear"]);
        Assert.IsType<RadialGradientBrush>(result.Brushes["Radial"]);
        Assert.IsType<SweepGradientBrush>(result.Brushes["Sweep"]);
        Assert.IsType<GlassBrush>(result.Brushes["Glass"]);
        Assert.IsType<NoBrush>(result.Brushes["None"]);

        var linear = Assert.IsType<LinearGradientBrush>(result.Brushes["Linear"]);
        Assert.Equal(GradientSpreadMode.Reflect, linear.SpreadMode);
        Assert.Equal(new PointF(0.1f, 0.2f), linear.Start);
        Assert.Equal(new PointF(0.8f, 0.9f), linear.End);
        Assert.Equal(0.75f, linear.Opacity);
        Assert.Equal(Matrix3x2.CreateTranslation(2f, 3f), linear.Transform);
        Assert.Equal(2, linear.GradientStops.Count);
        Assert.Equal(0.2f, linear.GradientStops[0].Offset);

        Assert.Equal(new ThemeTypography("Arial", 14f, FontStyle.Bold, 1.4f, 0.25f), result.Typography["Body"]);
        Assert.Equal(8d, result.Spacing["Medium"]);
        Assert.Equal(new Padding(8, 4, 8, 4), result.Padding["Control"]);
        Assert.Equal(32d, result.Sizing["ControlHeight"]);
        Assert.Equal(6d, result.Corners["Card"]);
        Assert.Equal(1d, result.BorderThickness["Default"]);
        Assert.Equal(new ThemeAnimationSettings(TimeSpan.FromMilliseconds(180), ThemeEasing.EaseOut), result.Animations["Fast"]);
        Assert.Equal(ThemeResourceKind.Padding, result.Resources["CardPadding"].Kind);
        Assert.Equal(new Padding(1, 2, 3, 4), result.Resources["CardPadding"].Value);
    }

    [Fact]
    public void SerializationIsDeterministicAndOrdersDictionaryKeys()
    {
        var serializer = new ThemeJsonSerializer();
        var theme = MinimalTheme();
        theme.Colors["Zulu"] = Color.Red;
        theme.Colors["Alpha"] = Color.Blue;

        string first = serializer.Serialize(theme);
        string second = serializer.Serialize(theme);

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("\"Alpha\"", StringComparison.Ordinal) < first.IndexOf("\"Zulu\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StreamAndFileSyncAsyncApisRoundTripWithoutClosingCallerStream()
    {
        var serializer = new ThemeJsonSerializer();
        ThemeDefinition theme = CompleteTheme();
        await using var stream = new MemoryStream();

        await serializer.SerializeAsync(theme, stream, indented: true);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        ThemeDefinition fromStream = await serializer.DeserializeAsync(stream);
        Assert.Equal(theme.Id, fromStream.Id);
        Assert.True(stream.CanRead);

        string path = Path.Combine(Path.GetTempPath(), $"mfn-theme-{Guid.NewGuid():N}.json");
        try
        {
            serializer.SaveFile(theme, path);
            Assert.Equal(theme.Id, serializer.LoadFile(path).Id);
            await serializer.SaveFileAsync(theme, path);
            Assert.Equal(theme.Id, (await serializer.LoadFileAsync(path)).Id);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"id\":\"a\",\"name\":\"A\"}", "$.schemaVersion")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"future\":true}", "$.future")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"id\":\"b\",\"name\":\"A\"}", "$.id")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"brushes\":{\"Bad\":{\"type\":\"System.Object\"}}}", "$.brushes.Bad.type")]
    public void UnsupportedSchemaUnknownDuplicateAndArbitraryTypesAreRejected(string json, string path)
    {
        ThemeSerializationException exception = Assert.Throws<ThemeSerializationException>(
            () => new ThemeJsonSerializer().Deserialize(json));

        Assert.Equal(path, exception.JsonPath);
    }

    [Fact]
    public void InternalSchemaMigrationExtensionCanUpgradeBeforeStrictParsing()
    {
        var serializer = new ThemeJsonSerializer(
            new ThemeSecurityLimits(),
            [new VersionZeroTestMigration()]);

        ThemeDefinition result = serializer.Deserialize(
            "{\"schemaVersion\":0,\"id\":\"migrated.theme\",\"name\":\"Migrated\"}");

        Assert.Equal(ThemeJsonSerializer.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal("migrated.theme", result.Id);
    }

    [Fact]
    public void MalformedJsonReportsAJsonPath()
    {
        ThemeSerializationException exception = Assert.Throws<ThemeSerializationException>(
            () => new ThemeJsonSerializer().Deserialize("{\"schemaVersion\":1,\"id\":"));

        Assert.NotNull(exception.JsonPath);
        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonFiniteNumberSyntaxIsRejected()
    {
        const string json = "{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"spacing\":{\"Bad\":1e9999}}";

        ThemeSerializationException exception = Assert.Throws<ThemeSerializationException>(
            () => new ThemeJsonSerializer().Deserialize(json));

        Assert.Equal("$.spacing.Bad", exception.JsonPath);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"variant\":\"1\"}", "$.variant")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"typography\":{\"Body\":{\"fontFamily\":\"Arial\",\"size\":12,\"style\":\"999\"}}}", "$.typography.Body.style")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"resources\":{\"Bad\":{\"type\":\"object\",\"value\":{}}}}", "$.resources.Bad.type")]
    public void NumericEnumsAndUnknownResourceDiscriminatorsAreRejectedAtTheirPaths(string json, string path)
    {
        ThemeSerializationException exception = Assert.Throws<ThemeSerializationException>(
            () => new ThemeJsonSerializer().Deserialize(json));

        Assert.Equal(path, exception.JsonPath);
    }

    [Fact]
    public void DocumentSizeDepthAndStringLimitsAreEnforced()
    {
        var sizeSerializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumDocumentBytes = 48 });
        Assert.Throws<ThemeSerializationException>(() => sizeSerializer.Deserialize(new string(' ', 49)));

        var depthSerializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumJsonDepth = 3 });
        Assert.Throws<ThemeSerializationException>(() => depthSerializer.Deserialize("{\"a\":{\"b\":{\"c\":{}}}}"));

        var stringSerializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumStringLength = 3 });
        ThemeSerializationException stringError = Assert.Throws<ThemeSerializationException>(
            () => stringSerializer.Deserialize("{\"schemaVersion\":1,\"id\":\"valid\",\"name\":\"Name\"}"));
        Assert.Equal("$.id", stringError.JsonPath);
    }

    [Fact]
    public void ConfiguredStringLimitCanExceedTheDefaultForTrustedApplications()
    {
        var serializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumStringLength = 1024 });
        string longValue = new('x', 600);
        var theme = MinimalTheme();
        theme.Resources["LongValue"] = ThemeResourceValue.FromString(longValue);

        ThemeDefinition result = serializer.Deserialize(serializer.Serialize(theme));

        Assert.Equal(longValue, result.Resources["LongValue"].Value);
    }

    [Fact]
    public void TokenAndGradientStopLimitsAreEnforced()
    {
        var tokenSerializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumTokenCount = 1 });
        ThemeSerializationException tokenError = Assert.Throws<ThemeSerializationException>(
            () => tokenSerializer.Deserialize(
                "{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"colors\":{\"A\":\"#FF000000\",\"B\":\"#FFFFFFFF\"}}"));
        Assert.Contains("token", tokenError.Message, StringComparison.OrdinalIgnoreCase);

        var stopSerializer = new ThemeJsonSerializer(new ThemeSecurityLimits { MaximumGradientStops = 1 });
        const string gradient = "{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"brushes\":{\"G\":{\"type\":\"linearGradient\",\"gradientStops\":[{\"color\":\"#FF000000\",\"offset\":0},{\"color\":\"#FFFFFFFF\",\"offset\":1}],\"start\":[0,0],\"end\":[1,1]}}}";
        ThemeSerializationException stopError = Assert.Throws<ThemeSerializationException>(() => stopSerializer.Deserialize(gradient));
        Assert.Equal("$.brushes.G.gradientStops", stopError.JsonPath);
    }

    [Theory]
    [InlineData("Bad Key")]
    [InlineData("1StartsWithNumber")]
    [InlineData("Bad/Path")]
    public void InvalidResourceKeysAreRejected(string key)
    {
        string json = $"{{\"schemaVersion\":1,\"id\":\"a\",\"name\":\"A\",\"colors\":{{\"{key}\":\"#FF000000\"}}}}";

        ThemeSerializationException exception = Assert.Throws<ThemeSerializationException>(
            () => new ThemeJsonSerializer().Deserialize(json));

        Assert.Contains("valid theme key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ThemeDefinition MinimalTheme()
        => new("sample.theme", "Sample Theme") { Variant = ThemeVariant.Dark };

    private static ThemeDefinition CompleteTheme()
    {
        var theme = new ThemeDefinition("sample.complete", "Complete")
        {
            Description = "Complete test theme",
            Author = "ModernFormsNext",
            BaseTheme = BuiltInThemes.BaseThemeId,
            Variant = ThemeVariant.Dark
        };
        theme.Metadata["sample"] = "value";
        theme.Tags.Add("demo");
        theme.Tags.Add("dark");
        theme.Colors["Custom"] = Color.FromArgb(128, 1, 2, 3);
        theme.Brushes["Solid"] = new SolidColorBrush(Color.CornflowerBlue)
        {
            Opacity = 0.5f,
            Transform = Matrix3x2.CreateScale(1.25f)
        };
        theme.Brushes["Linear"] = Gradient(new LinearGradientBrush
        {
            Start = new PointF(0.1f, 0.2f),
            End = new PointF(0.8f, 0.9f),
            SpreadMode = GradientSpreadMode.Reflect,
            Opacity = 0.75f,
            Transform = Matrix3x2.CreateTranslation(2f, 3f)
        });
        theme.Brushes["Radial"] = Gradient(new RadialGradientBrush
        {
            CenterPoint = new PointF(0.4f, 0.5f),
            GradientOrigin = new PointF(0.3f, 0.2f),
            Radius = 0.8f,
            SpreadMode = GradientSpreadMode.Repeat
        });
        theme.Brushes["Sweep"] = Gradient(new SweepGradientBrush
        {
            CenterPoint = new PointF(0.45f, 0.55f),
            StartAngle = -30f,
            EndAngle = 270f
        });
        theme.Brushes["Glass"] = new GlassBrush
        {
            Tint = Color.FromArgb(30, 1, 2, 3),
            SecondaryTint = Color.FromArgb(20, 4, 5, 6),
            Highlight = Color.FromArgb(40, 7, 8, 9),
            Border = Color.FromArgb(50, 10, 11, 12),
            ShowHighlight = false,
            ShowInnerBorder = true
        };
        theme.Brushes["None"] = new NoBrush();
        theme.Typography["Body"] = new ThemeTypography("Arial", 14f, FontStyle.Bold, 1.4f, 0.25f);
        theme.Spacing["Medium"] = 8d;
        theme.Padding["Control"] = new Padding(8, 4, 8, 4);
        theme.Sizing["ControlHeight"] = 32d;
        theme.Corners["Card"] = 6d;
        theme.BorderThickness["Default"] = 1d;
        theme.Animations["Fast"] = new ThemeAnimationSettings(TimeSpan.FromMilliseconds(180), ThemeEasing.EaseOut);
        theme.Resources["CardPadding"] = ThemeResourceValue.FromPadding(new Padding(1, 2, 3, 4));
        theme.Resources["Label"] = ThemeResourceValue.FromString("Hello");
        return theme;
    }

    private static T Gradient<T>(T brush) where T : GradientBrush
    {
        brush.GradientStops.Add(new GradientStop(Color.Red, 0.2f));
        brush.GradientStops.Add(new GradientStop(Color.Blue, 0.8f));
        return brush;
    }

    private sealed class VersionZeroTestMigration : IThemeSchemaMigration
    {
        public int SourceVersion => 0;

        public byte[] Migrate(ReadOnlyMemory<byte> source, ThemeSecurityLimits limits)
        {
            string json = Encoding.UTF8.GetString(source.Span);
            return Encoding.UTF8.GetBytes(json.Replace(
                "\"schemaVersion\":0",
                "\"schemaVersion\":1",
                StringComparison.Ordinal));
        }
    }
}
