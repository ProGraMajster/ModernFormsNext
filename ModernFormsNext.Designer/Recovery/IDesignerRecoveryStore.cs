namespace ModernFormsNext.Designer.Recovery;

/// <summary>
/// Provides the bounded recovery-artifact operations used by the Designer persistence coordinator.
/// </summary>
/// <remarks>
/// This seam keeps scheduling and lifecycle tests deterministic without exposing recovery storage
/// as public framework API. Implementations must limit deletion and quarantine to their owned root.
/// </remarks>
internal interface IDesignerRecoveryStore
{
    string RootPath { get; }

    DesignerRecoveryWriteResult Write(DesignerRecoverySnapshot snapshot);

    DesignerRecoveryCandidate Read(string artifactPath);

    DesignerRecoveryDiscoveryResult Discover();

    DesignerRecoveryFileOperationResult Delete(string artifactPath);

    DesignerRecoveryFileOperationResult Quarantine(string artifactPath, DateTimeOffset? timestampUtc = null);

    DesignerRecoveryCleanupResult Cleanup(
        DesignerRecoveryRetentionPolicy policy,
        IEnumerable<string>? protectedArtifactPaths = null,
        DateTimeOffset? nowUtc = null);
}
