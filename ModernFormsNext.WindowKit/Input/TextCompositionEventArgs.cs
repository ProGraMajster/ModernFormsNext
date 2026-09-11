namespace ModernFormsNext.WindowKit.Input;

/// <summary>Identifies a completed semantic transition in the existing editor's composition.</summary>
public enum TextCompositionStage
{
    /// <summary>A new provisional edit began.</summary>
    Started,
    /// <summary>The provisional text or range changed.</summary>
    Updated,
    /// <summary>Committed text replaced the provisional text or selection.</summary>
    Committed,
    /// <summary>Visible provisional text was accepted without replacement.</summary>
    Finished,
    /// <summary>The current checkpoint was rolled back.</summary>
    Canceled
}

/// <summary>Describes a composition transition without retaining entered text or an editor reference.</summary>
public sealed class TextCompositionEventArgs : EventArgs
{
    /// <summary>Creates transition metadata after the editor has applied its final state.</summary>
    /// <param name="stage">The completed transition.</param>
    /// <param name="start">Resulting composition start, or -1 when absent.</param>
    /// <param name="end">Resulting composition end, or -1 when absent.</param>
    /// <param name="revision">The resulting document revision.</param>
    public TextCompositionEventArgs(TextCompositionStage stage, int start, int end, long revision)
    {
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        if (!((start == -1 && end == -1) || (start >= 0 && end >= start)))
            throw new ArgumentOutOfRangeException(nameof(start));
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        Stage = stage;
        Start = start;
        End = end;
        Revision = revision;
    }
    /// <summary>Gets the completed transition.</summary>
    public TextCompositionStage Stage { get; }
    /// <summary>Gets the resulting absolute composition start, or -1.</summary>
    public int Start { get; }
    /// <summary>Gets the resulting absolute composition end, or -1.</summary>
    public int End { get; }
    /// <summary>Gets the resulting document observation token.</summary>
    public long Revision { get; }
}
