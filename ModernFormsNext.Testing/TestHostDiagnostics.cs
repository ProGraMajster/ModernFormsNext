using System.Collections.ObjectModel;
using System.Text;

namespace ModernFormsNext.Testing;

/// <summary>Provides a detached diagnostic snapshot of one deterministic headless test host.</summary>
public sealed class TestHostDiagnostics
{
    private readonly ReadOnlyCollection<ControlTreeSnapshot> controlTrees;
    private readonly ReadOnlyCollection<Exception> dispatcherExceptions;

    internal TestHostDiagnostics(
        int hostedWindowCount,
        int pendingDispatcherWorkCount,
        int pendingInvalidationCount,
        int activeAnimationCount,
        IEnumerable<ControlTreeSnapshot> controlTrees,
        IEnumerable<Exception> dispatcherExceptions)
    {
        HostedWindowCount = hostedWindowCount;
        PendingDispatcherWorkCount = pendingDispatcherWorkCount;
        PendingInvalidationCount = pendingInvalidationCount;
        ActiveAnimationCount = activeAnimationCount;
        this.controlTrees = Array.AsReadOnly(controlTrees.ToArray());
        this.dispatcherExceptions = Array.AsReadOnly(dispatcherExceptions.ToArray());
    }

    /// <summary>Gets the number of windows still owned by the host.</summary>
    public int HostedWindowCount { get; }

    /// <summary>Gets the number of queued UI-dispatcher work items.</summary>
    public int PendingDispatcherWorkCount { get; }

    /// <summary>Gets the number of headless visual invalidations awaiting explicit processing.</summary>
    public int PendingInvalidationCount { get; }

    /// <summary>Gets the process scheduler's active animation count without initializing it.</summary>
    public int ActiveAnimationCount { get; }

    /// <summary>Gets detached control trees for all non-closed hosted windows.</summary>
    public IReadOnlyList<ControlTreeSnapshot> ControlTrees => controlTrees;

    /// <summary>Gets captured exceptions from fire-and-forget dispatcher work.</summary>
    public IReadOnlyList<Exception> DispatcherExceptions => dispatcherExceptions;

    /// <summary>Returns a readable diagnostic report including every captured tree.</summary>
    /// <returns>The complete host diagnostic dump.</returns>
    public string Dump()
    {
        var builder = new StringBuilder();
        builder.Append("HostedWindows=").Append(HostedWindowCount)
            .Append("; PendingDispatcherWork=").Append(PendingDispatcherWorkCount)
            .Append("; PendingInvalidations=").Append(PendingInvalidationCount)
            .Append("; ActiveAnimations=").Append(ActiveAnimationCount)
            .Append("; DispatcherExceptions=").Append(DispatcherExceptions.Count);
        foreach (ControlTreeSnapshot tree in controlTrees)
        {
            builder.AppendLine();
            builder.Append(tree.Dump());
        }

        return builder.ToString();
    }

    /// <inheritdoc/>
    public override string ToString() => Dump();
}
