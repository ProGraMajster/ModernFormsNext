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
        IEnumerable<Exception> dispatcherExceptions,
        IEnumerable<string> focusedControlNames,
        IEnumerable<string> recentInputEvents)
    {
        HostedWindowCount = hostedWindowCount;
        PendingDispatcherWorkCount = pendingDispatcherWorkCount;
        PendingInvalidationCount = pendingInvalidationCount;
        ActiveAnimationCount = activeAnimationCount;
        this.controlTrees = Array.AsReadOnly(controlTrees.ToArray());
        this.dispatcherExceptions = Array.AsReadOnly(dispatcherExceptions.ToArray());
        FocusedControlNames = Array.AsReadOnly(focusedControlNames.ToArray());
        RecentInputEvents = Array.AsReadOnly(recentInputEvents.ToArray());
    }

    /// <summary>Gets the number of windows still owned by the host.</summary>
    public int HostedWindowCount { get; }

    /// <summary>Gets the number of queued UI-dispatcher work items, including dormant timers.</summary>
    /// <remarks>A nonzero value does not imply ready work; future timers wait for the host clock.</remarks>
    public int PendingDispatcherWorkCount { get; }

    /// <summary>Gets the number of headless visual invalidations awaiting explicit processing.</summary>
    public int PendingInvalidationCount { get; }

    /// <summary>Gets the process scheduler's active animation count without initializing it.</summary>
    public int ActiveAnimationCount { get; }

    /// <summary>Gets detached control trees for all non-closed hosted windows.</summary>
    public IReadOnlyList<ControlTreeSnapshot> ControlTrees => controlTrees;

    /// <summary>Gets captured exceptions from fire-and-forget dispatcher work.</summary>
    public IReadOnlyList<Exception> DispatcherExceptions => dispatcherExceptions;

    /// <summary>Gets the focused control name per open window; an empty name represents no named focus owner.</summary>
    public IReadOnlyList<string> FocusedControlNames { get; }

    /// <summary>Gets recent input kinds, grouped by window and bounded to 64 entries per window.</summary>
    /// <remarks>Key values and committed text are deliberately excluded.</remarks>
    public IReadOnlyList<string> RecentInputEvents { get; }

    /// <summary>Returns a readable diagnostic report including every captured tree.</summary>
    /// <returns>The complete host diagnostic dump.</returns>
    public string Dump()
    {
        var builder = new StringBuilder();
        builder.Append("HostedWindows=").Append(HostedWindowCount)
            .Append("; PendingDispatcherWork=").Append(PendingDispatcherWorkCount)
            .Append("; PendingInvalidations=").Append(PendingInvalidationCount)
            .Append("; ActiveAnimations=").Append(ActiveAnimationCount)
            .Append("; DispatcherExceptions=").Append(DispatcherExceptions.Count)
            .Append("; FocusedControls=").Append(string.Join(",", FocusedControlNames))
            .Append("; RecentInput=").Append(string.Join(",", RecentInputEvents));
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
