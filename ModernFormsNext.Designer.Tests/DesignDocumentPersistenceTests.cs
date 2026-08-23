using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignDocumentPersistenceTests
{
    [Fact]
    public void CurrentFormatVersionRoundTripsWithoutCompatibilityDiagnostics()
    {
        var serializer = new DesignDocumentSerializer();
        DesignDocument original = CreateDocument("Bieżący formularz");

        string json = serializer.Serialize(original);
        DesignDocumentDeserializationResult result = serializer.DeserializeWithDiagnostics(json);

        Assert.Equal(1, DesignDocumentSerializer.CurrentFormatVersion);
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, result.SourceFormatVersion);
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, result.FormatVersion);
        Assert.False(result.WasMigrated);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(original.FormName, result.Document.FormName);
    }

    [Fact]
    public void MissingFormatVersionUsesVersionOneCompatibilityDefaultWithDiagnostic()
    {
        const string json = """
            {
              "namespace": "Example",
              "className": "LegacyForm",
              "formName": "LegacyForm",
              "size": { "width": 640, "height": 480 },
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer();

        DesignDocumentDeserializationResult result = serializer.DeserializeWithDiagnostics(json);

        Assert.Null(result.SourceFormatVersion);
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, result.FormatVersion);
        Assert.False(result.WasMigrated);
        string diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("does not declare", diagnostic, StringComparison.Ordinal);
        Assert.Contains("backward compatibility", diagnostic, StringComparison.Ordinal);
        Assert.Equal("LegacyForm", serializer.Deserialize(json).FormName);
    }

    [Theory]
    [InlineData(0, "no migration")]
    [InlineData(2, "newer")]
    public void UnsupportedFormatVersionReportsVersionAndJsonPath(int version, string expectedReason)
    {
        string json = $$"""
            {
              "metadata": { "formatVersion": {{version}} },
              "namespace": "Example",
              "className": "UnsupportedForm",
              "formName": "UnsupportedForm",
              "size": { "width": 640, "height": 480 },
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer();

        JsonException exception = Assert.Throws<JsonException>(() => serializer.Deserialize(json));

        Assert.Equal("$.metadata.formatVersion", exception.Path);
        Assert.Contains($"version {version}", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedReason, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"version {DesignDocumentSerializer.CurrentFormatVersion}",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredMigrationUpgradesOlderJsonAndReturnsDiagnostics()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 0 },
              "namespace": "Example",
              "className": "MigratedForm",
              "legacyFormName": "Migrated title",
              "size": { "width": 640, "height": 480 },
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer([new VersionZeroMigration()]);

        DesignDocumentDeserializationResult result = serializer.DeserializeWithDiagnostics(json);

        Assert.Equal(0, result.SourceFormatVersion);
        Assert.Equal(DesignDocumentSerializer.CurrentFormatVersion, result.FormatVersion);
        Assert.True(result.WasMigrated);
        Assert.Equal("Migrated title", result.Document.FormName);
        Assert.Collection(
            result.Diagnostics,
            diagnostic => Assert.Contains("from version 0 to version 1", diagnostic, StringComparison.Ordinal),
            diagnostic => Assert.Equal("Renamed legacyFormName to formName.", diagnostic));
    }

    [Fact]
    public void MigrationThatDoesNotDeclareItsTargetVersionReportsClearFailure()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 0 },
              "namespace": "Example",
              "className": "BrokenMigration",
              "formName": "BrokenMigration",
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer([new WrongTargetVersionMigration()]);

        JsonException exception = Assert.Throws<JsonException>(() => serializer.Deserialize(json));

        Assert.Equal("$.metadata.formatVersion", exception.Path);
        Assert.Contains("declared target version 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("output version was 0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowingMigrationIsWrappedWithVersionContextAndOriginalCause()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 0 },
              "namespace": "Example",
              "className": "ThrowingMigration",
              "formName": "ThrowingMigration",
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer([new ThrowingMigration()]);

        JsonException exception = Assert.Throws<JsonException>(() => serializer.Deserialize(json));

        Assert.Equal("$.metadata.formatVersion", exception.Path);
        Assert.Contains("from format version 0 to 1 failed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("migration exploded", exception.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void MalformedMigrationOutputIsWrappedWithVersionContext()
    {
        const string json = """
            {
              "metadata": { "formatVersion": 0 },
              "namespace": "Example",
              "className": "MalformedMigration",
              "formName": "MalformedMigration",
              "controls": []
            }
            """;
        var serializer = new DesignDocumentSerializer([new MalformedOutputMigration()]);

        JsonException exception = Assert.Throws<JsonException>(() => serializer.Deserialize(json));

        Assert.Equal("$.metadata.formatVersion", exception.Path);
        Assert.Contains("from format version 0 to 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("produced malformed JSON", exception.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public void AtomicSaveReplacesExistingUtf8FileWithoutChangingAnOpenOldSnapshot()
    {
        string directory = CreateTemporaryDirectory();
        string path = IOPath.Combine(directory, "MainForm.mfdesign");
        var serializer = new DesignDocumentSerializer();
        string oldJson = serializer.Serialize(CreateDocument("Original"));
        File.WriteAllText(path, oldJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            using (var oldSnapshot = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                serializer.Save(path, CreateDocument("Zażółć gęślą jaźń"));

                Assert.Equal("Zażółć gęślą jaźń", serializer.Load(path).FormName);
                using var reader = new StreamReader(
                    oldSnapshot,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                Assert.Equal(oldJson, reader.ReadToEnd());
            }

            byte[] persistedBytes = File.ReadAllBytes(path);
            Assert.False(persistedBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Empty(Directory.GetFiles(directory, ".MainForm.mfdesign.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SerializationFailurePreservesExistingFileAndCreatesNoTemporaryArtifact()
    {
        string directory = CreateTemporaryDirectory();
        string path = IOPath.Combine(directory, "MainForm.mfdesign");
        const string original = "existing valid content";
        File.WriteAllText(path, original);
        DesignDocument unsupported = CreateDocument("Unsupported");
        unsupported.Metadata.FormatVersion = DesignDocumentSerializer.CurrentFormatVersion + 1;

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new DesignDocumentSerializer().Save(path, unsupported));

            Assert.Contains("cannot be serialized", exception.Message, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, ".MainForm.mfdesign.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DesignDocument CreateDocument(string formName)
        => new()
        {
            Metadata = new DesignDocumentMetadata
            {
                FormatVersion = DesignDocumentSerializer.CurrentFormatVersion,
                ToolName = "ModernFormsNext.Designer.Tests"
            },
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = formName,
            Size = new DesignSize(640, 480)
        };

    private static string CreateTemporaryDirectory()
    {
        string directory = IOPath.Combine(
            IOPath.GetTempPath(),
            $"ModernFormsNextDesignPersistenceTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class VersionZeroMigration : IDesignDocumentMigration
    {
        public int SourceFormatVersion => 0;

        public int TargetFormatVersion => DesignDocumentSerializer.CurrentFormatVersion;

        public DesignDocumentMigrationResult Migrate(string sourceJson)
        {
            JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(sourceJson));
            JsonObject metadata = Assert.IsType<JsonObject>(root["metadata"]);
            metadata["formatVersion"] = TargetFormatVersion;
            root["formName"] = root["legacyFormName"]?.DeepClone();
            root.Remove("legacyFormName");
            return new DesignDocumentMigrationResult(
                root.ToJsonString(),
                ["Renamed legacyFormName to formName."]);
        }
    }

    private sealed class WrongTargetVersionMigration : IDesignDocumentMigration
    {
        public int SourceFormatVersion => 0;

        public int TargetFormatVersion => DesignDocumentSerializer.CurrentFormatVersion;

        public DesignDocumentMigrationResult Migrate(string sourceJson) => new(sourceJson);
    }

    private sealed class ThrowingMigration : IDesignDocumentMigration
    {
        public int SourceFormatVersion => 0;

        public int TargetFormatVersion => DesignDocumentSerializer.CurrentFormatVersion;

        public DesignDocumentMigrationResult Migrate(string sourceJson)
            => throw new InvalidOperationException("migration exploded");
    }

    private sealed class MalformedOutputMigration : IDesignDocumentMigration
    {
        public int SourceFormatVersion => 0;

        public int TargetFormatVersion => DesignDocumentSerializer.CurrentFormatVersion;

        public DesignDocumentMigrationResult Migrate(string sourceJson)
            => new("{ malformed migration output");
    }
}
