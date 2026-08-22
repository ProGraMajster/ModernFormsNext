namespace ModernFormsNext.Designer.History;

/// <summary>
/// Identifies how the Designer transaction manager is currently applying model changes.
/// </summary>
public enum DesignerHistoryReplayMode
{
    /// <summary>
    /// No transaction or history replay is active.
    /// </summary>
    Idle,

    /// <summary>
    /// User-authored changes are being recorded into an active transaction.
    /// </summary>
    Recording,

    /// <summary>
    /// A committed undo unit is being reverted.
    /// </summary>
    Undoing,

    /// <summary>
    /// A previously undone unit is being applied again.
    /// </summary>
    Redoing,

    /// <summary>
    /// An incomplete transaction is being rolled back.
    /// </summary>
    RollingBack
}
