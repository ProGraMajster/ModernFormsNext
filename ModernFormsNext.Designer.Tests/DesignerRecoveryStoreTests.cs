using System.Text.Json;
using System.Text.Json.Nodes;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerRecoveryStoreTests
{
    private static readonly DesignerRecoverySessionIdentity FixedSession = new(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        processId: 4242);

    [Fact]
    public void SavedAndUnsavedDocumentIdentitiesAreStableAndUnambiguous()
    {
        using var directory = new TemporaryDirectory();
        var documentPath = IOPath.Combine(directory.Path, "Project", ".", "Form1.mfdesign");
        var projectPath = IOPath.Combine(directory.Path, "Project", "App.csproj");

        var first = DesignerRecoveryDocumentIdentity.ForSavedDocument(documentPath, projectPath);
        var same = DesignerRecoveryDocumentIdentity.ForSavedDocument(
            IOPath.GetFullPath(documentPath),
            IOPath.GetFullPath(projectPath));
        var otherProject = DesignerRecoveryDocumentIdentity.ForSavedDocument(
            documentPath,
            IOPath.Combine(directory.Path, "Other", "App.csproj"));
        var temporaryId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var unsaved = DesignerRecoveryDocumentIdentity.ForUnsavedDocument(temporaryId);

        Assert.Equal(first, same);
        Assert.NotEqual(first, otherProject);
        Assert.Equal(DesignerFileHash.Sha256HexLength, first.FileNameToken.Length);
        Assert.Equal($"unsaved:{temporaryId:N}", unsaved.Value);
        Assert.Equal(temporaryId, unsaved.TemporaryDocumentId);
        Assert.NotEqual(unsaved, DesignerRecoveryDocumentIdentity.ForUnsavedDocument(Guid.NewGuid()));
    }

    [Fact]
    public void DefaultRootUsesTheDedicatedPerUserDesignerRecoveryDirectory()
    {
        var path = DesignerRecoveryStore.GetDefaultRootPath();

        Assert.EndsWith(
            IOPath.Combine("ModernFormsNext", "Designer", "Recovery"),
            path,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteAndDiscoveryRoundTripVersionedEnvelopeAndRevisionTuple()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var timestamp = new DateTimeOffset(2026, 8, 23, 10, 11, 12, TimeSpan.Zero);
        var sourceHash = DesignerFileHash.ComputeUtf8Sha256("canonical source");
        var generatedHash = DesignerFileHash.ComputeUtf8Sha256("generated code");
        var snapshot = DesignerRecoverySnapshot.CaptureSaved(
            CreateDocument("Recovered value"),
            IOPath.Combine(directory.Path, "Project", "Form1.mfdesign"),
            IOPath.Combine(directory.Path, "Project", "App.csproj"),
            dirtyRevision: 42,
            revisionGeneration: 7,
            FixedSession,
            timestamp,
            sourceFileLastWriteUtc: timestamp.AddMinutes(-5),
            sourceHash,
            generatedHash);

        var write = store.Write(snapshot);
        var discovery = store.Discover();
        var candidate = Assert.Single(discovery.Candidates);

        Assert.True(write.Succeeded, write.Error);
        Assert.False(discovery.WasTruncated);
        Assert.Null(discovery.Error);
        Assert.Equal(DesignerRecoveryCandidateStatus.Valid, candidate.Status);
        Assert.NotNull(candidate.Document);
        Assert.Equal("Recovered value", candidate.Document.Controls[0].Properties["Text"].GetString());
        Assert.Equal(DesignerRecoveryFormat.CurrentVersion, candidate.Envelope?.FormatVersion);
        Assert.Equal(42, candidate.Envelope?.Metadata?.DirtyRevision);
        Assert.Equal(7, candidate.Envelope?.Metadata?.RevisionGeneration);
        Assert.Equal(FixedSession.SessionId, candidate.Envelope?.Metadata?.SessionId);
        Assert.Equal(FixedSession.ProcessId, candidate.Envelope?.Metadata?.ProcessId);
        Assert.Equal(sourceHash, candidate.Envelope?.Metadata?.SourceFileHashSha256);
        Assert.Equal(generatedHash, candidate.Envelope?.Metadata?.GeneratedCodeHashSha256);
        Assert.True(DesignerFileHash.EqualsSha256(
            candidate.Envelope!.SerializedDesignDocument,
            candidate.Envelope.PayloadSha256));
        Assert.Contains($".{FixedSession.SessionId:N}.{FixedSession.ProcessId}", IOPath.GetFileName(write.ArtifactPath));
    }

    [Fact]
    public void UnsavedSnapshotUsesTemporaryIdentityWithoutInventingCanonicalPath()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var temporaryDocumentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
            CreateDocument("Scratch value"),
            temporaryDocumentId,
            "ScratchForm.mfdesign",
            IOPath.Combine(directory.Path, "Project", "App.csproj"),
            dirtyRevision: 0,
            revisionGeneration: 3,
            FixedSession);

        var write = store.Write(snapshot);
        var candidate = store.Read(write.ArtifactPath);

        Assert.True(write.Succeeded, write.Error);
        Assert.Equal(DesignerRecoveryCandidateStatus.Valid, candidate.Status);
        Assert.True(candidate.Envelope?.Metadata?.IsUnsaved);
        Assert.Equal(temporaryDocumentId, candidate.Envelope?.Metadata?.TemporaryDocumentId);
        Assert.Null(candidate.Envelope?.Metadata?.DocumentPath);
        Assert.Equal("ScratchForm.mfdesign", candidate.Envelope?.Metadata?.SuggestedName);
        Assert.Equal(3, candidate.Envelope?.Metadata?.RevisionGeneration);
    }

    [Theory]
    [InlineData("documentPath")]
    [InlineData("projectPath")]
    public void SavedIdentitySourceTamperingIsCorruptEvenWhenPayloadChecksumRemainsValid(
        string metadataProperty)
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var write = store.Write(CaptureSaved(directory.Path, "Original", revision: 5));
        Assert.True(write.Succeeded, write.Error);
        var envelope = JsonNode.Parse(File.ReadAllText(write.ArtifactPath))!.AsObject();
        var metadata = envelope["metadata"]!.AsObject();
        var originalValue = metadata[metadataProperty]!.GetValue<string>();
        metadata[metadataProperty] = originalValue + ".tampered";
        AssertEnvelopePayloadChecksumIsValid(envelope);
        File.WriteAllText(write.ArtifactPath, envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var candidate = store.Read(write.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status);
        Assert.Null(candidate.Document);
        Assert.Contains("identity", candidate.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsavedTemporaryDocumentIdTamperingIsCorruptEvenWhenPayloadChecksumRemainsValid()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
            CreateDocument("Scratch value"),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "ScratchForm.mfdesign",
            projectPath: null,
            dirtyRevision: 4,
            revisionGeneration: 2,
            FixedSession);
        var write = store.Write(snapshot);
        Assert.True(write.Succeeded, write.Error);
        var envelope = JsonNode.Parse(File.ReadAllText(write.ArtifactPath))!.AsObject();
        envelope["metadata"]!["temporaryDocumentId"] = "bbbbbbbb-cccc-dddd-eeee-ffffffffffff";
        AssertEnvelopePayloadChecksumIsValid(envelope);
        File.WriteAllText(write.ArtifactPath, envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var candidate = store.Read(write.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status);
        Assert.Null(candidate.Document);
        Assert.Contains("identity", candidate.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayloadTamperingIsReportedAsCorruptWithoutDeserializingIt()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var write = store.Write(CaptureSaved(directory.Path, "Original", revision: 5));
        var json = File.ReadAllText(write.ArtifactPath);
        var tampered = json.Replace("Original", "Tampered", StringComparison.Ordinal);
        Assert.NotEqual(json, tampered);
        File.WriteAllText(write.ArtifactPath, tampered);

        var candidate = store.Read(write.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status);
        Assert.Null(candidate.Document);
        Assert.Contains("checksum", candidate.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedEnvelopeVersionIsReportedSeparatelyFromCorruption()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var write = store.Write(CaptureSaved(directory.Path, "Original", revision: 5));
        var json = File.ReadAllText(write.ArtifactPath);
        var unsupported = json.Replace(
            $"\"formatVersion\": {DesignerRecoveryFormat.CurrentVersion}",
            "\"formatVersion\": 999",
            StringComparison.Ordinal);
        Assert.NotEqual(json, unsupported);
        File.WriteAllText(write.ArtifactPath, unsupported);

        var candidate = store.Read(write.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Unsupported, candidate.Status);
        Assert.Null(candidate.Document);
        Assert.Equal(999, candidate.Envelope?.FormatVersion);
    }

    [Fact]
    public void InvalidJsonAndSemanticallyInvalidDocumentAreReportedAsCorrupt()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var store = new DesignerRecoveryStore(root);
        var invalidJsonPath = IOPath.Combine(root, "invalid.mfrecovery");
        File.WriteAllText(invalidJsonPath, "{ invalid json");
        var invalidDocument = CreateDocument("Invalid");
        invalidDocument.ClassName = string.Empty;
        var invalidDocumentWrite = store.Write(DesignerRecoverySnapshot.CaptureSaved(
            invalidDocument,
            IOPath.Combine(directory.Path, "Invalid.mfdesign"),
            projectPath: null,
            dirtyRevision: 1,
            revisionGeneration: 1,
            FixedSession));

        var invalidJson = store.Read(invalidJsonPath);
        var invalidModel = store.Read(invalidDocumentWrite.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, invalidJson.Status);
        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, invalidModel.Status);
        Assert.Contains("invalid", invalidModel.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailedAtomicCommitPreservesPreviousValidArtifactAndRemovesTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        var store = new DesignerRecoveryStore(root);
        var first = store.Write(CaptureSaved(directory.Path, "Previous", revision: 1));
        var failingStore = new DesignerRecoveryStore(root, atomicCommitter: new ThrowingAtomicCommitter());

        var failed = failingStore.Write(CaptureSaved(directory.Path, "Replacement", revision: 2));
        var preserved = store.Read(first.ArtifactPath);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(failed.Succeeded);
        Assert.Equal(first.ArtifactPath, failed.ArtifactPath);
        Assert.Equal(DesignerRecoveryCandidateStatus.Valid, preserved.Status);
        Assert.Equal("Previous", preserved.Document?.Controls[0].Properties["Text"].GetString());
        Assert.Equal(1, preserved.Envelope?.Metadata?.DirtyRevision);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void DiscoveryIsBoundedToConfiguredTopLevelEntryCount()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var paths = new List<string>();
        var now = DateTime.UtcNow;
        for (var index = 0; index < 4; index++)
        {
            var path = IOPath.Combine(root, $"candidate-{index}.mfrecovery");
            File.WriteAllText(path, "{ invalid");
            File.SetLastWriteTimeUtc(path, now.AddMinutes(index));
            paths.Add(path);
        }
        var store = new DesignerRecoveryStore(root, new DesignerRecoveryStoreOptions
        {
            MaxDiscoveryFiles = 2
        });

        var discovery = store.Discover();

        Assert.True(discovery.WasTruncated);
        Assert.Equal(2, discovery.Candidates.Count);
        Assert.All(discovery.Candidates, candidate => Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status));
        Assert.Equal([paths[3], paths[2]], discovery.Candidates.Select(candidate => candidate.ArtifactPath));
    }

    [Fact]
    public void UnrelatedTopLevelFilesConsumeTheBoundedDiscoveryEntryScan()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        for (var index = 0; index < 3; index++)
            File.WriteAllText(IOPath.Combine(root, $"unrelated-{index}.txt"), "ignored");
        var store = new DesignerRecoveryStore(root, new DesignerRecoveryStoreOptions
        {
            MaxDiscoveryEntries = 2,
            MaxDiscoveryFiles = 2
        });

        var discovery = store.Discover();

        Assert.True(discovery.WasTruncated);
        Assert.Empty(discovery.Candidates);
    }

    [Fact]
    public void DiscoveryStopsAtAggregateByteBudgetAndKeepsNewestCandidatesFirst()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var content = "{ invalid";
        var byteLength = System.Text.Encoding.UTF8.GetByteCount(content);
        var paths = new List<string>();
        var now = DateTime.UtcNow;
        for (var index = 0; index < 3; index++)
        {
            var path = IOPath.Combine(root, $"candidate-{index}.mfrecovery");
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, now.AddMinutes(index));
            paths.Add(path);
        }
        var store = new DesignerRecoveryStore(root, new DesignerRecoveryStoreOptions
        {
            MaxArtifactBytes = 64,
            MaxDiscoveryBytes = byteLength * 2L,
            MaxDiscoveryEntries = 10,
            MaxDiscoveryFiles = 10
        });

        var discovery = store.Discover();

        Assert.True(discovery.WasTruncated);
        Assert.Equal([paths[2], paths[1]], discovery.Candidates.Select(candidate => candidate.ArtifactPath));
        Assert.All(discovery.Candidates, candidate => Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status));
    }

    [Fact]
    public void ReadConsumesOnlyOneSentinelByteWhenArtifactGrowsPastConfiguredLimit()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var path = IOPath.Combine(root, "growing.mfrecovery");
        File.WriteAllBytes(path, new byte[8]);
        var growingStream = new GrowingReadStream(reportedLength: 8, actualLength: 32);
        var store = new DesignerRecoveryStore(
            root,
            new DesignerRecoveryStoreOptions { MaxArtifactBytes = 8 },
            openReadStream: _ => growingStream);

        var candidate = store.Read(path);

        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, candidate.Status);
        Assert.Contains("size limit", candidate.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(9, growingStream.BytesRead);
    }

    [Fact]
    public void ReadAcceptsValidArtifactAtExactConfiguredByteLimit()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        var writer = new DesignerRecoveryStore(root);
        var write = writer.Write(CaptureSaved(directory.Path, "exact limit", revision: 1));
        Assert.True(write.Succeeded, write.Error);
        var length = checked((int)new FileInfo(write.ArtifactPath).Length);
        var reader = new DesignerRecoveryStore(root, new DesignerRecoveryStoreOptions
        {
            MaxArtifactBytes = length,
            MaxDiscoveryBytes = length
        });

        var candidate = reader.Read(write.ArtifactPath);

        Assert.Equal(DesignerRecoveryCandidateStatus.Valid, candidate.Status);
        Assert.Equal("exact limit", candidate.Document?.Controls[0].Properties["Text"].GetString());
    }

    [Fact]
    public void DeleteAndQuarantineRejectMetadataAndFilesOutsideRecoveryRoot()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var store = new DesignerRecoveryStore(root);
        var outsidePath = IOPath.Combine(directory.Path, "outside.mfrecovery");
        File.WriteAllText(outsidePath, "do not delete");

        var delete = store.Delete(outsidePath);
        var quarantine = store.Quarantine(outsidePath);

        Assert.False(delete.Succeeded);
        Assert.False(quarantine.Succeeded);
        Assert.True(File.Exists(outsidePath));
        Assert.Contains("outside", delete.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptArtifactCanBeQuarantinedWithoutBeingRediscovered()
    {
        using var directory = new TemporaryDirectory();
        var store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
        var write = store.Write(CaptureSaved(directory.Path, "Original", revision: 1));
        File.WriteAllText(write.ArtifactPath, "{ corrupt");
        Assert.Equal(DesignerRecoveryCandidateStatus.Corrupt, store.Read(write.ArtifactPath).Status);

        var quarantine = store.Quarantine(write.ArtifactPath);
        var discovery = store.Discover();

        Assert.True(quarantine.Succeeded, quarantine.Error);
        Assert.False(File.Exists(write.ArtifactPath));
        Assert.True(File.Exists(quarantine.ResultPath));
        Assert.Equal(
            IOPath.Combine(store.RootPath, DesignerRecoveryFormat.QuarantineDirectoryName),
            IOPath.GetDirectoryName(quarantine.ResultPath));
        Assert.Empty(discovery.Candidates);
    }

    [Fact]
    public void CleanupRetainsProtectedAndNewestArtifactsAndRemovesOwnedStaleFiles()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        var store = new DesignerRecoveryStore(root);
        var now = DateTimeOffset.UtcNow;
        var protectedWrite = store.Write(CaptureSaved(directory.Path, "Protected", revision: 1, documentName: "Protected"));
        var middleWrite = store.Write(CaptureSaved(directory.Path, "Middle", revision: 2, documentName: "Middle"));
        var newestWrite = store.Write(CaptureSaved(directory.Path, "Newest", revision: 3, documentName: "Newest"));
        File.SetLastWriteTimeUtc(protectedWrite.ArtifactPath, now.AddHours(-3).UtcDateTime);
        File.SetLastWriteTimeUtc(middleWrite.ArtifactPath, now.AddHours(-2).UtcDateTime);
        File.SetLastWriteTimeUtc(newestWrite.ArtifactPath, now.AddHours(-1).UtcDateTime);

        var staleTemporaryPath = IOPath.Combine(root, $"{DesignerRecoveryFormat.TemporaryFilePrefix}crash{DesignerRecoveryFormat.TemporaryFileSuffix}");
        File.WriteAllText(staleTemporaryPath, "partial");
        File.SetLastWriteTimeUtc(staleTemporaryPath, now.AddDays(-2).UtcDateTime);
        var quarantineDirectory = IOPath.Combine(root, DesignerRecoveryFormat.QuarantineDirectoryName);
        Directory.CreateDirectory(quarantineDirectory);
        var staleQuarantinePath = IOPath.Combine(quarantineDirectory, "old.invalid");
        File.WriteAllText(staleQuarantinePath, "invalid");
        File.SetLastWriteTimeUtc(staleQuarantinePath, now.AddDays(-2).UtcDateTime);

        var cleanup = store.Cleanup(
            new DesignerRecoveryRetentionPolicy(
                maxAge: TimeSpan.FromDays(30),
                maxArtifacts: 1,
                temporaryFileMaxAge: TimeSpan.FromDays(1),
                quarantineMaxAge: TimeSpan.FromDays(1)),
            [protectedWrite.ArtifactPath],
            now);

        Assert.False(cleanup.WasTruncated);
        Assert.Empty(cleanup.Errors);
        Assert.True(File.Exists(protectedWrite.ArtifactPath));
        Assert.False(File.Exists(middleWrite.ArtifactPath));
        Assert.True(File.Exists(newestWrite.ArtifactPath));
        Assert.False(File.Exists(staleTemporaryPath));
        Assert.False(File.Exists(staleQuarantinePath));
    }

    [Fact]
    public void ReparsePointArtifactIsRejectedWithoutDeletingItsTarget()
    {
        using var directory = new TemporaryDirectory();
        var root = IOPath.Combine(directory.Path, "Recovery");
        Directory.CreateDirectory(root);
        var target = IOPath.Combine(directory.Path, "outside-target.txt");
        var link = IOPath.Combine(root, "linked.mfrecovery");
        File.WriteAllText(target, "preserve target");

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or NotSupportedException)
        {
            return;
        }

        var result = new DesignerRecoveryStore(root).Delete(link);

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(target));
        Assert.Equal("preserve target", File.ReadAllText(target));
    }

    [Fact]
    public void RawFileHashMatchesUtf8ContentAndChangesWithFile()
    {
        using var directory = new TemporaryDirectory();
        var path = IOPath.Combine(directory.Path, "source.mfdesign");
        File.WriteAllText(path, "abc");

        var first = DesignerFileHash.ComputeFileSha256(path);
        File.WriteAllText(path, "abcd");
        var second = DesignerFileHash.ComputeFileSha256(path);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", first);
        Assert.Equal(DesignerFileHash.ComputeUtf8Sha256("abcd"), second);
        Assert.NotEqual(first, second);
    }

    private static DesignerRecoverySnapshot CaptureSaved(
        string root,
        string text,
        long revision,
        string documentName = "Form1")
        => DesignerRecoverySnapshot.CaptureSaved(
            CreateDocument(text),
            IOPath.Combine(root, "Project", $"{documentName}.mfdesign"),
            IOPath.Combine(root, "Project", "App.csproj"),
            revision,
            revisionGeneration: 1,
            FixedSession,
            timestampUtc: new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));

    private static void AssertEnvelopePayloadChecksumIsValid(JsonObject envelope)
        => Assert.True(DesignerFileHash.EqualsSha256(
            envelope["serializedDesignDocument"]!.GetValue<string>(),
            envelope["payloadSha256"]!.GetValue<string>()));

    private static DesignDocument CreateDocument(string text)
    {
        var document = new DesignDocument
        {
            Namespace = "RecoveryTests",
            ClassName = "RecoveryForm",
            FormName = "RecoveryForm",
            Size = new DesignSize(800, 600)
        };
        var button = document.Controls.AddNode("Button", "button1", new DesignBounds(10, 20, 120, 32));
        button.Properties["Text"] = DesignPropertyValue.FromString(text);
        button.Properties["Padding"] = DesignPropertyValue.FromStructuredObject(
            "System.Windows.Forms.Padding",
            new Dictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Left"] = DesignPropertyValue.FromInt32(1),
                ["Top"] = DesignPropertyValue.FromInt32(2),
                ["Right"] = DesignPropertyValue.FromInt32(3),
                ["Bottom"] = DesignPropertyValue.FromInt32(4)
            });
        return document;
    }

    private sealed class GrowingReadStream(long reportedLength, int actualLength) : Stream
    {
        private readonly MemoryStream content = new(new byte[actualLength], writable: false);

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => reportedLength;

        public override long Position
        {
            get => content.Position;
            set => content.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = content.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => content.Seek(offset, origin);

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                content.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingAtomicCommitter : IDesignerAtomicFileCommitter
    {
        public void Commit(string temporaryPath, string destinationPath)
        {
            Assert.True(File.Exists(temporaryPath));
            Assert.True(File.Exists(destinationPath));
            throw new IOException("Simulated atomic replacement failure.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext.DesignerRecoveryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
