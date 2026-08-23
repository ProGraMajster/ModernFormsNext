using System.Globalization;
using System.Text;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Recovery;

internal static class DesignerRecoveryFormat
{
    public const int CurrentVersion = 1;
    public const string ArtifactExtension = ".mfrecovery";
    public const string QuarantineDirectoryName = "Quarantine";
    public const string TemporaryFilePrefix = ".mfn-recovery-";
    public const string TemporaryFileSuffix = ".tmp";
}

internal readonly record struct DesignerRecoveryDocumentIdentity
{
    private DesignerRecoveryDocumentIdentity(
        string value,
        string fileNameToken,
        bool isUnsaved,
        Guid? temporaryDocumentId)
    {
        Value = value;
        FileNameToken = fileNameToken;
        IsUnsaved = isUnsaved;
        TemporaryDocumentId = temporaryDocumentId;
    }

    public string Value { get; }

    public string FileNameToken { get; }

    public bool IsUnsaved { get; }

    public Guid? TemporaryDocumentId { get; }

    public static DesignerRecoveryDocumentIdentity ForSavedDocument(
        string documentPath,
        string? projectPath)
    {
        var canonicalDocumentPath = NormalizePath(documentPath);
        var canonicalProjectPath = NormalizeOptionalPath(projectPath);
        var identityInput = string.Join(
            '\n',
            "document=" + NormalizeForIdentity(canonicalDocumentPath),
            "project=" + NormalizeForIdentity(canonicalProjectPath ?? string.Empty));
        var hash = DesignerFileHash.ComputeUtf8Sha256(identityInput);
        return new DesignerRecoveryDocumentIdentity($"saved:{hash}", hash, isUnsaved: false, temporaryDocumentId: null);
    }

    public static DesignerRecoveryDocumentIdentity ForUnsavedDocument(Guid temporaryDocumentId)
    {
        if (temporaryDocumentId == Guid.Empty)
            throw new ArgumentException("An unsaved recovery document ID cannot be empty.", nameof(temporaryDocumentId));

        var token = temporaryDocumentId.ToString("N", CultureInfo.InvariantCulture);
        return new DesignerRecoveryDocumentIdentity(
            $"unsaved:{token}",
            $"unsaved-{token}",
            isUnsaved: true,
            temporaryDocumentId);
    }

    internal static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(path));
    }

    internal static string? NormalizeOptionalPath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : NormalizePath(path);

    private static string NormalizeForIdentity(string value)
        => OperatingSystem.IsWindows() ? value.ToUpperInvariant() : value;
}

internal readonly record struct DesignerRecoverySessionIdentity
{
    public DesignerRecoverySessionIdentity(Guid sessionId, int processId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("A recovery session ID cannot be empty.", nameof(sessionId));
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId), "A recovery process ID must be positive.");

        SessionId = sessionId;
        ProcessId = processId;
    }

    public Guid SessionId { get; }

    public int ProcessId { get; }

    public static DesignerRecoverySessionIdentity Create()
        => new(Guid.NewGuid(), Environment.ProcessId);
}

internal sealed class DesignerRecoverySnapshotMetadata
{
    public string FrameworkVersion { get; set; } = string.Empty;

    public string DocumentIdentity { get; set; } = string.Empty;

    public bool IsUnsaved { get; set; }

    public Guid? TemporaryDocumentId { get; set; }

    public string? DocumentPath { get; set; }

    public string? ProjectPath { get; set; }

    public string SuggestedName { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public DateTimeOffset? SourceFileLastWriteUtc { get; set; }

    public string? SourceFileHashSha256 { get; set; }

    public string? GeneratedCodeHashSha256 { get; set; }

    public long DirtyRevision { get; set; }

    public long RevisionGeneration { get; set; }

    public Guid SessionId { get; set; }

    public int ProcessId { get; set; }
}

/// <summary>
/// Contains an immutable, serialized committed Designer state ready for background persistence.
/// </summary>
internal sealed class DesignerRecoverySnapshot
{
    private DesignerRecoverySnapshot(
        DesignerRecoveryDocumentIdentity identity,
        DesignerRecoverySnapshotMetadata metadata,
        string serializedDesignDocument)
    {
        Identity = identity;
        Metadata = metadata;
        SerializedDesignDocument = serializedDesignDocument;
    }

    public DesignerRecoveryDocumentIdentity Identity { get; }

    public DesignerRecoverySnapshotMetadata Metadata { get; }

    public string SerializedDesignDocument { get; }

    public static DesignerRecoverySnapshot CaptureSaved(
        DesignDocument document,
        string documentPath,
        string? projectPath,
        long dirtyRevision,
        long revisionGeneration,
        DesignerRecoverySessionIdentity session,
        DateTimeOffset? timestampUtc = null,
        DateTimeOffset? sourceFileLastWriteUtc = null,
        string? sourceFileHashSha256 = null,
        string? generatedCodeHashSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(dirtyRevision);
        ArgumentOutOfRangeException.ThrowIfNegative(revisionGeneration);

        var canonicalDocumentPath = DesignerRecoveryDocumentIdentity.NormalizePath(documentPath);
        var canonicalProjectPath = DesignerRecoveryDocumentIdentity.NormalizeOptionalPath(projectPath);
        var identity = DesignerRecoveryDocumentIdentity.ForSavedDocument(canonicalDocumentPath, canonicalProjectPath);
        return new DesignerRecoverySnapshot(
            identity,
            CreateMetadata(
                identity,
                canonicalDocumentPath,
                canonicalProjectPath,
                IOPath.GetFileName(canonicalDocumentPath),
                dirtyRevision,
                revisionGeneration,
                session,
                timestampUtc,
                sourceFileLastWriteUtc,
                sourceFileHashSha256,
                generatedCodeHashSha256),
            DesignDocumentSerializer.Default.Serialize(document));
    }

    public static DesignerRecoverySnapshot CaptureUnsaved(
        DesignDocument document,
        Guid temporaryDocumentId,
        string suggestedName,
        string? projectPath,
        long dirtyRevision,
        long revisionGeneration,
        DesignerRecoverySessionIdentity session,
        DateTimeOffset? timestampUtc = null,
        string? generatedCodeHashSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedName);
        ArgumentOutOfRangeException.ThrowIfNegative(dirtyRevision);
        ArgumentOutOfRangeException.ThrowIfNegative(revisionGeneration);

        var identity = DesignerRecoveryDocumentIdentity.ForUnsavedDocument(temporaryDocumentId);
        return new DesignerRecoverySnapshot(
            identity,
            CreateMetadata(
                identity,
                documentPath: null,
                DesignerRecoveryDocumentIdentity.NormalizeOptionalPath(projectPath),
                suggestedName.Trim(),
                dirtyRevision,
                revisionGeneration,
                session,
                timestampUtc,
                sourceFileLastWriteUtc: null,
                sourceFileHashSha256: null,
                generatedCodeHashSha256),
            DesignDocumentSerializer.Default.Serialize(document));
    }

    private static DesignerRecoverySnapshotMetadata CreateMetadata(
        DesignerRecoveryDocumentIdentity identity,
        string? documentPath,
        string? projectPath,
        string suggestedName,
        long dirtyRevision,
        long revisionGeneration,
        DesignerRecoverySessionIdentity session,
        DateTimeOffset? timestampUtc,
        DateTimeOffset? sourceFileLastWriteUtc,
        string? sourceFileHashSha256,
        string? generatedCodeHashSha256)
        => new()
        {
            FrameworkVersion = typeof(DesignerRecoverySnapshot).Assembly.GetName().Version?.ToString() ?? "unknown",
            DocumentIdentity = identity.Value,
            IsUnsaved = identity.IsUnsaved,
            TemporaryDocumentId = identity.TemporaryDocumentId,
            DocumentPath = documentPath,
            ProjectPath = projectPath,
            SuggestedName = suggestedName,
            TimestampUtc = (timestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            SourceFileLastWriteUtc = sourceFileLastWriteUtc?.ToUniversalTime(),
            SourceFileHashSha256 = sourceFileHashSha256,
            GeneratedCodeHashSha256 = generatedCodeHashSha256,
            DirtyRevision = dirtyRevision,
            RevisionGeneration = revisionGeneration,
            SessionId = session.SessionId,
            ProcessId = session.ProcessId
        };
}

internal sealed class DesignerRecoveryEnvelope
{
    public int FormatVersion { get; set; } = DesignerRecoveryFormat.CurrentVersion;

    public DesignerRecoverySnapshotMetadata? Metadata { get; set; }

    public string PayloadSha256 { get; set; } = string.Empty;

    public string IntegritySha256 { get; set; } = string.Empty;

    public string SerializedDesignDocument { get; set; } = string.Empty;

    public static DesignerRecoveryEnvelope FromSnapshot(DesignerRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var envelope = new DesignerRecoveryEnvelope
        {
            Metadata = snapshot.Metadata,
            PayloadSha256 = DesignerFileHash.ComputeUtf8Sha256(snapshot.SerializedDesignDocument),
            SerializedDesignDocument = snapshot.SerializedDesignDocument
        };
        envelope.IntegritySha256 = ComputeIntegritySha256(
            envelope.FormatVersion,
            envelope.Metadata,
            envelope.PayloadSha256);
        return envelope;
    }

    /// <summary>
    /// Computes a deterministic checksum over the envelope version, recovery metadata, and the
    /// independently calculated payload checksum.
    /// </summary>
    /// <remarks>
    /// This detects accidental or local metadata tampering. It is not an authentication signature
    /// and does not claim protection against a party that can rewrite the entire artifact.
    /// </remarks>
    internal static string ComputeIntegritySha256(
        int formatVersion,
        DesignerRecoverySnapshotMetadata metadata,
        string payloadSha256)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);

        var builder = new StringBuilder();
        Append(builder, nameof(formatVersion), formatVersion.ToString(CultureInfo.InvariantCulture));
        Append(builder, nameof(metadata.FrameworkVersion), metadata.FrameworkVersion);
        Append(builder, nameof(metadata.DocumentIdentity), metadata.DocumentIdentity);
        Append(builder, nameof(metadata.IsUnsaved), metadata.IsUnsaved ? "1" : "0");
        Append(builder, nameof(metadata.TemporaryDocumentId), metadata.TemporaryDocumentId?.ToString("N"));
        Append(builder, nameof(metadata.DocumentPath), metadata.DocumentPath);
        Append(builder, nameof(metadata.ProjectPath), metadata.ProjectPath);
        Append(builder, nameof(metadata.SuggestedName), metadata.SuggestedName);
        Append(builder, nameof(metadata.TimestampUtc), metadata.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        Append(
            builder,
            nameof(metadata.SourceFileLastWriteUtc),
            metadata.SourceFileLastWriteUtc?.ToString("O", CultureInfo.InvariantCulture));
        Append(builder, nameof(metadata.SourceFileHashSha256), metadata.SourceFileHashSha256);
        Append(builder, nameof(metadata.GeneratedCodeHashSha256), metadata.GeneratedCodeHashSha256);
        Append(builder, nameof(metadata.DirtyRevision), metadata.DirtyRevision.ToString(CultureInfo.InvariantCulture));
        Append(
            builder,
            nameof(metadata.RevisionGeneration),
            metadata.RevisionGeneration.ToString(CultureInfo.InvariantCulture));
        Append(builder, nameof(metadata.SessionId), metadata.SessionId.ToString("N"));
        Append(builder, nameof(metadata.ProcessId), metadata.ProcessId.ToString(CultureInfo.InvariantCulture));
        Append(builder, nameof(payloadSha256), payloadSha256);
        return DesignerFileHash.ComputeUtf8Sha256(builder.ToString());
    }

    private static void Append(StringBuilder builder, string name, string? value)
    {
        builder.Append(name.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(name);
        builder.Append('=');
        if (value is null)
        {
            builder.Append("-1:;");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }
}

internal enum DesignerRecoveryCandidateStatus
{
    Valid,
    Corrupt,
    Unsupported
}

internal sealed record DesignerRecoveryCandidate(
    string ArtifactPath,
    DesignerRecoveryCandidateStatus Status,
    DateTimeOffset? LastWriteTimeUtc,
    DesignerRecoveryEnvelope? Envelope,
    DesignDocument? Document,
    string? Error);

internal sealed record DesignerRecoveryWriteResult(
    bool Succeeded,
    string ArtifactPath,
    string? Error);

internal sealed record DesignerRecoveryDiscoveryResult(
    IReadOnlyList<DesignerRecoveryCandidate> Candidates,
    bool WasTruncated,
    string? Error);

internal sealed record DesignerRecoveryFileOperationResult(
    bool Succeeded,
    string? ResultPath,
    string? Error);

internal sealed record DesignerRecoveryCleanupResult(
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<string> Errors,
    int InspectedEntryCount,
    bool WasTruncated);

internal sealed class DesignerRecoveryStoreOptions
{
    public int MaxArtifactBytes { get; init; } = 32 * 1024 * 1024;

    public int MaxDiscoveryFiles { get; init; } = 512;

    public int MaxDiscoveryEntries { get; init; } = 4096;

    public long MaxDiscoveryBytes { get; init; } = 128L * 1024 * 1024;

    public int MaxCleanupEntries { get; init; } = 4096;

    public int MaxDocumentNodes { get; init; } = 10_000;

    public int MaxDocumentDepth { get; init; } = 64;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxArtifactBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDiscoveryFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDiscoveryEntries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDiscoveryBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxCleanupEntries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDocumentNodes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDocumentDepth, 1);
    }
}

internal sealed class DesignerRecoveryRetentionPolicy
{
    public DesignerRecoveryRetentionPolicy(
        TimeSpan maxAge,
        int maxArtifacts,
        TimeSpan temporaryFileMaxAge,
        TimeSpan quarantineMaxAge)
    {
        if (maxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        if (maxArtifacts < 0)
            throw new ArgumentOutOfRangeException(nameof(maxArtifacts));
        if (temporaryFileMaxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(temporaryFileMaxAge));
        if (quarantineMaxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quarantineMaxAge));

        MaxAge = maxAge;
        MaxArtifacts = maxArtifacts;
        TemporaryFileMaxAge = temporaryFileMaxAge;
        QuarantineMaxAge = quarantineMaxAge;
    }

    public TimeSpan MaxAge { get; }

    public int MaxArtifacts { get; }

    public TimeSpan TemporaryFileMaxAge { get; }

    public TimeSpan QuarantineMaxAge { get; }

    public static DesignerRecoveryRetentionPolicy Default { get; } = new(
        maxAge: TimeSpan.FromDays(30),
        maxArtifacts: 200,
        temporaryFileMaxAge: TimeSpan.FromDays(1),
        quarantineMaxAge: TimeSpan.FromDays(30));
}
