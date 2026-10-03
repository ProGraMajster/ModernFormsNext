namespace ModernFormsNext;

/// <summary>Describes a confirmed change to a form's normal, minimized or maximized state.</summary>
/// <remarks>This immutable notification is platform-neutral and cannot cancel a transition.</remarks>
public sealed class WindowStateChangedEventArgs : EventArgs
{
    /// <summary>Creates a notification describing the previous and newly confirmed states.</summary>
    /// <param name="oldState">The previously confirmed state reported by the form.</param>
    /// <param name="newState">The newly confirmed state, already readable from Form.WindowState.</param>
    public WindowStateChangedEventArgs(FormWindowState oldState, FormWindowState newState)
        => (OldState, NewState) = (oldState, newState);

    /// <summary>Gets the previously confirmed state reported by the form.</summary>
    public FormWindowState OldState { get; }

    /// <summary>Gets the newly confirmed state.</summary>
    public FormWindowState NewState { get; }
}
