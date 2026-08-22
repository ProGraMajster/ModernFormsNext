namespace ModernFormsNext.Designer.History;

/// <summary>
/// Provides information about a user-visible Designer transaction or history replay.
/// </summary>
public sealed class DesignerHistoryEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerHistoryEventArgs"/> class.
    /// </summary>
    /// <param name="description">The user-visible operation description.</param>
    /// <param name="mode">The replay mode associated with the notification.</param>
    public DesignerHistoryEventArgs(string description, DesignerHistoryReplayMode mode)
    {
        Description = description ?? string.Empty;
        Mode = mode;
    }

    /// <summary>
    /// Gets the user-visible operation description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the replay mode associated with the notification.
    /// </summary>
    public DesignerHistoryReplayMode Mode { get; }
}
