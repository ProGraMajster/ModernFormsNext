using System.Security;
using System.Text;
using System.Text.Json;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Recovery;

/// <summary>
/// Persists and discovers versioned recovery artifacts inside one dedicated recovery root.
/// </summary>
internal sealed class DesignerRecoveryStore : IDesignerRecoveryStore
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly DesignerRecoveryStoreOptions options;
    private readonly IDesignerAtomicFileCommitter atomicCommitter;
    private readonly Func<string, Stream> openReadStream;
    private readonly StringComparison pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public DesignerRecoveryStore(
        string? recoveryRoot = null,
        DesignerRecoveryStoreOptions? options = null,
        IDesignerAtomicFileCommitter? atomicCommitter = null,
        Func<string, Stream>? openReadStream = null)
    {
        RootPath = NormalizeDedicatedRoot(recoveryRoot ?? GetDefaultRootPath());
        this.options = options ?? new DesignerRecoveryStoreOptions();
        this.options.Validate();
        this.atomicCommitter = atomicCommitter ?? DesignerAtomicFileCommitter.Instance;
        this.openReadStream = openReadStream ?? OpenRecoveryReadStream;
    }

    public string RootPath { get; }

    public static string GetDefaultRootPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = IOPath.GetTempPath();

        return IOPath.Combine(basePath, "ModernFormsNext", "Designer", "Recovery");
    }

    public string GetArtifactPath(DesignerRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateMetadata(snapshot.Metadata, snapshot.Identity);
        var fileName = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{snapshot.Identity.FileNameToken}.{snapshot.Metadata.SessionId:N}.{snapshot.Metadata.ProcessId}{DesignerRecoveryFormat.ArtifactExtension}");
        return IOPath.Combine(RootPath, fileName);
    }

    public DesignerRecoveryWriteResult Write(DesignerRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string artifactPath;

        try
        {
            EnsureSafeDirectory(RootPath, create: true);
            ValidateMetadata(snapshot.Metadata, snapshot.Identity);
            artifactPath = GetArtifactPath(snapshot);

            var envelope = DesignerRecoveryEnvelope.FromSnapshot(snapshot);
            var json = JsonSerializer.Serialize(envelope, EnvelopeJsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > options.MaxArtifactBytes)
            {
                return new DesignerRecoveryWriteResult(
                    false,
                    artifactPath,
                    $"The recovery artifact exceeds the {options.MaxArtifactBytes} byte limit.");
            }

            DesignerAtomicFileWriter.WriteUtf8(artifactPath, json, atomicCommitter);
            return new DesignerRecoveryWriteResult(true, artifactPath, Error: null);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            artifactPath = TryGetArtifactPathWithoutValidation(snapshot);
            return new DesignerRecoveryWriteResult(false, artifactPath, exception.Message);
        }
    }

    public DesignerRecoveryCandidate Read(string artifactPath)
        => ReadCore(artifactPath, options.MaxArtifactBytes).Candidate
            ?? Corrupt(artifactPath ?? string.Empty, null, "The recovery artifact exceeds the configured size limit.");

    private RecoveryReadOutcome ReadCore(string artifactPath, long maximumReadBytes)
    {
        var fallbackPath = artifactPath ?? string.Empty;
        DateTimeOffset? lastWriteTimeUtc = null;
        long bytesRead = 0;

        try
        {
            EnsureSafeDirectory(RootPath, create: false);
            if (!TryResolveOwnedFile(
                    artifactPath,
                    OwnedFileKind.Artifact,
                    out var fullPath,
                    out var ownershipError))
            {
                return new RecoveryReadOutcome(Corrupt(fallbackPath, null, ownershipError), bytesRead, BudgetExceeded: false);
            }

            fallbackPath = fullPath;
            var file = new FileInfo(fullPath);
            file.Refresh();
            if (!file.Exists)
                return new RecoveryReadOutcome(Corrupt(fullPath, null, "The recovery artifact does not exist."), bytesRead, BudgetExceeded: false);
            if (file.Length > options.MaxArtifactBytes)
            {
                return new RecoveryReadOutcome(
                    Corrupt(fullPath, file.LastWriteTimeUtc, "The recovery artifact exceeds the configured size limit."),
                    bytesRead,
                    BudgetExceeded: false);
            }
            if (file.Length > maximumReadBytes)
                return new RecoveryReadOutcome(Candidate: null, bytesRead, BudgetExceeded: true);

            var initialLength = file.Length;
            var initialLastWriteTimeUtc = file.LastWriteTimeUtc;
            lastWriteTimeUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            byte[] utf8Bytes;
            using (var stream = openReadStream(fullPath))
            {
                var openedLength = stream.Length;
                if (openedLength > options.MaxArtifactBytes)
                {
                    return new RecoveryReadOutcome(
                        Corrupt(fullPath, lastWriteTimeUtc, "The recovery artifact exceeds the configured size limit."),
                        bytesRead,
                        BudgetExceeded: false);
                }
                if (openedLength > maximumReadBytes)
                    return new RecoveryReadOutcome(Candidate: null, bytesRead, BudgetExceeded: true);
                if (openedLength != initialLength)
                {
                    return new RecoveryReadOutcome(
                        Corrupt(fullPath, lastWriteTimeUtc, "The recovery artifact changed while it was being opened."),
                        bytesRead,
                        BudgetExceeded: false);
                }

                utf8Bytes = GC.AllocateUninitializedArray<byte>(checked((int)openedLength));
                while (bytesRead < openedLength)
                {
                    var read = stream.Read(
                        utf8Bytes,
                        checked((int)bytesRead),
                        checked((int)(openedLength - bytesRead)));
                    if (read == 0)
                        break;
                    bytesRead += read;
                }

                var sentinel = stream.ReadByte();
                if (sentinel >= 0)
                {
                    bytesRead++;
                    if (openedLength == maximumReadBytes && maximumReadBytes < options.MaxArtifactBytes)
                        return new RecoveryReadOutcome(Candidate: null, bytesRead, BudgetExceeded: true);

                    var error = openedLength == options.MaxArtifactBytes
                        ? "The recovery artifact exceeds the configured size limit."
                        : "The recovery artifact changed while it was being read.";
                    return new RecoveryReadOutcome(Corrupt(fullPath, lastWriteTimeUtc, error), bytesRead, BudgetExceeded: false);
                }

                if (bytesRead != openedLength)
                {
                    return new RecoveryReadOutcome(
                        Corrupt(fullPath, lastWriteTimeUtc, "The recovery artifact changed while it was being read."),
                        bytesRead,
                        BudgetExceeded: false);
                }

                file.Refresh();
                if (!file.Exists
                    || stream.Length != openedLength
                    || file.Length != initialLength
                    || file.LastWriteTimeUtc != initialLastWriteTimeUtc)
                {
                    return new RecoveryReadOutcome(
                        Corrupt(fullPath, lastWriteTimeUtc, "The recovery artifact changed while it was being read."),
                        bytesRead,
                        BudgetExceeded: false);
                }
            }

            ReadOnlySpan<byte> jsonBytes = utf8Bytes;
            if (jsonBytes.Length >= 3
                && jsonBytes[0] == 0xEF
                && jsonBytes[1] == 0xBB
                && jsonBytes[2] == 0xBF)
                jsonBytes = jsonBytes[3..];
            var json = Encoding.UTF8.GetString(jsonBytes);

            var envelope = JsonSerializer.Deserialize<DesignerRecoveryEnvelope>(json, EnvelopeJsonOptions);
            if (envelope is null)
            {
                return new RecoveryReadOutcome(
                    Corrupt(fullPath, lastWriteTimeUtc, "The recovery artifact did not contain an envelope."),
                    bytesRead,
                    BudgetExceeded: false);
            }

            if (envelope.FormatVersion != DesignerRecoveryFormat.CurrentVersion)
            {
                return new RecoveryReadOutcome(
                    new DesignerRecoveryCandidate(
                        fullPath,
                        DesignerRecoveryCandidateStatus.Unsupported,
                        lastWriteTimeUtc,
                        envelope,
                        Document: null,
                        $"Recovery format version {envelope.FormatVersion} is unsupported."),
                    bytesRead,
                    BudgetExceeded: false);
            }

            ValidateEnvelope(envelope);
            if (!DesignerFileHash.EqualsSha256(envelope.SerializedDesignDocument, envelope.PayloadSha256))
            {
                return new RecoveryReadOutcome(
                    Corrupt(fullPath, lastWriteTimeUtc, "The recovery payload checksum is invalid.", envelope),
                    bytesRead,
                    BudgetExceeded: false);
            }

            var document = DesignDocumentSerializer.Default.Deserialize(envelope.SerializedDesignDocument);
            ValidateDocumentShape(document);
            var validation = new DesignDocumentValidator().Validate(document);
            if (!validation.IsValid)
            {
                return new RecoveryReadOutcome(
                    Corrupt(
                        fullPath,
                        lastWriteTimeUtc,
                        "The recovered design document is invalid: " + string.Join("; ", validation.Errors),
                        envelope),
                    bytesRead,
                    BudgetExceeded: false);
            }

            return new RecoveryReadOutcome(
                new DesignerRecoveryCandidate(
                    fullPath,
                    DesignerRecoveryCandidateStatus.Valid,
                    lastWriteTimeUtc,
                    envelope,
                    document,
                    Error: null),
                bytesRead,
                BudgetExceeded: false);
        }
        catch (Exception exception) when (IsRecoverableReadException(exception))
        {
            return new RecoveryReadOutcome(
                Corrupt(fallbackPath, lastWriteTimeUtc, exception.Message),
                bytesRead,
                BudgetExceeded: false);
        }
    }

    public DesignerRecoveryDiscoveryResult Discover()
    {
        try
        {
            EnsureSafeDirectory(RootPath, create: true);
            var entries = new List<DiscoveryEntry>(options.MaxDiscoveryFiles);
            var entryComparer = Comparer<DiscoveryEntry>.Create(CompareDiscoveryEntries);
            var wasTruncated = false;
            var inspectedEntryCount = 0;

            // Enumeration is deliberately top-level. Keep only a bounded newest-first set in
            // memory; recovery discovery never searches projects, user folders, or metadata paths.
            foreach (var path in Directory.EnumerateFiles(RootPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (inspectedEntryCount == options.MaxDiscoveryEntries)
                {
                    wasTruncated = true;
                    break;
                }

                inspectedEntryCount++;
                if (!string.Equals(IOPath.GetExtension(path), DesignerRecoveryFormat.ArtifactExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                DateTimeOffset lastWriteTimeUtc;
                try
                {
                    lastWriteTimeUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                }
                catch (Exception exception) when (IsRecoverableFileException(exception))
                {
                    lastWriteTimeUtc = DateTimeOffset.MinValue;
                }

                var entry = new DiscoveryEntry(path, lastWriteTimeUtc);
                var index = entries.BinarySearch(entry, entryComparer);
                if (index < 0)
                    index = ~index;
                if (index < options.MaxDiscoveryFiles)
                {
                    entries.Insert(index, entry);
                    if (entries.Count > options.MaxDiscoveryFiles)
                    {
                        entries.RemoveAt(entries.Count - 1);
                        wasTruncated = true;
                    }
                }
                else
                {
                    wasTruncated = true;
                }
            }

            var candidates = new List<DesignerRecoveryCandidate>(entries.Count);
            long totalBytesRead = 0;
            foreach (var entry in entries)
            {
                var remainingBytes = Math.Max(0, options.MaxDiscoveryBytes - totalBytesRead);
                var outcome = ReadCore(entry.Path, Math.Min(options.MaxArtifactBytes, remainingBytes));
                if (outcome.BudgetExceeded)
                {
                    wasTruncated = true;
                    break;
                }

                if (outcome.Candidate is { } candidate)
                    candidates.Add(candidate);
                totalBytesRead += outcome.BytesRead;
            }

            return new DesignerRecoveryDiscoveryResult(
                candidates,
                wasTruncated,
                Error: null);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return new DesignerRecoveryDiscoveryResult([], WasTruncated: false, exception.Message);
        }
    }

    private int CompareDiscoveryEntries(DiscoveryEntry left, DiscoveryEntry right)
    {
        var timestampComparison = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
        return timestampComparison != 0
            ? timestampComparison
            : string.Compare(left.Path, right.Path, pathComparison);
    }

    public DesignerRecoveryFileOperationResult Delete(string artifactPath)
    {
        try
        {
            EnsureSafeDirectory(RootPath, create: false);
            if (!TryResolveOwnedFile(
                    artifactPath,
                    OwnedFileKind.Artifact | OwnedFileKind.Temporary | OwnedFileKind.Quarantined,
                    out var fullPath,
                    out var error))
            {
                return new DesignerRecoveryFileOperationResult(false, ResultPath: null, error);
            }

            File.Delete(fullPath);
            return new DesignerRecoveryFileOperationResult(true, fullPath, Error: null);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return new DesignerRecoveryFileOperationResult(false, ResultPath: null, exception.Message);
        }
    }

    public DesignerRecoveryFileOperationResult Quarantine(
        string artifactPath,
        DateTimeOffset? timestampUtc = null)
    {
        try
        {
            EnsureSafeDirectory(RootPath, create: false);
            if (!TryResolveOwnedFile(
                    artifactPath,
                    OwnedFileKind.Artifact,
                    out var fullPath,
                    out var error))
            {
                return new DesignerRecoveryFileOperationResult(false, ResultPath: null, error);
            }

            if (!File.Exists(fullPath))
                return new DesignerRecoveryFileOperationResult(false, ResultPath: null, "The recovery artifact does not exist.");

            var quarantineDirectory = IOPath.Combine(RootPath, DesignerRecoveryFormat.QuarantineDirectoryName);
            EnsureSafeDirectory(quarantineDirectory, create: true);
            var stamp = (timestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("yyyyMMddTHHmmssfffffffZ", System.Globalization.CultureInfo.InvariantCulture);
            var destination = IOPath.Combine(
                quarantineDirectory,
                $"{IOPath.GetFileName(fullPath)}.{stamp}.{Guid.NewGuid():N}.invalid");

            if (!TryResolveOwnedFile(destination, OwnedFileKind.Quarantined, out destination, out error))
                return new DesignerRecoveryFileOperationResult(false, ResultPath: null, error);

            File.Move(fullPath, destination, overwrite: false);
            return new DesignerRecoveryFileOperationResult(true, destination, Error: null);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return new DesignerRecoveryFileOperationResult(false, ResultPath: null, exception.Message);
        }
    }

    public DesignerRecoveryCleanupResult Cleanup(
        DesignerRecoveryRetentionPolicy policy,
        IEnumerable<string>? protectedArtifactPaths = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var deleted = new List<string>();
        var errors = new List<string>();
        var entries = new List<CleanupEntry>();
        var protectedPaths = new HashSet<string>(pathComparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var truncated = false;
        var inspectedEntryCount = 0;

        try
        {
            EnsureSafeDirectory(RootPath, create: true);
            foreach (var path in protectedArtifactPaths ?? [])
            {
                if (TryResolveOwnedFile(path, OwnedFileKind.Artifact, out var fullPath, out var error))
                    protectedPaths.Add(fullPath);
                else
                    errors.Add(error ?? $"Protected recovery path '{path}' is outside the recovery root.");
            }

            CollectCleanupEntries(RootPath, inQuarantine: false, entries, ref inspectedEntryCount, ref truncated, errors);

            var quarantineDirectory = IOPath.Combine(RootPath, DesignerRecoveryFormat.QuarantineDirectoryName);
            if (!truncated && Directory.Exists(quarantineDirectory))
            {
                try
                {
                    EnsureSafeDirectory(quarantineDirectory, create: false);
                    CollectCleanupEntries(quarantineDirectory, inQuarantine: true, entries, ref inspectedEntryCount, ref truncated, errors);
                }
                catch (Exception exception) when (IsRecoverableFileException(exception))
                {
                    errors.Add(exception.Message);
                }
            }
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            errors.Add(exception.Message);
            return new DesignerRecoveryCleanupResult(deleted, errors, inspectedEntryCount, truncated);
        }

        var now = (nowUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var removed = new HashSet<string>(protectedPaths.Comparer);

        foreach (var entry in entries)
        {
            var maxAge = entry.Kind switch
            {
                OwnedFileKind.Temporary => policy.TemporaryFileMaxAge,
                OwnedFileKind.Quarantined => policy.QuarantineMaxAge,
                _ => policy.MaxAge
            };

            if (protectedPaths.Contains(entry.Path) || now - entry.LastWriteTimeUtc <= maxAge)
                continue;

            TryDeleteForCleanup(entry.Path, deleted, errors, removed);
        }

        // Protected artifacts represent currently open documents. They are never charged against
        // the retained inactive-artifact budget; otherwise one protected old file could force the
        // newest recoverable closed document to be deleted.
        var retainedArtifacts = entries
            .Where(entry => entry.Kind == OwnedFileKind.Artifact
                && !removed.Contains(entry.Path)
                && !protectedPaths.Contains(entry.Path))
            .OrderByDescending(entry => entry.LastWriteTimeUtc)
            .ToList();
        var excess = Math.Max(0, retainedArtifacts.Count - policy.MaxArtifacts);

        for (var index = retainedArtifacts.Count - 1; index >= 0 && excess > 0; index--)
        {
            var entry = retainedArtifacts[index];
            if (TryDeleteForCleanup(entry.Path, deleted, errors, removed))
                excess--;
        }

        return new DesignerRecoveryCleanupResult(deleted, errors, inspectedEntryCount, truncated);
    }

    private void CollectCleanupEntries(
        string directory,
        bool inQuarantine,
        List<CleanupEntry> entries,
        ref int inspectedEntryCount,
        ref bool truncated,
        List<string> errors)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (inspectedEntryCount == options.MaxCleanupEntries)
            {
                truncated = true;
                return;
            }

            inspectedEntryCount++;

            var kind = inQuarantine
                ? OwnedFileKind.Quarantined
                : IsArtifactFile(path)
                    ? OwnedFileKind.Artifact
                    : IsTemporaryFile(path)
                        ? OwnedFileKind.Temporary
                        : OwnedFileKind.None;
            if (kind == OwnedFileKind.None)
                continue;

            if (!TryResolveOwnedFile(path, kind, out var fullPath, out var error))
            {
                errors.Add(error ?? $"Recovery cleanup rejected '{path}'.");
                continue;
            }

            try
            {
                entries.Add(new CleanupEntry(
                    fullPath,
                    kind,
                    new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero)));
            }
            catch (Exception exception) when (IsRecoverableFileException(exception))
            {
                errors.Add($"Could not inspect recovery file '{fullPath}': {exception.Message}");
            }
        }
    }

    private bool TryDeleteForCleanup(
        string path,
        List<string> deleted,
        List<string> errors,
        HashSet<string> removed)
    {
        var result = Delete(path);
        if (!result.Succeeded)
        {
            errors.Add(result.Error ?? $"Could not delete recovery file '{path}'.");
            return false;
        }

        deleted.Add(path);
        removed.Add(path);
        return true;
    }

    private void ValidateEnvelope(DesignerRecoveryEnvelope envelope)
    {
        if (envelope.Metadata is null)
            throw new InvalidDataException("The recovery envelope metadata is missing.");
        if (string.IsNullOrWhiteSpace(envelope.SerializedDesignDocument))
            throw new InvalidDataException("The recovery payload is empty.");
        if (!DesignerFileHash.IsSha256(envelope.PayloadSha256))
            throw new InvalidDataException("The recovery payload checksum is malformed.");

        ValidateMetadata(envelope.Metadata, expectedIdentity: null);
    }

    private static void ValidateMetadata(
        DesignerRecoverySnapshotMetadata metadata,
        DesignerRecoveryDocumentIdentity? expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.FrameworkVersion))
            throw new InvalidDataException("Recovery framework version metadata is missing.");
        if (string.IsNullOrWhiteSpace(metadata.DocumentIdentity))
            throw new InvalidDataException("Recovery document identity metadata is missing.");
        if (metadata.SessionId == Guid.Empty)
            throw new InvalidDataException("Recovery session identity metadata is missing.");
        if (metadata.ProcessId <= 0)
            throw new InvalidDataException("Recovery process identity metadata is invalid.");
        if (metadata.TimestampUtc == default)
            throw new InvalidDataException("Recovery timestamp metadata is missing.");
        if (metadata.DirtyRevision < 0)
            throw new InvalidDataException("Recovery dirty revision metadata is invalid.");
        if (metadata.RevisionGeneration < 0)
            throw new InvalidDataException("Recovery revision generation metadata is invalid.");
        if (string.IsNullOrWhiteSpace(metadata.SuggestedName))
            throw new InvalidDataException("Recovery suggested-name metadata is missing.");
        if (metadata.SourceFileHashSha256 is not null && !DesignerFileHash.IsSha256(metadata.SourceFileHashSha256))
            throw new InvalidDataException("Recovery source-file hash metadata is malformed.");
        if (metadata.GeneratedCodeHashSha256 is not null && !DesignerFileHash.IsSha256(metadata.GeneratedCodeHashSha256))
            throw new InvalidDataException("Recovery generated-code hash metadata is malformed.");

        DesignerRecoveryDocumentIdentity reconstructedIdentity;
        if (metadata.IsUnsaved)
        {
            if (metadata.TemporaryDocumentId is not Guid id || id == Guid.Empty)
                throw new InvalidDataException("Unsaved recovery metadata requires a temporary document ID.");
            if (metadata.DocumentPath is not null)
                throw new InvalidDataException("Unsaved recovery metadata cannot contain a canonical document path.");

            reconstructedIdentity = DesignerRecoveryDocumentIdentity.ForUnsavedDocument(id);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(metadata.DocumentPath))
                throw new InvalidDataException("Saved-document recovery metadata requires a canonical path.");
            if (metadata.TemporaryDocumentId is not null)
                throw new InvalidDataException("Saved-document recovery metadata cannot contain a temporary document ID.");

            reconstructedIdentity = DesignerRecoveryDocumentIdentity.ForSavedDocument(
                metadata.DocumentPath,
                metadata.ProjectPath);
        }

        // The payload checksum deliberately protects only the serialized design model. Rebuild the
        // stable identity from its source metadata so a modified path, project, or scratch ID cannot
        // redirect an otherwise valid recovery artifact to a different Designer document.
        if (!string.Equals(metadata.DocumentIdentity, reconstructedIdentity.Value, StringComparison.Ordinal)
            || metadata.IsUnsaved != reconstructedIdentity.IsUnsaved
            || metadata.TemporaryDocumentId != reconstructedIdentity.TemporaryDocumentId)
        {
            throw new InvalidDataException("Recovery document identity does not match its source metadata.");
        }

        if (expectedIdentity is { } identity && reconstructedIdentity != identity)
        {
            throw new InvalidDataException("Recovery snapshot identity metadata is inconsistent.");
        }
    }

    private void ValidateDocumentShape(DesignDocument document)
    {
        if (document.Metadata is null
            || document.Properties is null
            || document.Events is null
            || document.Controls is null)
        {
            throw new InvalidDataException("The recovered design document contains missing model collections.");
        }

        var stack = new Stack<(DesignControlNode Node, int Depth)>();
        foreach (var node in document.Controls)
        {
            if (node is null)
                throw new InvalidDataException("The recovered design document contains a null control node.");
            stack.Push((node, 1));
        }

        var nodeCount = 0;
        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            nodeCount++;
            if (nodeCount > options.MaxDocumentNodes)
                throw new InvalidDataException("The recovered design document exceeds the node limit.");
            if (depth > options.MaxDocumentDepth)
                throw new InvalidDataException("The recovered design document exceeds the depth limit.");
            if (node.TypeName is null
                || node.Name is null
                || node.Properties is null
                || node.Events is null
                || node.Children is null)
            {
                throw new InvalidDataException("The recovered design document contains an incomplete control node.");
            }

            foreach (var child in node.Children)
            {
                if (child is null)
                    throw new InvalidDataException("The recovered design document contains a null child node.");
                stack.Push((child, depth + 1));
            }
        }
    }

    private bool TryResolveOwnedFile(
        string? path,
        OwnedFileKind allowedKinds,
        out string fullPath,
        out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "A recovery file path is required.";
            return false;
        }

        try
        {
            fullPath = IOPath.GetFullPath(path);
            var relative = IOPath.GetRelativePath(RootPath, fullPath);
            var segments = relative.Split(
                [IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (relative == "."
                || IOPath.IsPathRooted(relative)
                || segments.Any(segment => segment is "." or ".."))
            {
                error = $"Recovery path '{path}' is outside the recovery root.";
                return false;
            }

            OwnedFileKind actualKind;
            if (segments.Length == 1 && IsArtifactFile(fullPath))
            {
                actualKind = OwnedFileKind.Artifact;
            }
            else if (segments.Length == 1 && IsTemporaryFile(fullPath))
            {
                actualKind = OwnedFileKind.Temporary;
            }
            else if (segments.Length == 2
                && string.Equals(segments[0], DesignerRecoveryFormat.QuarantineDirectoryName, pathComparison))
            {
                actualKind = OwnedFileKind.Quarantined;
            }
            else
            {
                error = $"Recovery path '{path}' is not an owned recovery file.";
                return false;
            }

            if ((allowedKinds & actualKind) == 0)
            {
                error = $"Recovery path '{path}' is not valid for this operation.";
                return false;
            }

            EnsureSafeDirectory(RootPath, create: false);
            if (actualKind == OwnedFileKind.Quarantined)
                EnsureSafeDirectory(IOPath.GetDirectoryName(fullPath)!, create: false);
            if (File.Exists(fullPath) && IsReparsePoint(fullPath))
            {
                error = $"Recovery path '{path}' is a reparse point.";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            error = exception.Message;
            return false;
        }
    }

    private void EnsureSafeDirectory(string directory, bool create)
    {
        var fullDirectory = IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(directory));
        if (!IsWithinOrEqualRoot(fullDirectory))
            throw new SecurityException($"Directory '{directory}' is outside the recovery root.");

        // Check existing ancestors before creating a child so cleanup never walks through a
        // junction or symbolic link into a location outside the dedicated recovery tree.
        for (var current = fullDirectory; !string.IsNullOrWhiteSpace(current); current = IOPath.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && IsReparsePoint(current))
                throw new SecurityException($"Recovery directory '{current}' is a reparse point.");

            var parent = IOPath.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, pathComparison))
                break;
        }

        if (create)
            Directory.CreateDirectory(fullDirectory);
        else if (!Directory.Exists(fullDirectory))
            throw new DirectoryNotFoundException($"Recovery directory '{fullDirectory}' does not exist.");

        if (IsReparsePoint(fullDirectory))
            throw new SecurityException($"Recovery directory '{fullDirectory}' is a reparse point.");
    }

    private bool IsWithinOrEqualRoot(string path)
    {
        if (string.Equals(path, RootPath, pathComparison))
            return true;

        var prefix = IOPath.EndsInDirectorySeparator(RootPath)
            ? RootPath
            : RootPath + IOPath.DirectorySeparatorChar;
        return path.StartsWith(prefix, pathComparison);
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool IsArtifactFile(string path)
        => string.Equals(IOPath.GetExtension(path), DesignerRecoveryFormat.ArtifactExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsTemporaryFile(string path)
    {
        var fileName = IOPath.GetFileName(path);
        return fileName.StartsWith(DesignerRecoveryFormat.TemporaryFilePrefix, StringComparison.Ordinal)
            && fileName.EndsWith(DesignerRecoveryFormat.TemporaryFileSuffix, StringComparison.Ordinal);
    }

    private static Stream OpenRecoveryReadStream(string path)
        => new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920,
            FileOptions.SequentialScan);

    private static string NormalizeDedicatedRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = IOPath.TrimEndingDirectorySeparator(IOPath.GetFullPath(path));
        var volumeRoot = IOPath.TrimEndingDirectorySeparator(IOPath.GetPathRoot(fullPath) ?? string.Empty);
        if (string.Equals(fullPath, volumeRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("The recovery root must be a dedicated directory, not a filesystem root.", nameof(path));

        return fullPath;
    }

    private string TryGetArtifactPathWithoutValidation(DesignerRecoverySnapshot snapshot)
    {
        try
        {
            return IOPath.Combine(
                RootPath,
                $"{snapshot.Identity.FileNameToken}.{snapshot.Metadata.SessionId:N}.{snapshot.Metadata.ProcessId}{DesignerRecoveryFormat.ArtifactExtension}");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static DesignerRecoveryCandidate Corrupt(
        string path,
        DateTimeOffset? lastWriteTimeUtc,
        string? error,
        DesignerRecoveryEnvelope? envelope = null)
        => new(
            path,
            DesignerRecoveryCandidateStatus.Corrupt,
            lastWriteTimeUtc,
            envelope,
            Document: null,
            string.IsNullOrWhiteSpace(error) ? "The recovery artifact is invalid." : error);

    private static bool IsRecoverableReadException(Exception exception)
        => IsRecoverableFileException(exception)
        || exception is JsonException
            or InvalidDataException
            or NullReferenceException;

    private static bool IsRecoverableFileException(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or SecurityException;

    [Flags]
    private enum OwnedFileKind
    {
        None = 0,
        Artifact = 1,
        Temporary = 2,
        Quarantined = 4
    }

    private sealed record CleanupEntry(
        string Path,
        OwnedFileKind Kind,
        DateTimeOffset LastWriteTimeUtc);

    private sealed record DiscoveryEntry(
        string Path,
        DateTimeOffset LastWriteTimeUtc);

    private sealed record RecoveryReadOutcome(
        DesignerRecoveryCandidate? Candidate,
        long BytesRead,
        bool BudgetExceeded);
}
